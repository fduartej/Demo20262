using _20262.Models.Entities;

namespace _20262.Models.ViewModels;

public class ProductoDetalleViewModel
{
    public Producto Producto { get; set; } = new();

    public bool Recordado { get; set; }
}