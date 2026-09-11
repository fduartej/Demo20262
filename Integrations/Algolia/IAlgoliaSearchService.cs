namespace _20262.Integrations.Algolia;

public interface IAlgoliaSearchService
{
    Task<AlgoliaSearchResult<AlgoliaProducto>> SearchAsync(
        string query,
        int? categoriaId = null,
        int page = 0,
        int hitsPerPage = 20,
        CancellationToken cancellationToken = default);
}