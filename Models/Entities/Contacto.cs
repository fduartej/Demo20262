using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _20262.Models.Entities;

[Table("t_contactos")]
public class Contacto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    /// <summary>Sentimiento predicho por el modelo de ML.NET: "Positivo" o "Negativo" (null si no se clasificó).</summary>
    [StringLength(20)]
    public string? Sentimiento { get; set; }

    /// <summary>Probabilidad (0-1) de la clase predicha por el modelo.</summary>
    public double? ProbabilidadSentimiento { get; set; }
}
