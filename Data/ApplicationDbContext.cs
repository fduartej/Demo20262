using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using _20262.Models.Entities;

namespace _20262.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Contacto> Contactos { get; set; }

    public DbSet<Producto> Productos { get; set; }

    public DbSet<Categoria> Categorias { get; set; }
}
