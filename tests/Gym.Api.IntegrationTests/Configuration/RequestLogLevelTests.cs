using Gym.Api.Configuration;

using Microsoft.AspNetCore.Http;

using Serilog.Events;

namespace Gym.Api.IntegrationTests.Configuration;

/// <summary>
/// The level of each request's summary line (task 11.3). Four days of production logs showed a
/// cancelled request written as a 500 error and the board's own refreshes filling 79% of the log.
/// </summary>
public sealed class RequestLogLevelTests
{
    private static LogEventLevel LevelFor(string method, string path, int status, Exception? exception = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.StatusCode = status;

        return SerilogConfiguration.GetLevel(context, elapsed: 5, exception);
    }

    [Theory]
    [InlineData("/api/attendance/currently-inside")]
    [InlineData("/api/attendance/today-by-hour")]
    [InlineData("/api/lockers")]
    [InlineData("/api/payables/due-soon")]
    public void GetLevel_BoardRefreshSucceeds_IsDebug(string path) =>
        LevelFor(HttpMethods.Get, path, StatusCodes.Status200OK).ShouldBe(LogEventLevel.Debug);

    [Fact]
    public void GetLevel_BoardRefreshFails_IsStillLogged()
    {
        LevelFor(HttpMethods.Get, "/api/lockers", StatusCodes.Status401Unauthorized).ShouldBe(LogEventLevel.Information);
        LevelFor(HttpMethods.Get, "/api/lockers", StatusCodes.Status500InternalServerError).ShouldBe(LogEventLevel.Error);
    }

    [Theory]
    [InlineData("GET", "/api/lockers/10c4e700-0000-7000-8000-000000000001")]
    [InlineData("POST", "/api/lockers/10c4e700-0000-7000-8000-000000000001/out-of-service")]
    [InlineData("GET", "/api/members")]
    public void GetLevel_AnythingButARefresh_IsInformation(string method, string path) =>
        LevelFor(method, path, StatusCodes.Status200OK).ShouldBe(LogEventLevel.Information);

    [Fact]
    public void GetLevel_RequestCancelledByTheBrowser_IsInformationNotError() =>
        LevelFor(HttpMethods.Get, "/api/sales", StatusCodes.Status499ClientClosedRequest).ShouldBe(LogEventLevel.Information);

    [Fact]
    public void GetLevel_ServerFailure_IsError()
    {
        LevelFor(HttpMethods.Get, "/api/sales", StatusCodes.Status500InternalServerError).ShouldBe(LogEventLevel.Error);
        LevelFor(HttpMethods.Get, "/api/sales", StatusCodes.Status200OK, new InvalidOperationException()).ShouldBe(LogEventLevel.Error);
    }

    [Fact]
    public void GetLevel_HealthProbe_IsDebug() =>
        LevelFor(HttpMethods.Get, "/health", StatusCodes.Status200OK).ShouldBe(LogEventLevel.Debug);
}
