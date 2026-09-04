using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _20262.Data;
using _20262.Models;

namespace _20262.Controllers;

public class ProductosController : Controller
{
    private readonly ApplicationDbContext _context;

    private const string SessionKey = "ProductosRecordados";

    public ProductosController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index(int? categoriaId, string? busqueda)
    {
        IQueryable<Producto> productos = _context.Productos
            .Include(p => p.Categoria);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim();
            productos = productos.Where(p => p.Nombre.Contains(termino));
        }

        if (categoriaId.HasValue)
        {
            productos = productos.Where(p => p.CategoriaId == categoriaId.Value);
        }

        return View(new ProductoViewModel
        {
            Productos = productos.ToList(),
            Categorias = _context.Categorias.OrderBy(c => c.Nombre).ToList(),
            CategoriaId = categoriaId,
            Busqueda = busqueda,
            Recordados = ObtenerRecordados()
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
