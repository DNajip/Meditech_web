using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
namespace MediTech.Backend.Controllers;

[Authorize]
public class CitasController(MediTechContext context, ILogger<CitasController> logger) : Controller
{
    private readonly MediTechContext _context = context;
    private readonly ILogger<CitasController> _logger = logger;

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
            .Include(c => c.PosiblePaciente)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .Where(c => c.Fecha >= start.Date && c.Fecha <= end.Date)
            .ToListAsync();

        var events = citas.Select(c => new
        {
            id = c.IdCita,
            title = c.IdPaciente != null 
                ? $"{c.Paciente?.Persona?.PrimerNombre ?? "S/N"} {c.Paciente?.Persona?.PrimerApellido ?? ""}".Trim()
                : $"{c.PosiblePaciente?.PrimerNombre ?? "S/N"} {c.PosiblePaciente?.PrimerApellido ?? ""}".Trim(),
            start = c.Fecha.Add(c.HoraInicio),
            end = c.Fecha.Add(c.HoraFin),
            color = c.IdEstadoCita == 4 ? "#E1E4E8" : (c.IdPaciente == null ? "#F59E0B" : (c.IdEstadoCita == 2 ? "#10B981" : "#3B82F6")), // Gray for canceled (4)
            extendedProps = new
            {
                pacienteId = c.IdPaciente,
                posiblePacienteId = c.IdPosiblePaciente,
                tratamiento = c.Tratamiento?.NombreTratamiento ?? "Consulta General",
                observaciones = c.Observaciones ?? "",
                estadoId = c.IdEstadoCita,
                telefono = c.Telefono,
                isProspect = c.IdPaciente == null
            }
        });

        return Json(events);
    }

    // GET: Citas/Hoy — JSON for "Agenda de Hoy" offcanvas panel
    [HttpGet]
    public async Task<IActionResult> Hoy()
    {
        var hoy = DateTime.Today;
        var citas = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.PosiblePaciente)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .Where(c => c.Fecha == hoy)
            .OrderBy(c => c.HoraInicio)
            .ToListAsync();

        var result = citas.Select(c => new
        {
            id = c.IdCita,
            paciente = c.IdPaciente != null 
                ? $"{c.Paciente?.Persona?.PrimerNombre ?? "S/N"} {c.Paciente?.Persona?.PrimerApellido ?? ""}".Trim()
                : $"{c.PosiblePaciente?.PrimerNombre ?? "S/N"} {c.PosiblePaciente?.PrimerApellido ?? ""}".Trim(),
            horaInicio = c.HoraInicio.ToString(@"hh\:mm"),
            horaFin = c.HoraFin.ToString(@"hh\:mm"),
            telefono = c.Telefono,
            tratamiento = c.Tratamiento?.NombreTratamiento ?? "Consulta General",
            estadoId = c.IdEstadoCita
        });

        return Json(result);
    }
    public async Task<IActionResult> GetTodayAgenda()
    {
        var today = DateTime.Today;
        var citas = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.PosiblePaciente)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .Where(c => c.Fecha == today && c.IdEstadoCita == 1)
            .OrderBy(c => c.HoraInicio)
            .ToListAsync();

        var agenda = citas.Select(c => new
        {
            id = c.IdCita,
            horaInicio = DateTime.Today.Add(c.HoraInicio).ToString("hh:mm tt"),
            horaFin = DateTime.Today.Add(c.HoraFin).ToString("hh:mm tt"),
            paciente = c.IdPaciente != null 
                ? $"{c.Paciente?.Persona?.PrimerNombre ?? "S/N"} {c.Paciente?.Persona?.PrimerApellido ?? ""}".Trim()
                : $"{c.PosiblePaciente?.PrimerNombre ?? "S/N"} {c.PosiblePaciente?.PrimerApellido ?? ""}".Trim(),
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
                (p.Persona!.PrimerNombre.Contains(term) ||
                 p.Persona.PrimerApellido.Contains(term) ||
                 p.Persona.NumIdentificacion.Contains(term)))
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
            .Where(c => c.Paciente!.Persona!.PrimerNombre.Contains(term) ||
                        c.Paciente.Persona!.PrimerApellido.Contains(term) ||
                        c.Paciente.Persona!.NumIdentificacion.Contains(term))
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
            .Include(c => c.PosiblePaciente)
            .Include(c => c.Tratamiento)
            .Include(c => c.Estado)
            .FirstOrDefaultAsync(m => m.IdCita == id);

        if (cita == null) return NotFound();

        return Json(new
        {
            id = cita.IdCita,
            paciente = cita.IdPaciente != null 
                ? $"{cita.Paciente?.Persona?.PrimerNombre ?? "S/N"} {cita.Paciente?.Persona?.PrimerApellido ?? ""}".Trim()
                : $"{cita.PosiblePaciente?.PrimerNombre ?? "S/N"} {cita.PosiblePaciente?.PrimerApellido ?? ""}".Trim(),
            pacienteId = cita.IdPaciente,
            posiblePacienteId = cita.IdPosiblePaciente,
            identificacion = cita.Paciente?.Persona?.NumIdentificacion ?? "",
            tratamiento = cita.Tratamiento?.NombreTratamiento ?? "General",
            color = "#3B82F6",
            fecha = cita.Fecha.ToString("dd/MM/yyyy"),
            horaInicio = DateTime.Today.Add(cita.HoraInicio).ToString("hh:mm tt"),
            horaFin = DateTime.Today.Add(cita.HoraFin).ToString("hh:mm tt"),
            observaciones = cita.Observaciones ?? "",
            estado = cita.EstadoCita?.DescEstadoCita ?? "ACTIVO",
            estadoId = cita.IdEstadoCita,
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateJson(int? PacienteId, int? PosiblePacienteId, int IdTratamiento, DateTime Fecha, string HoraInicio, string HoraFin, string Observaciones, string Telefono)
    {
        if (PacienteId == null && PosiblePacienteId == null)
            return Json(new { success = false, message = "Debe seleccionar un paciente o un prospecto." });
            
        if (string.IsNullOrEmpty(Telefono))
            return Json(new { success = false, message = "El teléfono celular es obligatorio." });
 
        if (!TimeSpan.TryParse(HoraInicio, out TimeSpan hInicio))
            return Json(new { success = false, message = "Formato de hora de inicio inválido." });
        if (!TimeSpan.TryParse(HoraFin, out TimeSpan hFin))
            return Json(new { success = false, message = "Formato de hora de fin inválido." });

        var cita = new Cita
        {
            IdPaciente = PacienteId,
            IdPosiblePaciente = PosiblePacienteId,
            IdTratamiento = IdTratamiento,
            Fecha = Fecha,
            HoraInicio = hInicio,
            HoraFin = hFin,
            Observaciones = Observaciones,
            Telefono = Telefono,
            IdEstadoCita = 1, // 1: Programada
            IdEstado = 1,     // 1: Activo
            FechaCreacion = DateTime.Now
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
            var innerMsg = ex.InnerException != null ? " | " + ex.InnerException.Message : "";
            return Json(new { success = false, message = "Error al guardar en la base de datos: " + ex.Message + innerMsg });
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
            cita.IdEstadoCita = 2; // Atendida / En Sala
            _context.Update(cita);
            await _context.SaveChangesAsync();
            return Json(new { success = true, idCita = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing appointment status.");
            return Json(new { success = false, message = "No se pudo actualizar el estado de la cita." });
        }
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
            cita.IdEstadoCita = 4; // 4: Cancelada
            _context.Update(cita);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cita cancelada correctamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling appointment.");
            return Json(new { success = false, message = "No se pudo cancelar la cita." });
        }
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
        ViewBag.TiposIdentificacion = new SelectList(await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync(), "IdTipoIdentificacion", "DescTipo");
        ViewBag.Generos = new SelectList(await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync(), "IdGenero", "DescGenero");
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
                    (p.Persona.PrimerNombre != null && p.Persona.PrimerNombre.Contains(t)) ||
                    (p.Persona.PrimerApellido != null && p.Persona.PrimerApellido.Contains(t)) ||
                    (p.Persona.NumIdentificacion != null && p.Persona.NumIdentificacion.Contains(t))
                )
            );
        }

        var patients = await query
            .Select(p => new {
                id = p.IdPaciente,
                label = (p.Persona!.PrimerNombre ?? "S/N") + " " + (p.Persona.PrimerApellido ?? "") + " - " + (p.Persona.NumIdentificacion ?? "S/I"),
                isProspect = false,
                telefono = p.Persona.Telefono
            })
            .Take(10)
            .ToListAsync();

        var normalizedTermForProspect = term.ToLower();
        var prospects = await _context.PosiblePacientes
            .Where(p => p.IdEstado == 1 && (
                p.PrimerNombre.Contains(term) || 
                p.PrimerApellido.Contains(term) || 
                p.Telefono.Contains(term)))
            .Select(p => new {
                id = p.IdPosiblePaciente,
                label = "[PROSPECTO] " + p.PrimerNombre + " " + p.PrimerApellido + " - " + p.Telefono,
                isProspect = true,
                telefono = p.Telefono
            })
            .Take(5)
            .ToListAsync();

        var results = patients.Cast<object>().Concat(prospects.Cast<object>());

        return Json(results);
    }

    // POST: Citas/CreateProspectoJson
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProspectoJson(string primerNombre, string primerApellido, string telefono, string? segundoNombre = null, string? segundoApellido = null)
    {
        if (string.IsNullOrWhiteSpace(primerNombre) || string.IsNullOrWhiteSpace(primerApellido) || string.IsNullOrWhiteSpace(telefono))
            return Json(new { success = false, message = "Nombre, Apellido y Teléfono son obligatorios." });

        try
        {
            var prospecto = new PosiblePaciente
            {
                PrimerNombre = primerNombre.ToUpper(),
                PrimerApellido = primerApellido.ToUpper(),
                SegundoNombre = segundoNombre?.ToUpper(),
                SegundoApellido = segundoApellido?.ToUpper(),
                Telefono = telefono,
                IdEstado = 1
            };

            _context.PosiblePacientes.Add(prospecto);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = prospecto.IdPosiblePaciente, label = "[NUEVO] " + prospecto.PrimerNombre + " " + prospecto.PrimerApellido });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creardo prospecto.");
            return Json(new { success = false, message = "Error al guardar el prospecto." });
        }
    }

    // POST: Citas/ConvertirProspecto
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertirProspecto(int idCita, int idPosiblePaciente, int idTipoIdentificacion, string numIdentificacion, int idGenero, DateTime fechaNacimiento, string? direccion, string? contactoEmergencia, string? telefonoEmergencia)
    {
        // Validación de rango de fecha para evitar SqlDateTime overflow
        if (fechaNacimiento < new DateTime(1753, 1, 1) || fechaNacimiento > new DateTime(9999, 12, 31))
        {
            return Json(new { success = false, message = "La fecha de nacimiento no es válida para el sistema." });
        }

        try
        {
            var rawSql = "EXEC ADM.SP_CONVERTIR_POSIBLE_A_PACIENTE @IdPosiblePaciente, @IdCita, @IdTipoIdentificacion, @NumIdentificacion, @IdGenero, @FechaNacimiento, @Email, @Direccion, @ContactoEmergencia, @TelefonoEmergencia";
            
            var parameters = new[] {
                new Microsoft.Data.SqlClient.SqlParameter("@IdPosiblePaciente", idPosiblePaciente),
                new Microsoft.Data.SqlClient.SqlParameter("@IdCita", idCita),
                new Microsoft.Data.SqlClient.SqlParameter("@IdTipoIdentificacion", idTipoIdentificacion),
                new Microsoft.Data.SqlClient.SqlParameter("@NumIdentificacion", numIdentificacion),
                new Microsoft.Data.SqlClient.SqlParameter("@IdGenero", idGenero),
                new Microsoft.Data.SqlClient.SqlParameter("@FechaNacimiento", fechaNacimiento),
                new Microsoft.Data.SqlClient.SqlParameter("@Email", DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@Direccion", (object?)direccion ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@ContactoEmergencia", (object?)contactoEmergencia ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@TelefonoEmergencia", (object?)telefonoEmergencia ?? DBNull.Value)
            };

            await _context.Database.ExecuteSqlRawAsync(rawSql, parameters);

            // Asegurar que el estado de la cita cambie a Atendida (2) para el flujo de consulta
            var cita = await _context.Citas.FindAsync(idCita);
            if (cita != null)
            {
                cita.IdEstadoCita = 2; // Atendida / En Sala
                _context.Update(cita);
                await _context.SaveChangesAsync();
            }
            
            return Json(new { success = true, idCita = idCita, message = "Paciente convertido y registrado correctamente. Redirigiendo a consulta..." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al convertir prospecto.");
            return Json(new { success = false, message = "Error durante la conversión: " + ex.Message });
        }
    }
    // GET: Citas/BuscarTratamientos
    [HttpGet]
    public async Task<IActionResult> BuscarTratamientos(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Json(new List<object>());

        var query = _context.Tratamientos
            .Where(t => t.IdEstado == 1 && t.NombreTratamiento != null && t.NombreTratamiento.Contains(term));

        var results = await query
            .Select(t => new {
                id = t.IdTratamiento,
                nombre = t.NombreTratamiento
            })
            .Take(10)
            .ToListAsync();

        return Json(results);
    }

    // GET: Citas/BuscarPacientePorTelefono
    [HttpGet]
    public async Task<IActionResult> BuscarPacientePorTelefono(string telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return Json(new { success = false });

        var paciente = await _context.Pacientes
            .Include(p => p.Persona)
            .FirstOrDefaultAsync(p => p.IdEstado == 1 && p.Persona!.Telefono == telefono);

        if (paciente != null)
        {
            return Json(new { 
                success = true, 
                id = paciente.IdPaciente, 
                nombre = $"{paciente.Persona!.PrimerNombre} {paciente.Persona.PrimerApellido}",
                isProspect = false
            });
        }

        var prospecto = await _context.PosiblePacientes
            .FirstOrDefaultAsync(p => p.IdEstado == 1 && p.Telefono == telefono);

        if (prospecto != null)
        {
            return Json(new { 
                success = true, 
                id = prospecto.IdPosiblePaciente, 
                nombre = $"{prospecto.PrimerNombre} {prospecto.PrimerApellido}",
                isProspect = true
            });
        }

        return Json(new { success = false });
    }

    // GET: Citas/GetPacienteCitas/5 — JSON for patient folder history
    [HttpGet("Citas/GetPacienteCitas/{id}")]
    public async Task<IActionResult> GetPacienteCitas(int id)
    {
        var citas = await _context.Citas
            .Include(c => c.Tratamiento)
            .Include(c => c.EstadoCita)
            .Where(c => c.IdPaciente == id && c.IdEstado == 1) // Only active/enabled appointments
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.HoraInicio)
            .ToListAsync();

        var result = citas.Select(c => new
        {
            id = c.IdCita,
            fecha = c.Fecha.ToString("dd/MM/yyyy"),
            hora = DateTime.Today.Add(c.HoraInicio).ToString("hh:mm tt"),
            tratamiento = c.Tratamiento?.NombreTratamiento ?? "Consulta General",
            estado = c.EstadoCita?.DescEstadoCita ?? "Programada",
            estadoId = c.IdEstadoCita
        });

        return Json(new { success = true, data = result });
    }

    // GET: Citas/GetRecepcionData/5 — JSON data for the triage modal
    [HttpGet]
    public async Task<IActionResult> GetRecepcionData(int id)
    {
        var cita = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Tratamiento)
            .FirstOrDefaultAsync(m => m.IdCita == id);

        if (cita == null)
            return Json(new { success = false, error = "Cita no encontrada." });

        // Obtener usuario logueado (resiliente a reinicio de servidor)
        Usuario? usuario = null;
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdStr, out int userId))
        {
            usuario = await _context.Usuarios
                .Include(u => u.Empleado)
                .FirstOrDefaultAsync(u => u.IdUsuario == userId);
        }

        // Fallback: buscar por nombre de usuario si el ID no coincide (sesión obsoleta)
        if (usuario == null)
        {
            var allNames = User.FindAll(System.Security.Claims.ClaimTypes.Name).Select(c => c.Value).ToList();
            foreach (var name in allNames)
            {
                usuario = await _context.Usuarios
                    .Include(u => u.Empleado)
                    .FirstOrDefaultAsync(u => u.Username == name);
                if (usuario != null) break;
            }
        }

        if (usuario == null)
            return Json(new { success = false, error = "Sesión expirada. Por favor cierre sesión e ingrese de nuevo." });

        // Admin: si no tiene empleado, buscar cualquier empleado activo como fallback
        int idMedico;
        if (usuario.IdEmpleado != null)
        {
            idMedico = usuario.IdEmpleado.Value;
        }
        else
        {
            var fallbackEmpleado = await _context.Empleados.FirstOrDefaultAsync(e => e.IdEstado == 1);
            idMedico = fallbackEmpleado?.IdEmpleado ?? 1;
        }

        // Buscar consulta existente EN_PROCESO
        var estadoEnProceso = await _context.Estados.FirstOrDefaultAsync(e => e.DescEstado == "EN PROCESO");
        var consultaExistente = await _context.Consultas
            .Include(c => c.SignosVitales)
            .FirstOrDefaultAsync(c => c.IdCita == id && c.IdEstado == (estadoEnProceso != null ? estadoEnProceso.IdEstado : 0));

        int idConsulta = 0;
        SignosVitales? signosExistentes = null;
        string? motivoExistente = null, diagnosticoExistente = null, observacionesExistentes = null;

        if (consultaExistente != null)
        {
            idConsulta = consultaExistente.IdConsulta;
            signosExistentes = consultaExistente.SignosVitales;
            motivoExistente = consultaExistente.MotivoConsulta;
            diagnosticoExistente = consultaExistente.DiagnosticoPrincipal;
            observacionesExistentes = consultaExistente.Observaciones;
        }

        var patientName = cita.IdPaciente != null
            ? $"{cita.Paciente?.Persona?.PrimerNombre} {cita.Paciente?.Persona?.PrimerApellido}"
            : "Paciente";
        var fechaHora = $"{cita.Fecha:dd/MM/yyyy} - {DateTime.Today.Add(cita.HoraInicio):hh:mm tt}";

        return Json(new
        {
            success = true,
            idCita = cita.IdCita,
            idMedico,
            idEstado = estadoEnProceso?.IdEstado ?? 1,
            idConsulta,
            pacienteNombre = patientName,
            fechaHora,
            tratamiento = cita.Tratamiento?.NombreTratamiento ?? "Consulta General",
            observacionesCita = cita.Observaciones,
            signos = signosExistentes != null ? new
            {
                presionArterial = signosExistentes.PresionArterial,
                temperatura = signosExistentes.Temperatura,
                frecuenciaCardiaca = signosExistentes.FrecuenciaCardiaca,
                saturacionOxigeno = signosExistentes.SaturacionOxigeno,
                peso = signosExistentes.Peso,
                altura = signosExistentes.Altura
            } : null,
            motivo = motivoExistente,
            diagnostico = diagnosticoExistente,
            observaciones = observacionesExistentes
        });
    }

    // POST: Citas/GuardarRecepcion — Save triage data via AJAX
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarRecepcion(
        [FromForm] int IdCita, [FromForm] int IdMedico, [FromForm] int IdEstado, [FromForm] int IdConsulta,
        [FromForm] string? MotivoConsulta, [FromForm] string? DiagnosticoPrincipal, [FromForm] string? Observaciones,
        [Bind(Prefix = "signos")] SignosVitales signos)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            Consulta consulta;

            if (IdConsulta > 0)
            {
                // Actualizar consulta existente
                consulta = await _context.Consultas.Include(c => c.SignosVitales)
                    .FirstAsync(c => c.IdConsulta == IdConsulta);
                consulta.MotivoConsulta = MotivoConsulta;
                consulta.DiagnosticoPrincipal = DiagnosticoPrincipal;
                consulta.Observaciones = Observaciones;
                _context.Update(consulta);
            }
            else
            {
                // Crear nueva consulta
                consulta = new Consulta
                {
                    IdCita = IdCita,
                    IdMedico = IdMedico,
                    IdEstado = IdEstado,
                    FechaConsulta = DateTime.Now,
                    MotivoConsulta = MotivoConsulta,
                    DiagnosticoPrincipal = DiagnosticoPrincipal,
                    Observaciones = Observaciones
                };
                _context.Consultas.Add(consulta);
                await _context.SaveChangesAsync();
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
            var cita = await _context.Citas.FindAsync(IdCita);
            return Json(new
            {
                success = true,
                idPaciente = cita?.IdPaciente,
                idConsulta = consulta.IdConsulta
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Json(new { success = false, error = "Error al guardar: " + ex.Message });
        }
    }
}

