using System.Text.Json.Serialization;

namespace _20262.Integrations.Algolia;

public class AlgoliaProducto
{
    [JsonPropertyName("objectID")]
    public string ObjectID { get; set; } = string.Empty;

    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("Descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("Precio")]
    public decimal Precio { get; set; }

    [JsonPropertyName("ImagenUrl")]
    public string ImagenUrl { get; set; } = string.Empty;

    [JsonPropertyName("Stock")]
    public int Stock { get; set; }

    [JsonPropertyName("CategoriaId")]
    public int CategoriaId { get; set; }

    [JsonPropertyName("CategoriaNombre")]
    public string CategoriaNombre { get; set; } = string.Empty;
}