using Microsoft.EntityFrameworkCore;

namespace MediTech.Models
{
    public class MediTechContext : DbContext
    {
        public MediTechContext(DbContextOptions<MediTechContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Paciente> Pacientes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Role>()
                .HasIndex(r => r.NombreRol)
                .IsUnique();
                
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}
