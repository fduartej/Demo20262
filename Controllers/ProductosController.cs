using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using _20262.Models.Entities;
using _20262.Models.ViewModels;
using _20262.Services;

namespace _20262.Controllers;

public class ProductosController : Controller
{
    private readonly ProductoService _productoService;
    private readonly PieSocketService _pieSocketService;
    private readonly Microsoft.Extensions.Options.IOptions<PieSocketOptions> _pieSocketOptions;

    private const string SessionKey = "ProductosRecordados";

    public ProductosController(ProductoService productoService, PieSocketService pieSocketService, Microsoft.Extensions.Options.IOptions<PieSocketOptions> pieSocketOptions)
    {
        _productoService = productoService;
        _pieSocketService = pieSocketService;
        _pieSocketOptions = pieSocketOptions;
    }

    public async Task<IActionResult> Index(int? categoriaId, string? busqueda)
    {
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
            Recordados = ObtenerRecordados(),
            WebSocketUrl = _pieSocketOptions.Value.WebSocketUrl
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReducirStock(int id)
    {
        var nuevoStock = await _productoService.ReducirStockAsync(id);

        if (nuevoStock.HasValue)
        {
            await _pieSocketService.PublicarStockReducidoAsync(id, nuevoStock.Value);
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
