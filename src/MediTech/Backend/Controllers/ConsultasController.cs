using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Linq;

namespace MediTech.Backend.Controllers
{
    [Authorize]
    public class ConsultasController : Controller
    {
        private readonly MediTechContext _context;
        private readonly IMemoryCache _cache;
        private const string TasaCacheKey = "ActiveExchangeRate";

        public ConsultasController(MediTechContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: Consultas
        public async Task<IActionResult> Index()
        {
            var consultas = await _context.Consultas
                .Include(c => c.Cita!)
                    .ThenInclude(ci => ci.Paciente!)
                        .ThenInclude(p => p.Persona!)
                .Include(c => c.Medico!)
                    .ThenInclude(e => e.Persona!)
                .Include(c => c.Estado)
                .OrderByDescending(c => c.FechaConsulta)
                .ToListAsync();

            return View(consultas);
        }

        // GET: Consultas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var consulta = await _context.Consultas
                .Include(c => c.Cita!)
                    .ThenInclude(ci => ci.Paciente!)
                        .ThenInclude(p => p.Persona!)
                .Include(c => c.Cita!)
                    .ThenInclude(ci => ci.Tratamiento!)
                .Include(c => c.Medico!)
                    .ThenInclude(e => e.Persona!)
                .Include(c => c.SignosVitales!)
                .Include(c => c.Diagnosticos)
                .Include(c => c.Estado)
                .FirstOrDefaultAsync(m => m.IdConsulta == id);

            if (consulta == null) return NotFound();

            return View(consulta);
        }

        // GET: Consultas/Recepcion/5 (Triage/Reception Form)
        public async Task<IActionResult> Recepcion(int? id)
        {
            if (id == null)
            {
                TempData["Error"] = "ID de cita no proporcionado.";
                return RedirectToAction("Index", "Citas");
            }

            var cita = await _context.Citas
                .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
                .Include(c => c.Tratamiento)
                .FirstOrDefaultAsync(m => m.IdCita == id);

            if (cita == null)
            {
                TempData["Error"] = "Cita no encontrada.";
                return RedirectToAction("Index", "Citas");
            }

            // Buscar si ya existe una consulta EN PROCESO para esta cita
            var consultaExistente = await _context.Consultas
                .Include(c => c.SignosVitales)
                .Include(c => c.Estado)
                .Include(c => c.Cita).ThenInclude(ci => ci!.Paciente).ThenInclude(p => p!.Persona)
                .Include(c => c.Cita).ThenInclude(ci => ci!.Tratamiento)
                .FirstOrDefaultAsync(c => c.IdCita == id && c.Estado!.DescEstado == "EN PROCESO");

            if (consultaExistente != null)
            {
                if (consultaExistente.SignosVitales == null) consultaExistente.SignosVitales = new SignosVitales();
                return View(consultaExistente);
            }

            // Obtener el ID del usuario desde los claims (NombreIdentificador)
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                TempData["Error"] = "Sesión de usuario no válida.";
                return RedirectToAction("Index", "Citas");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Empleado)
                .FirstOrDefaultAsync(u => u.IdUsuario == userId);

            if (usuario?.IdEmpleado == null)
            {
                var username = User.Identity?.Name ?? "Desconocido";
                TempData["Error"] = $"El usuario '{username}' no tiene un registro de empleado asociado para realizar consultas.";
                return RedirectToAction("Index", "Citas");
            }

            var estadoEnProceso = await _context.Estados.FirstOrDefaultAsync(e => e.DescEstado == "EN PROCESO");
            
            var nuevaConsulta = new Consulta
            {
                IdCita = id.Value,
                IdMedico = usuario.IdEmpleado.Value,
                IdEstado = estadoEnProceso?.IdEstado ?? 1,
                FechaConsulta = DateTime.Now,
                SignosVitales = new SignosVitales(),
                Cita = cita
            };

            return View(nuevaConsulta);
        }

        // POST: Consultas/Recepcion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recepcion(Consulta consulta, SignosVitales signos)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existente = await _context.Consultas
                    .Include(c => c.SignosVitales)
                    .FirstOrDefaultAsync(c => c.IdCita == consulta.IdCita && c.IdEstado == consulta.IdEstado);

                if (existente == null)
                {
                    _context.Consultas.Add(consulta);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    existente.MotivoConsulta = consulta.MotivoConsulta;
                    existente.DiagnosticoPrincipal = consulta.DiagnosticoPrincipal;
                    existente.Observaciones = consulta.Observaciones;
                    _context.Update(existente);
                    consulta = existente;
                }

                // Guardar/Actualizar Signos Vitales
                var svExistente = await _context.SignosVitales.FirstOrDefaultAsync(s => s.IdConsulta == consulta.IdConsulta);
                if (svExistente != null)
                {
                    svExistente.PresionArterial = signos.PresionArterial;
                    svExistente.Temperatura = signos.Temperatura;
                    svExistente.FrecuenciaCardiaca = signos.FrecuenciaCardiaca;
                    svExistente.SaturacionOxigeno = signos.SaturacionOxigeno;
                    svExistente.Peso = signos.Peso;
                    svExistente.Altura = signos.Altura;
                    _context.Update(svExistente);
                }
                else
                {
                    signos.IdConsulta = consulta.IdConsulta;
                    _context.Add(signos);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Obtener IdPaciente para la redirección
                var cita = await _context.Citas.FindAsync(consulta.IdCita);
                return RedirectToAction("Ficha", "Pacientes", new { id = cita?.IdPaciente, consultaId = consulta.IdConsulta, modo = "consulta" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Error al guardar la recepción: " + ex.Message);
                
                // Re-cargar datos para la vista
                consulta.Cita = await _context.Citas
                    .Include(c => c.Paciente!).ThenInclude(p => p.Persona!)
                    .Include(c => c.Tratamiento)
                    .FirstOrDefaultAsync(m => m.IdCita == consulta.IdCita);
                return View(consulta);
            }
        }

        // GET: Consultas/Atender/5 (id is IdCita)
        public async Task<IActionResult> Atender(int? id)
        {
            if (id == null) return RedirectToAction("Index", "Citas");

            var cita = await _context.Citas
                .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
                .Include(c => c.Tratamiento)
                .FirstOrDefaultAsync(m => m.IdCita == id);

            if (cita == null) return NotFound();

            // Verificar si ya tiene una consulta
            var consultaExistente = await _context.Consultas.FirstOrDefaultAsync(c => c.IdCita == id);
            if (consultaExistente != null)
            {
                return RedirectToAction("Details", new { id = consultaExistente.IdConsulta });
            }

            // Obtener el ID del médico (usuario logueado)
            var username = User.Identity?.Name;
            var usuario = await _context.Usuarios
                .Include(u => u.Empleado)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (usuario?.IdEmpleado == null)
            {
                TempData["Error"] = "No se pudo identificar al médico para esta consulta.";
                return RedirectToAction("Index", "Citas");
            }

            var nuevaConsulta = new Consulta
            {
                IdCita = cita.IdCita,
                IdMedico = usuario!.IdEmpleado!.Value,
                MotivoConsulta = cita.Observaciones,
                IdEstado = 1,
                Cita = cita
            };

            nuevaConsulta.SignosVitales = new SignosVitales();

            return View("Create", nuevaConsulta);
        }

        // POST: Consultas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Consulta consulta, SignosVitales signos)
        {
            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. Guardar Consulta
                    _context.Add(consulta);
                    await _context.SaveChangesAsync();

                    // 2. Guardar Signos Vitales vinculados
                    signos.IdConsulta = consulta.IdConsulta;
                    _context.Add(signos);

                    // 3. Actualizar estado de la Cita a FINALIZADA (3)
                    var cita = await _context.Citas.FindAsync(consulta.IdCita);
                    if (cita != null)
                    {
                        cita.IdEstadoCita = 3; // FINALIZADA
                        _context.Update(cita);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Consulta registrada y guardada exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Ocurrió un error al guardar la consulta: " + ex.Message);
                }
            }

            // Si falla, volver a cargar datos básicos
            consulta.Cita = await _context.Citas
                .Include(c => c.Paciente!).ThenInclude(p => p.Persona!)
                .FirstOrDefaultAsync(m => m.IdCita == consulta.IdCita);
            
            return View(consulta);
        }

        // GET: Consultas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var consulta = await _context.Consultas
                .Include(c => c.SignosVitales!)
                .Include(c => c.Cita!).ThenInclude(ci => ci.Paciente!).ThenInclude(p => p.Persona!)
                .FirstOrDefaultAsync(m => m.IdConsulta == id);

            if (consulta == null) return NotFound();

            ViewData["IdEstado"] = new SelectList(_context.Estados, "IdEstado", "DescEstado", consulta.IdEstado);
            return View(consulta);
        }

        // POST: Consultas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Consulta consulta, SignosVitales signos)
        {
            if (id != consulta.IdConsulta) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(consulta);
                    
                    var svExistente = await _context.SignosVitales.FirstOrDefaultAsync(s => s.IdConsulta == id);
                    if (svExistente != null)
                    {
                        svExistente.PresionArterial = signos.PresionArterial;
                        svExistente.Temperatura = signos.Temperatura;
                        svExistente.FrecuenciaCardiaca = signos.FrecuenciaCardiaca;
                        svExistente.SaturacionOxigeno = signos.SaturacionOxigeno;
                        svExistente.Peso = signos.Peso;
                        svExistente.Altura = signos.Altura;
                        _context.Update(svExistente);
                    }
                    else
                    {
                        signos.IdConsulta = id;
                        _context.Add(signos);
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Consulta actualizada correctamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ConsultaExists(consulta.IdConsulta)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(consulta);
        }

        // POST: Consultas/CerrarConsulta
        [HttpPost]
        public async Task<IActionResult> CerrarConsulta([FromBody] CerrarConsultaDto data)
        {
            Console.WriteLine("=== CIERRE CONSULTA: Inicio ===");

            if (data == null || data.IdConsulta <= 0)
            {
                Console.WriteLine("CIERRE CONSULTA: Datos inválidos o IdConsulta <= 0");
                return Json(new { success = false, message = "Datos inválidos." });
            }

            // Null safety para Items
            data.Items ??= new List<ItemCierreDto>();

            Console.WriteLine($"CIERRE CONSULTA: IdConsulta={data.IdConsulta}, Items={data.Items.Count}, Diagnóstico={data.DiagnosticoFinal?.Length ?? 0} chars");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var consulta = await _context.Consultas
                    .Include(c => c.Cita)
                    .FirstOrDefaultAsync(c => c.IdConsulta == data.IdConsulta);

                if (consulta == null)
                {
                    Console.WriteLine($"CIERRE CONSULTA: Consulta {data.IdConsulta} NO encontrada en BD.");
                    return Json(new { success = false, message = "Consulta no encontrada." });
                }

                var estadoFinalizada = await _context.Estados.FirstOrDefaultAsync(e => e.DescEstado == "FINALIZADA");
                var idEstadoFinalizada = estadoFinalizada?.IdEstado ?? 3;

                if (consulta.IdEstado == idEstadoFinalizada)
                    return Json(new { success = false, message = "La consulta ya fue finalizada previamente." });

                // 1. Actualizar Consulta
                consulta.DiagnosticoPrincipal = data.DiagnosticoFinal;
                consulta.Observaciones = data.Observaciones;
                consulta.IdEstado = idEstadoFinalizada;
                _context.Update(consulta);

                // 2. Actualizar estado de la Cita (sincronizar calendario)
                if (consulta.Cita != null)
                {
                    consulta.Cita.IdEstadoCita = 3; // FINALIZADA/ATENDIDA
                    _context.Update(consulta.Cita);
                    Console.WriteLine($"CIERRE CONSULTA: Cita {consulta.Cita.IdCita} actualizada a IdEstadoCita=3");
                }

                // 3. Crear Cuenta en CAJA
                var configMoneda = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
                // Recalcular total en el backend (no confiar solo en subtotal del frontend)
                decimal total = data.Items.Sum(i => i.Cantidad * i.PrecioUnitario);
                Console.WriteLine($"CIERRE CONSULTA: Total calculado = {total}");

                var nuevaCuenta = new Cuenta
                {
                    IdPaciente = consulta.Cita?.IdPaciente,
                    IdConsulta = consulta.IdConsulta,
                    TotalBruto = total,
                    Descuento = 0,
                    TotalFinal = total,
                    IdMonedaBase = configMoneda?.IdMonedaBase,
                    FechaCreacion = DateTime.Now
                };
                _context.Cuentas.Add(nuevaCuenta);
                await _context.SaveChangesAsync(); // Para obtener IdCuenta

                Console.WriteLine($"CIERRE CONSULTA: Cuenta creada con IdCuenta={nuevaCuenta.IdCuenta}");

                // 4. Procesar Items (Detalles clínicos, Cuenta Detalles e Inventario)
                foreach (var item in data.Items)
                {
                    // Detalle Clínico
                    var consultaDetalle = new ConsultaDetalle
                    {
                        IdConsulta = consulta.IdConsulta,
                        TipoItem = item.TipoItem?.ToUpper(),
                        IdReferencia = item.IdReferencia,
                        Descripcion = item.Descripcion,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = item.PrecioUnitario,
                        Subtotal = item.Cantidad * item.PrecioUnitario
                    };
                    _context.ConsultaDetalles.Add(consultaDetalle);

                    // Detalle Cuenta (Caja)
                    var cuentaDetalle = new CuentaDetalle
                    {
                        IdCuenta = nuevaCuenta.IdCuenta,
                        TipoItem = item.TipoItem?.ToUpper(),
                        IdReferencia = item.IdReferencia,
                        Descripcion = item.Descripcion,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = item.PrecioUnitario,
                        Subtotal = item.Cantidad * item.PrecioUnitario
                    };
                    _context.CuentaDetalles.Add(cuentaDetalle);

                    // Inventario (si es PRODUCTO)
                    if (item.TipoItem?.ToUpper() == "PRODUCTO")
                    {
                        var producto = await _context.Productos.FindAsync(item.IdReferencia);
                        if (producto == null)
                            throw new Exception($"Producto no encontrado (ID: {item.IdReferencia}).");

                        if (producto.Stock < item.Cantidad)
                            throw new Exception($"Stock insuficiente para el producto: {producto.Nombre}. Stock actual: {producto.Stock}. Req: {item.Cantidad}");

                        producto.Stock -= item.Cantidad;
                        _context.Update(producto);

                        var movInventario = new MovimientoInventario
                        {
                            IdProducto = producto.IdProducto,
                            TipoMovimiento = "VENTA",
                            Cantidad = item.Cantidad,
                            Observacion = $"Cierre de consulta #{consulta.IdConsulta}",
                            FechaMovimiento = DateTime.Now
                        };
                        _context.MovimientosInventario.Add(movInventario);
                    }

                    Console.WriteLine($"  Item: {item.TipoItem} - {item.Descripcion} x{item.Cantidad} @ {item.PrecioUnitario}");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"=== CIERRE CONSULTA: Éxito. CuentaId={nuevaCuenta.IdCuenta} ===");
                return Json(new { success = true, idConsulta = consulta.IdConsulta, cuentaId = nuevaCuenta.IdCuenta });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"=== CIERRE CONSULTA: ERROR — {ex.Message} ===");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCatTratamientos()
        {
            var tratamientos = await _context.Tratamientos
                .Where(t => t.IdEstado == 1) // 1 ACTIVO
                .Select(t => new { 
                    id = t.IdTratamiento, 
                    nombre = t.NombreTratamiento, 
                    precio = t.Precio ?? 0 
                })
                .ToListAsync();
            return Json(tratamientos);
        }

        [HttpGet]
        public async Task<IActionResult> GetCatProductos()
        {
            var productos = await _context.Productos
                .Where(p => p.Activo && p.Stock > 0)
                .Select(p => new { 
                    id = p.IdProducto, 
                    nombre = p.Nombre, 
                    precio = p.Precio ?? 0,
                    stock = p.Stock 
                })
                .ToListAsync();
            return Json(productos);
        }

        [HttpGet]
        public async Task<IActionResult> GetHistorialPaciente(int id)
        {
            try
            {
                var rawConsultas = await _context.Consultas
                    .Include(c => c.Estado)
                    .Include(c => c.Medico!)
                        .ThenInclude(m => m.Persona)
                    .Include(c => c.SignosVitales)
                    .Include(c => c.ConsultaDetalles)
                    .Where(c => c.Cita!.IdPaciente == id && c.Estado!.DescEstado == "FINALIZADA")
                    .OrderByDescending(c => c.FechaConsulta)
                    .ToListAsync();

                var consultas = rawConsultas.Select(c => new {
                    idConsulta = c.IdConsulta,
                    fecha = c.FechaConsulta.ToString("dd/MM/yyyy"),
                    hora = c.FechaConsulta.ToString("hh:mm tt"),
                    medico = c.Medico != null && c.Medico.Persona != null ? $"{c.Medico.Persona.PrimerNombre} {c.Medico.Persona.PrimerApellido}" : "Médico —",
                    diagnostico = c.DiagnosticoPrincipal ?? "Sin diagnóstico",
                    observaciones = c.Observaciones ?? "Cerrada",
                    signos = c.SignosVitales != null ? new {
                        pa = c.SignosVitales.PresionArterial ?? "--/--",
                        temp = c.SignosVitales.Temperatura != null ? c.SignosVitales.Temperatura.Value.ToString("0.0") : "--",
                        fc = c.SignosVitales.FrecuenciaCardiaca?.ToString() ?? "--",
                        sat = c.SignosVitales.SaturacionOxigeno?.ToString() ?? "--",
                        peso = c.SignosVitales.Peso?.ToString("0.00") ?? "--",
                        talla = c.SignosVitales.Altura?.ToString("0.00") ?? "--",
                        bmi = c.SignosVitales.BMI != null ? c.SignosVitales.BMI.Value.ToString("0.0") : "--.-"
                    } : null,
                    items = c.ConsultaDetalles.Select(d => new {
                        descripcion = d.Descripcion,
                        tipo = d.TipoItem,
                        cant = d.Cantidad,
                        precio = d.PrecioUnitario?.ToString("N2") ?? "0.00",
                        subtotal = d.Subtotal?.ToString("N2") ?? "0.00"
                    })
                });

                return Json(new { success = true, data = consultas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al obtener historial: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMonedaConfig()
        {
            try
            {
                var config = await _context.ConfiguracionesMoneda
                    .Include(c => c.MonedaBase)
                    .FirstOrDefaultAsync();

                if (config == null || config.MonedaBase == null)
                    return Json(new { success = false, message = "Configuración de moneda no encontrada." });

                return Json(new { 
                    success = true, 
                    id = config.IdMonedaBase, 
                    simbolo = config.MonedaBase.Simbolo ?? "$",
                    codigo = config.MonedaBase.Codigo
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private bool ConsultaExists(int id)
        {
            return _context.Consultas.Any(e => e.IdConsulta == id);
        }
    }
}

