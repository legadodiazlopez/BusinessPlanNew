using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Compania> Companias { get; set; }
        public DbSet<Trabajador> Trabajadores { get; set; }
        public DbSet<Mensaje> Mensajes { get; set; }
        public DbSet<Resena> Resenas { get; set; }

        // Añade este bloque para quitar las advertencias de precisión:
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Compania>().Property(c => c.MontoPago).HasPrecision(18, 2);
            modelBuilder.Entity<Trabajador>().Property(t => t.Salario).HasPrecision(18, 2);
        }
    }
}