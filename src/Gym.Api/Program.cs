using Gym.Api.Configuration;
using Gym.Api.Middleware;
using Gym.Application;
using Gym.Infrastructure;

using Scalar.AspNetCore;

using Serilog;

// Covers failures that happen before the host exists; replaced by the configured logger below.
SerilogConfiguration.UseBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseConfiguredSerilog();

    // The composition root names layers, not packages. Everything a layer needs is registered
    // by that layer's own extension method.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddOpenApi();

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddVitePolicy(builder.Configuration);
    }

    var app = builder.Build();

    // First in the pipeline, so the id is attached to everything that follows, including
    // failures raised by later middleware.
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(SerilogConfiguration.ConfigureRequestLogging);

    // Scalar and a permissive CORS policy are development conveniences. Gating them on the
    // environment makes "do not ship the API explorer" a property of the code rather than a
    // deployment checklist item somebody has to remember.
    if (app.Environment.IsDevelopment())
    {
        app.UseCors(CorsConfiguration.PolicyName);

        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("Gym Management API"));
    }

    // Reports the database, not just the process: see AddDbContextCheck in AddInfrastructure.
    // Anonymous on purpose — this is what a container orchestrator and CI poll.
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "The API terminated unexpectedly during startup.");
    throw;
}
finally
{
    // Serilog buffers when writing to Seq over the network, so an abrupt exit would drop the
    // last events — including, typically, the ones explaining why it exited.
    Log.CloseAndFlush();
}

// Exposed so task 0.6's WebApplicationFactory<Program> has a type to name. Top-level
// statements compile to an internal Program class, which the test project cannot see.
public partial class Program;
