using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShipManagement.Api.Errors;
using ShipManagement.Domain.Errors;

namespace ShipManagement.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string> ExpectedMappings => new()
    {
        { RequestValidationException.ForField("pageSize", "pageSize must be between 1 and 100."), 400, ErrorCodes.ValidationFailed },
        { new NotFoundException(ErrorCodes.ShipNotFound, "Ship 'SHIP09' was not found."), 404, ErrorCodes.ShipNotFound },
        { new ConflictException("SHIP_CODE_CONFLICT", "Ship code 'SHIP01' already exists."), 409, "SHIP_CODE_CONFLICT" },
        { new ShipInactiveException("Ship 'SHIP04' is inactive."), 409, ErrorCodes.ShipInactive },
        { new ForbiddenException("Administrator role required."), 403, ErrorCodes.Forbidden },
        { new BadHttpRequestException("too big", StatusCodes.Status413PayloadTooLarge), 413, ErrorCodes.PayloadTooLarge },
        { new PreconditionFailedException("Ship 'SHIP01' was modified by someone else."), 412, ErrorCodes.PreconditionFailed },
        { new DependencyUnavailableException("The database is temporarily unavailable.", TimeSpan.FromSeconds(15)), 503, ErrorCodes.DatabaseUnavailable },
    };

    [Fact]
    public async Task Database_Outage_Is_A_503_With_Retry_After_And_No_Internal_Details()
    {
        var cause = new InvalidOperationException("Login failed for user 'ship_api' on server sql-prod-01");

        var (httpContext, body) = await HandleAsync(
            new DependencyUnavailableException("The database is temporarily unavailable.", TimeSpan.FromSeconds(4.2), cause));

        httpContext.Response.StatusCode.Should().Be(503);
        httpContext.Response.Headers.RetryAfter.ToString().Should().Be("5");
        body.GetRawText().Should().NotContain("sql-prod-01").And.NotContain("ship_api");
    }

    [Theory]
    [MemberData(nameof(ExpectedMappings))]
    public async Task Domain_Errors_Map_To_Status_And_Code(Exception exception, int status, string code)
    {
        var (httpContext, body) = await HandleAsync(exception);

        httpContext.Response.StatusCode.Should().Be(status);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
        body.GetProperty("code").GetString().Should().Be(code);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Validation_Errors_List_Each_Field()
    {
        var (_, body) = await HandleAsync(RequestValidationException.ForField("period", "period is required."));

        body.GetProperty("errors").GetProperty("period")[0].GetString().Should().Be("period is required.");
    }

    [Fact]
    public async Task Unexpected_Errors_Return_A_Generic_500_Without_Internal_Details()
    {
        var exception = new InvalidOperationException("Login failed for user 'ship_api'. Server=sql-prod-01; at Foo.Bar()");

        var (httpContext, body) = await HandleAsync(exception);
        var raw = body.GetRawText();

        httpContext.Response.StatusCode.Should().Be(500);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.InternalError);
        raw.Should().NotContain("ship_api").And.NotContain("sql-prod-01").And.NotContain("Foo.Bar").And.NotContain("InvalidOperationException");
    }

    private static async Task<(HttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsExtensions.AddDefaults)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);

        (await handler.TryHandleAsync(httpContext, exception, CancellationToken.None)).Should().BeTrue();

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return (httpContext, document.RootElement.Clone());
    }
}
