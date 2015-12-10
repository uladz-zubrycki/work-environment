#I @"..\packages"
#r @"LibGit2Sharp\lib\net40\LibGit2Sharp.dll"
#load @"Common.fsx"

open Common
open LibGit2Sharp
open System.IO
open System.Linq
open System.Diagnostics

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

(* Extensions *)
type Result<'a> = Success of 'a | Failure of string option

let getMaybeRebasingBranchRemoteName repoPath branch =
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

let getActiveBranchRemoteName repoPath = 
    if not (Directory.Exists repoPath) then
        failwith <| sprintf "Can't find git repository at path '%s'. Directory doesn't exist." repoPath
    
    if not (Repository.IsValid (repoPath)) then
        failwith <| sprintf "Can't open git repository at path '%s'." repoPath 
    
    let repository = new Repository (repoPath)
    let currentBranch = repository.Head

    if currentBranch.IsTracking then
        let remoteName = currentBranch.Remote.Name
        let trackedBranchName = currentBranch.TrackedBranch.Name
        let remoteBranchName = trackedBranchName.Substring(remoteName.Length + 1)
        Success(remoteBranchName)
    else
        match getMaybeRebasingBranchRemoteName repoPath currentBranch with
        | Success(remoteBranchName) -> Success(remoteBranchName)
        | Failure(Some(errorMessage)) -> Failure(Some(errorMessage))
        | Failure(None) -> 
            currentBranch.Name |> 
            sprintf "Can't get remote branch name for local branch '%s'. There is no tracked branch info available, while there is no rebase or merge in progress."
            |> Some
            |> Failure
