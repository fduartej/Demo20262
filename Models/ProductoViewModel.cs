namespace _20262.Models;

public class ProductoViewModel
{
    public List<Producto> Productos { get; set; } = [];
}

public class Producto
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public string ImagenUrl { get; set; } = string.Empty;

    public int Stock { get; set; }

    public bool SinStock => Stock <= 0;
}
