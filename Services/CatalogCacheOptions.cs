namespace _20262.Services;

public class CatalogCacheOptions
{
    public const string SectionName = "Redis";

    public string ProductosKey { get; set; } = "CatalogoProductos";

    public string CategoriasKey { get; set; } = "CatalogoCategorias";

    public int TtlMinutes { get; set; } = 60;

    public bool WarmupOnStartup { get; set; } = true;
}