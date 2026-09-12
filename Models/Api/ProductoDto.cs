using System.ComponentModel.DataAnnotations;
using _20262.Models.Entities;

namespace _20262.Models.Api;

public class ProductoDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public int CategoriaId { get; set; }

    public string? Categoria { get; set; }

    [Required]
    [Range(0, 99999999.99, ErrorMessage = "El precio debe ser mayor o igual a 0.")]
    public decimal Precio { get; set; }

    [Required]
    [StringLength(1000)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string ImagenUrl { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "El stock debe ser mayor o igual a 0.")]
    public int Stock { get; set; }

    public bool SinStock { get; set; }

    public static ProductoDto FromEntity(Producto producto) => new()
    {
        Id = producto.Id,
        Nombre = producto.Nombre,
        CategoriaId = producto.CategoriaId,
        Categoria = producto.Categoria?.Nombre,
        Precio = producto.Precio,
        Descripcion = producto.Descripcion,
        ImagenUrl = producto.ImagenUrl,
        Stock = producto.Stock,
        SinStock = producto.SinStock
    };
}