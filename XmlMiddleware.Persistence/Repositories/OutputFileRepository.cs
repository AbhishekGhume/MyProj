using Microsoft.EntityFrameworkCore;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Domain.Entities;
using XmlMiddleware.Domain.Enums;
using XmlMiddleware.Persistence.Context;

namespace XmlMiddleware.Persistence.Repositories;

public class OutputFileRepository : IOutputFileRepository
{
    private readonly ApplicationDbContext _context;

    public OutputFileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OutputFile?> GetByIdAsync(
        long outputFileId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OutputFiles
            .FirstOrDefaultAsync(
                x => x.OutputFileId == outputFileId,
                cancellationToken);
    }

    public async Task<List<OutputFile>> GetByBatchIdAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OutputFiles
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.OutputType)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OutputFile>> GetPendingOutputsAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OutputFiles
            .Where(x =>
                x.BatchId == batchId &&
                (x.Status == OutputStatus.Pending ||
                 x.Status == OutputStatus.RetryPending))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        OutputFile outputFile,
        CancellationToken cancellationToken = default)
    {
        await _context.OutputFiles.AddAsync(
            outputFile,
            cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<OutputFile> outputFiles,
        CancellationToken cancellationToken = default)
    {
        await _context.OutputFiles.AddRangeAsync(
            outputFiles,
            cancellationToken);
    }

    public void Update(OutputFile outputFile)
    {
        _context.OutputFiles.Update(outputFile);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}