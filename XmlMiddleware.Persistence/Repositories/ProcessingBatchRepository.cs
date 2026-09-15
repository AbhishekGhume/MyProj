using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Persistence.Repositories;

public class ProcessingBatchRepository : IProcessingBatchRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessingBatchRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessingBatch?> GetByIdAsync(long batchId)
    {
        return await _context.ProcessingBatches
            .Include(x => x.OutputFiles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.BatchId == batchId);
    }

    public async Task<ProcessingBatch?> GetByHashAsync(string fileHash)
    {
        return await _context.ProcessingBatches
            .FirstOrDefaultAsync(x => x.FileHash == fileHash);
    }

    public async Task<ProcessingBatch?> GetByCorrelationIdAsync(Guid correlationId)
    {
        return await _context.ProcessingBatches
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId);
    }

    public async Task AddAsync(ProcessingBatch batch)
    {
        await _context.ProcessingBatches.AddAsync(batch);
    }

    public void Update(ProcessingBatch batch)
    {
        _context.ProcessingBatches.Update(batch);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}