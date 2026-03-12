using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;
using MediTech.Services;

namespace MediTech.Controllers;

[Authorize]
public class CitasController : Controller
{
    private readonly MediTechContext _context;
    private readonly IGoogleCalendarService _googleCalendar;
    private readonly ILogger<CitasController> _logger;

    public CitasController(MediTechContext context, IGoogleCalendarService googleCalendar, ILogger<CitasController> logger)
    {
        _context = context;
        _googleCalendar = googleCalendar;
        _logger = logger;
    }

    // GET: Citas
    public async Task<IActionResult> Index()
    {
        var citas = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.HoraInicio)
            .ToListAsync();
        return View(citas);
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

                    // Sync with Google Calendar
                    var paciente = await _context.Pacientes
                        .Include(p => p.Persona)
                        .FirstOrDefaultAsync(p => p.IdPaciente == cita.IdPaciente);
                    
                    var tratamiento = await _context.Tratamientos
                        .FirstOrDefaultAsync(t => t.IdTratamiento == cita.IdTratamiento);

                    var startDateTime = cita.Fecha.Date.Add(cita.HoraInicio);
                    var endDateTime = cita.Fecha.Date.Add(cita.HoraFin);

                    var googleEventId = await _googleCalendar.CreateEventAsync(
                        $"Consulta - {paciente?.Persona?.PrimerNombre} {paciente?.Persona?.PrimerApellido}",
                        $"Tratamiento: {tratamiento?.NombreTratamiento}. Obs: {cita.Observaciones}",
                        startDateTime,
                        endDateTime
                    );

                    if (!string.IsNullOrEmpty(googleEventId))
                    {
                        cita.GoogleEventId = googleEventId;
                        await _context.SaveChangesAsync();
                    }

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
    public async Task<IActionResult> Edit(int id, [Bind("IdCita,IdPaciente,IdTratamiento,Fecha,HoraInicio,HoraFin,Observaciones,GoogleEventId,IdEstado")] Cita? cita)
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

                    // Update Google Calendar
                    if (!string.IsNullOrEmpty(cita.GoogleEventId))
                    {
                        var paciente = await _context.Pacientes.Include(p => p.Persona).FirstOrDefaultAsync(p => p.IdPaciente == cita.IdPaciente);
                        var tratamiento = await _context.Tratamientos.FirstOrDefaultAsync(t => t.IdTratamiento == cita.IdTratamiento);

                        var startDateTime = cita.Fecha.Date.Add(cita.HoraInicio);
                        var endDateTime = cita.Fecha.Date.Add(cita.HoraFin);

                        await _googleCalendar.UpdateEventAsync(
                            cita.GoogleEventId,
                            $"Consulta - {paciente?.Persona?.PrimerNombre} {paciente?.Persona?.PrimerApellido} (Actualizada)",
                            $"Tratamiento: {tratamiento?.NombreTratamiento}. Obs: {cita.Observaciones}",
                            startDateTime, endDateTime
                        );
                    }

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

            if (!string.IsNullOrEmpty(cita.GoogleEventId))
            {
                await _googleCalendar.DeleteEventAsync(cita.GoogleEventId);
            }

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
            // Delete from Google Calendar
            if (!string.IsNullOrEmpty(cita.GoogleEventId))
            {
                await _googleCalendar.DeleteEventAsync(cita.GoogleEventId);
            }

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
}
