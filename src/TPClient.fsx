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
open StringUtils

Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

type private TpUserInfo = { UserName: string; Password: string; }
//type private HttpErrorResponse = JsonProvider<".\JsonSamples\GithubClient\HttpError.json">
//type GetProjectsResponse = JsonProvider<"""..\JsonSamples\TargetProcess\GetProjects.json""">

let private curDir = Directory.GetCurrentDirectory()
let private apiRoot = "https://plan.tpondemand.com/api/v1"

[<AutoOpen>]
module Filtering = 
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
        
    let createFilterQueryValue (filters: Filter list) = 
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
module Paging = 
    type PagingOptions = 
        | SkipTake of skip: int * take: int
        | GetPage of pageSize: int * pageIndex: int

    let createPagingQueryParameters = function
        | SkipTake(skip, take) -> 
            seq { 
                yield ("skip", skip |> toString) 
                yield ("take", take |> toString)}
        | GetPage(pageSize, pageIndex) -> 
            seq { 
                yield ("skip", pageSize * pageIndex |> toString) 
                yield ("take", pageSize |> toString)}

[<AutoOpen>]
module private Http = 
    type IResponseParser<'a> = abstract member Parse: string -> 'a 
    type ICollectionResponseParser<'a> = abstract member Parse: string -> 'a []

    let withDefaultParameters userInfo request = 
        request
        |> withBasicAuthentication userInfo.UserName userInfo.Password
        |> withHeader (Accept("application/json"))

    let maybeWithFiltering filter request = 
        let maybeFilteringStr = filter |> Option.map createFilterQueryValue
        request |> maybeWithQueryStringParam ("where", maybeFilteringStr)

    let maybeWithPaging pager request = 
        pager 
        |> Option.map createPagingQueryParameters
        |> Option.map (fun queryStringParams -> request |> withQueryStringParams queryStringParams)
        |> function
           | Some(newRequest) -> newRequest
           | None -> request

    let processTPResponse handlers (response: Response) = 
        let handlersDict = handlers |> dict
        let createResponseMessage (message: string) =
            let message = message.Trim([|'.'|])
            match response.EntityBody with
            | Some(responseBody) -> sprintf "%s. Status code: '%d', response body: '%s'." message response.StatusCode responseBody
            | None -> sprintf "%s. Status code: '%d'." message response.StatusCode 
        
        match handlersDict.TryGetValue response.StatusCode with
        | true, handler -> response.EntityBody |> handler
        | false, _ -> 
            let statusCodeStr = response.StatusCode.ToString()
            match statusCodeStr with
            | "400" -> apiFailure "TargetProcess api communication error: Unauthorized. Wrong or missed credentials."
            | "401" -> apiFailure "TargetProcess api communication error: Bad format. Incorrect parameter or query string."
            | "403" -> apiFailure "TargetProcess api communication error: Forbidden. A user has insufficient rights to perform an action."
            | "404" -> apiFailure "TargetProcess api communication error: Requested Entity not found."
            | "500" -> apiFailure "TargetProcess api server-side error."
            | "501" -> apiFailure "TargetProcess api server-side error: Not implemented. The requested action is either not supported or not implemented yet."
            | StartsWith "4" -> apiFailure <| createResponseMessage "Unexpected TargetProcess api communication error."
            | StartsWith "5" -> apiFailure <| createResponseMessage "TargetProcess api server-side error."
            | _ -> apiFailure <| createResponseMessage "Nonsupported TargetProcess api response type."

    let getTpResponse url userInfo filters pager = 
        createRequest Get url
        |> withDefaultParameters userInfo
        |> maybeWithFiltering filters
        |> maybeWithPaging pager
        |> getResponse

    let getSingle<'a, 'b when 'a :> IResponseParser<'b>> (parser: 'a, url, userInfo, filters, pager) = 
        getTpResponse url userInfo filters pager
        |> processTPResponse [200, (fun responseBody -> responseBody |> Option.map parser.Parse)] 
    
    let getCollection<'a, 'b when 'a :> ICollectionResponseParser<'b>> (parser: 'a, url, userInfo, filters, pager) = 
        getTpResponse url userInfo filters pager
        |> processTPResponse [
            200, (function 
                    | Some(responseBody) -> parser.Parse responseBody  
                    | None -> Array.empty<'b> )]
    
module private GetContextRequest = 
    type private Response = JsonProvider<"""JsonSamples\TargetProcess\GetContext.json""">
    let private url = apiRoot |> sprintf "%s/Context" 
    let private parser = 
        { new IResponseParser<Response.Root> with 
            member this.Parse responseBody = Response.Parse responseBody}

    let execute(userInfo, filters, pager) = getSingle(parser, url, userInfo, filters, pager) 

module private GetProjectsRequest = 
    type private Response = JsonProvider<"""JsonSamples\TargetProcess\GetProjects.json""">
    let private url = apiRoot |> sprintf "%s/Projects" 
    let private parser = 
        { new ICollectionResponseParser<Response.Item> with 
            member this.Parse responseBody = (Response.Parse responseBody).Items}

    let execute (userInfo, filters, pager) = getCollection(parser, url, userInfo, filters, pager) 

type TargetProcessClient(username, password) =
    let userInfo = { UserName = username; Password = password }
    
    member __.GetContext(?filters, ?pager) = GetContextRequest.execute(userInfo, filters, pager)
    member __.GetProjects(?filters, ?pager) = GetProjectsRequest.execute(userInfo, filters, pager)

let tpclient = new TargetProcessClient("EMPTY", "EMPTY")
let tpProject = tpclient.GetProjects([Filters.equal("Name", "TP3")]) |> Seq.head
let context = 
    tpclient.GetContext(
        filters = [Filters.equal("projectIds", tpProject.Id)],
        pager = SkipTake(0, 25)).Value

