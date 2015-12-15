#I @"../packages"
#r @"Http.fs/lib/net40/HttpClient.dll"

open HttpClient
open System.Net

let maybeWithQueryStringParam name value request = 
    if value |> Option.isSome then
        request |> withQueryStringItem { name = name; value = value.Value }
    else request

let maybeWithQueryString parameters request = 
    (request, parameters) 
    ||> Seq.fold (fun req (name, value) -> req |> maybeWithQueryStringParam name value) 

let apiFailure message = message |> WebException |> raise 