namespace _20262.Integrations.Algolia;

public class AlgoliaSearchResult<T>
{
    public List<T> Hits { get; set; } = [];

    public int NbHits { get; set; }

    public int Page { get; set; }

    public int HitsPerPage { get; set; }

    public int NbPages { get; set; }

    public Dictionary<string, int>? Facets { get; set; }
}