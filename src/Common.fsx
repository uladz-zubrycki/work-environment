open System
open System.IO
open System.Diagnostics
open Microsoft.FSharp.Reflection

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

module Union = 
    let toString (v: 'a) =
        let (case, _) = FSharpValue.GetUnionFields(v, typeof<'a>) 
        case.Name

type Result<'a> = Success of 'a | Failure of string option

let toLower(s: string) = s.ToLower()
let toStringLower(o: obj) = o.ToString() |> toLower
let (@@) fst snd = Path.Combine(fst, snd)

let normalizePath relativeRoot path =
    let path = Environment.ExpandEnvironmentVariables path
    let absolutePath = if Path.IsPathRooted path then path else relativeRoot @@ path
   
    absolutePath
    |> Path.GetFullPath
    |> (fun p -> p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))

let startProcess(fileName, args, startNewShell) =
    let procInfo = new ProcessStartInfo()
    procInfo.FileName <- fileName
    procInfo.Arguments <- args
    procInfo.UseShellExecute <- startNewShell
    Process.Start(procInfo)

let startProcessInDir(dirPath, fileName, args, startNewShell) = 
    if not (Directory.Exists dirPath) then
        failwith <| sprintf "Directory doesn't exist '%s'" dirPath
    
    let processFilePath = dirPath @@ fileName
    if not (File.Exists processFilePath) then
        failwith <| sprintf "File doesn't exist '%s'" processFilePath

    let currentDirectory = Directory.GetCurrentDirectory()
    Directory.SetCurrentDirectory (dirPath)
    
    try
        startProcess(processFilePath, args, startNewShell) 
    finally
        Directory.SetCurrentDirectory(currentDirectory)
