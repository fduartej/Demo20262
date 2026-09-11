namespace _20262.Integrations.Algolia;

public class AlgoliaOptions
{
    public const string SectionName = "Algolia";

    public string ApplicationId { get; set; } = string.Empty;

    public string ApiKeySearch { get; set; } = string.Empty;

    public string ApiKeyWrite { get; set; } = string.Empty;

    public string IndexName { get; set; } = "productos";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApplicationId) &&
        !string.IsNullOrWhiteSpace(ApiKeySearch) &&
        !string.IsNullOrWhiteSpace(ApiKeyWrite);
}