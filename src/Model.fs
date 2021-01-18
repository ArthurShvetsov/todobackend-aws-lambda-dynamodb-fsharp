module TodoBackend.Model

[<CLIMutable>]
type TodoItem = {
    Id: string
    Title: string
    Completed: bool
    Order: int
}