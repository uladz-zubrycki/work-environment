#I @"../packages"
#r @"Http.fs/lib/net40/HttpClient.dll"
#r @"FSharp.Data/lib/net40/FSharp.Data.dll"
#load "Http.fsx"
#load "Common.fsx"

open Http
open HttpClient
open Common
open System
open System.IO
open FSharp.Data

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

type private TpUserInfo = { UserName: string; Password: string; }
//type private HttpErrorResponse = JsonProvider<".\JsonSamples\GithubClient\HttpError.json">
//type GetProjectsResponse = JsonProvider<"""..\JsonSamples\TargetProcess\GetProjects.json""">

let private curDir = Directory.GetCurrentDirectory()
let private apiRoot = "https://plan.tpondemand.com/api/v1"
let private userInfo = { UserName = "EMPTY"; Password = "EMPTY" }

[<AutoOpen>]
module Filters = 
    type Filter<'a> = 
        | Equal of attr: string * value: 'a
        | NotEqual of attr: string * value: 'a
        | Greater of attr: string * value: 'a
        | GreaterOrEqual of attr: string * value: 'a
        | Less of attr: string * value: 'a
        | LessOrEqual of attr: string * value: 'a
        | InList of attr: string * values: 'a list
        | Contains of attr: string * value: 'a
        | IsNull of attr: string
        | IsNotNull of attr: string
    
    type Filter = 
        | StringFilter of Filter<string> 
        | IntFilter of Filter<int> 
        | FloatFilter of Filter<float> 
        | DateTimeFilter of Filter<DateTime> 
    
    type Filters = 
        static member equal (attr: string, value: string) = StringFilter(Equal(attr, value)) 
        static member equal (attr: string, value: int) = IntFilter(Equal(attr, value)) 
        static member equal (attr: string, value: float) = FloatFilter(Equal(attr, value)) 
        static member equal (attr: string, value: DateTime) = DateTimeFilter(Equal(attr, value)) 
        static member notEqual (attr: string, value: string) = StringFilter(NotEqual(attr, value)) 
        static member notEqual (attr: string, value: int) = IntFilter(NotEqual(attr, value)) 
        static member notEqual (attr: string, value: float) = FloatFilter(NotEqual(attr, value)) 
        static member notEqual (attr: string, value: DateTime) = DateTimeFilter(NotEqual(attr, value)) 
        static member greater (attr: string, value: string) = StringFilter(Greater(attr, value)) 
        static member greater (attr: string, value: int) = IntFilter(Greater(attr, value)) 
        static member greater (attr: string, value: float) = FloatFilter(Greater(attr, value)) 
        static member greater (attr: string, value: DateTime) = DateTimeFilter(Greater(attr, value)) 
        static member greaterOrEqual (attr: string, value: string) = StringFilter(GreaterOrEqual(attr, value)) 
        static member greaterOrEqual (attr: string, value: int) = IntFilter(GreaterOrEqual(attr, value)) 
        static member greaterOrEqual (attr: string, value: float) = FloatFilter(GreaterOrEqual(attr, value)) 
        static member greaterOrEqual (attr: string, value: DateTime) = DateTimeFilter(GreaterOrEqual(attr, value)) 
        static member less (attr: string, value: string) = StringFilter(Less(attr, value)) 
        static member less (attr: string, value: int) = IntFilter(Less(attr, value)) 
        static member less (attr: string, value: float) = FloatFilter(Less(attr, value)) 
        static member less (attr: string, value: DateTime) = DateTimeFilter(Less(attr, value))
        static member lessOrEqual (attr: string, value: string) = StringFilter(LessOrEqual(attr, value)) 
        static member lessOrEqual (attr: string, value: int) = IntFilter(LessOrEqual(attr, value)) 
        static member lessOrEqual (attr: string, value: float) = FloatFilter(LessOrEqual(attr, value)) 
        static member lessOrEqual (attr: string, value: DateTime) = DateTimeFilter(LessOrEqual(attr, value))
        static member inList (attr: string, values: string list) = StringFilter(InList(attr, values)) 
        static member inList (attr: string, values: int list) = IntFilter(InList(attr, values)) 
        static member inList (attr: string, values: float list) = FloatFilter(InList(attr, values)) 
        static member inList (attr: string, values: DateTime list) = DateTimeFilter(InList(attr, values))
        static member contains (attr: string, value: string) = StringFilter(Contains(attr, value)) 
        static member contains (attr: string, value: int) = IntFilter(Contains(attr, value)) 
        static member contains (attr: string, value: float) = FloatFilter(Contains(attr, value)) 
        static member contains (attr: string, value: DateTime) = DateTimeFilter(Contains(attr, value))
        static member isNull (attr: string) = StringFilter(IsNull(attr)) 
        static member isNotNull (attr: string) = StringFilter(IsNotNull(attr)) 
        
    let createFilterQuery (filters: Filter list) = 
        let getOperandStr value = 
            match value with
            | DateTime(value) -> "'" + value.ToString("yyyy-MM-dd") + "'"
            | Int32(value) -> value.ToString()
            | Float(value) -> value.ToString()
            | _ -> "'" + value.ToString() + "'"

        let getArrayOperandStr values = 
            values 
            |> Seq.map getOperandStr 
            |> String.concat ", "

        let createOperatorStr (op: Filter<'a>) = 
            match op with
            | Equal(attr, value) -> sprintf "%s eq %s" attr (getOperandStr value)
            | NotEqual(attr, value) -> sprintf "%s ne %s" attr (getOperandStr value)
            | Greater(attr, value) -> sprintf "%s gt %s" attr (getOperandStr value)
            | GreaterOrEqual(attr, value) -> sprintf "%s gte %s" attr (getOperandStr value)
            | Less(attr, value) -> sprintf "%s lt %s" attr (getOperandStr value)
            | LessOrEqual(attr, value) -> sprintf "%s lte %s" attr (getOperandStr value)
            | InList(attr, values) -> sprintf "%s in (%s)" attr (getArrayOperandStr values)
            | Contains(attr, value) -> sprintf "%s contains %s" attr (getOperandStr value)
            | IsNull(attr) -> sprintf "%s is null" attr
            | IsNotNull(attr) -> sprintf "%s is not null" attr

        filters 
        |> Seq.map (fun op -> 
            match op with 
            | StringFilter(op) -> createOperatorStr op
            | IntFilter(op) -> createOperatorStr op
            | FloatFilter(op) -> createOperatorStr op
            | DateTimeFilter(op) -> createOperatorStr op) 
        |> String.concat " and "

[<AutoOpen>]
module private Http = 
    let withDefaultParameters userInfo request = 
        request
        |> withBasicAuthentication userInfo.UserName userInfo.Password
        |> withHeader (Accept("application/json"))

    let maybeWithFilters filter request = 
        let maybeFilterStr = filter |> Option.map createFilterQuery
        request |> maybeWithQueryStringParam "where" maybeFilterStr

module private GetContext = 
    let private url = apiRoot |> sprintf "%s/Context" 
    type private Response = JsonProvider<"""JsonSamples\TargetProcess\GetContext.json""">

    type GetContextCommand = 
        static member Execute userInfo filters =
            let response = 
                createRequest Get url
                |> withDefaultParameters userInfo
                |> maybeWithFilters filters
                |> getResponse

            match (response.StatusCode, response.EntityBody) with
            | 200, Some(response) -> Response.Parse response
            | statusCode, Some(response) -> 
                apiFailure <| sprintf "Unexpected TargetProcess communication error. Status code: '%d', response body: '%s'." statusCode response
            | _ -> apiFailure <| sprintf "Unexpected TargetProcess communication error."

module private GetProjects = 
    let private url = apiRoot |> sprintf "%s/Projects" 
    type private Response = JsonProvider<"""JsonSamples\TargetProcess\GetProjects.json""">
   
    type GetProjectsCommand = 
        static member Execute userInfo filters =
            ()

open GetContext
type TargetProcessClient(username, password) =
    let userInfo = { UserName = username; Password = password }
    member __.GetContext(?filters) = GetContextCommand.Execute userInfo filters

let tpclient = new TargetProcessClient(userInfo.UserName, userInfo.Password)
let context = tpclient.GetContext([Filters.equal("projectIds", 49105)])

let projectName = "TP3"
let projectsUrl = sprintf "%s/Projects" apiRoot
let contextUrl = sprintf "%s/Context" apiRoot

let projects = 
    createRequest Get projectsUrl
    |> withDefaultParameters userInfo
    |> withQueryStringItem {name = "where"; value = sprintf "Name eq '%s'" projectName}
    |> getResponse
