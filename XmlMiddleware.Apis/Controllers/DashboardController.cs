using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Domain.Enums;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var totalFiles =
            await _context.ProcessingBatches.CountAsync(
                cancellationToken);

        var completed =
            await _context.ProcessingBatches.CountAsync(
                x => x.Status == BatchStatus.Completed,
                cancellationToken);

        // Everything that ended without producing outputs and was not a duplicate.
        var failed =
            await _context.ProcessingBatches.CountAsync(
                x => x.Status == BatchStatus.Failed
                  || x.Status == BatchStatus.DeadLettered
                  || x.Status == BatchStatus.Rejected
                  || x.Status == BatchStatus.ValidationFailed,
                cancellationToken);

        var duplicates =
            await _context.ProcessingBatches.CountAsync(
                x => x.Status == BatchStatus.Duplicate,
                cancellationToken);

        var processing =
            await _context.ProcessingBatches.CountAsync(
                x => x.Status == BatchStatus.Received
                  || x.Status == BatchStatus.Validated
                  || x.Status == BatchStatus.Processing
                  || x.Status == BatchStatus.RetryPending,
                cancellationToken);

        return Ok(new
        {
            TotalFiles = totalFiles,
            Completed = completed,
            Failed = failed,
            Duplicates = duplicates,
            Processing = processing
        });
    }
}