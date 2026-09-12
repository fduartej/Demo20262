using System.Text.Json;
using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _20262.Integrations.Algolia;

public class AlgoliaSearchService : IAlgoliaSearchService
{
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaSearchService> _logger;
    private readonly Lazy<SearchClient> _client;

    public AlgoliaSearchService(
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaSearchService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<SearchClient>(() => new SearchClient(_options.ApplicationId, _options.ApiKeySearch));
    }

    public async Task<AlgoliaSearchResult<AlgoliaProducto>> SearchAsync(
        string query,
        int? categoriaId = null,
        int page = 0,
        int hitsPerPage = 20,
        CancellationToken cancellationToken = default)
    {
        var queryLog = SanitizeForLog(query);
        _logger.LogInformation("Algolia.SearchAsync | AppId='{AppId}' Index='{Index}' Query='{Query}' CategoriaId={CategoriaId} Page={Page} HitsPerPage={HitsPerPage}",
            _options.ApplicationId, _options.IndexName, queryLog, categoriaId, page, hitsPerPage);

        try
        {
            var searchParams = new SearchParams(new SearchParamsObject
            {
                Query = query,
                Filters = categoriaId.HasValue ? $"CategoriaId:{categoriaId.Value}" : null,
                Page = page,
                HitsPerPage = hitsPerPage,
                Facets = ["CategoriaId"]
            });

            var response = await _client.Value.SearchSingleIndexAsync<AlgoliaProducto>(
                _options.IndexName, searchParams, null, cancellationToken);

            var facets = response.Facets?.TryGetValue("CategoriaId", out var categoriaFacets) == true
                ? categoriaFacets
                : null;

            _logger.LogInformation("Algolia.SearchAsync | Respuesta: {Hits} hits, {NbPages} páginas, página {Page}, {Facets} facets de categoría.",
                response.NbHits, response.NbPages, response.Page, facets?.Count ?? 0);

            var hits = response.Hits ?? [];
            for (var i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                _logger.LogInformation(
                    "Algolia.Hit[{Index}] | ObjectID={ObjectID} Id={Id} Nombre='{Nombre}' Precio={Precio} Stock={Stock} CategoriaId={CategoriaId} CategoriaNombre='{CategoriaNombre}' ImagenUrl='{ImagenUrl}' Descripcion='{Descripcion}'",
                    i, h.ObjectID, h.Id, h.Nombre, h.Precio, h.Stock, h.CategoriaId, h.CategoriaNombre, h.ImagenUrl, h.Descripcion);
            }

            if (hits.Count > 0)
            {
                _logger.LogInformation("Algolia.Debug | Primer hit (deserializado): {Json}",
                    JsonSerializer.Serialize(hits[0]));

                var primerHitVacio = hits[0].Id == 0 && string.IsNullOrEmpty(hits[0].Nombre);
                if (primerHitVacio)
                {
                    try
                    {
                        var raw = await _client.Value.SearchSingleIndexWithHTTPInfoAsync(
                            _options.IndexName, searchParams, null, cancellationToken);
                        if (raw?.Body is not null)
                        {
                            using var reader = new StreamReader(raw.Body);
                            var json = await reader.ReadToEndAsync(cancellationToken);
                            if (json.Length > 8000)
                            {
                                json = json[..8000] + "... (truncado)";
                            }
                            _logger.LogInformation("Algolia.Debug | Respuesta cruda de Algolia: {Json}", json);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Algolia.Debug | No se pudo obtener la respuesta cruda.");
                    }
                }
            }

            if (facets is not null)
            {
                foreach (var (nombre, conteo) in facets)
                {
                    _logger.LogInformation("Algolia.Facet | Categoría '{Nombre}' => {Conteo}", nombre, conteo);
                }
            }

            return new AlgoliaSearchResult<AlgoliaProducto>
            {
                Hits = hits,
                NbHits = response.NbHits ?? 0,
                Page = response.Page ?? 0,
                HitsPerPage = response.HitsPerPage ?? hitsPerPage,
                NbPages = response.NbPages ?? 0,
                Facets = facets
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Algolia.SearchAsync | Error buscando '{Query}' en Algolia.", queryLog);
            return new AlgoliaSearchResult<AlgoliaProducto>();
        }
    }

    private static string SanitizeForLog(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r", "\\r").Replace("\n", "\\n");
}