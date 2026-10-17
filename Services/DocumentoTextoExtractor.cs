using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace _20262.Services;

public static class DocumentoTextoExtractor
{
    public static readonly string[] ExtensionesPermitidas = [".md", ".markdown", ".txt", ".pdf"];

    public static string ObtenerTipo(string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "pdf",
            ".md" or ".markdown" => "markdown",
            _ => "texto"
        };
    }

    public static async Task<string> ExtraerTextoAsync(
        IFormFile archivo,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extension == ".pdf")
        {
            return ExtraerPdf(archivo, cancellationToken);
        }

        using var reader = new StreamReader(archivo.OpenReadStream(), Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string ExtraerPdf(IFormFile archivo, CancellationToken cancellationToken)
    {
        using var stream = archivo.OpenReadStream();
        using var documento = PdfDocument.Open(stream);

        var texto = new StringBuilder();
        foreach (var pagina in documento.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            texto.AppendLine(ContentOrderTextExtractor.GetText(pagina));
            texto.AppendLine();
        }

        return texto.ToString();
    }
}
