using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MediTech.Controllers
{
    [Authorize]
    public class ConsultasController(MediTechContext context) : Controller
    {
        private readonly MediTechContext _context = context;

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

        private bool ConsultaExists(int id)
        {
            return _context.Consultas.Any(e => e.IdConsulta == id);
        }
    }
}
