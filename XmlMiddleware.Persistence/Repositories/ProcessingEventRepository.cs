using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Persistence.Repositories;

public class ProcessingEventRepository : IProcessingEventRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessingEventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        ProcessingEvent processingEvent,
        CancellationToken cancellationToken = default)
    {
        await _context.ProcessingEvents.AddAsync(
            processingEvent,
            cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<ProcessingEvent> processingEvents,
        CancellationToken cancellationToken = default)
    {
        await _context.ProcessingEvents.AddRangeAsync(
            processingEvents,
            cancellationToken);
    }

    public async Task<List<ProcessingEvent>> GetByBatchIdAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessingEvents
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.EventId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}