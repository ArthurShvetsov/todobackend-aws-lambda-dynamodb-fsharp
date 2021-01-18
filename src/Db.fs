namespace TodoBackend

open Amazon.DynamoDBv2.DataModel

open FSharp.Control.Tasks

open System
open System.Threading.Tasks

open TodoBackend.Model

module Db =
    let getAll (dbContext: IDynamoDBContext) = task {
        let scan = dbContext.ScanAsync<TodoItem>(null)
     
        let! items = scan.GetNextSetAsync()
        
        while not <| scan.IsDone do
            let! remaining = scan.GetRemainingAsync()
            items.AddRange(remaining)
        
        return items
    }
    
    let get (dbContext: IDynamoDBContext) (id: string) = task {
        let! item = dbContext.LoadAsync<TodoItem>(id)
        
        if obj.ReferenceEquals(item, null)  then
            return None
        else
            return Some item
    }
    
    let insert (dbContext: IDynamoDBContext) (title: string) (order: int) = task {
        let item = {
            Id = Guid.NewGuid().ToString("N")
            Title = title
            Completed = false
            Order = order
        }
        
        do! dbContext.SaveAsync<TodoItem>(item)
        
        return item
    }
    
    let patch (dbContext: IDynamoDBContext) (id: string) (title: string option) (completed: bool option) (order: int option) = task {
        let! existingItem = get dbContext id
        match existingItem with
        | Some x ->
            let patchedItem = {
                Id = id
                Title = title |> Option.defaultValue x.Title
                Completed = completed |> Option.defaultValue x.Completed
                Order = order |> Option.defaultValue x.Order
            }
            
            do! dbContext.SaveAsync<TodoItem>(patchedItem)
            
            return Some patchedItem
        | None ->
            return None
    }
    
    let delete (dbContext: IDynamoDBContext) (id: string) = task {
        do! dbContext.DeleteAsync<TodoItem>(id)
        ()
    }
    
    let deleteAll(dbContext: IDynamoDBContext) = task {
        // According to Stackoverflow to delete all records from a table, it's more efficient to drop and created new table
        // To keep this code simple, here we load all items and remove them one by one with individual request
        let! items = getAll(dbContext)

        let! _ = items
               |> Seq.map(fun x -> delete dbContext x.Id)
               |> Seq.toArray
               |> Task.WhenAll
        ()
    }