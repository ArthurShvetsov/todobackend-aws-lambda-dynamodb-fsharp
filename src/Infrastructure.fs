module TodoBackend.Infrastructure

open Amazon
open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.DataModel

open Newtonsoft.Json
open Newtonsoft.Json.Serialization

open TodoBackend.Model

let configureSerialization() =
    JsonConvert.DefaultSettings <- fun () -> JsonSerializerSettings(ContractResolver = CamelCasePropertyNamesContractResolver())

let getTodoTableNameFromEnvironmentVariable() =
    System.Environment.GetEnvironmentVariable("TodoTable")

let configureDynamoDbMapping(tableName: string) =
    if not <| System.String.IsNullOrEmpty tableName then
        AWSConfigsDynamoDB.Context.TypeMappings.[typeof<TodoItem>] <- Amazon.Util.TypeMapping(typeof<TodoItem>, tableName)

let createDynamoDbContext(ddbClient: IAmazonDynamoDB) =
    new DynamoDBContext(ddbClient, DynamoDBContextConfig(Conversion = DynamoDBEntryConversion.V2))