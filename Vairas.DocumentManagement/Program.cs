using Vairas.DocumentManagement.Configuration;
using Vairas.DocumentManagement.Middleware;
using Vairas.DocumentManagement.Startup;

if (await StartupCommands.TryRunOfflineAsync(args)) return;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonLogging();
builder.Services
    .AddDocumentOptions(builder.Configuration)
    .AddStrictJsonSerialization()
    .AddExceptionHandling()
    .AddOpenApiDocument()
    .AddDocumentDatabase()
    .AddDocumentServices()
    .AddDocumentInfrastructure()
    .AddDependencyHttpClients()
    .AddBackgroundWorkers()
    .AddBearerAuthentication()
    .AddEntryAuthorization()
    .AddServiceHealthChecks()
    .AddRequestRateLimiting()
    .AddServiceTelemetry(builder.Configuration);

var app = builder.Build();

if (await StartupCommands.TryRunWithHostAsync(app, args)) return;

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(context => context.HttpContext.WriteProblemAsync(context.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be completed."));
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapServiceHealthChecks();
app.MapOpenApiDocument();
app.Run();

public partial class Program;
