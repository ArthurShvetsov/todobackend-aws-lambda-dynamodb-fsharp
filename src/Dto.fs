module TodoBackend.Dto

open Amazon.Lambda.APIGatewayEvents
open System
open TodoBackend.Model

[<CLIMutable>]
type CreateTodoItemRequest = {
    Title: string
    Order: int
}

[<CLIMutable>]
type PatchTodoItemRequest = {
    Title: string
    Completed: Nullable<bool>
    Order: Nullable<int>
}

[<CLIMutable>]
type TodoItemDto = {
    Id: string
    Title: string
    Completed: bool
    Order: int
    Url: string
}

module internal TodoItemDto =
    let private getItemUrl (request: APIGatewayHttpApiV2ProxyRequest) (id: string) =
        $"https://{request.RequestContext.DomainName}/{request.RequestContext.Stage}/{id}"
        
    let fromDomain (model: TodoItem) (request: APIGatewayHttpApiV2ProxyRequest) : TodoItemDto =
        {
            Id = model.Id
            Title = model.Title
            Completed = model.Completed
            Order = model.Order
            Url = getItemUrl request model.Id
        }