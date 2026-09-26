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
        var rows = await _context.ProcessingBatches
            .AsNoTracking()
            .OrderByDescending(x => x.BatchId)
            .Select(x => new
            {
                x.BatchId,
                x.CorrelationId,
                x.FileName,
                x.Status,
                x.CurrentStep,
                x.RetryCount,
                x.DuplicateCase,
                x.OriginalBatchId,
                x.ReceivedDateTime,
                x.ProcessingStartDateTime,
                x.ProcessingEndDateTime
            })
            .ToListAsync();

        var batches = rows.Select(x => new
        {
            x.BatchId,
            x.CorrelationId,
            x.FileName,
            Status = x.Status.ToString(),
            x.CurrentStep,
            x.RetryCount,
            DuplicateCase = x.DuplicateCase?.ToString(),
            x.OriginalBatchId,
            x.ReceivedDateTime,
            x.ProcessingStartDateTime,
            x.ProcessingEndDateTime
        });

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
            batch.FileHash,
            batch.BlobPath,
            DuplicateCase = batch.DuplicateCase?.ToString(),
            batch.OriginalBatchId,
            batch.ErrorCode,
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
            .OrderBy(x => x.EventId)
            .Select(x => new
            {
                x.EventId,
                EventType = x.EventType.ToString(),
                x.Status,
                x.FromState,
                x.ToState,
                x.Message,
                x.ErrorCode,
                x.ErrorMessage,
                x.FunctionName,
                x.AttemptNumber,
                x.OutputFileId,
                x.CorrelationId,
                x.FileName,
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

    /// <summary>
    /// Search the journey by any of CorrelationId, BatchId or FileName
    /// (the same three identifiers every log line carries).
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> SearchEvents(
        [FromQuery] Guid? correlationId,
        [FromQuery] long? batchId,
        [FromQuery] string? fileName)
    {
        if (correlationId is null &&
            batchId is null &&
            string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(
                "Provide at least one of correlationId, batchId or fileName.");
        }

        var query = _context.ProcessingEvents
            .AsNoTracking()
            .AsQueryable();

        if (correlationId.HasValue)
        {
            query = query.Where(x => x.CorrelationId == correlationId.Value);
        }

        if (batchId.HasValue)
        {
            query = query.Where(x => x.BatchId == batchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            query = query.Where(x => x.FileName == fileName);
        }

        var events = await query
            .OrderBy(x => x.EventId)
            .Select(x => new
            {
                x.EventId,
                x.BatchId,
                x.CorrelationId,
                x.FileName,
                EventType = x.EventType.ToString(),
                x.Status,
                x.FromState,
                x.ToState,
                x.Message,
                x.ErrorCode,
                x.ErrorMessage,
                x.AttemptNumber,
                x.CreatedDateTime
            })
            .ToListAsync();

        return Ok(events);
    }
}