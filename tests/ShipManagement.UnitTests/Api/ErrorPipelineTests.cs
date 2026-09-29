using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShipManagement.Api.Auth;
using ShipManagement.Api.Errors;
using ShipManagement.Domain.Errors;

namespace ShipManagement.UnitTests.Api;

public class ErrorPipelineTests
{
    private static ServiceProvider Services() =>
        new ServiceCollection()
            .AddLogging()
            .AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsExtensions.AddDefaults)
            .BuildServiceProvider();

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Model_Binding_Errors_Become_A_Validation_Problem_With_CamelCase_Field_Names()
    {
        var httpContext = new DefaultHttpContext { RequestServices = Services() };
        httpContext.Response.Body = new MemoryStream();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.fullName", "The JSON value could not be converted.");
        modelState.AddModelError("PageSize", "The value 'abc' is not valid.");
        modelState.AddModelError(string.Empty, string.Empty);
        modelState.SetModelValue("Valid", "ok", "ok");

        var result = InvalidModelStateResponse.Create(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState));
        await result.ExecuteResultAsync(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));

        httpContext.Response.StatusCode.Should().Be(400);
        var body = await ReadBodyAsync(httpContext);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.ValidationFailed);
        var errors = body.GetProperty("errors");
        errors.GetProperty("fullName")[0].GetString().Should().Be("The JSON value could not be converted.");
        errors.GetProperty("pageSize")[0].GetString().Should().Be("The value 'abc' is not valid.");
        errors.GetProperty("body")[0].GetString().Should().Be("The value is invalid.");
        errors.TryGetProperty("valid", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("$.fullName", "fullName")]
    [InlineData("FullName", "fullName")]
    [InlineData("pageSize", "pageSize")]
    [InlineData("$", "body")]
    [InlineData("", "body")]
    public void Model_State_Keys_Are_Normalised(string key, string expected)
    {
        InvalidModelStateResponse.NormalizeKey(key).Should().Be(expected);
    }

    [Theory]
    [InlineData(400, ErrorCodes.ValidationFailed)]
    [InlineData(401, ErrorCodes.Unauthorized)]
    [InlineData(403, ErrorCodes.Forbidden)]
    [InlineData(404, ErrorCodes.NotFound)]
    [InlineData(409, ErrorCodes.Conflict)]
    [InlineData(412, ErrorCodes.PreconditionFailed)]
    [InlineData(413, ErrorCodes.PayloadTooLarge)]
    [InlineData(415, ErrorCodes.UnsupportedMediaType)]
    [InlineData(429, ErrorCodes.RateLimited)]
    [InlineData(503, ErrorCodes.DatabaseUnavailable)]
    [InlineData(500, ErrorCodes.InternalError)]
    public void Framework_Problems_Get_A_Code_And_TraceId_From_Their_Status(int status, string code)
    {
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails { Status = status },
        };

        ProblemDetailsExtensions.AddDefaults(context);

        context.ProblemDetails.Extensions["code"].Should().Be(code);
        context.ProblemDetails.Extensions["traceId"].Should().NotBeNull();
    }

    [Fact]
    public void An_Explicit_Code_Is_Never_Overwritten()
    {
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails { Status = 409, Extensions = { ["code"] = ErrorCodes.ShipInactive } },
        };

        ProblemDetailsExtensions.AddDefaults(context);

        context.ProblemDetails.Extensions["code"].Should().Be(ErrorCodes.ShipInactive);
    }

    [Fact]
    public async Task Cancelled_Request_From_A_Departed_Client_Writes_Nothing()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var httpContext = new DefaultHttpContext { RequestServices = Services(), RequestAborted = aborted.Token };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_Without_A_Departed_Client_Is_An_Unexpected_Error()
    {
        var httpContext = new DefaultHttpContext { RequestServices = Services() };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Unreadable_Request_Is_A_400_Without_Parser_Details()
    {
        var httpContext = new DefaultHttpContext { RequestServices = Services() };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(httpContext, new BadHttpRequestException("Unexpected end of request content."), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(400);
        var body = await ReadBodyAsync(httpContext);
        body.GetProperty("detail").GetString().Should().Be("The request could not be read.");
    }

    [Fact]
    public void Current_User_Outside_A_Request_Fails_Loudly()
    {
        var user = new CurrentUser(new HttpContextAccessor());
        var act = () => user.IsAdministrator;
        act.Should().Throw<InvalidOperationException>();
    }
}
