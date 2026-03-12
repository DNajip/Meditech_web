using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MediTech.Models
{
    public static class DbInitializer
    {
        public static void Initialize(MediTechContext context)
        {
            Console.WriteLine("--- DB INITIALIZER START ---");
            
            var existingAdmin = context.Usuarios
                .Include(u => u.Empleado)
                .ThenInclude(e => e != null ? e.Persona : null)
                .FirstOrDefault(u => u.Username == "admin");

            if (existingAdmin != null)
            {
                Console.WriteLine($"Deleting existing admin user and related records. ID: {existingAdmin.IdUsuario}");
                
                var empleado = existingAdmin.Empleado;
                var persona = empleado?.Persona;

                context.Usuarios.Remove(existingAdmin);
                if (empleado != null) context.Empleados.Remove(empleado);
                if (persona != null) context.Personas.Remove(persona);
                
                context.SaveChanges();
            }

            Console.WriteLine("Creating fresh Admin records...");
            // ... (ommited for brevity as I'm using replace_file_content for a block)
            // Wait, I should replace exactly what I want to fix.
            // 1. Create Persona
            var adminPersona = new Persona
            {
                PrimerNombre = "Admin",
                PrimerApellido = "User",
                IdTipoIdentificacion = 1, // CEDULA
                NumIdentificacion = "000-000000-0000",
                IdGenero = 1, // MASCULINO
                FechaNacimiento = new DateTime(1990, 1, 1),
                Email = "admin@meditech.com",
                Telefono = "0000-0000",
                Direccion = "Sede Central",
                IdEstado = 1 // ACTIVO
            };
            context.Personas.Add(adminPersona);
            context.SaveChanges();
            Console.WriteLine($"Persona Created. ID: {adminPersona.IdPersona}");

            // 2. Create Empleado (ADM.EMPLEADOS)
            var adminEmpleado = new Empleado
            {
                IdPersona = adminPersona.IdPersona,
                IdRol = 1, // ADMINISTRADOR
                FechaContratacion = DateTime.Now,
                IdEstado = 1 // ACTIVO
            };
            context.Empleados.Add(adminEmpleado);
            context.SaveChanges();
            Console.WriteLine($"Empleado Created. ID: {adminEmpleado.IdEmpleado}");

            // 3. Create Usuario (ADM.USUARIOS)
            CreatePasswordHash("admin123", out byte[] passwordHash, out byte[] passwordSalt);

            var adminUsuario = new Usuario
            {
                Username = "admin",
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                IdEmpleado = adminEmpleado.IdEmpleado,
                IdRol = 1, // ADMINISTRADOR
                IdEstado = 1 // ACTIVO
            };
            context.Usuarios.Add(adminUsuario);
            context.SaveChanges();
            Console.WriteLine("Admin User Seeded Successfully.");

            // 4. Ensure Catalogs have Active State (Fix for missing dropdowns)
            Console.WriteLine("Verifying catalog states...");
            var generosSinEstado = context.Generos.Where(g => g.IdEstado == null || g.IdEstado == 0).ToList();
            if (generosSinEstado.Any())
            {
                foreach (var g in generosSinEstado) g.IdEstado = 1;
                Console.WriteLine($"Updated {generosSinEstado.Count} Generos to Active state.");
            }

            var tiposSinEstado = context.TiposIdentificacion.Where(t => t.IdEstado == 0).ToList();
            if (tiposSinEstado.Any())
            {
                foreach (var t in tiposSinEstado) t.IdEstado = 1;
                Console.WriteLine($"Updated {tiposSinEstado.Count} TiposIdentificacion to Active state.");
            }

            SeedTreatments(context);

            context.SaveChanges();
            Console.WriteLine("--- DB INITIALIZER END ---");
        }

        private static void SeedTreatments(MediTechContext context)
        {
            if (context.Tratamientos.Any()) return;

            Console.WriteLine("Seeding base treatments...");
            var treatments = new List<Tratamiento>
            {
                new Tratamiento { NombreTratamiento = "CONSULTA GENERAL", Precio = 40, IdEstado = 1 },
                new Tratamiento { NombreTratamiento = "LIMPIEZA DENTAL", Precio = 50, IdEstado = 1 },
                new Tratamiento { NombreTratamiento = "EXTRACCION SIMPLE", Precio = 45, IdEstado = 1 },
                new Tratamiento { NombreTratamiento = "ORTODONCIA - CONTROL", Precio = 35, IdEstado = 1 }
            };

            context.Tratamientos.AddRange(treatments);
            context.SaveChanges();
            Console.WriteLine("Treatments Seeded Successfully.");
        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }
    }
}
