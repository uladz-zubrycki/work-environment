open System
open System.IO
open System.Diagnostics
open Microsoft.FSharp.Reflection

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

type Result<'a> = Success of 'a | Failure of string option

module Union = 
    let toString (v: 'a) =
        let (case, _) = FSharpValue.GetUnionFields(v, typeof<'a>) 
        case.Name

module String = 
    let toLower(s: string) = s.ToLower()

[<AutoOpen>]
module ConvertUtils = 
    let private parseWith parser value = 
        match parser value with
            | true, v -> Some(v)
            | false, _ -> None

    let private castTo<'a, 'b> (o: 'a) = 
        match o.GetType() with
        | t when t = typeof<'b> -> Some((box o) :?> 'b)
        | _ -> None

    let (|DateTimeString|_|) = parseWith DateTime.TryParse
    let (|FloatString|_|) = parseWith Double.TryParse
    let (|Int32String|_|) = parseWith Int32.TryParse
    let (|DateTime|_|) = castTo<'a, DateTime>
    let (|Float|_|) = castTo<'a, float>
    let (|Int32|_|) = castTo<'a, int>
    let (|String|_|) = castTo<'a, string>

[<AutoOpen>]
module LogUtils = 
    let private printColored color message = 
        let cmdColor = Console.ForegroundColor
        Console.ForegroundColor <- color
        printfn "%s" message
        Console.ForegroundColor <- cmdColor
    
    let printInfo message = printColored ConsoleColor.DarkGray message
    let printWarn message = printColored ConsoleColor.DarkYellow message
    let printSuccess message = printColored ConsoleColor.DarkGreen message 
    let printError message = printColored ConsoleColor.DarkRed message

[<AutoOpen>]
module PathUtils =
    let (@@) fst snd = Path.Combine(fst, snd)
    
    let normalizePath path relativeRoot =
        try
            path
            |> Environment.ExpandEnvironmentVariables
            |> (fun p -> if Path.IsPathRooted p then p else relativeRoot @@ p)
            |> Path.GetFullPath
            |> (fun p -> p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        with
            | :? System.Security.SecurityException
            | :? System.NotSupportedException 
            | :? System.IO.PathTooLongException as ex ->
              raise <| new System.IO.IOException (sprintf "Can't normalize path '%s'." path, ex)

[<AutoOpen>]
module ProcessUtils =
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

let toStringLower(o: obj) = o.ToString() |> String.toLower
    
let failure message = 
    printError message
    exit 0