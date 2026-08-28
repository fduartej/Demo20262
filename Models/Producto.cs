using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _20262.Models;

[Table("t_productos")]
public class Producto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public int CategoriaId { get; set; }

    [ForeignKey(nameof(CategoriaId))]
    public Categoria? Categoria { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Precio { get; set; }

    [Required]
    [StringLength(1000)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string ImagenUrl { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "int")]
    public int Stock { get; set; }

    [NotMapped]
    public bool SinStock => Stock <= 0;
}
