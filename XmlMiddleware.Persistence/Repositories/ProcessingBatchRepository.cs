using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Persistence.Repositories;

public class ProcessingBatchRepository : IProcessingBatchRepository
{
    private readonly ApplicationDbContext _context;

    public ProcessingBatchRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessingBatch?> GetByIdAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessingBatches
            .Include(x => x.OutputFiles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                x => x.BatchId == batchId,
                cancellationToken);
    }

    public async Task<ProcessingBatch?> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessingBatches
            .Include(x => x.OutputFiles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId,
                cancellationToken);
    }

    public async Task<List<ProcessingBatch>> GetHashCandidatesAsync(
        string fileHash,
        long beforeBatchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessingBatches
            .AsNoTracking()
            .Where(x =>
                x.FileHash == fileHash &&
                x.BatchId < beforeBatchId &&
                x.Status != BatchStatus.Duplicate)
            .OrderBy(x => x.BatchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessingBatch?> ReloadAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();

        return await GetByIdAsync(
            batchId,
            cancellationToken);
    }

    public async Task AddAsync(
        ProcessingBatch batch,
        CancellationToken cancellationToken = default)
    {
        await _context.ProcessingBatches.AddAsync(
            batch,
            cancellationToken);
    }

    public void Update(ProcessingBatch batch)
    {
        _context.ProcessingBatches.Update(batch);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProcessingBatch?> GetLatestDeadLetteredByHashAsync(
    string fileHash,
    CancellationToken cancellationToken = default)
    {
        return await _context.ProcessingBatches
            .Include(x => x.OutputFiles)
            .AsSplitQuery()
            .Where(x =>
                x.FileHash == fileHash &&
                x.Status == BatchStatus.DeadLettered)
            .OrderByDescending(x => x.BatchId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}