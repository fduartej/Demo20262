using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using _20262.Models;

namespace _20262.Controllers;

public class ProductosController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _environment;

    public ProductosController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public IActionResult Index()
    {
        var path = Path.Combine(_environment.WebRootPath, "data", "productos.json");
        var productos = new List<Producto>();

        if (System.IO.File.Exists(path))
        {
            var json = System.IO.File.ReadAllText(path);
            productos = JsonSerializer.Deserialize<List<Producto>>(json, JsonOptions) ?? [];
        }

        return View(new ProductoViewModel { Productos = productos });
    }
}
