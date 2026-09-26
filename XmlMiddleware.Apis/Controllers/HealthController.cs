using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    private readonly IServiceBusHealthService
        _serviceBusHealthService;

    public HealthController(
        ApplicationDbContext context,
        IConfiguration configuration,
        IServiceBusHealthService serviceBusHealthService)
    {
        _context = context;
        _configuration = configuration;
        _serviceBusHealthService =
            serviceBusHealthService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var serviceBus =
            await _serviceBusHealthService
                .GetHealthAsync();

        var response = new
        {
            Database =
                await CheckDatabaseAsync(),

            BlobStorage =
                await CheckBlobStorageAsync(),

            ServiceBus =
                serviceBus,

            CheckedAt =
                DateTime.UtcNow
        };

        return Ok(response);
    }

    private async Task<bool> CheckDatabaseAsync()
    {
        try
        {
            return await _context
                .Database
                .CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckBlobStorageAsync()
    {
        try
        {
            var connectionString =
                _configuration["BlobConnectionString"]
                ?? _configuration["AzureWebJobsStorage"];

            var blobServiceClient =
                new BlobServiceClient(
                    connectionString);

            await blobServiceClient
                .GetPropertiesAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }
}