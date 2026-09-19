using System.ComponentModel.DataAnnotations;

namespace _20262.Models.Api;

public class PedidoRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Debe indicar un producto válido.")]
    public int ProductoId { get; set; }

    [Range(1, 99, ErrorMessage = "La cantidad debe estar entre 1 y 99.")]
    public int Cantidad { get; set; } = 1;
}