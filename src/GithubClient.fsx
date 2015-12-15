#I @"..\packages"
#r @"Http.fs\lib\net40\HttpClient.dll"
#r @"FSharp.Data\lib\net40\FSharp.Data.dll"
#load "Common.fsx"
#load "Http.fsx"

open System
open Common
open FSharp.Data
open HttpClient
open Microsoft.FSharp.Reflection
open System.Net
open System.IO
open Http

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

let private curDir = Directory.GetCurrentDirectory()
let private apiRoot = "api.github.com"

type SortDirection = Asc | Desc with override x.ToString() = Union.toString x
type RepoInfo = { Owner: string; Repo: string; UserName: string; Password: string; }
type HttpError400Response = JsonProvider<".\JsonSamples\GithubClient\HttpError400.json">
type HttpError422Response = JsonProvider<".\JsonSamples\GithubClient\HttpError422.json">
type HttpErrorResponse = JsonProvider<".\JsonSamples\GithubClient\HttpError.json">

let withRepoParameters repoInfo request = 
    request 
    |> withHeader (UserAgent(repoInfo.UserName))
    |> withBasicAuthentication repoInfo.UserName repoInfo.Password

[<AutoOpen>]
module GetPullRequests = 
    let private buildUrl = sprintf "https://%s/repos/%s/%s/pulls"
    type private Response = JsonProvider<".\JsonSamples\GithubClient\GetPullRequests.json">
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

            let response = 
                createRequest Get (buildUrl apiRoot repoInfo.Owner repoInfo.Repo)
                |> withRepoParameters repoInfo
                |> maybeWithQueryString ["state", maybeState;
                                         "head", maybeHead;
                                         "base", baseBranch;
                                         "sort", maybeSortType;
                                         "direction", maybeSortDirection;]
                |> getResponse

            match (response.StatusCode, response.EntityBody) with
            | 200, Some(response) -> 
                Response.Parse response
            | 400, Some(response) -> 
                let error = HttpErrorResponse.Parse response
                apiFailure <| sprintf "Bad request. Error: '%s'" error.Message 
            | 401, Some(response) -> apiFailure"Github communication error: Bad credentials."
            | 403, Some(response) -> apiFailure "Github communication error: Forbidden."
            | statusCode, Some(response) -> 
                apiFailure <| sprintf "Unexpected github communication error. Status code: '%d', response body: '%s'." statusCode response
            | _ -> apiFailure <| sprintf "Unexpected github communication error."

type GithubClient(owner, repo, username, password) =
    let repoInfo = { Owner = owner; Repo = repo; UserName = username; Password = password }

    member __.GetPullRequests(?state, ?head, ?baseBranch, ?sort) = 
        GetPullRequestsCommand.Execute repoInfo (state, head, baseBranch, sort)