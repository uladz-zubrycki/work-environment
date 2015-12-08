#I @"..\..\packages"
#r @"LibGit2Sharp\lib\net40\LibGit2Sharp.dll"
#load @"..\Config.fsx"
#load @"..\Common.fsx"
#load @"..\GithubClient.fsx"

open Common
open Config
open LibGit2Sharp
open System.IO
open System.Linq
open System.Diagnostics
open GithubClient

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

(* Global constants *)
let repoPath = getConfigPathValue config.Repository.Path

(* Extensions *)
type Result<'a> = Success of 'a | Failure of string option

let createGithubBranchUrl remoteBranchName = sprintf @"%s/tree/%s" config.Repository.Url remoteBranchName

let getMaybeRebasingBranchRemoteName branch =
    let repoInternalPath = repoPath @@ ".git"
    let maybeRebaseDir = 
        ["rebase-apply"; "rebase-merge"]
        |> Seq.map (fun dirName -> repoInternalPath @@ dirName)
        |> Seq.tryFind Directory.Exists
    
    let isRebaseInProgress = maybeRebaseDir.IsSome

    if not isRebaseInProgress then Failure(None)
    else
        let rebaseDir = maybeRebaseDir.Value
        let maybeBranchCanonicalName = 
            let headNameFilePath = repoInternalPath @@ rebaseDir @@ "head-name"
            if File.Exists headNameFilePath then Some (File.ReadAllText headNameFilePath) else None
            
        match maybeBranchCanonicalName with
        | None -> Failure(Some("Can't get remote branch name, while rebasing with no ORIG_HEAD info available."))
        | Some branchCanonicalName -> 
            let branchName = branchCanonicalName.Substring("refs/heads/".Length)
            Success(branchName)

let getBranchRemoteName (branch: Branch) = 
    if branch.IsTracking then
        let remoteName = branch.Remote.Name
        let trackedBranchName = branch.TrackedBranch.Name
        let remoteBranchName = trackedBranchName.Substring(remoteName.Length + 1)
        Success(remoteBranchName)
    else
        match getMaybeRebasingBranchRemoteName branch with
        | Success(remoteBranchName) -> Success(remoteBranchName)
        | Failure(Some(errorMessage)) -> Failure(Some(errorMessage))
        | Failure(None) -> 
            "Can't get remote branch name for local branch. \n" +
            "There is no tracked branch info available, while there is no rebase or merge in progress."
            |> Some
            |> Failure

(* Script logic *)
if not (Directory.Exists repoPath) then
    configFailure <| sprintf "Can't find git repository at path '%s'. Directory doesn't exist." repoPath
    
if not (Repository.IsValid (repoPath)) then
    configFailure <| sprintf "Can't open git repository at path '%s'." repoPath 
    
let repository = new Repository (repoPath)
let currentBranch = repository.Head
match getBranchRemoteName currentBranch with
| Failure(Some(message)) -> failure <| sprintf "Can't open github for branch '%s'. Error: '%s'." currentBranch.Name message
| Failure(None) -> failure <| sprintf "Can't open github for branch '%s'. Unknown error." currentBranch.Name
| Success(branchName) -> 
    let repo = getConfigValue config.Repository.Name
    let owner = getConfigValue config.Repository.Owner
    let username = getConfigValue config.Repository.Username
    let password = getConfigValue config.Repository.Password
    let githubClient = new GithubClient(owner, repo, username, password)
    
    let prs = 
        githubClient.GetPullRequests(
            state = Open, 
            head = { User = owner; RefName = branchName },
            sort = (Asc, Updated))

    if prs |> Array.isEmpty 
    then branchName |> createGithubBranchUrl |> Process.Start
    else prs.[0].Links.Html.Href |> Process.Start