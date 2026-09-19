using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _20262.Models.Entities;

[Table("t_pedidos_registrados")]
public class PedidoRegistrado
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid OrdenId { get; set; }

    [Required]
    public int ProductoId { get; set; }

    [Required]
    [StringLength(100)]
    public string NombreProducto { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Precio { get; set; }

    [Required]
    public int Cantidad { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime RegistradoEn { get; set; }

    [Required]
    [StringLength(50)]
    public string Estado { get; set; } = string.Empty;
}