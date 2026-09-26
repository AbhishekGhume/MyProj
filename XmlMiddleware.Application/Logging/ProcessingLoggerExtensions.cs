using Microsoft.Extensions.Logging;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Application.Logging;

/// <summary>
/// One log format for the whole pipeline:
///   {Message} | CorrelationId=.. | BatchId=.. | FileName=.. [| Error=..]
/// Searching by any one of CorrelationId, BatchId or FileName returns every line.
/// Database-related lines start with "DB:".
/// </summary>
public static class ProcessingLoggerExtensions
{
    public static void LogProcessing(
        this ILogger logger,
        ProcessingContext context,
        LogLevel level,
        string message,
        string? error = null,
        Exception? exception = null)
    {
        logger.LogProcessing(
            level,
            context.CorrelationId,
            context.BatchId,
            context.FileName,
            message,
            error,
            exception);
    }

    public static void LogProcessing(
        this ILogger logger,
        LogLevel level,
        Guid correlationId,
        long? batchId,
        string fileName,
        string message,
        string? error = null,
        Exception? exception = null)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var errorText = error ?? exception?.Message;

        if (errorText is null)
        {
            logger.Log(
                level,
                "{Message} | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName}",
                message,
                correlationId,
                batchId,
                fileName);

            return;
        }

        logger.Log(
            level,
            exception,
            "{Message} | CorrelationId={CorrelationId} | BatchId={BatchId} | FileName={FileName} | Error={Error}",
            message,
            correlationId,
            batchId,
            fileName,
            errorText);
    }
}