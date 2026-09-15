using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Interfaces.Repositories;

public interface IOutputFileRepository
{
    Task<OutputFile?> GetByIdAsync(long outputFileId);

    Task<List<OutputFile>> GetByBatchIdAsync(long batchId);

    Task<List<OutputFile>> GetPendingOutputsAsync(long batchId);

    Task AddAsync(OutputFile outputFile);

    Task AddRangeAsync(IEnumerable<OutputFile> outputFiles);

    void Update(OutputFile outputFile);

    Task SaveChangesAsync();
}