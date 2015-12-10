#I @"..\..\packages"
#r @"LibGit2Sharp\lib\net40\LibGit2Sharp.dll"
#load @"..\Config.fsx"
#load @"..\Common.fsx"
#load @"..\GithubClient.fsx"
#load @"..\Repository.fsx"

open Common
open Config
open LibGit2Sharp
open System.IO
open System.Linq
open System.Diagnostics
open Repository
open GithubClient
open System.Net

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

let repoPath = getConfigPathValue config.Repository.Path
let createGithubBranchUrl remoteBranchName = sprintf @"%s/tree/%s" config.Repository.Url remoteBranchName

if not (Directory.Exists repoPath) then
    configFailure <| sprintf "Can't find git repository at path '%s'. Directory doesn't exist." repoPath
    
if not (Repository.IsValid (repoPath)) then
    configFailure <| sprintf "Can't open git repository at path '%s'." repoPath 
    
match getActiveBranchRemoteName repoPath with
| Failure(Some(message)) -> failure <| sprintf "Can't open github for active branch. Error: '%s'." message
| Failure(None) -> failure "Can't open github for active branch. Unknown error." 
| Success(branchName) -> 
    let repo = getConfigValue config.Repository.Name
    let owner = getConfigValue config.Repository.Owner
    let username = getConfigValue config.Repository.Username
    let password = getConfigValue config.Repository.Password
    let githubClient = new GithubClient(owner, repo, username, password)
    
    try
        let pullRequests = 
            githubClient.GetPullRequests(
                state = Open, 
                head = { User = owner; RefName = branchName },
                sort = (Asc, Updated))

        if pullRequests |> Array.isEmpty 
        then branchName |> createGithubBranchUrl |> Process.Start
        else pullRequests.[0].Links.Html.Href |> Process.Start
    with
    | :? WebException as ex -> failure <| sprintf "Can't get pull requests data. %s" ex.Message