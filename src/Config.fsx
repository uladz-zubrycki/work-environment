#I @"..\packages"
#r @"FSharp.Data\lib\net40\FSharp.Data.dll"
#r @"Argu\lib\net40\Argu.dll"
#load "Common.fsx"

open System.IO
open FSharp.Data
open Common
open System
open Nessos.Argu

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

type Arguments = Config_Path of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config_Path(_) -> "Specifies path to the config file."

type private Config = JsonProvider<""".\JsonSamples\Config.json""">

let private curDir = Directory.GetCurrentDirectory()
let private argsParser = ArgumentParser.Create<Arguments>()
let private args = argsParser.ParseCommandLine(inputs = fsi.CommandLineArgs, ignoreUnrecognized = true)
let private defaultConfigPath = normalizePath curDir "..\.config.json"  

let private configPath = 
    let pathArg = args.GetResult(<@Config_Path@>, defaultValue = defaultConfigPath)
    if not (File.Exists pathArg) then failure <| sprintf "Can't find configuration file at '%s'." pathArg
    else pathArg

let private configFolder = Path.GetDirectoryName configPath
let config = Config.Load(configPath)

let configFailure (message: string) = 
    let message = message.TrimEnd([|'.'|])
    failure "%s.\nCheck your configuration file '%s'." message configPath

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
