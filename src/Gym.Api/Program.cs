// Task 0.1 keeps this at the bare minimum that builds and runs.
// Serilog, OpenAPI, CORS and the DI extension methods arrive in task 0.4;
// the /health endpoint arrives in task 0.3.
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
