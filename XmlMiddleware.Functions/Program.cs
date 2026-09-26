using Azure.Messaging.ServiceBus.Administration;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using XmlMiddleware.Functions.Startup;
using XmlMiddleware.Infrastructure.DependencyInjection;
using XmlMiddleware.Persistence.Context;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration["SqlConnectionString"]);
});

builder.Services.AddInfrastructure();
builder.Services.AddScoped<
    ServiceBusStartupCheck>();

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Logging.SetMinimumLevel(
    LogLevel.Information);

builder.Logging.AddFilter(
    "Microsoft",
    LogLevel.Warning);

var app = builder.Build();

using (var scope =
app.Services.CreateScope())
{
    var serviceBusStartupCheck =
    scope.ServiceProvider
    .GetRequiredService<
    ServiceBusStartupCheck>();

    await serviceBusStartupCheck
    .CheckAsync();
}


await app.RunAsync();