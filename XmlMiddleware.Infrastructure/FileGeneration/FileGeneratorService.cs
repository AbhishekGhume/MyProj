using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Models;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class FileGeneratorService : IFileGeneratorService
{
    private readonly TxtGenerator _txtGenerator;
    private readonly CsvGenerator _csvGenerator;
    private readonly JsonGenerator _jsonGenerator;
    private readonly DatGenerator _datGenerator;

    private readonly PdfGenerator _pdfGenerator;

    private readonly XlsxGenerator _xlsxGenerator;

    public FileGeneratorService(
        TxtGenerator txtGenerator,
        CsvGenerator csvGenerator,
        JsonGenerator jsonGenerator,
        DatGenerator datGenerator,
        PdfGenerator pdfGenerator,
        XlsxGenerator xlsxGenerator)
    {
        _txtGenerator = txtGenerator;
        _csvGenerator = csvGenerator;
        _jsonGenerator = jsonGenerator;
        _datGenerator = datGenerator;
        _pdfGenerator = pdfGenerator;
        _xlsxGenerator = xlsxGenerator;
    }

    public async Task<MemoryStream> GenerateAsync(
        List<CanonicalOrderModel> orders,
        OutputType outputType)
    {
        MemoryStream stream =
            outputType switch
            {
                OutputType.Txt =>
                    _txtGenerator.Generate(orders),

                OutputType.Csv =>
                    _csvGenerator.Generate(orders),

                OutputType.Json =>
                    _jsonGenerator.Generate(orders),

                OutputType.Dat =>
                    _datGenerator.Generate(orders),

                OutputType.Pdf =>
                    _pdfGenerator.Generate(orders),

                OutputType.Xlsx => 
                    _xlsxGenerator.Generate(orders),

                _ => throw new NotSupportedException(
                    $"Output type {outputType} not supported")
            };

        return await Task.FromResult(stream);
    }
}