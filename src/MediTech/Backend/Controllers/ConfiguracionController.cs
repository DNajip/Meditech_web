using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace MediTech.Backend.Controllers;

[Authorize(Roles = "ADMINISTRADOR")] 
public class ConfiguracionController : Controller
{
    private readonly MediTechContext _context;
    private readonly IMemoryCache _cache;
    private const string TasaCacheKey = "ActiveExchangeRate";

    public ConfiguracionController(MediTechContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var config = await _context.ConfiguracionesMoneda
            .Include(c => c.MonedaBase)
            .FirstOrDefaultAsync();

        var tasaActiva = await _context.TasasCambio
            .Include(t => t.MonedaOrigen)
            .Include(t => t.MonedaDestino)
            .FirstOrDefaultAsync(t => t.Activo);

        var historial = await _context.TasasCambio
            .OrderByDescending(t => t.Fecha)
            .Take(10)
            .ToListAsync();

        ViewBag.TasaActiva = tasaActiva;
        ViewBag.Historial = historial;
        ViewBag.Monedas = await _context.Monedas.ToListAsync();

        // Asegurar que el rol CAJERO existe (Self-healing reforzado)
        var rolesDB = await _context.Roles.ToListAsync();
        if (!rolesDB.Any(r => r.DescRol.Trim().Equals("CAJERO", StringComparison.OrdinalIgnoreCase)))
        {
            _context.Roles.Add(new Role { DescRol = "CAJERO", IdEstado = 1 });
            await _context.SaveChangesAsync();
            rolesDB = await _context.Roles.ToListAsync(); // Recargar
        }
        
        ViewBag.Roles = rolesDB;

        // Cargar Usuarios y Módulos para la nueva sección de administración
        ViewBag.Usuarios = await _context.Usuarios
            .Include(u => u.Role)
            .Include(u => u.Empleado)
                .ThenInclude(e => e!.Persona)
            .Include(u => u.UsuarioModulos)
            .ToListAsync();

        ViewBag.Generos = await _context.Generos.ToListAsync();
        ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.ToListAsync();

        ViewBag.TodosModulos = await _context.Modulos
            .Where(m => m.IdEstado == 1 && m.Nombre != "Exámenes" && m.Controller != "Examenes")
            .OrderBy(m => m.Orden)
            .ToListAsync();

        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarTasa(decimal valor)
    {
        if (valor <= 0)
        {
            TempData["Error"] = "La tasa debe ser mayor a cero.";
            return RedirectToAction(nameof(Index));
        }

        var tasaActual = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
        if (tasaActual != null && tasaActual.Valor == valor)
        {
            TempData["Info"] = "El valor es idéntico a la tasa actual.";
            return RedirectToAction(nameof(Index));
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Desactivar tasas anteriores
            var activas = await _context.TasasCambio.Where(t => t.Activo).ToListAsync();
            foreach (var t in activas) t.Activo = false;

            var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
            if (config == null) throw new Exception("Configuración de moneda no encontrada.");

            var monedaBaseId = config.IdMonedaBase;
            var monedaDestino = await _context.Monedas.FirstOrDefaultAsync(m => m.IdMoneda != monedaBaseId);

            if (monedaDestino == null) throw new Exception("No hay una moneda secundaria configurada para la tasa de cambio.");

            var nuevaTasa = new TasaCambio
            {
                IdMonedaOrigen = monedaBaseId,
                IdMonedaDestino = monedaDestino.IdMoneda,
                Valor = valor,
                Activo = true,
                Fecha = DateTime.Now,
                UsuarioModificacion = User.Identity?.Name ?? "SISTEMA"
            };

            _context.TasasCambio.Add(nuevaTasa);
            
            // También actualizar el campo legacy en configuracion por si acaso se usa en algun lugar aun no refactorizado
            config.TasaCambio = valor;
            config.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Invalidar caché
            _cache.Remove(TasaCacheKey);

            TempData["Success"] = "Tasa de cambio actualizada exitosamente.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Error al actualizar tasa: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarMonedaBase(int idMonedaBase)
    {
        var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
        
        // Restricción Crítica: No se puede cambiar si existen datos registrados
        bool tieneDatos = await _context.Productos.AnyAsync() || 
                          await _context.Cuentas.AnyAsync() || 
                          await _context.Pagos.AnyAsync();

        if (tieneDatos)
        {
            TempData["Error"] = "OPERACIÓN BLOQUEADA: No se puede cambiar la moneda base porque ya existen productos, cuentas o pagos registrados en el sistema.";
            return RedirectToAction(nameof(Index));
        }

        if (config == null)
        {
            config = new ConfiguracionMoneda { IdMonedaBase = idMonedaBase };
            _context.ConfiguracionesMoneda.Add(config);
        }
        else
        {
            config.IdMonedaBase = idMonedaBase;
            config.FechaActualizacion = DateTime.Now;
            config.UsuarioModificacion = User.Identity?.Name ?? "SISTEMA";
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Moneda base actualizada exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPermisosUsuario(int idUsuario, List<int> modulosSeleccionados)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioModulos)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);

        if (usuario == null)
        {
            TempData["Error"] = "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Remover permisos actuales
            _context.UsuarioModulos.RemoveRange(usuario.UsuarioModulos);
            await _context.SaveChangesAsync();

            // Agregar nuevos permisos
            if (modulosSeleccionados != null && modulosSeleccionados.Any())
            {
                foreach (var moduloId in modulosSeleccionados)
                {
                    _context.UsuarioModulos.Add(new UsuarioModulo
                    {
                        IdUsuario = idUsuario,
                        IdModulo = moduloId
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = $"Permisos actualizados para el usuario {usuario.Username}.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Error al actualizar permisos: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarUsuario(string username, string password, int idRol, 
        string nombre, string apellido, string numIdentificacion, int idGenero, int idTipoIdentificacion, 
        DateTime fechaNacimiento, string telefono)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(numIdentificacion))
        {
            var msg = "Todos los campos obligatorios deben estar llenos.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = false, message = msg });
            TempData["Error"] = msg;
            return RedirectToAction(nameof(Index));
        }

        if (await _context.Usuarios.AnyAsync(u => u.Username == username))
        {
            var msg = "El nombre de usuario ya existe.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = false, message = msg });
            TempData["Error"] = msg;
            return RedirectToAction(nameof(Index));
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Limpieza de residuos
            var personaExistente = await _context.Personas
                .Include(p => p.Empleados)
                .FirstOrDefaultAsync(p => p.NumIdentificacion == numIdentificacion);

            Persona persona;
            if (personaExistente != null)
            {
                if (!personaExistente.Empleados.Any())
                {
                    persona = personaExistente;
                    persona.PrimerNombre = nombre;
                    persona.PrimerApellido = apellido;
                    persona.IdGenero = idGenero;
                    persona.IdTipoIdentificacion = idTipoIdentificacion;
                    persona.FechaNacimiento = fechaNacimiento;
                    persona.Telefono = telefono;
                    _context.Personas.Update(persona);
                }
                else
                {
                    throw new Exception("Ya existe un empleado registrado con esa identificación.");
                }
            }
            else
            {
                persona = new Persona
                {
                    PrimerNombre = nombre,
                    PrimerApellido = apellido,
                    NumIdentificacion = numIdentificacion,
                    IdGenero = idGenero,
                    IdTipoIdentificacion = idTipoIdentificacion,
                    FechaNacimiento = fechaNacimiento,
                    Telefono = telefono,
                    FechaCreacion = DateTime.Now,
                    IdEstado = 1
                };
                _context.Personas.Add(persona);
            }
            
            await _context.SaveChangesAsync();

            var empleado = new Empleado
            {
                IdPersona = persona.IdPersona,
                IdRol = idRol,
                FechaContratacion = DateTime.Now,
                IdEstado = 1
            };
            _context.Empleados.Add(empleado);
            await _context.SaveChangesAsync();

            CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);
            var usuario = new Usuario
            {
                Username = username,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                IdEmpleado = empleado.IdEmpleado,
                IdRol = idRol,
                IdEstado = 1,
                FechaCreacion = DateTime.Now
            };
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var modulosDefault = await _context.Modulos.Where(m => m.IdEstado == 1).ToListAsync();
            if (idRol == 1) // ADMIN
            {
                foreach (var m in modulosDefault)
                {
                    _context.UsuarioModulos.Add(new UsuarioModulo { IdUsuario = usuario.IdUsuario, IdModulo = m.IdModulo });
                }
            }
            else
            {
                var dashboard = modulosDefault.FirstOrDefault(m => m.Controller == "Home");
                if (dashboard != null)
                {
                    _context.UsuarioModulos.Add(new UsuarioModulo { IdUsuario = usuario.IdUsuario, IdModulo = dashboard.IdModulo });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            TempData["Success"] = $"Usuario {username} creado exitosamente.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = ex.Message });
            TempData["Error"] = "Error al registrar usuario: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetUsuario(int id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Empleado)
                .ThenInclude(e => e!.Persona)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);

        if (usuario == null) return NotFound();

        return Json(new
        {
            idUsuario = usuario.IdUsuario,
            username = usuario.Username,
            idRol = usuario.IdRol,
            idEstado = usuario.IdEstado,
            nombre = usuario.Empleado?.Persona?.PrimerNombre,
            apellido = usuario.Empleado?.Persona?.PrimerApellido,
            numIdentificacion = usuario.Empleado?.Persona?.NumIdentificacion,
            idGenero = usuario.Empleado?.Persona?.IdGenero,
            idTipoIdentificacion = usuario.Empleado?.Persona?.IdTipoIdentificacion,
            fechaNacimiento = usuario.Empleado?.Persona?.FechaNacimiento?.ToString("yyyy-MM-dd"),
            telefono = usuario.Empleado?.Persona?.Telefono
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarUsuario(int idUsuario, string? password, int idRol, string nombre, string apellido, int idEstado)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Empleado)
                .ThenInclude(e => e!.Persona)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);

        if (usuario == null)
        {
            return Json(new { success = false, message = "Usuario no encontrado." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Actualizar Persona
            if (usuario.Empleado?.Persona != null)
            {
                usuario.Empleado.Persona.PrimerNombre = nombre;
                usuario.Empleado.Persona.PrimerApellido = apellido;
                usuario.Empleado.Persona.IdEstado = idEstado;
            }

            // 2. Actualizar Empleado
            if (usuario.Empleado != null)
            {
                usuario.Empleado.IdRol = idRol;
                usuario.Empleado.IdEstado = idEstado;
            }

            // 3. Actualizar Usuario
            usuario.IdRol = idRol;
            usuario.IdEstado = idEstado;

            if (!string.IsNullOrEmpty(password))
            {
                CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);
                usuario.PasswordHash = passwordHash;
                usuario.PasswordSalt = passwordSalt;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Json(new { success = true, message = "Usuario actualizado correctamente." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Json(new { success = false, message = "Error al actualizar: " + ex.Message });
        }
    }


    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}

