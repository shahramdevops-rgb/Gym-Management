using Gym.Infrastructure;

// Serilog, OpenAPI, CORS and AddApplication() arrive in task 0.4.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Reports the database, not just the process: see AddDbContextCheck in AddInfrastructure.
// Anonymous on purpose — this is what a container orchestrator and CI poll.
app.MapHealthChecks("/health");

app.Run();

// Exposed so task 0.6's WebApplicationFactory<Program> has a type to name. Top-level
// statements compile to an internal Program class, which the test project cannot see.
public partial class Program;
