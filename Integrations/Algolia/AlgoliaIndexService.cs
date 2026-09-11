using Algolia.Search.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using _20262.Data;

namespace _20262.Integrations.Algolia;

public interface IAlgoliaIndexService
{
    Task<int> SyncProductosAsync(CancellationToken cancellationToken = default);
    Task DeleteProductoAsync(int productoId, CancellationToken cancellationToken = default);
}

public class AlgoliaIndexService : IAlgoliaIndexService
{
    private readonly AlgoliaOptions _options;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AlgoliaIndexService> _logger;
    private readonly Lazy<SearchClient> _client;

    public AlgoliaIndexService(
        IOptions<AlgoliaOptions> options,
        ApplicationDbContext context,
        ILogger<AlgoliaIndexService> logger)
    {
        _options = options.Value;
        _context = context;
        _logger = logger;
        _client = new Lazy<SearchClient>(() => new SearchClient(_options.ApplicationId, _options.ApiKeyWrite));
    }

    public async Task<int> SyncProductosAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing products to Algolia index '{IndexName}'...", _options.IndexName);

        var productos = await _context.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .ToListAsync(cancellationToken);

        var records = productos.Select(p => new AlgoliaProducto
        {
            ObjectID = p.Id.ToString(),
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Precio = p.Precio,
            ImagenUrl = p.ImagenUrl,
            Stock = p.Stock,
            CategoriaId = p.CategoriaId,
            CategoriaNombre = p.Categoria?.Nombre ?? string.Empty
        }).ToList();

        await _client.Value.ReplaceAllObjectsAsync(_options.IndexName, records, 1000, null, null, cancellationToken);

        _logger.LogInformation("Indexed {Count} products to Algolia.", records.Count);
        return records.Count;
    }

    public async Task DeleteProductoAsync(int productoId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.Value.DeleteObjectAsync(_options.IndexName, productoId.ToString(), null, cancellationToken);
            _logger.LogInformation("Deleted product {ProductId} from Algolia.", productoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId} from Algolia.", productoId);
        }
    }
}