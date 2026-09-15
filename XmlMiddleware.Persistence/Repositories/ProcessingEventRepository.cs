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

    public async Task AddAsync(ProcessingEvent processingEvent)
    {
        await _context.ProcessingEvents.AddAsync(processingEvent);
    }

    public async Task AddRangeAsync(IEnumerable<ProcessingEvent> processingEvents)
    {
        await _context.ProcessingEvents.AddRangeAsync(processingEvents);
    }

    public async Task<List<ProcessingEvent>> GetByBatchIdAsync(long batchId)
    {
        return await _context.ProcessingEvents
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.CreatedDateTime)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}