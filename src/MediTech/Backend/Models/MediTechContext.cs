using Microsoft.EntityFrameworkCore;

namespace MediTech.Backend.Models
{
    public class MediTechContext : DbContext
    {
        public MediTechContext(DbContextOptions<MediTechContext> options) : base(options)
        {
        }

        public DbSet<Estado> Estados { get; set; } = null!;
        public DbSet<TipoIdentificacion> TiposIdentificacion { get; set; } = null!;
        public DbSet<Genero> Generos { get; set; } = null!;
        public DbSet<Moneda> Monedas { get; set; } = null!;
        public DbSet<Persona> Personas { get; set; } = null!;
        public DbSet<Empleado> Empleados { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Paciente> Pacientes { get; set; } = null!;
        public DbSet<PosiblePaciente> PosiblePacientes { get; set; } = null!;
        public DbSet<Tratamiento> Tratamientos { get; set; } = null!;
        public DbSet<EstadoCita> EstadosCita { get; set; } = null!;
        public DbSet<Cita> Citas { get; set; } = null!;
        public DbSet<Consulta> Consultas { get; set; } = null!;
        public DbSet<SignosVitales> SignosVitales { get; set; } = null!;
        public DbSet<Diagnostico> Diagnosticos { get; set; } = null!;
        public DbSet<Producto> Productos { get; set; } = null!;
        public DbSet<MovimientoInventario> MovimientosInventario { get; set; } = null!;
        public DbSet<ConfiguracionMoneda> ConfiguracionesMoneda { get; set; } = null!;
        public DbSet<TasaCambio> TasasCambio { get; set; } = null!;
        public DbSet<Cuenta> Cuentas { get; set; } = null!;
        public DbSet<CuentaDetalle> CuentaDetalles { get; set; } = null!;
        public DbSet<Pago> Pagos { get; set; } = null!;
        public DbSet<HistorialClinico> HistorialesClinicos { get; set; } = null!;
        public DbSet<FotoClinica> FotosClinicas { get; set; } = null!;
        public DbSet<DocumentoClinico> DocumentosClinicos { get; set; } = null!;
        public DbSet<ConsultaDetalle> ConsultaDetalles { get; set; } = null!;
        public DbSet<Examen> Examenes { get; set; } = null!;
        public DbSet<Modulo> Modulos { get; set; } = null!;
        public DbSet<UsuarioModulo> UsuarioModulos { get; set; } = null!;


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

            modelBuilder.Entity<UsuarioModulo>()
                .HasKey(um => new { um.IdUsuario, um.IdModulo });

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

            modelBuilder.Entity<Examen>()
                .Property(e => e.FechaOrden)
                .HasDefaultValueSql("SYSDATETIME()");


            modelBuilder.Entity<Producto>(p =>
            {
                p.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");
                p.ToTable(tb => tb.HasTrigger("TR_ACTUALIZAR_STOCK_POR_CUENTA")); // Por futuros triggers de ventas
            });

            modelBuilder.Entity<MovimientoInventario>(m =>
            {
                m.Property(x => x.FechaMovimiento).HasDefaultValueSql("SYSDATETIME()");
                m.ToTable(tb => tb.HasTrigger("TR_ACTUALIZAR_STOCK_MOVIMIENTO"));
            });

            // Monedas y Finanzas: Forzar precisión a 2 decimales para consistencia
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            modelBuilder.Entity<TasaCambio>()
                .HasIndex(t => new { t.IdMonedaOrigen, t.IdMonedaDestino })
                .HasDatabaseName("UX_TASA_ACTIVA")
                .HasFilter("[ACTIVO] = 1")
                .IsUnique();
        }
    }
}

