namespace _20262.Models;

public class ProductoViewModel
{
    public List<Producto> Productos { get; set; } = [];

    public List<Categoria> Categorias { get; set; } = [];

    public int? CategoriaId { get; set; }

    public string? Busqueda { get; set; }

    public List<int> Recordados { get; set; } = [];
}