using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BatchesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BatchesController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all processing batches.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var batches = await _context.ProcessingBatches
            .OrderByDescending(x => x.BatchId)
            .Select(x => new
            {
                x.BatchId,
                x.CorrelationId,
                x.FileName,
                Status = x.Status.ToString(),
                x.CurrentStep,
                x.RetryCount,
                x.ReceivedDateTime,
                x.ProcessingStartDateTime,
                x.ProcessingEndDateTime
            })
            .ToListAsync();

        return Ok(batches);
    }

    /// <summary>
    /// Get batch details by id.
    /// </summary>
    [HttpGet("{batchId:long}")]
    public async Task<IActionResult> GetById(
        long batchId)
    {
        var batch = await _context.ProcessingBatches
            .Include(x => x.OutputFiles)
            .FirstOrDefaultAsync(x =>
                x.BatchId == batchId);

        if (batch is null)
        {
            return NotFound(
                $"Batch {batchId} not found.");
        }

        return Ok(new
        {
            batch.BatchId,
            batch.CorrelationId,
            batch.FileName,
            Status = batch.Status.ToString(),
            batch.CurrentStep,
            batch.RetryCount,
            batch.RecordCount,
            batch.ErrorMessage,
            batch.ReceivedDateTime,
            batch.ProcessingStartDateTime,
            batch.ProcessingEndDateTime,

            OutputFiles =
                batch.OutputFiles
                    .Select(x => new
                    {
                        x.OutputFileId,
                        OutputType = x.OutputType.ToString(),
                        Status = x.Status.ToString(),
                        x.AttemptCount,
                        x.OutputBlobPath,
                        x.ErrorMessage,
                        x.StartedDateTime,
                        x.CompletedDateTime
                    })
        });
    }

    /// <summary>
    /// Get complete batch journey.
    /// </summary>
    [HttpGet("{batchId:long}/journey")]
    public async Task<IActionResult> GetJourney(
        long batchId)
    {
        var batchExists = await _context.ProcessingBatches
            .AnyAsync(x => x.BatchId == batchId);

        if (!batchExists)
        {
            return NotFound(
                $"Batch {batchId} not found.");
        }

        var journey = await _context.ProcessingEvents
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.CreatedDateTime)
            .Select(x => new
            {
                x.EventId,
                EventType = x.EventType.ToString(),
                x.Status,
                x.Message,
                x.FunctionName,
                x.AttemptNumber,
                x.OutputFileId,
                x.CreatedDateTime
            })
            .ToListAsync();

        return Ok(journey);
    }

    /// <summary>
    /// Get all output files for a batch.
    /// </summary>
    [HttpGet("{batchId:long}/outputs")]
    public async Task<IActionResult> GetOutputs(
        long batchId)
    {
        var batchExists = await _context.ProcessingBatches
            .AnyAsync(x => x.BatchId == batchId);

        if (!batchExists)
        {
            return NotFound(
                $"Batch {batchId} not found.");
        }

        var outputs = await _context.OutputFiles
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.OutputType)
            .Select(x => new
            {
                x.OutputFileId,
                OutputType = x.OutputType.ToString(),
                Status = x.Status.ToString(),
                x.AttemptCount,
                x.OutputBlobPath,
                x.ErrorCode,
                x.ErrorMessage,
                x.StartedDateTime,
                x.CompletedDateTime
            })
            .ToListAsync();

        return Ok(outputs);
    }
}