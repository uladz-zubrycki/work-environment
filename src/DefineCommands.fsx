#I @"..\packages"
#r @"Argu\lib\net40\Argu.dll"
#load "Commands.fsx"
#load "Config.fsx"
#load "Common.fsx"

open Commands
open Config
open Common
open Nessos.Argu
open System.IO

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

type Arguments = Action of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Action(_) -> "Specifies which action should be performed. Possible options: generate, delete"

let curDir = Directory.GetCurrentDirectory()
let outputDirectory = normalizePath (curDir @@ @"..\bin") (Directory.GetCurrentDirectory())
let repoFolder = getConfigPathValue(config.Repository.Path)
let commandsPath = curDir @@ "Commands"
let getCommandPath commandFile = commandsPath @@ commandFile

let parser = ArgumentParser.Create<Arguments>()
let parsedArgs = parser.ParseCommandLine(inputs = fsi.CommandLineArgs, ignoreUnrecognized = true)
let action = parsedArgs.GetResult(<@Action@>, defaultValue = "generate")

defineCommand 
    { Name = "github";
      Description = "Opens Github for current git branch. Opens page for branch pull-request, if exists; otherwise branch page itself.";
      Action = ExecuteFsx(getCommandPath "Github.fsx", None) }

defineCommand 
    { Name = "tp";
      Description = "Opens TargetProcess entity view for current git branch.";
      Action = ExecuteFsx(getCommandPath "TargetProcess.fsx", None) }

defineCommand 
    { Name = "build";
      Description = "Builds backend by running buildTpAll.backend.tuned.cmd in solution folder.";
      Action = ExecuteBat(repoFolder, "buildTpAll.backend.tuned.cmd", None) }

defineCommand
    { Name = "update-db";
      Description = "Updates database by running createDatabase.cmd with db-name parameter in solution folder.";
      Action = ExecuteBat(repoFolder, "createDatabase.cmd", Some(config.DatabaseName)) }

defineCommand
    { Name = "recreate-db";
      Description = "Creates database by running createDatabase.cmd in solution folder.";
      Action = ExecuteBat(repoFolder, "createDatabase.cmd", None) }

defineCommand
    { Name = "watcher";
      Description = "Starts webpack watcher by running runWatcher.cmd in solution folder.";
      Action = ExecuteBat(repoFolder, "runWatcher.cmd", None) }

defineCommand
    { Name = "clean";
      Description = "Removes solution build artifacts.";
      Action = ExecuteBatTemplate(repoFolder, getCommandPath "clean.bat") }

defineCommand
    { Name = "start-vs";
      Description = "Starts Visual Studio, opening TP solution.";
      Action = ExecuteExe(getConfigPathValue config.VsPath, Some(getConfigPathValue config.SolutionPath)) }

defineCommand
    { Name = "kill-vs";
      Description = "Closes Visual Studio by killing its process.";
      Action = ExecuteBatTemplate(".", getCommandPath "kill-vs.bat") }

defineCommand
    { Name = "restart-vs";
      Description = "Restarts Visual Studio opening TP solution.";
      Action = ExecuteComposite(["kill-vs"; "start-vs"]) }
      
defineCommand
    { Name = "rebuild";
      Description = "Deletes all artifacts and builds solution from scratch.";
      Action = ExecuteComposite(["clean"; "build"]) }

match action with
| "generate" -> generateCommands outputDirectory
| "delete" -> deleteCommands outputDirectory
| _ -> failwith <| sprintf "Unknown action value '%s'. Supported options: 'generate', 'delete'." action