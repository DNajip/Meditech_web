using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MediTech.Backend.Models
{
    public static class DbInitializer
    {
        public static void Initialize(MediTechContext context)
        {
            Console.WriteLine("--- DB INITIALIZER START ---");

            // Apply schema migrations for new columns/tables
            ApplyMigrations(context);

            // NOTE: Removed aggressive DELETE of clinical/financial data.
            // The previous DELETE FROM CLI.CONSULTAS was destroying active consultations on every restart,
            // causing "Consulta no encontrada" errors in the closure flow.
            
            
            var existingAdmin = context.Usuarios
                .Include(u => u.Empleado)
                .ThenInclude(e => e != null ? e.Persona : null)
                .FirstOrDefault(u => u.Username == "admin");

            if (existingAdmin != null)
            {
                Console.WriteLine($"Deleting existing admin user and related records. ID: {existingAdmin.IdUsuario}");
                
                // Clear module permissions to avoid FK conflict
                context.Database.ExecuteSqlRaw("DELETE FROM ADM.USUARIO_MODULOS WHERE ID_USUARIO = {0}", existingAdmin.IdUsuario);

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

            // 5. Assign all modules to users for testing (Assign all to admin, some to others)
            var allModules = context.Modulos.ToList();
            var adminUser = context.Usuarios.Include(u => u.UsuarioModulos).FirstOrDefault(u => u.Username == "admin");
            if (adminUser != null && !adminUser.UsuarioModulos.Any())
            {
                foreach (var mod in allModules)
                {
                    context.UsuarioModulos.Add(new UsuarioModulo { IdUsuario = adminUser.IdUsuario, IdModulo = mod.IdModulo });
                }
            }

            var doctorUser = context.Usuarios.Include(u => u.UsuarioModulos).FirstOrDefault(u => u.Username == "doctor");
            if (doctorUser != null && !doctorUser.UsuarioModulos.Any())
            {
                // Doctors see Citas, Pacientes, Consultas, Examenes
                var docControllers = new[] { "Home", "Citas", "Pacientes", "Consultas" };
                var docModules = allModules.Where(m => docControllers.Contains(m.Controller));
                foreach (var mod in docModules)
                {
                    context.UsuarioModulos.Add(new UsuarioModulo { IdUsuario = doctorUser.IdUsuario, IdModulo = mod.IdModulo });
                }
            }

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
                        TASA_CAMBIO DECIMAL(12,2) NOT NULL,
                        FECHA_ACTUALIZACION DATETIME2 DEFAULT SYSDATETIME(),
                        USUARIO_MODIFICACION VARCHAR(80),
                        FOREIGN KEY(ID_MONEDA_BASE) REFERENCES CAT.MONEDAS(ID_MONEDA)
                    );
                END
                ELSE
                BEGIN
                    -- Asegurar precisión de 2 decimales si ya existe
                    ALTER TABLE ADM.CONFIGURACION_MONEDA ALTER COLUMN TASA_CAMBIO DECIMAL(12,2) NOT NULL;
                END
            ");

            // 1.06 Create ADM.TASA_CAMBIO if it doesn't exist
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'ADM' AND t.name = 'TASA_CAMBIO')
                BEGIN
                    CREATE TABLE ADM.TASA_CAMBIO (
                        ID_TASA INT IDENTITY CONSTRAINT PK_TASA_CAMBIO PRIMARY KEY,
                        ID_MONEDA_ORIGEN INT NOT NULL,
                        ID_MONEDA_DESTINO INT NOT NULL,
                        VALOR DECIMAL(18,2) NOT NULL,
                        FECHA DATETIME2 DEFAULT SYSDATETIME(),
                        ACTIVO BIT DEFAULT 1,
                        USUARIO_MODIFICACION VARCHAR(80),
                        FOREIGN KEY(ID_MONEDA_ORIGEN) REFERENCES CAT.MONEDAS(ID_MONEDA),
                        FOREIGN KEY(ID_MONEDA_DESTINO) REFERENCES CAT.MONEDAS(ID_MONEDA)
                    );
                    
                    EXEC('CREATE UNIQUE INDEX UX_TASA_ACTIVA ON ADM.TASA_CAMBIO(ID_MONEDA_ORIGEN, ID_MONEDA_DESTINO) WHERE ACTIVO = 1');
                END
                ELSE
                BEGIN
                    -- Asegurar precisión de 2 decimales si ya existe
                    ALTER TABLE ADM.TASA_CAMBIO ALTER COLUMN VALOR DECIMAL(18,2) NOT NULL;
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

                -- 1.2 Ensure CAT.ESTADOS has consultation specific states
                IF NOT EXISTS (SELECT 1 FROM CAT.ESTADOS WHERE DESC_ESTADO = 'EN PROCESO')
                    INSERT INTO CAT.ESTADOS (DESC_ESTADO) VALUES ('EN PROCESO');
                IF NOT EXISTS (SELECT 1 FROM CAT.ESTADOS WHERE DESC_ESTADO = 'FINALIZADA')
                    INSERT INTO CAT.ESTADOS (DESC_ESTADO) VALUES ('FINALIZADA');
                IF NOT EXISTS (SELECT 1 FROM CAT.ESTADOS WHERE DESC_ESTADO = 'CANCELADA')
                    INSERT INTO CAT.ESTADOS (DESC_ESTADO) VALUES ('CANCELADA');

                -- 1.3 Create CLI.HISTORIAL_CLINICO if it doesn't exist
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CLI' AND t.name = 'HISTORIAL_CLINICO')
                BEGIN
                    CREATE TABLE CLI.HISTORIAL_CLINICO (
                        ID_HISTORIAL INT IDENTITY CONSTRAINT PK_HISTORIAL PRIMARY KEY,
                        ID_PACIENTE INT NOT NULL,
                        ALERGIAS BIT NOT NULL DEFAULT 0,
                        ALERGIAS_DETALLE VARCHAR(500),
                        DIABETES BIT NOT NULL DEFAULT 0,
                        TOMA_MEDICAMENTO BIT NOT NULL DEFAULT 0,
                        MEDICAMENTO_DETALLE VARCHAR(500),
                        HIPERTENSION BIT NOT NULL DEFAULT 0,
                        EMBARAZADA BIT NOT NULL DEFAULT 0,
                        CARDIACOS BIT NOT NULL DEFAULT 0,
                        ANTECEDENTE_ONCOLOGICO BIT NOT NULL DEFAULT 0,
                        OTROS_PADECIMIENTOS BIT NOT NULL DEFAULT 0,
                        OTROS_PADECIMIENTOS_DETALLE VARCHAR(500),
                        CONSUME_ALCOHOL BIT NOT NULL DEFAULT 0,
                        FUMA_CIGARRILLOS BIT NOT NULL DEFAULT 0,
                        REALIZA_EJERCICIO BIT NOT NULL DEFAULT 0,
                        CIRUGIAS_ESTETICAS BIT NOT NULL DEFAULT 0,
                        CIRUGIAS_ESTETICAS_DETALLE VARCHAR(500),
                        FECHA_REGISTRO DATETIME2 DEFAULT SYSDATETIME(),
                        FECHA_ACTUALIZACION DATETIME2,
                        FOREIGN KEY(ID_PACIENTE) REFERENCES CLI.PACIENTES(ID_PACIENTE)
                    );
                END
 
                -- 1.4 Remove duplicated column TOMA_MEDICAMENTOS_HABITO if it exists
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CLI].[HISTORIAL_CLINICO]') AND name = 'TOMA_MEDICAMENTOS_HABITO')
                BEGIN
                    DECLARE @ConstraintName nvarchar(200)
                    SELECT @ConstraintName = Name FROM sys.default_constraints 
                    WHERE parent_object_id = OBJECT_ID('[CLI].[HISTORIAL_CLINICO]') 
                    AND parent_column_id = (SELECT column_id FROM sys.columns WHERE parent_object_id = OBJECT_ID('[CLI].[HISTORIAL_CLINICO]') AND name = 'TOMA_MEDICAMENTOS_HABITO')
                    
                    IF @ConstraintName IS NOT NULL EXEC('ALTER TABLE [CLI].[HISTORIAL_CLINICO] DROP CONSTRAINT [' + @ConstraintName + ']')
                    ALTER TABLE [CLI].[HISTORIAL_CLINICO] DROP COLUMN [TOMA_MEDICAMENTOS_HABITO];
                END
 
                -- 1.5 Create CLI.FOTOS_CLINICAS if it doesn't exist
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CLI' AND t.name = 'FOTOS_CLINICAS')
                BEGIN
                    CREATE TABLE CLI.FOTOS_CLINICAS (
                        ID_FOTO INT IDENTITY CONSTRAINT PK_FOTOS_CLINICAS PRIMARY KEY,
                        ID_PACIENTE INT NOT NULL,
                        TITULO NVARCHAR(200),
                        CONTENIDO VARBINARY(MAX) NOT NULL,
                        CONTENT_TYPE NVARCHAR(50) NOT NULL,
                        FECHA_REGISTRO DATETIME2 DEFAULT SYSDATETIME(),
                        FOREIGN KEY(ID_PACIENTE) REFERENCES CLI.PACIENTES(ID_PACIENTE)
                    );
                END

                -- 1.6 Create DOC schema if it doesn't exist
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'DOC')
                    EXEC('CREATE SCHEMA DOC');

                -- 1.7 Create DOC.DOCUMENTOS_CLINICOS if it doesn't exist
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'DOC' AND t.name = 'DOCUMENTOS_CLINICOS')
                BEGIN
                    CREATE TABLE DOC.DOCUMENTOS_CLINICOS (
                        ID_DOCUMENTO INT IDENTITY CONSTRAINT PK_DOCUMENTOS_CLINICOS PRIMARY KEY,
                        ID_PACIENTE INT NOT NULL,
                        NOMBRE_ARCHIVO NVARCHAR(200) NOT NULL,
                        TITULO NVARCHAR(200),
                        CONTENIDO VARBINARY(MAX) NOT NULL,
                        CONTENT_TYPE NVARCHAR(100) NOT NULL,
                        TAMANO_BYTES BIGINT NOT NULL DEFAULT 0,
                        FECHA_REGISTRO DATETIME2 DEFAULT SYSDATETIME(),
                        FOREIGN KEY(ID_PACIENTE) REFERENCES CLI.PACIENTES(ID_PACIENTE)
                    );
                END

                -- 1.8 Create CLI.CONSULTA_DETALLE if it doesn't exist
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CLI' AND t.name = 'CONSULTA_DETALLE')
                BEGIN
                    CREATE TABLE CLI.CONSULTA_DETALLE (
                        ID_DETALLE_CONSULTA INT IDENTITY CONSTRAINT PK_CONSULTA_DETALLE PRIMARY KEY,
                        ID_CONSULTA INT NOT NULL,
                        TIPO_ITEM VARCHAR(20),
                        ID_REFERENCIA INT,
                        DESCRIPCION NVARCHAR(200),
                        CANTIDAD INT,
                        PRECIO_UNITARIO DECIMAL(12,2),
                        SUBTOTAL DECIMAL(12,2),
                        FOREIGN KEY(ID_CONSULTA) REFERENCES CLI.CONSULTAS(ID_CONSULTA)
                    );
                END

                -- Cleanup old table if exists
                IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CLI' AND t.name = 'EXAMEN')
                BEGIN
                    DROP TABLE CLI.EXAMEN;
                END

                -- 1.9 Create CLI.EXAMENES if it doesn't exist
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'CLI' AND t.name = 'EXAMENES')
                BEGIN
                    CREATE TABLE CLI.EXAMENES (
                        ID_EXAMEN INT IDENTITY CONSTRAINT PK_EXAMENES PRIMARY KEY,
                        ID_PACIENTE INT NOT NULL,
                        ID_CONSULTA INT NULL,
                        NOMBRE_EXAMEN NVARCHAR(200) NOT NULL,
                        FECHA_ORDEN DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                        FECHA_RESULTADO DATETIME2 NULL,
                        IMAGEN_RESULTADO VARBINARY(MAX) NULL,
                        CONTENT_TYPE NVARCHAR(50) NULL,
                        COMENTARIO_MEDICO NVARCHAR(MAX) NULL,
                        ID_ESTADO INT NOT NULL DEFAULT 1,
                        FOREIGN KEY(ID_PACIENTE) REFERENCES CLI.PACIENTES(ID_PACIENTE),
                        FOREIGN KEY(ID_CONSULTA) REFERENCES CLI.CONSULTAS(ID_CONSULTA),
                        FOREIGN KEY(ID_ESTADO) REFERENCES CAT.ESTADOS(ID_ESTADO)
                    );
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
            string[] tratamientoColumns = { "DOSIS", "VIA_ADMINISTRACION", "FRECUENCIA", "DURACION_TRATAMIENTO", "FECHA_ACTUALIZACION" };
            foreach (var col in tratamientoColumns)
            {
                string type = col == "FECHA_ACTUALIZACION" ? "DATETIME2 NULL" : "VARCHAR(100) NULL";
                
                context.Database.ExecuteSqlRaw($@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CAT].[TRATAMIENTOS]') AND name = '{col}')
                    BEGIN
                        ALTER TABLE [CAT].[TRATAMIENTOS] ADD [{col}] {type};
                    END
                ");
            }

            // Professional Currency Model Migrations: 
            // 4.1 Remove ID_MONEDA from items
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[INV].[PRODUCTOS]') AND name = 'ID_MONEDA')
                BEGIN
                    DECLARE @sqlP NVARCHAR(MAX) = '';
                    SELECT @sqlP += 'ALTER TABLE [INV].[PRODUCTOS] DROP CONSTRAINT ' + QUOTENAME(name) + ';'
                    FROM sys.objects 
                    WHERE parent_object_id = OBJECT_ID('[INV].[PRODUCTOS]') 
                    AND (type = 'F' OR type = 'D') -- Foreign keys or Default constraints
                    AND name IN (
                        SELECT fk.name FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                        WHERE c.name = 'ID_MONEDA' AND c.object_id = OBJECT_ID('[INV].[PRODUCTOS]')
                        UNION
                        SELECT d.name FROM sys.default_constraints d JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
                        WHERE c.name = 'ID_MONEDA' AND c.object_id = OBJECT_ID('[INV].[PRODUCTOS]')
                    );
                    IF @sqlP <> '' EXEC sp_executesql @sqlP;
                    ALTER TABLE [INV].[PRODUCTOS] DROP COLUMN [ID_MONEDA];
                END

                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CAT].[TRATAMIENTOS]') AND name = 'ID_MONEDA')
                BEGIN
                    DECLARE @sqlT NVARCHAR(MAX) = '';
                    SELECT @sqlT += 'ALTER TABLE [CAT].[TRATAMIENTOS] DROP CONSTRAINT ' + QUOTENAME(name) + ';'
                    FROM sys.objects 
                    WHERE parent_object_id = OBJECT_ID('[CAT].[TRATAMIENTOS]') 
                    AND (type = 'F' OR type = 'D')
                    AND name IN (
                        SELECT fk.name FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                        WHERE c.name = 'ID_MONEDA' AND c.object_id = OBJECT_ID('[CAT].[TRATAMIENTOS]')
                        UNION
                        SELECT d.name FROM sys.default_constraints d JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
                        WHERE c.name = 'ID_MONEDA' AND c.object_id = OBJECT_ID('[CAT].[TRATAMIENTOS]')
                    );
                    IF @sqlT <> '' EXEC sp_executesql @sqlT;
                    ALTER TABLE [CAT].[TRATAMIENTOS] DROP COLUMN [ID_MONEDA];
                END
            ");

            // 4.2 Add MONTO_BASE, MONTO_RECIBIDO, VUELTO to PAGOS
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CAJA].[PAGOS]') AND name = 'MONTO_BASE')
                    ALTER TABLE [CAJA].[PAGOS] ADD [MONTO_BASE] DECIMAL(12,2) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CAJA].[PAGOS]') AND name = 'MONTO_RECIBIDO')
                    ALTER TABLE [CAJA].[PAGOS] ADD [MONTO_RECIBIDO] DECIMAL(12,2) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CAJA].[PAGOS]') AND name = 'VUELTO')
                    ALTER TABLE [CAJA].[PAGOS] ADD [VUELTO] DECIMAL(12,2) NULL;
            ");

            // Seed Monedas if empty
            if (!context.Monedas.Any())
            {
                context.Monedas.AddRange(
                    new Moneda { Codigo = "USD", Nombre = "DOLAR", Simbolo = "$" },
                    new Moneda { Codigo = "NIO", Nombre = "CORDOBA", Simbolo = "C$" }
                );
                context.SaveChanges();
            }

            // Seed Tasa de Cambio (ADM.TASA_CAMBIO) if empty
            if (!context.TasasCambio.Any())
            {
                var usd = context.Monedas.FirstOrDefault(m => m.Codigo == "USD");
                var nio = context.Monedas.FirstOrDefault(m => m.Codigo == "NIO");
                if (usd != null && nio != null)
                {
                    context.TasasCambio.Add(new TasaCambio
                    {
                        IdMonedaOrigen = usd.IdMoneda,
                        IdMonedaDestino = nio.IdMoneda,
                        Valor = 36.60m,
                        Fecha = DateTime.Now,
                        Activo = true,
                        UsuarioModificacion = "SISTEMA"
                    });
                    context.SaveChanges();
                }
            }

            // Seed Configuración (ADM.CONFIGURACION_MONEDA) if empty
            if (!context.ConfiguracionesMoneda.Any())
            {
                var usd = context.Monedas.FirstOrDefault(m => m.Codigo == "USD");
                if (usd != null)
                {
                    context.ConfiguracionesMoneda.Add(new ConfiguracionMoneda
                    {
                        IdMonedaBase = usd.IdMoneda,
                        TasaCambio = 36.60m, // Mantener por compatibilidad legacy hasta que se limpie
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
                        MONTO_BASE DECIMAL(12,2),
                        ID_MONEDA INT,
                        METODO_PAGO VARCHAR(30),
                        TASA_CAMBIO_APLICADA DECIMAL(18,6),
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

            // 5. Create ADM.SP_CONVERTIR_POSIBLE_A_PACIENTE
            context.Database.ExecuteSqlRaw(@"
                CREATE OR ALTER PROCEDURE ADM.SP_CONVERTIR_POSIBLE_A_PACIENTE
                    @IdPosiblePaciente INT,
                    @IdCita INT,
                    @IdTipoIdentificacion INT,
                    @NumIdentificacion VARCHAR(25),
                    @IdGenero INT,
                    @FechaNacimiento DATE,
                    @Email VARCHAR(120) = NULL,
                    @Direccion VARCHAR(250) = NULL,
                    @ContactoEmergencia VARCHAR(120) = NULL,
                    @TelefonoEmergencia VARCHAR(20) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        DECLARE @NewPersonaId INT;
                        DECLARE @NewPacienteId INT;
                        DECLARE @PNombre VARCHAR(40), @SNombre VARCHAR(40), @PApellido VARCHAR(40), @SApellido VARCHAR(40), @Tel VARCHAR(20);

                        SELECT @PNombre = PRIMER_NOMBRE, @SNombre = SEGUNDO_NOMBRE, 
                               @PApellido = PRIMER_APELLIDO, @SApellido = SEGUNDO_APELLIDO,
                               @Tel = TELEFONO
                        FROM CLI.POSIBLES_PACIENTES WHERE ID_POSIBLE_PACIENTE = @IdPosiblePaciente;

                        IF @PNombre IS NULL THROW 50001, 'Posible paciente no encontrado.', 1;

                        -- CHECK IF PERSONA ALREADY EXISTS BY ID
                        SELECT @NewPersonaId = ID_PERSONA FROM ADM.PERSONAS WHERE NUM_IDENTIFICACION = @NumIdentificacion;

                        IF @NewPersonaId IS NULL
                        BEGIN
                            INSERT INTO ADM.PERSONAS (PRIMER_NOMBRE, SEGUNDO_NOMBRE, PRIMER_APELLIDO, SEGUNDO_APELLIDO, 
                                                     ID_TIPO_IDENTIFICACION, NUM_IDENTIFICACION, ID_GENERO, FECHA_NACIMIENTO, 
                                                     EMAIL, TELEFONO, DIRECCION, ID_ESTADO)
                            VALUES (@PNombre, @SNombre, @PApellido, @SApellido, @IdTipoIdentificacion, @NumIdentificacion, @IdGenero, @FechaNacimiento, 
                                    @Email, @Tel, @Direccion, 1);
                            
                            SET @NewPersonaId = SCOPE_IDENTITY();
                        END

                        -- CHECK IF PACIENTE ALREADY EXISTS FOR THIS PERSONA
                        SELECT @NewPacienteId = ID_PACIENTE FROM CLI.PACIENTES WHERE ID_PERSONA = @NewPersonaId;

                        IF @NewPacienteId IS NULL
                        BEGIN
                            INSERT INTO CLI.PACIENTES (ID_PERSONA, CONTACTO_EMERGENCIA, TELEFONO_EMERGENCIA, ID_ESTADO)
                            VALUES (@NewPersonaId, @ContactoEmergencia, @TelefonoEmergencia, 1);
                            SET @NewPacienteId = SCOPE_IDENTITY();
                        END

                        UPDATE CLI.CITAS SET ID_PACIENTE = @NewPacienteId, ID_POSIBLE_PACIENTE = NULL, ID_ESTADO_CITA = 2 WHERE ID_CITA = @IdCita;

                        UPDATE CLI.POSIBLES_PACIENTES SET ID_ESTADO = 2 WHERE ID_POSIBLE_PACIENTE = @IdPosiblePaciente;

                        COMMIT;
                        SELECT @NewPacienteId AS ID_PACIENTE_GENERADO;
                    END TRY
                    BEGIN CATCH
                        ROLLBACK;
                        THROW;
                    END CATCH
                END
            ");

            // 6. Fix missing FechaResultado for exams that already have results or comments
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[CLI].[EXAMENES]'))
                BEGIN
                    UPDATE [CLI].[EXAMENES] 
                    SET [FECHA_RESULTADO] = [FECHA_ORDEN] 
                    WHERE [FECHA_RESULTADO] IS NULL 
                    AND ([COMENTARIO_MEDICO] IS NOT NULL OR [IMAGEN_RESULTADO] IS NOT NULL);
                END

                -- 7. Create ADM.MODULOS and ADM.USUARIO_MODULOS
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MODULOS' AND schema_id = SCHEMA_ID('ADM'))
                BEGIN
                    CREATE TABLE ADM.MODULOS (
                        ID_MODULO INT IDENTITY PRIMARY KEY,
                        NOMBRE VARCHAR(100) NOT NULL,
                        ICONO VARCHAR(50), 
                        CONTROLLER VARCHAR(100) NOT NULL, 
                        ORDEN INT DEFAULT 0,
                        ID_ESTADO INT DEFAULT 1,
                        FOREIGN KEY (ID_ESTADO) REFERENCES CAT.ESTADOS(ID_ESTADO)
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'USUARIO_MODULOS' AND schema_id = SCHEMA_ID('ADM'))
                BEGIN
                    CREATE TABLE ADM.USUARIO_MODULOS (
                        ID_USUARIO INT NOT NULL,
                        ID_MODULO INT NOT NULL,
                        PRIMARY KEY (ID_USUARIO, ID_MODULO),
                        FOREIGN KEY (ID_USUARIO) REFERENCES ADM.USUARIOS(ID_USUARIO),
                        FOREIGN KEY (ID_MODULO) REFERENCES ADM.MODULOS(ID_MODULO)
                    );
                END

                -- Ensure modules have IdEstado = 1
                UPDATE ADM.MODULOS SET ID_ESTADO = 1 WHERE ID_ESTADO IS NULL;
            ");

            // Seed Modules if empty
            if (!context.Modulos.Any())
            {
                context.Modulos.AddRange(
                    new Modulo { Nombre = "Dashboard", Icono = "fas fa-th-large", Controller = "Home", Orden = 1, IdEstado = 1 },
                    new Modulo { Nombre = "Citas", Icono = "fas fa-calendar-alt", Controller = "Citas", Orden = 2, IdEstado = 1 },
                    new Modulo { Nombre = "Pacientes", Icono = "fas fa-user-injured", Controller = "Pacientes", Orden = 3, IdEstado = 1 },
                    new Modulo { Nombre = "Tratamientos", Icono = "fas fa-hand-holding-medical", Controller = "Tratamientos", Orden = 4, IdEstado = 1 },
                    new Modulo { Nombre = "Consultas", Icono = "fas fa-stethoscope", Controller = "Consultas", Orden = 5, IdEstado = 1 },
                    new Modulo { Nombre = "Inventario", Icono = "fas fa-boxes", Controller = "Inventario", Orden = 6, IdEstado = 1 },
                    new Modulo { Nombre = "Caja y Pagos", Icono = "fas fa-cash-register", Controller = "Caja", Orden = 7, IdEstado = 1 },

                    new Modulo { Nombre = "Configuración", Icono = "fas fa-cog", Controller = "Configuracion", Orden = 9, IdEstado = 1 }
                );
                context.SaveChanges();
            }

            Console.WriteLine("Schema migrations applied.");
        }

        private static void SeedTreatments(MediTechContext context)
        {
            if (context.Tratamientos.Any()) return;

            var usd = context.Monedas.FirstOrDefault(m => m.Codigo == "USD");

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

