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

    public async Task<OutputFile?> GetByIdAsync(long outputFileId)
    {
        return await _context.OutputFiles
            .FirstOrDefaultAsync(x => x.OutputFileId == outputFileId);
    }

    public async Task<List<OutputFile>> GetByBatchIdAsync(long batchId)
    {
        return await _context.OutputFiles
            .Where(x => x.BatchId == batchId)
            .ToListAsync();
    }

    public async Task AddAsync(OutputFile outputFile)
    {
        await _context.OutputFiles.AddAsync(outputFile);
    }

    public async Task AddRangeAsync(IEnumerable<OutputFile> outputFiles)
    {
        await _context.OutputFiles.AddRangeAsync(outputFiles);
    }

    public void Update(OutputFile outputFile)
    {
        _context.OutputFiles.Update(outputFile);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<List<OutputFile>> GetPendingOutputsAsync(long batchId)
    {
        return await _context.OutputFiles
            .Where(x =>
                x.BatchId == batchId &&
                (x.Status == OutputStatus.Pending ||
                 x.Status == OutputStatus.RetryPending))
            .ToListAsync();
    }
}