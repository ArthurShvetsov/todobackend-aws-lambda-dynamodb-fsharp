namespace TodoBackend

open Amazon.Lambda.APIGatewayEvents
open Newtonsoft.Json
open System.Collections.Generic
open System.Net

module ApiHelpers = 
    let private ID_QUERY_STRING_NAME = "id"

    let getItemId (proxyRequest: APIGatewayHttpApiV2ProxyRequest) : Option<string> =
        if isNull proxyRequest.PathParameters || not <| proxyRequest.PathParameters.ContainsKey(ID_QUERY_STRING_NAME) then
            None
        else
            Some(proxyRequest.PathParameters.[ID_QUERY_STRING_NAME])

    let Ok (content: 'T) =
        APIGatewayHttpApiV2ProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = JsonConvert.SerializeObject content,
            Headers = Dictionary<_, _>(dict [ ("Content-Type", "application/json") ])
        )
        
    let NotFound() =
        APIGatewayHttpApiV2ProxyResponse(
            StatusCode = int HttpStatusCode.NotFound
        )

    let BadRequest() =
        APIGatewayHttpApiV2ProxyResponse(
            StatusCode = int HttpStatusCode.BadRequest
        )