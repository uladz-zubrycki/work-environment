#I @"..\packages"
#r @"Http.fs\lib\net40\HttpClient.dll"
#r @"FSharp.Data\lib\net40\FSharp.Data.dll"
#load "Common.fsx"

open System
open Common
open FSharp.Data
open HttpClient
open Microsoft.FSharp.Reflection
open System.IO

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

(* Global constants *)
let private curDir = Directory.GetCurrentDirectory()
let private apiRoot = "api.github.com"

(* Common Types *)
type SortDirection = Asc | Desc with override x.ToString() = Union.toString x
type RepoInfo = { Owner: string; Repo: string; UserName: string; Password: string; }

(* Extensions *)
let withRepoParameters repoInfo request = 
    request 
    |> withHeader (UserAgent(repoInfo.UserName))
    |> withBasicAuthentication repoInfo.UserName repoInfo.Password

let maybeWithQueryStringParam name value request = 
    if value |> Option.isSome then
        request |> withQueryStringItem { name = name; value = value.Value }
    else request

let maybeWithQueryString parameters request = 
    (request, parameters) 
    ||> Seq.fold (fun req (name, value) -> req |> maybeWithQueryStringParam name value) 

[<AutoOpen>]
module GetPullRequests = 
    let private buildUrl = sprintf "https://%s/repos/%s/%s/pulls"
    type private Response = JsonProvider<".\GithubClient\GetPullRequests.json">
    type PullRequestState = Open | Closed | All with override x.ToString() = Union.toString x
    type PullRequestHead = { User: string; RefName: string }
    type PullRequestSortType = Created | Updated | Popularity | LongRunning with override x.ToString() = Union.toString x
    type PullRequestSort = SortDirection * PullRequestSortType

    type GetPullRequestsCommand = 
        static member Execute(repoInfo: RepoInfo)
                             (state: PullRequestState option, 
                              head: PullRequestHead option, 
                              baseBranch: string option, 
                              sort: PullRequestSort option) =
            let maybeState = state |> Option.map toStringLower
            let maybeHead = head |> Option.map(fun head-> sprintf "%s:%s" head.User head.RefName)
            let maybeSortDirection = sort |> Option.map (fst >> toStringLower)
            let maybeSortType = sort |> Option.map (snd >> toStringLower)

            createRequest Get (buildUrl apiRoot repoInfo.Owner repoInfo.Repo)
            |> withRepoParameters repoInfo
            |> maybeWithQueryString ["state", maybeState;
                                     "head", maybeHead;
                                     "base", baseBranch;
                                     "sort", maybeSortType;
                                     "direction", maybeSortDirection;]
            |> getResponseBody
            |> Response.Parse

type GithubClient(owner, repo, username, password) =
    let repoInfo = { Owner = owner; Repo = repo; UserName = username; Password = password }

    member __.GetPullRequests(?state, ?head, ?baseBranch, ?sort) = 
        GetPullRequestsCommand.Execute repoInfo (state, head, baseBranch, sort)