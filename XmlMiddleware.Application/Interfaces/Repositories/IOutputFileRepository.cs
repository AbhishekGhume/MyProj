using XmlMiddleware.Domain.Entities;

namespace XmlMiddleware.Application.Interfaces.Repositories;

public interface IOutputFileRepository
{
    Task<OutputFile?> GetByIdAsync(
        long outputFileId,
        CancellationToken cancellationToken = default);

    Task<List<OutputFile>> GetByBatchIdAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task<List<OutputFile>> GetPendingOutputsAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        OutputFile outputFile,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<OutputFile> outputFiles,
        CancellationToken cancellationToken = default);

    void Update(OutputFile outputFile);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}