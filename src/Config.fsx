#I @"..\packages"
#r @"FSharp.Data\lib\net40\FSharp.Data.dll"
#load "Common.fsx"

open System.IO
open FSharp.Data
open Common
open System

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

[<Literal>]
let private configRelativePath = """..\.config.json"""
let private curDir = Directory.GetCurrentDirectory()
let private configPath = configRelativePath |> normalizePath curDir
let private configFolder = Path.GetDirectoryName configPath
type private Config = JsonProvider<configRelativePath>
let config = Config.Load(configRelativePath)

Directory.SetCurrentDirectory curDir

let configFailure (message: string) = 
    let message = message.TrimEnd([|'.'|])
    printfn "%s.\nCheck your configuration file '%s'." message configPath
    exit 0

let failure message = 
    printfn "%s" message
    exit 0

let getConfigValue (value: string) = 
    if value.[0] = '%' && value.[value.Length - 1] = '%' then
        let envVarName = value.Trim [|'%'|]
        let envVarValue = Environment.GetEnvironmentVariable envVarName
        if envVarValue = null then value else envVarValue
    else value

let getConfigPathValue value = 
    value 
    |> getConfigValue 
    |> normalizePath configFolder
