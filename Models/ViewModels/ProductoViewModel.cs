using _20262.Integrations.Algolia;
using _20262.Models.Entities;

namespace _20262.Models.ViewModels;

public class ProductoViewModel
{
    public List<Producto> Productos { get; set; } = [];

    public List<Categoria> Categorias { get; set; } = [];

    public int? CategoriaId { get; set; }

    public string? Busqueda { get; set; }

    public List<int> Recordados { get; set; } = [];

    public AlgoliaSearchResult<AlgoliaProducto>? AlgoliaResult { get; set; }
}