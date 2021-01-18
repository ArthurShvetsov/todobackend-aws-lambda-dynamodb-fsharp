namespace TodoBackend.Tests

open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.Model
open FSharp.Control.Tasks
open Microsoft.Extensions.Configuration
open System.Collections.Generic
open System.Threading.Tasks
open Xunit.Abstractions

module TestHelpers = 
    let private getConfig() =
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build()
            
    let getDynamoDbClient() =
        getConfig()
            .GetAWSOptions()
            .CreateServiceClient<IAmazonDynamoDB>()

    let createTableAsync(dbClient: IAmazonDynamoDB, tableName: string, testOutput: ITestOutputHelper) = async {
        let createTableRequest =
            CreateTableRequest(
                TableName = tableName,
                ProvisionedThroughput = ProvisionedThroughput(5L, 5L),
                KeySchema = List<_>([
                    KeySchemaElement(KeyType = KeyType.HASH, AttributeName = "Id")
                ]),
                AttributeDefinitions = List<_>([
                    AttributeDefinition(AttributeName = "Id", AttributeType = ScalarAttributeType.S)
                ]))
        
        let! createTableResponse = dbClient.CreateTableAsync(createTableRequest) |> Async.AwaitTask
        
        testOutput.WriteLine($"Creating DynamoDB Table \"{tableName}\" response Status Code: \"{createTableResponse.HttpStatusCode}\"")
        testOutput.WriteLine($"Waiting for DynamoDB Table \"{tableName}\" to become ACTIVE...")
        
        let describeTableRequest = DescribeTableRequest(TableName = tableName)

        let mutable continueLooping = true
        while continueLooping do
            let! response = dbClient.DescribeTableAsync(describeTableRequest) |> Async.AwaitTask
            if response.Table.TableStatus = TableStatus.ACTIVE then
                continueLooping <- false
            else
                continueLooping <- true
                Task.Delay(1000) |> Async.AwaitTask |> ignore
                
        testOutput.WriteLine($"DynamoDB Table \"{tableName}\" initialized.")
    }