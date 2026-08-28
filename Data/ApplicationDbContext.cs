using Microsoft.EntityFrameworkCore;
using _20262.Models;

namespace _20262.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Contacto> Contactos { get; set; }

    public DbSet<Producto> Productos { get; set; }
}
