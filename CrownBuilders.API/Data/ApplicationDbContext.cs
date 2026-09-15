using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Compania> Companias { get; set; }
        public DbSet<Trabajador> Trabajadores { get; set; }
        public DbSet<Conversacion> Conversaciones { get; set; }
        public DbSet<Mensaje> Mensajes { get; set; }
        public DbSet<Resena> Resenas { get; set; }

        // Añade este bloque para quitar las advertencias de precisión:
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Compania>().Property(c => c.MontoPago).HasPrecision(18, 2);
            modelBuilder.Entity<Trabajador>().Property(t => t.Salario).HasPrecision(18, 2);

            modelBuilder.Entity<Conversacion>()
                .HasIndex(c => new { c.TrabajadorId, c.CompaniaId })
                .IsUnique();

            modelBuilder.Entity<Conversacion>()
                .HasMany(c => c.Mensajes)
                .WithOne(m => m.Conversacion)
                .HasForeignKey(m => m.ConversacionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Mensaje>()
                .HasIndex(m => new { m.ConversacionId, m.ClientMessageId })
                .IsUnique();

            modelBuilder.Entity<Mensaje>()
                .Property(m => m.Texto)
                .HasMaxLength(4000);
        }
    }
}
