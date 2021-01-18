namespace TodoBackend.Tests

open System
open Amazon.DynamoDBv2.DataModel
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents
open FSharp.Control.Tasks
open Newtonsoft.Json
open Xunit
open Xunit.Abstractions

open TodoBackend
open TodoBackend.Model

type ApiIntegrationTest(output: ITestOutputHelper) =
    let tableName = $"todo-backend-{Guid.NewGuid():N}"
    
    let dynamoDbClient = TestHelpers.getDynamoDbClient()
    let dynamoDbContext = new DynamoDBContext(dynamoDbClient)

    do Infrastructure.configureDynamoDbMapping(tableName)
    do TestHelpers.createTableAsync(dynamoDbClient, tableName, output) |> Async.RunSynchronously
    
    let createProxyRequest(pathParameters, body) =
        APIGatewayHttpApiV2ProxyRequest(
            RequestContext = APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext(DomainName = "todo-backend.com", Stage = "Prod"),
            PathParameters = pathParameters,
            Body = body)
    
    interface IDisposable with
        member x.Dispose() =
            output.WriteLine("Disposing...")
            output.WriteLine($"Deleting DynamoDB Table \"{tableName}\"")
            
            dynamoDbContext.Dispose()
            
            dynamoDbClient.DeleteTableAsync(tableName).Wait()
            dynamoDbClient.Dispose()
   
    [<Fact>]
    member _.``GetItems returns list of Todo items from DB``() = task {
        // Arrange
        let item1: TodoItem = {Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 1"; Completed = false}
        let item2: TodoItem = {Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 2"; Completed = true}
        do! dynamoDbContext.SaveAsync(item1)
        do! dynamoDbContext.SaveAsync(item2)

        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(null, null)
        
        // Act
        let! response = functions.GetItems request context

        // Assert
        let actualTodoItems = JsonConvert.DeserializeObject<Dto.TodoItemDto list>(response.Body)
        let expectedTodoItems: Dto.TodoItemDto list = [
            { Id = item1.Id; Title = item1.Title; Completed = item1.Completed; Order = item1.Order; Url = $"https://todo-backend.com/Prod/{item1.Id}"; }
            { Id = item2.Id; Title = item2.Title; Completed = item2.Completed; Order = item2.Order; Url = $"https://todo-backend.com/Prod/{item2.Id}"; }
        ]
        
        Assert.Equal(200, response.StatusCode)
        Assert.Equal<Dto.TodoItemDto seq>(expectedTodoItems |> Seq.sortBy (fun x -> x.Id), actualTodoItems |> Seq.sortBy (fun x -> x.Id))
    }
   
    [<Fact>]
    member _.``GetItems when DB is empty returns empty list``() = task {
        // Arrange
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(null, null)
        
        // Act
        let! response = functions.GetItems request context

        // Assert
        let actualTodoItems = JsonConvert.DeserializeObject<Dto.TodoItemDto list>(response.Body)

        Assert.Equal(200, response.StatusCode)
        Assert.Empty(actualTodoItems)
    }
    
    [<Fact>]
    member _.``GetItem when Todo item exists returns item``() = task {
        // Arrange
        let todoItem: TodoItem = {Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 1"; Completed = false}
        do! dynamoDbContext.SaveAsync(todoItem)
        
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(dict["id", todoItem.Id], null)

        // Act
        let! response = functions.GetItem request context

        // Assert
        let actualTodoItem = JsonConvert.DeserializeObject<Dto.TodoItemDto>(response.Body)
        let expectedTodoItem: Dto.TodoItemDto = { Id = todoItem.Id; Title = todoItem.Title; Completed = todoItem.Completed; Order = todoItem.Order; Url = $"https://todo-backend.com/Prod/{todoItem.Id}"; }
        
        Assert.Equal(200, response.StatusCode)
        Assert.Equal(expectedTodoItem, actualTodoItem)
    }
        
    [<Fact>]
    member _.``GetItem when Todo item does exist returns not found``() = task {
        // Arrange
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(dict["id", "NOT_FOUND"], null)

        // Act
        let! response = functions.GetItem request context

        // Assert
        Assert.Equal(404, response.StatusCode)
        Assert.Null(response.Body)
    }
    
    [<Fact>]
    member _.``PostItem returns created Todo item``() = task {
        // Arrange
        let createRequest: Dto.CreateTodoItemRequest = { Title = "Test Item"; Order = 256 }
        
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(null, createRequest |> JsonConvert.SerializeObject)
        
        // Act
        let! response = functions.PostItem request context

        // Assert
        Assert.Equal(200, response.StatusCode)
        
        let createdTodoItem = JsonConvert.DeserializeObject<Dto.TodoItemDto>(response.Body)
        
        Assert.NotNull(createdTodoItem.Id)
        Assert.Equal("Test Item", createdTodoItem.Title)
        Assert.Equal(false, createdTodoItem.Completed)
        Assert.Equal(256, createdTodoItem.Order)
        Assert.Equal($"https://todo-backend.com/Prod/{createdTodoItem.Id}", createdTodoItem.Url)
    }
    
    [<Fact>]
    member _.``PatchItem returns updated Todo item``() = task {
        // Arrange
        let todoItem: TodoItem = { Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 1"; Completed = false }
        do! dynamoDbContext.SaveAsync(todoItem)
        
        let patchRequest: Dto.PatchTodoItemRequest = { Title = "Patched title"; Completed = Nullable true; Order = Nullable 10 }
        
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(dict["id", todoItem.Id], patchRequest |> JsonConvert.SerializeObject)
        
        // Act
        let! response = functions.PatchItem request context

        // Assert
        Assert.Equal(200, response.StatusCode)
        
        let patchedTodoItem = JsonConvert.DeserializeObject<Dto.TodoItemDto>(response.Body)
        
        Assert.Equal(todoItem.Id, patchedTodoItem.Id)
        Assert.Equal("Patched title", patchedTodoItem.Title)
        Assert.Equal(true, patchedTodoItem.Completed)
        Assert.Equal(10, patchedTodoItem.Order)
        Assert.Equal($"https://todo-backend.com/Prod/{todoItem.Id}", patchedTodoItem.Url)
    }
    
    [<Fact>]
    member _.``DeleteItems deletes all items in DB``() = task {
        // Arrange
        let item1: TodoItem = {Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 1"; Completed = false}
        let item2: TodoItem = {Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 2"; Completed = true}
        do! dynamoDbContext.SaveAsync(item1)
        do! dynamoDbContext.SaveAsync(item2)
        
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(null, null)
        
        // Act
        let! deleteItemsResponse = functions.DeleteItems request context

        // Assert
        Assert.Equal(200, deleteItemsResponse.StatusCode)
        Assert.Null(deleteItemsResponse.Body |> JsonConvert.DeserializeObject<Dto.TodoItemDto list>)
        
        let! getItemsResponse = functions.GetItems request context

        Assert.Equal(200, getItemsResponse.StatusCode)
        Assert.Empty(getItemsResponse.Body |> JsonConvert.DeserializeObject<Dto.TodoItemDto list>)
    }
    
    [<Fact>]
    member _.``DeleteItem deletes Todo item from DB``() = task {
        // Arrange
        let todoItem: TodoItem = {Id = Guid.NewGuid().ToString("N"); Order = 0; Title = "test title 1"; Completed = false}
        do! dynamoDbContext.SaveAsync(todoItem)
        
        let functions = Functions(dynamoDbClient, tableName)
        let context = TestLambdaContext()
        let request = createProxyRequest(dict["id", todoItem.Id], null)
        
        // Act
        let! deleteItemResponse = functions.DeleteItem request context

        // Assert
        Assert.Equal(200, deleteItemResponse.StatusCode)
        Assert.Null(deleteItemResponse.Body |> JsonConvert.DeserializeObject<Dto.TodoItemDto>)
        
        let! getItemResponse = functions.GetItem request context

        Assert.Equal(404, getItemResponse.StatusCode)
    }