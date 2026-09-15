using Microsoft.Extensions.DependencyInjection;
using XmlMiddleware.Application.Interfaces.Repositories;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Infrastructure.Auditing;
using XmlMiddleware.Infrastructure.BlobStorage;
using XmlMiddleware.Infrastructure.FileGeneration;
using XmlMiddleware.Infrastructure.Hashing;
using XmlMiddleware.Infrastructure.Leasing;
using XmlMiddleware.Infrastructure.Mapping;
using XmlMiddleware.Infrastructure.ServiceBus;
using XmlMiddleware.Infrastructure.Validation;
using XmlMiddleware.Persistence.Repositories;

namespace XmlMiddleware.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IProcessingBatchRepository, ProcessingBatchRepository>();

        services.AddScoped<IOutputFileRepository, OutputFileRepository>();

        services.AddScoped<IProcessingEventRepository, ProcessingEventRepository>();

        services.AddScoped<IHashService, HashService>();

        services.AddScoped<IAuditService, AuditService>();

        services.AddScoped<IXmlValidationService, XmlValidationService>();

        services.AddScoped<ILeaseService, LeaseService>();

        services.AddScoped<IMessagePublisherService, QueuePublisherService>();

        services.AddScoped<IBlobStorageService, BlobStorageService>();

        services.AddScoped<IBlobStorageService, BlobStorageService>();

        services.AddScoped<IMessagePublisherService, QueuePublisherService>();

        services.AddScoped<IXmlMapperService, XmlMapperService>();

        services.AddScoped<TxtGenerator>();

        services.AddScoped<IFileGeneratorService, FileGeneratorService>();

        services.AddScoped<CsvGenerator>();

        services.AddScoped<JsonGenerator>();

        services.AddScoped<DatGenerator>();

        services.AddScoped<PdfGenerator>();

        services.AddScoped<XlsxGenerator>();

        return services;
    }
}