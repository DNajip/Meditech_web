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

            // Apply schema migrations for new columns/tables
            ApplyMigrations(context);

            // Clean clinical data to avoid FK conflicts when deleting users (Development only)
            context.Database.ExecuteSqlRaw("DELETE FROM CLI.SIGNOS_VITALES; DELETE FROM CLI.DIAGNOSTICOS; DELETE FROM CLI.CONSULTAS;");
            
            
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

            // SEED DOCTOR
            if (!context.Usuarios.Any(u => u.Username == "doctor"))
            {
                var docPersona = new Persona
                {
                    PrimerNombre = "Danny",
                    PrimerApellido = "Pravia",
                    IdTipoIdentificacion = 1,
                    NumIdentificacion = "111-111111-1111",
                    IdGenero = 1,
                    FechaNacimiento = new DateTime(1985, 5, 10),
                    Email = "danny.pravia@meditech.com",
                    Telefono = "8888-8888",
                    Direccion = "Consultorio A1",
                    IdEstado = 1
                };
                context.Personas.Add(docPersona);
                context.SaveChanges();

                var docEmpleado = new Empleado
                {
                    IdPersona = docPersona.IdPersona,
                    IdRol = 2, // DOCTOR
                    FechaContratacion = DateTime.Now,
                    IdEstado = 1
                };
                context.Empleados.Add(docEmpleado);
                context.SaveChanges();

                CreatePasswordHash("doctor123", out byte[] dHash, out byte[] dSalt);
                context.Usuarios.Add(new Usuario
                {
                    Username = "doctor",
                    PasswordHash = dHash,
                    PasswordSalt = dSalt,
                    IdEmpleado = docEmpleado.IdEmpleado,
                    IdRol = 2,
                    IdEstado = 1
                });
                context.SaveChanges();
                Console.WriteLine("Doctor User Seeded Successfully.");
            }

            // SEED ASISTENTE
            if (!context.Usuarios.Any(u => u.Username == "asistente"))
            {
                var asisPersona = new Persona
                {
                    PrimerNombre = "Maria",
                    PrimerApellido = "Lopez",
                    IdTipoIdentificacion = 1,
                    NumIdentificacion = "222-222222-2222",
                    IdGenero = 2,
                    FechaNacimiento = new DateTime(1995, 8, 20),
                    Email = "maria.lopez@meditech.com",
                    Telefono = "7777-7777",
                    Direccion = "Recepción",
                    IdEstado = 1
                };
                context.Personas.Add(asisPersona);
                context.SaveChanges();

                var asisEmpleado = new Empleado
                {
                    IdPersona = asisPersona.IdPersona,
                    IdRol = 3, // ASISTENTE
                    FechaContratacion = DateTime.Now,
                    IdEstado = 1
                };
                context.Empleados.Add(asisEmpleado);
                context.SaveChanges();

                CreatePasswordHash("asis123", out byte[] aHash, out byte[] aSalt);
                context.Usuarios.Add(new Usuario
                {
                    Username = "asistente",
                    PasswordHash = aHash,
                    PasswordSalt = aSalt,
                    IdEmpleado = asisEmpleado.IdEmpleado,
                    IdRol = 3,
                    IdEstado = 1
                });
                context.SaveChanges();
                Console.WriteLine("Asistente User Seeded Successfully.");
            }

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

        /// <summary>
        /// Applies incremental schema changes to the running database.
        /// Uses IF NOT EXISTS to be idempotent (safe to run multiple times).
        /// </summary>
        private static void ApplyMigrations(MediTechContext context)
        {
            Console.WriteLine("Applying schema migrations...");

            // 1. Create CAT.MONEDAS if it doesn't exist
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CAT' AND t.name = 'MONEDAS')
                BEGIN
                    CREATE TABLE CAT.MONEDAS (
                        ID_MONEDA INT IDENTITY CONSTRAINT PK_MONEDAS PRIMARY KEY,
                        CODIGO VARCHAR(10) NOT NULL,
                        NOMBRE VARCHAR(50) NOT NULL,
                        SIMBOLO VARCHAR(5)
                    );
                END
            ");

            // 1.05 Create ADM.CONFIGURACION_MONEDA if it doesn't exist
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'ADM' AND t.name = 'CONFIGURACION_MONEDA')
                BEGIN
                    CREATE TABLE ADM.CONFIGURACION_MONEDA(
                        ID_CONFIGURACION INT IDENTITY CONSTRAINT PK_CONFIG_MONEDA PRIMARY KEY,
                        ID_MONEDA_BASE INT NOT NULL,
                        TASA_CAMBIO DECIMAL(12,4) NOT NULL,
                        FECHA_ACTUALIZACION DATETIME2 DEFAULT SYSDATETIME(),
                        USUARIO_MODIFICACION VARCHAR(80),
                        FOREIGN KEY(ID_MONEDA_BASE) REFERENCES CAT.MONEDAS(ID_MONEDA)
                    );
                END
            ");

            // 1.1 Create CAT.ESTADO_CITA if it doesn't exist
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CAT' AND t.name = 'ESTADO_CITA')
                BEGIN
                    CREATE TABLE CAT.ESTADO_CITA (
                        ID_ESTADO_CITA INT IDENTITY PRIMARY KEY,
                        DESC_ESTADO_CITA VARCHAR(50) NOT NULL,
                        ID_ESTADO INT NOT NULL DEFAULT 1
                    );
                    INSERT INTO CAT.ESTADO_CITA (DESC_ESTADO_CITA) VALUES 
                    ('PROGRAMADA'), ('EN CURSO'), ('FINALIZADA'), ('CANCELADA'), ('NO ASISTIÓ');
                END
            ");

            // 2. Add TELEFONO column to CLI.CITAS
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CLI].[CITAS]') AND name = 'TELEFONO')
                BEGIN
                    ALTER TABLE [CLI].[CITAS] ADD [TELEFONO] VARCHAR(20) NOT NULL DEFAULT '00000000'
                END
            ");

            // 2.1 Add ID_ESTADO_CITA column to CLI.CITAS
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CLI].[CITAS]') AND name = 'ID_ESTADO_CITA')
                BEGIN
                    ALTER TABLE [CLI].[CITAS] ADD [ID_ESTADO_CITA] INT NOT NULL DEFAULT 1;
                    ALTER TABLE [CLI].[CITAS] ADD FOREIGN KEY (ID_ESTADO_CITA) REFERENCES CAT.ESTADO_CITA(ID_ESTADO_CITA);
                END
            ");

            // 2.2 Add ID_POSIBLE_PACIENTE column to CLI.CITAS
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CLI].[CITAS]') AND name = 'ID_POSIBLE_PACIENTE')
                BEGIN
                    ALTER TABLE [CLI].[CITAS] ADD [ID_POSIBLE_PACIENTE] INT NULL;
                    ALTER TABLE [CLI].[CITAS] ADD FOREIGN KEY (ID_POSIBLE_PACIENTE) REFERENCES CLI.POSIBLE_PACIENTES(ID_POSIBLE_PACIENTE);
                END
            ");

            // 3. Add extra columns to CAT.TRATAMIENTOS if they don't exist
            string[] tratamientoColumns = { "ID_MONEDA", "DOSIS", "VIA_ADMINISTRACION", "FRECUENCIA", "DURACION_TRATAMIENTO", "FECHA_ACTUALIZACION" };
            foreach (var col in tratamientoColumns)
            {
                string type = col == "ID_MONEDA" ? "INT NULL" : (col == "FECHA_ACTUALIZACION" ? "DATETIME2 NULL" : "VARCHAR(100) NULL");
                string fk = col == "ID_MONEDA" ? " IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_TRATAMIENTOS_MONEDAS') ALTER TABLE CAT.TRATAMIENTOS ADD FOREIGN KEY (ID_MONEDA) REFERENCES CAT.MONEDAS(ID_MONEDA);" : "";
                
                context.Database.ExecuteSqlRaw($@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CAT].[TRATAMIENTOS]') AND name = '{col}')
                    BEGIN
                        ALTER TABLE [CAT].[TRATAMIENTOS] ADD [{col}] {type};
                        {fk}
                    END
                ");
            }

            // Seed Monedas if empty
            if (!context.Monedas.Any())
            {
                context.Monedas.AddRange(
                    new Moneda { Codigo = "USD", Nombre = "DOLAR", Simbolo = "$" },
                    new Moneda { Codigo = "NIO", Nombre = "CORDOBA", Simbolo = "C$" }
                );
                context.SaveChanges();
            }

            // Seed Tasa de Cambio if empty
            if (!context.ConfiguracionesMoneda.Any())
            {
                var usd = context.Monedas.FirstOrDefault(m => m.Codigo == "USD");
                if (usd != null)
                {
                    context.ConfiguracionesMoneda.Add(new ConfiguracionMoneda
                    {
                        IdMonedaBase = usd.IdMoneda,
                        TasaCambio = 36.6215m, // Tasa de ejemplo oficial
                        FechaActualizacion = DateTime.Now,
                        UsuarioModificacion = "SISTEMA"
                    });
                    context.SaveChanges();
                }
            }

            // 5. Create CAJA schema and tables if they don't exist
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'CAJA')
                    EXEC('CREATE SCHEMA CAJA');
            ");

            // Drop and recreate CAJA tables to ensure correct schema v4.0
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns c 
                    JOIN sys.tables t ON c.object_id = t.object_id 
                    JOIN sys.schemas s ON t.schema_id = s.schema_id 
                    WHERE s.name = 'CAJA' AND t.name = 'PAGOS' AND c.name = 'TASA_CAMBIO_APLICADA')
                BEGIN
                    -- Tables exist with wrong structure, drop and recreate
                    IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CAJA' AND t.name = 'PAGOS')
                        DROP TABLE CAJA.PAGOS;
                    IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CAJA' AND t.name = 'CUENTA_DETALLE')
                        DROP TABLE CAJA.CUENTA_DETALLE;
                    IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CAJA' AND t.name = 'CUENTAS')
                        DROP TABLE CAJA.CUENTAS;

                    CREATE TABLE CAJA.CUENTAS(
                        ID_CUENTA INT IDENTITY CONSTRAINT PK_CUENTAS PRIMARY KEY,
                        ID_PACIENTE INT,
                        ID_CONSULTA INT,
                        TOTAL_BRUTO DECIMAL(12,2),
                        DESCUENTO DECIMAL(12,2) DEFAULT 0,
                        TOTAL_FINAL DECIMAL(12,2),
                        ID_MONEDA_BASE INT,
                        FECHA_CREACION DATETIME2 DEFAULT SYSDATETIME(),
                        FOREIGN KEY(ID_PACIENTE) REFERENCES CLI.PACIENTES(ID_PACIENTE),
                        FOREIGN KEY(ID_CONSULTA) REFERENCES CLI.CONSULTAS(ID_CONSULTA),
                        FOREIGN KEY(ID_MONEDA_BASE) REFERENCES CAT.MONEDAS(ID_MONEDA)
                    );

                    CREATE TABLE CAJA.CUENTA_DETALLE(
                        ID_DETALLE INT IDENTITY CONSTRAINT PK_CUENTA_DETALLE PRIMARY KEY,
                        ID_CUENTA INT NOT NULL,
                        TIPO_ITEM VARCHAR(20),
                        ID_REFERENCIA INT,
                        DESCRIPCION VARCHAR(200),
                        CANTIDAD INT,
                        PRECIO_UNITARIO DECIMAL(12,2),
                        SUBTOTAL DECIMAL(12,2),
                        FOREIGN KEY(ID_CUENTA) REFERENCES CAJA.CUENTAS(ID_CUENTA)
                    );

                    CREATE TABLE CAJA.PAGOS(
                        ID_PAGO INT IDENTITY CONSTRAINT PK_PAGOS PRIMARY KEY,
                        ID_CUENTA INT NOT NULL,
                        MONTO DECIMAL(12,2),
                        ID_MONEDA INT,
                        METODO_PAGO VARCHAR(30),
                        TASA_CAMBIO_APLICADA DECIMAL(12,4),
                        FECHA_PAGO DATETIME2 DEFAULT SYSDATETIME(),
                        FOREIGN KEY(ID_CUENTA) REFERENCES CAJA.CUENTAS(ID_CUENTA),
                        FOREIGN KEY(ID_MONEDA) REFERENCES CAT.MONEDAS(ID_MONEDA)
                    );
                END
            ");

            // 4. Clean negative stock and apply CHECK constraint
            context.Database.ExecuteSqlRaw(@"
                -- Fix existing corrupted data
                UPDATE [INV].[PRODUCTOS] SET [STOCK] = 0 WHERE [STOCK] < 0;

                -- Apply the constraint if it doesn't exist
                IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_PRODUCTO_STOCK_POSITIVO')
                BEGIN
                    ALTER TABLE [INV].[PRODUCTOS] ADD CONSTRAINT CHK_PRODUCTO_STOCK_POSITIVO CHECK (STOCK >= 0);
                END
            ");

            Console.WriteLine("Schema migrations applied.");
        }

        private static void SeedTreatments(MediTechContext context)
        {
            if (context.Tratamientos.Any()) return;

            var usd = context.Monedas.FirstOrDefault(m => m.Codigo == "USD");

            Console.WriteLine("Seeding base treatments...");
            var treatments = new List<Tratamiento>
            {
                new Tratamiento { NombreTratamiento = "CONSULTA GENERAL", Precio = 40, IdEstado = 1, IdMoneda = usd?.IdMoneda },
                new Tratamiento { NombreTratamiento = "LIMPIEZA DENTAL", Precio = 50, IdEstado = 1, IdMoneda = usd?.IdMoneda },
                new Tratamiento { NombreTratamiento = "EXTRACCION SIMPLE", Precio = 45, IdEstado = 1, IdMoneda = usd?.IdMoneda },
                new Tratamiento { NombreTratamiento = "ORTODONCIA - CONTROL", Precio = 35, IdEstado = 1, IdMoneda = usd?.IdMoneda }
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
