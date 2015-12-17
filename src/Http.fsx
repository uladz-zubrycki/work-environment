#I @"../packages"
#r @"Http.fs/lib/net40/HttpClient.dll"

open HttpClient
open System.Net

let private foldToRequest items (request: Request) fn = (request, items) ||> Seq.fold (fun req item -> req |> fn item) 
let withQueryStringParam (name, value) request = request |> withQueryStringItem { name = name; value = value }

let maybeWithQueryStringParam (name, value) request = 
    value 
    |> Option.map (fun v -> request |> withQueryStringParam (name, v))
    |> function 
       | Some(newRequest) -> newRequest
       | None -> request

let withQueryStringParams parameters request = foldToRequest parameters request withQueryStringParam
let maybeWithQueryStringParams parameters request = foldToRequest parameters request maybeWithQueryStringParam
let apiFailure message = message |> WebException |> raise 