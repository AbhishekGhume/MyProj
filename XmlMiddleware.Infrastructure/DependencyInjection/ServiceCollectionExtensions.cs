using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XmlMiddleware.Application.Configuration;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Infrastructure.Auditing;
using XmlMiddleware.Infrastructure.BlobStorage;
using XmlMiddleware.Infrastructure.FileGeneration;
using XmlMiddleware.Infrastructure.Hashing;
using XmlMiddleware.Infrastructure.Mapping;
using XmlMiddleware.Infrastructure.Persistence;
using XmlMiddleware.Infrastructure.ServiceBus;
using XmlMiddleware.Infrastructure.Validation;
using XmlMiddleware.Persistence.Repositories;

namespace XmlMiddleware.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        // Blob folder names come from app settings; defaults match the previous hard-coded values.
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();

            return new ProcessingFolders
            {
                Input = configuration["InputFolder"] ?? "input",
                InProcessing = configuration["InProcessingFolder"] ?? "inprocessing",
                Output = configuration["OutputFolder"] ?? "output",
                OutputStaging = configuration["OutputStagingFolder"] ?? "output-staging",
                Archive = configuration["ArchiveFolder"] ?? "archive",
                Error = configuration["ErrorFolder"] ?? "error",
                Container = configuration["ContainerName"] ?? string.Empty
            };
        });

        services.AddScoped<IProcessingBatchRepository, ProcessingBatchRepository>();

        services.AddScoped<IOutputFileRepository, OutputFileRepository>();

        services.AddScoped<IProcessingEventRepository, ProcessingEventRepository>();

        services.AddScoped<IHashService, HashService>();

        services.AddScoped<IAuditService, AuditService>();

        services.AddScoped<IXmlValidationService, XmlValidationService>();

        services.AddScoped<IBlobStorageService, BlobStorageService>();

        services.AddScoped<IXmlMapperService, XmlMapperService>();

        services.AddScoped<TxtGenerator>();

        services.AddScoped<CsvGenerator>();

        services.AddScoped<JsonGenerator>();

        services.AddScoped<DatGenerator>();

        services.AddScoped<PdfGenerator>();

        services.AddScoped<XlsxGenerator>();

        services.AddScoped<IFileGeneratorService, FileGeneratorService>();

        services.AddScoped<IOrderPersistenceService, OrderPersistenceService>();

        services.AddScoped<IServiceBusHealthService, ServiceBusHealthService>();

        return services;
    }
}