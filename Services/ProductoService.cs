using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using _20262.Data;
using _20262.Models;

namespace _20262.Services;

public class ProductoService
{
    private readonly IDistributedCache _cache;
    private readonly ApplicationDbContext _context;
    private readonly CatalogCacheOptions _options;
    private readonly ILogger<ProductoService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };

    public ProductoService(
        IDistributedCache cache,
        ApplicationDbContext context,
        IOptions<CatalogCacheOptions> options,
        ILogger<ProductoService> logger)
    {
        _cache = cache;
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<Producto>> ObtenerProductosAsync(CancellationToken cancellationToken = default)
    {
        var cacheado = await ObtenerDeCacheAsync<List<Producto>>(_options.ProductosKey, cancellationToken);
        if (cacheado is not null)
        {
            return cacheado;
        }

        var productos = await _context.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .ToListAsync(cancellationToken);

        await GuardarEnCacheAsync(_options.ProductosKey, productos, cancellationToken);
        return productos;
    }

    public async Task<List<Categoria>> ObtenerCategoriasAsync(CancellationToken cancellationToken = default)
    {
        var cacheado = await ObtenerDeCacheAsync<List<Categoria>>(_options.CategoriasKey, cancellationToken);
        if (cacheado is not null)
        {
            return cacheado;
        }

        var categorias = await _context.Categorias
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .ToListAsync(cancellationToken);

        await GuardarEnCacheAsync(_options.CategoriasKey, categorias, cancellationToken);
        return categorias;
    }

    public async Task PrecalentarCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.WarmupOnStartup)
        {
            _logger.LogInformation("Warmup de catálogo deshabilitado (Redis:WarmupOnStartup=false).");
            return;
        }

        _logger.LogInformation("Precalentando catálogo en Redis...");
        var productos = await ObtenerProductosAsync(cancellationToken);
        var categorias = await ObtenerCategoriasAsync(cancellationToken);
        _logger.LogInformation("Catálogo precalentado en Redis: {Productos} productos, {Categorias} categorías.",
            productos.Count, categorias.Count);
    }

    private async Task<T?> ObtenerDeCacheAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var json = await _cache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al leer '{Key}' desde Redis.", key);
        }

        return null;
    }

    private async Task GuardarEnCacheAsync<T>(string key, T data, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.TtlMinutes)
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al guardar '{Key}' en Redis.", key);
        }
    }
}