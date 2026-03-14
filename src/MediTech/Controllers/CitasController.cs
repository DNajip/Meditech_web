using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;namespace MediTech.Controllers;

[Authorize]
public class CitasController : Controller
{
    private readonly MediTechContext _context;
    private readonly ILogger<CitasController> _logger;

    public CitasController(MediTechContext context, ILogger<CitasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Citas — FullCalendar view
    public async Task<IActionResult> Index()
    {
        await PrepareDropdowns();
        return View();
    }

    // GET: Citas/GetEvents — JSON feed for FullCalendar
    [HttpGet]
    public async Task<IActionResult> GetEvents(DateTime start, DateTime end)
    {
        var citas = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .Where(c => c.Fecha >= start.Date && c.Fecha <= end.Date)
            .ToListAsync();

        var events = citas.Select(c => new
        {
            id = c.IdCita,
            title = $"{c.Paciente?.Persona?.PrimerNombre} {c.Paciente?.Persona?.PrimerApellido}",
            start = c.Fecha.Date.Add(c.HoraInicio).ToString("yyyy-MM-ddTHH:mm:ss"),
            end = c.Fecha.ToString("yyyy-MM-dd") + "T" + c.HoraFin.ToString(@"hh\:mm\:ss"),
            color = "#3B82F6",
            extendedProps = new
            {
                pacienteId = c.IdPaciente,
                tratamiento = c.Tratamiento?.NombreTratamiento ?? "Consulta General",
                observaciones = c.Observaciones ?? "",
                estado = c.Estado?.DescEstado ?? "ACTIVO",
                estadoId = c.IdEstado,
                identificacion = c.Paciente?.Persona?.NumIdentificacion ?? "",
                telefono = c.Telefono
            }
        });

        return Json(events);
    }

    // GET: Citas/GetTodayAgenda — JSON for "Today's Agenda" panel
    [HttpGet]
    public async Task<IActionResult> GetTodayAgenda()
    {
        var today = DateTime.Today;
        var citas = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .Where(c => c.Fecha == today && c.IdEstado == 1)
            .OrderBy(c => c.HoraInicio)
            .ToListAsync();

        var agenda = citas.Select(c => new
        {
            id = c.IdCita,
            horaInicio = DateTime.Today.Add(c.HoraInicio).ToString("hh:mm tt"),
            horaFin = DateTime.Today.Add(c.HoraFin).ToString("hh:mm tt"),
            paciente = $"{c.Paciente?.Persona?.PrimerNombre} {c.Paciente?.Persona?.PrimerApellido}",
            tratamiento = c.Tratamiento?.NombreTratamiento ?? "General",
            color = "#3B82F6",
            observaciones = c.Observaciones ?? "",
            pacienteId = c.IdPaciente,
            estado = c.Estado?.DescEstado ?? "ACTIVO"
        });

        return Json(new { count = citas.Count, items = agenda });
    }

    // GET: Citas/Search?term=xxx — JSON search for patients/appointments
    [HttpGet]
    public async Task<IActionResult> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Json(new { patients = Array.Empty<object>(), appointments = Array.Empty<object>() });

        var normalizedTerm = term.ToUpper();

        var patients = await _context.Pacientes
            .Include(p => p.Persona)
            .Where(p => p.IdEstado == 1 &&
                (p.Persona!.PrimerNombre.ToUpper().Contains(normalizedTerm) ||
                 p.Persona.PrimerApellido.ToUpper().Contains(normalizedTerm) ||
                 p.Persona.NumIdentificacion.ToUpper().Contains(normalizedTerm)))
            .Take(5)
            .Select(p => new
            {
                id = p.IdPaciente,
                nombre = $"{p.Persona!.PrimerNombre} {p.Persona.PrimerApellido}",
                identificacion = p.Persona.NumIdentificacion
            })
            .ToListAsync();

        var appointments = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .Where(c => c.Paciente!.Persona!.PrimerNombre.ToUpper().Contains(normalizedTerm) ||
                        c.Paciente.Persona!.PrimerApellido.ToUpper().Contains(normalizedTerm) ||
                        c.Paciente.Persona!.NumIdentificacion.ToUpper().Contains(normalizedTerm))
            .OrderByDescending(c => c.Fecha)
            .Take(5)
            .Select(c => new
            {
                id = c.IdCita,
                paciente = $"{c.Paciente!.Persona!.PrimerNombre} {c.Paciente.Persona.PrimerApellido}",
                fecha = c.Fecha.ToString("dd/MM/yyyy"),
                hora = DateTime.Today.Add(c.HoraInicio).ToString("hh:mm tt"),
                tratamiento = c.Tratamiento != null ? c.Tratamiento.NombreTratamiento : "Consulta General"
            })
            .ToListAsync();

        return Json(new { patients, appointments });
    }

    // GET: Citas/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var cita = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .FirstOrDefaultAsync(m => m.IdCita == id);

        if (cita == null) return NotFound();

        return View(cita);
    }

    // GET: Citas/GetEventDetail/5 — JSON for event popover
    [HttpGet]
    public async Task<IActionResult> GetEventDetail(int id)
    {
        var cita = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .FirstOrDefaultAsync(m => m.IdCita == id);

        if (cita == null) return NotFound();

        return Json(new
        {
            id = cita.IdCita,
            paciente = $"{cita.Paciente?.Persona?.PrimerNombre} {cita.Paciente?.Persona?.PrimerApellido}",
            pacienteId = cita.IdPaciente,
            identificacion = cita.Paciente?.Persona?.NumIdentificacion ?? "",
            tratamiento = cita.Tratamiento?.NombreTratamiento ?? "General",
            color = "#3B82F6",
            fecha = cita.Fecha.ToString("dd/MM/yyyy"),
            horaInicio = DateTime.Today.Add(cita.HoraInicio).ToString("hh:mm tt"),
            horaFin = DateTime.Today.Add(cita.HoraFin).ToString("hh:mm tt"),
            observaciones = cita.Observaciones ?? "",
            estado = cita.Estado?.DescEstado ?? "ACTIVO",
            estadoId = cita.IdEstado,
            telefono = cita.Telefono
        });
    }

    // GET: Citas/Create
    public async Task<IActionResult> Create()
    {
        await PrepareDropdowns();
        return View();
    }

    // POST: Citas/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("IdPaciente,IdTratamiento,Fecha,HoraInicio,HoraFin,Observaciones")] Cita? cita)
    {
        if (cita == null) return BadRequest();
        if (ModelState.IsValid)
        {
            // Validations
            if (cita.Fecha < DateTime.Today)
            {
                ModelState.AddModelError("Fecha", "No se pueden crear citas en el pasado.");
            }
            if (cita.HoraFin <= cita.HoraInicio)
            {
                ModelState.AddModelError("HoraFin", "La hora de fin debe ser mayor a la de inicio.");
            }

            // Overlap Conflict Check
            var conflict = await _context.Citas.AnyAsync(c =>
                c.Fecha == cita.Fecha &&
                cita.HoraInicio < c.HoraFin &&
                cita.HoraFin > c.HoraInicio);

            if (conflict)
            {
                ModelState.AddModelError("", "Ya existe una cita programada que se solapa con este horario.");
            }

            if (ModelState.ErrorCount == 0)
            {
                try
                {
                    cita.IdEstado = 1; // Activo/Programada
                    _context.Add(cita);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cita creada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving appointment.");
                    ModelState.AddModelError("", "Error al guardar en la base de datos.");
                }
            }
        }

        await PrepareDropdowns();
        return View(cita);
    }

    // POST: Citas/CreateJson — AJAX endpoint for modal create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateJson(int pacienteId, int tratamientoId, DateTime fecha, string horaInicio, string horaFin, string observaciones, string telefono)
    {
        if (pacienteId == 0)
            return Json(new { success = false, message = "El paciente es obligatorio." });
            
        if (string.IsNullOrEmpty(telefono))
            return Json(new { success = false, message = "El teléfono celular es obligatorio." });

        if (!TimeSpan.TryParse(horaInicio, out TimeSpan hInicio))
            return Json(new { success = false, message = "Formato de hora de inicio inválido." });
        if (!TimeSpan.TryParse(horaFin, out TimeSpan hFin))
            return Json(new { success = false, message = "Formato de hora de fin inválido." });

        var cita = new Cita
        {
            IdPaciente = pacienteId,
            IdTratamiento = tratamientoId,
            Fecha = fecha,
            HoraInicio = hInicio,
            HoraFin = hFin,
            Observaciones = observaciones,
            Telefono = telefono,
            IdEstado = 1 // 1: Programada (puedes ajustar según tu catálogo)
        };

        // Validations
        if (cita.Fecha < DateTime.Today)
            return Json(new { success = false, message = "No se pueden crear citas en el pasado." });
        if (cita.HoraFin <= cita.HoraInicio)
            return Json(new { success = false, message = "La hora de fin debe ser mayor a la de inicio." });

        var conflict = await _context.Citas.AnyAsync(c =>
            c.Fecha == cita.Fecha &&
            cita.HoraInicio < c.HoraFin &&
            cita.HoraFin > c.HoraInicio);

        if (conflict)
            return Json(new { success = false, message = "Ya existe una cita que se solapa con este horario." });

        try
        {
            cita.IdEstado = 1;
            _context.Add(cita);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = cita.IdCita });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving appointment via AJAX.");
            return Json(new { success = false, message = "Error al guardar en la base de datos." });
        }
    }

    // GET: Citas/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var cita = await _context.Citas.FindAsync(id);
        if (cita == null) return NotFound();

        await PrepareDropdowns();
        return View(cita);
    }

    // POST: Citas/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("IdCita,IdPaciente,IdTratamiento,Fecha,HoraInicio,HoraFin,Observaciones,IdEstado,Telefono")] Cita? cita)
    {
        if (cita == null) return NotFound();
        if (id != cita.IdCita) return NotFound();

        if (ModelState.IsValid)
        {
            // Validations
            if (cita.Fecha < DateTime.Today)
            {
                ModelState.AddModelError("Fecha", "No se pueden crear citas en el pasado.");
            }
            if (cita.HoraFin <= cita.HoraInicio)
            {
                ModelState.AddModelError("HoraFin", "La hora de fin debe ser mayor a la de inicio.");
            }

            if (ModelState.ErrorCount == 0)
            {
                try
                {
                    _context.Update(cita);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cita actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CitaExists(cita.IdCita)) return NotFound();
                    else throw;
                }
            }
        }
        await PrepareDropdowns();
        return View(cita);
    }

    // POST: Citas/MarcarAtendida/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarAtendida(int id)
    {
        var cita = await _context.Citas.FindAsync(id);
        if (cita == null) return NotFound();

        try
        {
            cita.IdEstado = 2; // Atendida / En Sala
            _context.Update(cita);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Paciente registrado como En Sala de Espera / Atendido.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing appointment status.");
            TempData["ErrorMessage"] = "No se pudo actualizar el estado de la cita.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Citas/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var cita = await _context.Citas.FindAsync(id);
        if (cita == null) return NotFound();

        try
        {
            cita.IdEstado = 3; // Cancelada
            _context.Update(cita);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cita cancelada correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling appointment.");
            TempData["ErrorMessage"] = "No se pudo cancelar la cita.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Citas/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var cita = await _context.Citas.FindAsync(id);
        if (cita != null)
        {
            _context.Citas.Remove(cita);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cita eliminada correctamente.";
        }
        
        return RedirectToAction(nameof(Index));
    }

    private bool CitaExists(int id)
    {
        return _context.Citas.Any(e => e.IdCita == id);
    }

    private async Task PrepareDropdowns()
    {
        var pacientes = await _context.Pacientes
            .Include(p => p.Persona)
            .Where(p => p.IdEstado == 1)
            .Select(p => new {
                p.IdPaciente,
                Nombre = p.Persona != null ? $"{p.Persona.PrimerNombre} {p.Persona.PrimerApellido} ({p.Persona.NumIdentificacion})" : "Paciente sin nombre"
            }).ToListAsync();

        var tratamientos = await _context.Tratamientos
            .Where(t => t.IdEstado == 1)
            .ToListAsync();

        ViewBag.Pacientes = new SelectList(pacientes, "IdPaciente", "Nombre");
        ViewBag.Tratamientos = new SelectList(tratamientos, "IdTratamiento", "NombreTratamiento");
        ViewBag.Estados = new SelectList(await _context.Estados.ToListAsync(), "IdEstado", "DescEstado");
    }

    // GET: Citas/BuscarPacientes
    [HttpGet]
    public async Task<IActionResult> BuscarPacientes(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Json(new List<object>());

        var query = _context.Pacientes
            .Include(p => p.Persona)
            .Where(p => p.IdEstado == 1);

        var terms = term.ToLower().Split(' ');
        
        foreach (var t in terms)
        {
            query = query.Where(p => 
                p.Persona != null && (
                    (p.Persona.PrimerNombre != null && p.Persona.PrimerNombre.ToLower().Contains(t)) ||
                    (p.Persona.PrimerApellido != null && p.Persona.PrimerApellido.ToLower().Contains(t)) ||
                    (p.Persona.NumIdentificacion != null && p.Persona.NumIdentificacion.ToLower().Contains(t))
                )
            );
        }

        var results = await query
            .Select(p => new {
                id = p.IdPaciente,
                label = $"{p.Persona.PrimerNombre} {p.Persona.PrimerApellido} - {p.Persona.NumIdentificacion}"
            })
            .Take(10)
            .ToListAsync();

        return Json(results);
    }
}
