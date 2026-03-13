using Microsoft.EntityFrameworkCore;

namespace MediTech.Models
{
    public class MediTechContext : DbContext
    {
        public MediTechContext(DbContextOptions<MediTechContext> options) : base(options)
        {
        }

        public DbSet<Estado> Estados { get; set; } = null!;
        public DbSet<TipoIdentificacion> TiposIdentificacion { get; set; } = null!;
        public DbSet<Genero> Generos { get; set; } = null!;
        public DbSet<Persona> Personas { get; set; } = null!;
        public DbSet<Empleado> Empleados { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Paciente> Pacientes { get; set; } = null!;
        public DbSet<Tratamiento> Tratamientos { get; set; } = null!;
        public DbSet<Cita> Citas { get; set; } = null!;
        public DbSet<GoogleToken> GoogleTokens { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Schema configurations are already defined via [Table] attributes in models
            
            // Unique constraints
            modelBuilder.Entity<Persona>()
                .HasIndex(p => new { p.IdTipoIdentificacion, p.NumIdentificacion })
                .IsUnique();
                
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Cita>()
                .HasIndex(c => new { c.IdPaciente, c.Fecha, c.HoraInicio })
                .IsUnique();

            // Default values
            modelBuilder.Entity<Estado>()
                .Property(e => e.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");

            modelBuilder.Entity<Persona>()
                .Property(p => p.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");

            modelBuilder.Entity<Paciente>()
                .Property(p => p.FechaRegistro)
                .HasDefaultValueSql("SYSDATETIME()");

            modelBuilder.Entity<Usuario>()
                .Property(u => u.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");

            modelBuilder.Entity<Cita>()
                .Property(c => c.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");
        }
    }
}
