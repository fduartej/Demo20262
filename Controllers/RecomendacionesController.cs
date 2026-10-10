using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using _20262.Models.ViewModels;
using _20262.Services;

namespace _20262.Controllers;

/// <summary>
/// Página de recomendaciones de productos para el usuario logueado, generadas con un modelo de
/// factorización de matrices (ML.NET) entrenado sobre product_ratings_simple_10k.csv.
/// </summary>
[Authorize]
public class RecomendacionesController : Controller
{
    private readonly IRecommendationService _recomendacionService;
    private readonly ProductoService _productoService;
    private readonly int _topK;

    public RecomendacionesController(
        IRecommendationService recomendacionService,
        ProductoService productoService,
        IOptions<RecommendationOptions> recomendacionOptions)
    {
        _recomendacionService = recomendacionService;
        _productoService = productoService;
        _topK = Math.Max(0, recomendacionOptions.Value.TopK);
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var catalogo = await _productoService.ObtenerProductosAsync(cancellationToken);

        var recomendados = await _recomendacionService.RecomendarAsync(
            userId,
            catalogo.Select(p => p.Id),
            _recomendacionService.EstaHabilitado ? _topK : 0,
            cancellationToken);

        var items = recomendados
            .Select(r => new RecomendacionProductoVM
            {
                Producto = catalogo.First(p => p.Id == r.ProductId),
                Score = r.Score,
                DeFrio = r.DeFrio
            })
            .ToList();

        return View(new RecomendacionesViewModel
        {
            ModeloListo = _recomendacionService.EstaListo,
            UsuarioConocido = _recomendacionService.EsUsuarioConocido(userId),
            TopK = _topK,
            Recomendaciones = items,
            Metricas = _recomendacionService.UltimasMetricas
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reentrenar(CancellationToken cancellationToken)
    {
        var ok = await _recomendacionService.ReentrenarAsync(cancellationToken);

        TempData[ok ? "RecomendacionOk" : "RecomendacionError"] = ok
            ? "Modelo de recomendación reentrenado correctamente."
            : "No se pudo reentrenar el modelo. Revisa el log y la configuración Recomendacion.";

        return RedirectToAction(nameof(Index));
    }
}