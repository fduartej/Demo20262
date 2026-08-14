using System.ComponentModel.DataAnnotations;

namespace _20262.Models;

public class ContactViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El mensaje es obligatorio")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre 10 y 1000 caracteres")]
    public string Message { get; set; } = string.Empty;
}
