namespace TodoBackend.Tests

open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents

open TodoBackend

module FunctionsTest =
    [<Fact>]
    let ``Call HTTP GET on Root``() =
        let functions = Functions()
        let context = TestLambdaContext()
        let request = APIGatewayProxyRequest()
        let response = functions.GetHandler request context

        Assert.Equal(200, response.StatusCode)
        Assert.Equal("Hello AWS Serverless", response.Body)
