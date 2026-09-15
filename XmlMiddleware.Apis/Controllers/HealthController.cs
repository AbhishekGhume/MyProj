//using Microsoft.AspNetCore.Mvc;

//namespace XmlMiddleware.Api.Controllers;

//[ApiController]
//[Route("api/health")]
//public class HealthController : ControllerBase
//{
//    [HttpGet]
//    public IActionResult Get()
//    {
//        return Ok(new
//        {
//            Status = "Healthy",
//            Timestamp = DateTime.UtcNow
//        });
//    }
//}

using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public HealthController(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var response = new
        {
            Database = await CheckDatabaseAsync(),
            BlobStorage = await CheckBlobStorageAsync(),
            ServiceBus = await CheckServiceBusAsync(),
            CheckedAt = DateTime.UtcNow
        };

        return Ok(response);
    }

    private async Task<bool> CheckDatabaseAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
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
                _configuration["BlobConnectionString"];

            var blobServiceClient =
                new BlobServiceClient(connectionString);

            await blobServiceClient.GetPropertiesAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckServiceBusAsync()
    {
        try
        {
            var connectionString =
                _configuration["ServiceBusConnectionString"];

            var administrationClient =
                new ServiceBusAdministrationClient(
                    connectionString);

            await administrationClient
                .GetNamespacePropertiesAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }
}