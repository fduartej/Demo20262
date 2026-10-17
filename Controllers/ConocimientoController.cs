using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using _20262.Integrations.Groq;
using _20262.Integrations.Qdrant;
using _20262.Models.ViewModels;
using _20262.Services;

namespace _20262.Controllers;

/// <summary>
/// Módulo de administración para cargar documentos (Markdown, texto o PDF) a la base de
/// conocimiento vectorial en Qdrant Cloud, que luego consumirá el agente de Groq.
/// </summary>
[Authorize(Roles = "Admin")]
public class ConocimientoController : Controller
{
    private const long TamanoMaximoBytes = 10 * 1024 * 1024;

    private readonly IKnowledgeBaseService _conocimiento;
    private readonly QdrantOptions _qdrant;
    private readonly GroqOptions _groq;
    private readonly ILogger<ConocimientoController> _logger;

    public ConocimientoController(
        IKnowledgeBaseService conocimiento,
        IOptions<QdrantOptions> qdrant,
        IOptions<GroqOptions> groq,
        ILogger<ConocimientoController> logger)
    {
        _conocimiento = conocimiento;
        _qdrant = qdrant.Value;
        _groq = groq.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? busqueda, int pagina = 1, CancellationToken cancellationToken = default)
    {
        return View(await ConstruirModeloAsync(busqueda, pagina, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cargar(IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            TempData["ConocimientoError"] = "Selecciona un archivo Markdown (.md), texto (.txt) o PDF.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!DocumentoTextoExtractor.ExtensionesPermitidas.Contains(extension))
        {
            TempData["ConocimientoError"] =
                $"Formato no soportado ({extension}). Formatos permitidos: .md, .markdown, .txt y .pdf.";
            return RedirectToAction(nameof(Index));
        }

        if (archivo.Length > TamanoMaximoBytes)
        {
            TempData["ConocimientoError"] = "El archivo supera el límite de 10 MB.";
            return RedirectToAction(nameof(Index));
        }

        if (!_conocimiento.EstaConfigurado)
        {
            TempData["ConocimientoError"] =
                "Qdrant no está configurado. Completa la sección Qdrant en appsettings.json.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var nombre = Path.GetFileName(archivo.FileName);
            var tipo = DocumentoTextoExtractor.ObtenerTipo(nombre);
            var texto = await DocumentoTextoExtractor.ExtraerTextoAsync(archivo, cancellationToken);

            if (string.IsNullOrWhiteSpace(texto))
            {
                TempData["ConocimientoError"] = "No se pudo extraer texto del documento.";
                return RedirectToAction(nameof(Index));
            }

            var fragmentos = await _conocimiento.CargarDocumentoAsync(nombre, tipo, texto, cancellationToken);
            TempData["ConocimientoOk"] = $"'{nombre}' indexado en Qdrant: {fragmentos} fragmento(s).";

            _logger.LogInformation(
                "Conocimiento.Cargar | '{Documento}' ({Tipo}) cargado por {Usuario}: {Fragmentos} fragmentos.",
                nombre, tipo, User.Identity?.Name, fragmentos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al indexar el documento de conocimiento.");
            TempData["ConocimientoError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["ConocimientoError"] = "Documento no válido.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var eliminados = await _conocimiento.EliminarDocumentoAsync(id, cancellationToken);
            TempData["ConocimientoOk"] = $"Documento eliminado ({eliminados} fragmento(s)).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar el documento {DocumentoId}.", id);
            TempData["ConocimientoError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Probar(
        string? consulta,
        string? busqueda,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        var modelo = await ConstruirModeloAsync(busqueda, pagina, cancellationToken);
        modelo.Consulta = consulta;

        if (!string.IsNullOrWhiteSpace(consulta) && _conocimiento.EstaConfigurado)
        {
            try
            {
                modelo.Resultados = (await _conocimiento.BuscarAsync(consulta, 5, cancellationToken)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al probar la base de conocimiento.");
                TempData["ConocimientoError"] = ex.Message;
            }
        }

        return View(nameof(Index), modelo);
    }

    private async Task<ConocimientoViewModel> ConstruirModeloAsync(
        string? busqueda,
        int pagina,
        CancellationToken cancellationToken)
    {
        var modelo = new ConocimientoViewModel
        {
            QdrantConfigurado = _conocimiento.EstaConfigurado,
            QdrantEndpoint = _qdrant.Endpoint,
            Coleccion = _qdrant.CollectionName,
            ModeloEmbedding = _qdrant.EmbeddingModel,
            GroqConfigurado = _groq.EstaConfigurado,
            GroqModelo = _groq.Model,
            Busqueda = busqueda,
            Pagina = Math.Max(pagina, 1)
        };

        if (_conocimiento.EstaConfigurado)
        {
            try
            {
                modelo.Documentos = (await _conocimiento.ObtenerDocumentosAsync(busqueda, cancellationToken)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar los documentos de la base de conocimiento.");
                TempData["ConocimientoError"] = ex.Message;
            }
        }

        if (modelo.Pagina > modelo.TotalPaginas)
        {
            modelo.Pagina = modelo.TotalPaginas;
        }

        return modelo;
    }
}
