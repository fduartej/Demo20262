using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _20262.Data;
using _20262.Models;

namespace _20262.Controllers;

public class ProductosController : Controller
{
    private readonly ApplicationDbContext _context;

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
            Busqueda = busqueda
        });
    }
}
