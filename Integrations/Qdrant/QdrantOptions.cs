namespace _20262.Integrations.Qdrant;

public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string ClusterId { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string CollectionName { get; set; } = "conocimiento_tienda";

    public string EmbeddingModel { get; set; } = "intfloat/multilingual-e5-small";

    public int VectorSize { get; set; } = 384;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(CollectionName);
}
