namespace TodoBackend

open Amazon.DynamoDBv2
open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents
open FSharp.Control.Tasks
open Newtonsoft.Json
open System
open TodoBackend.Dto
open TodoBackend.ApiHelpers

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()

type Functions(ddbClient: IAmazonDynamoDB, tableName: string) =
    static do Infrastructure.configureSerialization()

    do Infrastructure.configureDynamoDbMapping(tableName)
    let dbContext = Infrastructure.createDynamoDbContext(ddbClient)
    
    new() =
        Functions(new AmazonDynamoDBClient(), Infrastructure.getTodoTableNameFromEnvironmentVariable())

    member __.GetItems (request: APIGatewayHttpApiV2ProxyRequest) (ctx: ILambdaContext) = task {
        let! todoItems = Db.getAll dbContext
        
        let response = todoItems
                       |> Seq.map (fun item -> TodoItemDto.fromDomain item request)

        return Ok response
    }

    member __.GetItem (proxyRequest: APIGatewayHttpApiV2ProxyRequest) (ctx: ILambdaContext) = task {
        let itemId = getItemId proxyRequest
        match itemId with
        | Some id ->
            let! item = Db.get dbContext id
            
            match item with
            | Some x -> return Ok(TodoItemDto.fromDomain x proxyRequest)
            | None -> return NotFound()
        | None -> return NotFound()
    }

    member __.PostItem (proxyRequest: APIGatewayHttpApiV2ProxyRequest) (ctx: ILambdaContext) = task {
        let requestDto = JsonConvert.DeserializeObject<CreateTodoItemRequest>(proxyRequest.Body)
        
        let! item = Db.insert dbContext requestDto.Title requestDto.Order
        
        return Ok(TodoItemDto.fromDomain item proxyRequest)
    }

    member __.PatchItem (proxyRequest: APIGatewayHttpApiV2ProxyRequest) (ctx: ILambdaContext) = task {
        let itemId = getItemId proxyRequest
        match itemId with
        | Some id -> 
            let requestDto = JsonConvert.DeserializeObject<PatchTodoItemRequest>(proxyRequest.Body)

            let title = if not <| String.IsNullOrEmpty requestDto.Title then Some requestDto.Title else None
            let completed = requestDto.Completed |> Option.ofNullable
            let order = requestDto.Order |> Option.ofNullable
            
            let! item = Db.patch dbContext id title completed order
            
            match item with
            | Some x -> return Ok(TodoItemDto.fromDomain x proxyRequest)
            | None -> return NotFound()
        | None -> return NotFound()
    }
        
    member __.DeleteItems (proxyRequest: APIGatewayHttpApiV2ProxyRequest) (ctx: ILambdaContext) = task {
        let! _ = Db.deleteAll dbContext
        
        return Ok()
    }
        
    member __.DeleteItem (proxyRequest: APIGatewayHttpApiV2ProxyRequest) (ctx: ILambdaContext) = task {
        let itemId = getItemId proxyRequest
        match itemId with
        | Some id ->
            do! Db.delete dbContext id
            
            return Ok()
        | None -> return BadRequest()
    }