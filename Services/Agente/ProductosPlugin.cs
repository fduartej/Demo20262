using System.ComponentModel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using _20262.Data;
using _20262.Models.Api;
using _20262.Models.Entities;

namespace _20262.Services.Agente;

/// <summary>
/// Funciones del agente para consultar el catálogo de productos (tabla "t_productos"): nombre,
/// precio, stock, descripción e imagen. Los productos encontrados se envían al chat como tarjetas.
/// </summary>
public sealed class ProductosPlugin
{
    private const int MaxResultados = 5;

    private readonly ApplicationDbContext _context;
    private readonly SesionAgente _sesion;
    private readonly ILogger<ProductosPlugin> _logger;

    public ProductosPlugin(
        ApplicationDbContext context,
        SesionAgente sesion,
        ILogger<ProductosPlugin> logger)
    {
        _context = context;
        _sesion = sesion;
        _logger = logger;
    }

    [KernelFunction("BuscarProductos")]
    [Description(
        "Busca productos en el catálogo de la tienda por nombre o descripción y devuelve su precio, stock, " +
        "categoría y descripción. Úsala cuando pregunten por un producto, su precio o su disponibilidad. " +
        "La interfaz mostrará automáticamente una tarjeta con la imagen y los datos del producto.")]
    public async Task<string> BuscarProductosAsync(
        [Description("Nombre o palabras clave del producto, por ejemplo 'alimento para perros'.")] string texto,
        CancellationToken cancellationToken = default)
    {
        var busqueda = texto?.Trim() ?? string.Empty;
        if (busqueda.Length < 2)
        {
            return "Indica el nombre o palabras clave del producto que se debe buscar.";
        }

        var termino = busqueda.ToLowerInvariant();

        try
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Nombre.ToLower().Contains(termino) || p.Descripcion.ToLower().Contains(termino))
                .OrderBy(p => p.Nombre)
                .Take(MaxResultados)
                .ToListAsync(cancellationToken);

            if (productos.Count == 0)
            {
                return $"No se encontraron productos que coincidan con '{busqueda}' en el catálogo de la tienda.";
            }

            foreach (var producto in productos)
            {
                _sesion.AgregarProducto(TarjetaProducto.Desde(producto));
            }

            return Formatear("Resultados del catálogo:", productos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo buscar en el catálogo de productos.");
            return "No se pudo consultar el catálogo de productos en este momento.";
        }
    }

    [KernelFunction("ObtenerProductoPorId")]
    [Description(
        "Obtiene un producto del catálogo por su identificador numérico e incluye precio, stock, " +
        "descripción, categoría e imagen. Úsala para dar el detalle exacto de un producto concreto.")]
    public async Task<string> ObtenerProductoPorIdAsync(
        [Description("Identificador numérico del producto, por ejemplo 1.")] int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return "El identificador del producto debe ser un número positivo.";
        }

        try
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (producto is null)
            {
                return $"No existe un producto con el identificador {id} en el catálogo de la tienda.";
            }

            _sesion.AgregarProducto(TarjetaProducto.Desde(producto));
            return Formatear("Producto encontrado:", [producto]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo obtener el producto {ProductoId} del catálogo.", id);
            return "No se pudo consultar el catálogo de productos en este momento.";
        }
    }

    private static string Formatear(string titulo, IReadOnlyList<Producto> productos)
    {
        var respuesta = new StringBuilder(titulo);

        foreach (var producto in productos)
        {
            respuesta.AppendLine();
            respuesta.AppendLine();
            respuesta.AppendLine(
                $"{producto.Nombre} (ID {producto.Id}) - Categoría: {producto.Categoria?.Nombre ?? "Sin categoría"} - " +
                $"Precio: S/ {producto.Precio:0.00} - {DescribirStock(producto.Stock)}");
            respuesta.Append($"Descripción: {producto.Descripcion}");
        }

        return respuesta.ToString();
    }

    private static string DescribirStock(int stock) =>
        stock <= 0
            ? "sin stock por el momento"
            : $"stock disponible: {stock} {(stock == 1 ? "unidad" : "unidades")}";
}
