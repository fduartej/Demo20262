using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using _20262.Integrations.Algolia;
using _20262.Models.Entities;
using _20262.Models.ViewModels;
using _20262.Services;

namespace _20262.Controllers;

public class ProductosController : Controller
{
    private readonly ProductoService _productoService;
    private readonly IAlgoliaSearchService _algoliaSearch;
    private readonly IAlgoliaIndexService _algoliaIndex;
    private readonly bool _algoliaConfigurado;
    private readonly ILogger<ProductosController> _logger;

    private const string SessionKey = "ProductosRecordados";

    public ProductosController(
        ProductoService productoService,
        IAlgoliaSearchService algoliaSearch,
        IAlgoliaIndexService algoliaIndex,
        IOptions<AlgoliaOptions> algoliaOptions,
        ILogger<ProductosController> logger)
    {
        _productoService = productoService;
        _algoliaSearch = algoliaSearch;
        _algoliaIndex = algoliaIndex;
        _algoliaConfigurado = algoliaOptions.Value.IsConfigured;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int? categoriaId, string? busqueda, int page = 0)
    {
        _logger.LogInformation("Productos.Index | busqueda='{Busqueda}' categoriaId={CategoriaId} page={Page} algoliaConfigurado={Algolia}",
            busqueda, categoriaId, page, _algoliaConfigurado);

        if (!string.IsNullOrWhiteSpace(busqueda) && _algoliaConfigurado)
        {
            _logger.LogInformation("Productos.Index | Usando Algolia para la búsqueda '{Busqueda}'.", busqueda);
            return await SearchWithAlgolia(busqueda, categoriaId, page);
        }

        _logger.LogInformation("Productos.Index | Usando catálogo (Redis/DB) para el listado.");

        var productos = await _productoService.ObtenerProductosAsync();
        var categorias = await _productoService.ObtenerCategoriasAsync();

        IEnumerable<Producto> resultado = productos;

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim();
            resultado = resultado.Where(p => p.Nombre.Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        if (categoriaId.HasValue)
        {
            resultado = resultado.Where(p => p.CategoriaId == categoriaId.Value);
        }

        return View(new ProductoViewModel
        {
            Productos = resultado.ToList(),
            Categorias = categorias,
            CategoriaId = categoriaId,
            Busqueda = busqueda,
            Recordados = ObtenerRecordados()
        });
    }

    private async Task<IActionResult> SearchWithAlgolia(string busqueda, int? categoriaId, int page)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _algoliaSearch.SearchAsync(busqueda, categoriaId, page);
        stopwatch.Stop();

        _logger.LogInformation("Algolia | Consulta='{Busqueda}' CategoriaId={CategoriaId} page={Page} => {Hits} resultados ({NbPages} páginas) en {Ms}ms",
            busqueda, categoriaId, page, result.NbHits, result.NbPages, stopwatch.ElapsedMilliseconds);

        var categorias = await _productoService.ObtenerCategoriasAsync();

        var productos = result.Hits.Select(h => new Producto
        {
            Id = h.Id,
            Nombre = h.Nombre,
            Descripcion = h.Descripcion,
            Precio = h.Precio,
            ImagenUrl = h.ImagenUrl,
            Stock = h.Stock,
            CategoriaId = h.CategoriaId,
            Categoria = new Categoria
            {
                Id = h.CategoriaId,
                Nombre = string.IsNullOrWhiteSpace(h.CategoriaNombre)
                    ? categorias.FirstOrDefault(c => c.Id == h.CategoriaId)?.Nombre ?? string.Empty
                    : h.CategoriaNombre
            }
        }).ToList();

        var categoriasFiltradas = result.Facets?
            .Where(kvp => int.TryParse(kvp.Key, out _))
            .Select(kvp =>
            {
                var id = int.Parse(kvp.Key);
                var nombre = categorias.FirstOrDefault(c => c.Id == id)?.Nombre;
                return new Categoria { Id = id, Nombre = nombre ?? $"Categoría {id}" };
            })
            .ToList() ?? categorias;

        return View(new ProductoViewModel
        {
            Productos = productos,
            Categorias = categoriasFiltradas,
            CategoriaId = categoriaId,
            Busqueda = busqueda,
            Recordados = ObtenerRecordados(),
            AlgoliaResult = result
        });
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var productos = await _productoService.ObtenerProductosAsync();
        var producto = productos.FirstOrDefault(p => p.Id == id);

        if (producto is null)
        {
            _logger.LogWarning("Productos.Detalle | Producto {Id} no encontrado.", id);
            return NotFound();
        }

        return View(new ProductoDetalleViewModel
        {
            Producto = producto,
            Recordado = User.Identity?.IsAuthenticated == true && ObtenerRecordados().Contains(producto.Id)
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Recordar(int id)
    {
        var recordados = ObtenerRecordados();

        if (!recordados.Contains(id))
        {
            recordados.Add(id);
            GuardarRecordados(recordados);
        }

        return RedirectToLocal();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NoRecordar(int id)
    {
        var recordados = ObtenerRecordados();

        if (recordados.Remove(id))
        {
            GuardarRecordados(recordados);
        }

        return RedirectToLocal();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncAlgolia()
    {
        _logger.LogInformation("Algolia | Iniciando sincronización completa del índice por '{User}'.", User.Identity?.Name);
        var count = await _algoliaIndex.SyncProductosAsync();
        _logger.LogInformation("Algolia | Sincronización completada: {Count} productos.", count);
        TempData["AlgoliaSync"] = $"Se sincronizaron {count} productos en Algolia.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToLocal()
    {
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer))
        {
            return Redirect(referer);
        }

        return RedirectToAction(nameof(Index));
    }

    private List<int> ObtenerRecordados()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        return string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<int>>(json) ?? [];
    }

    private void GuardarRecordados(List<int> ids)
    {
        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(ids));
    }
}