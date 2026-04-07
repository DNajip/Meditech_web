using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;

namespace MediTech.Backend.Controllers;

[Authorize]
public class PacientesController(MediTechContext context) : Controller
{
    private readonly MediTechContext _context = context;

    // GET: /Pacientes
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        int pageSize = 15;

        var query = _context.Pacientes
            .Include(p => p.Persona)
            .Where(p => p.IdEstado == 1);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(p =>
                p.Persona!.PrimerNombre.Contains(search) ||
                p.Persona.PrimerApellido.Contains(search) ||
                p.Persona.NumIdentificacion.Contains(search) ||
                p.Persona.Telefono.Contains(search)
            );
        }

        var totalItems = await query.CountAsync();
        var pacientes = await query
            .OrderByDescending(p => p.FechaRegistro)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.TotalItems = totalItems;

        return View(pacientes);
    }

    // GET: /Pacientes/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var paciente = await _context.Pacientes
            .Include(p => p.Persona)
                .ThenInclude(per => per!.TipoIdentificacion)
            .Include(p => p.Persona)
                .ThenInclude(per => per!.Genero)
            .FirstOrDefaultAsync(p => p.IdPaciente == id);

        if (paciente == null) return NotFound();
        return View(paciente);
    }

    // GET: /Pacientes/Ficha/5
    public async Task<IActionResult> Ficha(int id, int? consultaId = null, string? modo = null)
    {
        var paciente = await _context.Pacientes
            .Include(p => p.Persona)
                .ThenInclude(per => per!.TipoIdentificacion)
            .Include(p => p.Persona)
                .ThenInclude(per => per!.Genero)
            .FirstOrDefaultAsync(p => p.IdPaciente == id);

        if (paciente == null) return NotFound();
        
        ViewBag.ConsultaId = consultaId;
        ViewBag.Modo = modo;

        // Si hay una consulta activa, cargar sus datos
        if (consultaId.HasValue)
        {
            var consultaActiva = await _context.Consultas
                .Include(c => c.SignosVitales)
                .Include(c => c.Diagnosticos)
                .Include(c => c.Cita!)
                    .ThenInclude(ci => ci.Tratamiento)
                .FirstOrDefaultAsync(c => c.IdConsulta == consultaId.Value);
            
            ViewBag.ConsultaActiva = consultaActiva;
        }

        return View(paciente);
    }

    // GET: /Pacientes/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync();
        ViewBag.Generos = await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync();
        return View();
    }

    // POST: /Pacientes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Paciente paciente)
    {
        if (paciente.Persona == null)
        {
            ModelState.AddModelError("", "Datos de persona requeridos.");
        }

        if (ModelState.IsValid && paciente.Persona != null)
        {
            // Logic Check: Age Validation (Min 15 years)
            var today = DateTime.Today;
            var birthDate = paciente.Persona.FechaNacimiento ?? DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;

            if (birthDate > today)
            {
                ModelState.AddModelError("Persona.FechaNacimiento", "La fecha de nacimiento no puede ser futura.");
            }
            else if (age < 15)
            {
                ModelState.AddModelError("Persona.FechaNacimiento", "El paciente debe tener al menos 15 años de edad.");
            }

            // Logic Check: Duplicate Identification
            string numIdent = paciente.Persona.NumIdentificacion?.Trim() ?? "";
            int? tipoIdent = paciente.Persona.IdTipoIdentificacion;

            var exists = await _context.Personas.AnyAsync(p => 
                p.IdTipoIdentificacion == tipoIdent &&
                p.NumIdentificacion == numIdent);

            if (exists)
            {
                ModelState.AddModelError("Persona.NumIdentificacion", "Ya existe una persona registrada con este número de identificación.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync();
            ViewBag.Generos = await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync();
            return View(paciente);
        }
        
        try 
        {
            paciente.Persona!.IdEstado = 1;
            paciente.Persona.FechaCreacion = DateTime.Now;
            paciente.FechaRegistro = DateTime.Now;
            paciente.IdEstado = 1;

            _context.Pacientes.Add(paciente);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Paciente registrado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            string errorMsg = "Ocurrió un error al guardar. Verifique si el paciente ya existe.";
            if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
            {
                errorMsg = "Ya existe una persona registrada con este número de identificación (Error de Base de Datos).";
            }
            
            ModelState.AddModelError("", errorMsg);
            ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync();
            ViewBag.Generos = await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync();
            return View(paciente);
        }
    }

    // GET: /Pacientes/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var paciente = await _context.Pacientes
            .Include(p => p.Persona)
            .FirstOrDefaultAsync(p => p.IdPaciente == id);

        if (paciente == null) return NotFound();
        
        ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync();
        ViewBag.Generos = await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync();
        
        return View(paciente);
    }

    // POST: /Pacientes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Paciente? paciente)
    {
        if (paciente == null) return NotFound();
        if (id != paciente.IdPaciente) return NotFound();

        if (paciente.Persona == null) return NotFound();

        if (ModelState.IsValid)
        {
            // Age Check
            var today = DateTime.Today;
            var birthDate = paciente.Persona.FechaNacimiento ?? DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;

            if (birthDate > today)
            {
                ModelState.AddModelError("Persona.FechaNacimiento", "La fecha de nacimiento no puede ser futura.");
            }
            else if (age < 15)
            {
                ModelState.AddModelError("Persona.FechaNacimiento", "El paciente debe tener al menos 15 años de edad.");
            }

            // Duplicate check for edit
            var existing = await _context.Pacientes
                .Include(p => p.Persona)
                .FirstOrDefaultAsync(p => p.IdPaciente == id);

            if (existing != null && existing.Persona != null)
            {
                string numIdent = paciente.Persona.NumIdentificacion?.Trim() ?? "";
                int? tipoIdent = paciente.Persona.IdTipoIdentificacion;

                var duplicate = await _context.Personas.AnyAsync(p => 
                    p.IdPersona != existing.IdPersona &&
                    p.IdTipoIdentificacion == tipoIdent &&
                    p.NumIdentificacion == numIdent);

                if (duplicate)
                {
                    ModelState.AddModelError("Persona.NumIdentificacion", "Ya existe otra persona registrada con este número de identificación.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync();
            ViewBag.Generos = await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync();
            return View(paciente);
        }

        var existingToUpdate = await _context.Pacientes
            .Include(p => p.Persona)
            .FirstOrDefaultAsync(p => p.IdPaciente == id);

        if (existingToUpdate == null || existingToUpdate.Persona == null) return NotFound();

        if (existingToUpdate != null && existingToUpdate.Persona != null && paciente.Persona != null)
        {
            existingToUpdate.Persona.PrimerNombre = paciente.Persona.PrimerNombre ?? string.Empty;
            existingToUpdate.Persona.SegundoNombre = paciente.Persona.SegundoNombre;
            existingToUpdate.Persona.PrimerApellido = paciente.Persona.PrimerApellido ?? string.Empty;
            existingToUpdate.Persona.SegundoApellido = paciente.Persona.SegundoApellido;
            existingToUpdate.Persona.IdTipoIdentificacion = paciente.Persona.IdTipoIdentificacion;
            existingToUpdate.Persona.NumIdentificacion = paciente.Persona.NumIdentificacion ?? string.Empty;
            existingToUpdate.Persona.IdGenero = paciente.Persona.IdGenero;
            existingToUpdate.Persona.FechaNacimiento = paciente.Persona.FechaNacimiento;
            existingToUpdate.Persona.Telefono = paciente.Persona.Telefono ?? string.Empty;
            existingToUpdate.Persona.Direccion = paciente.Persona.Direccion;
            existingToUpdate.Persona.Email = paciente.Persona.Email;
        }

        if (existingToUpdate != null)
        {
            existingToUpdate.Ocupacion = paciente.Ocupacion;
            existingToUpdate.ContactoEmergencia = paciente.ContactoEmergencia;
            existingToUpdate.TelefonoEmergencia = paciente.TelefonoEmergencia;

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Paciente actualizado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                string errorMsg = "Error al actualizar los datos.";
                if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
                {
                    errorMsg = "Ya existe otra persona registrada con este número de identificación (Error de Base de Datos).";
                }
                
                ModelState.AddModelError("", errorMsg);
                ViewBag.TiposIdentificacion = await _context.TiposIdentificacion.Where(t => t.IdEstado == 1).ToListAsync();
                ViewBag.Generos = await _context.Generos.Where(g => g.IdEstado == 1).ToListAsync();
                return View(paciente);
            }
        }
        
        return NotFound();
    }

    // GET: /Pacientes/GetHistorial/5
    [HttpGet]
    public async Task<IActionResult> GetHistorial(int id)
    {
        var historial = await _context.HistorialesClinicos
            .FirstOrDefaultAsync(h => h.IdPaciente == id);
        
        return Json(new { success = true, data = historial });
    }

    // POST: /Pacientes/GuardarHistorial
    [HttpPost]
    public async Task<IActionResult> GuardarHistorial([FromBody] HistorialClinico model)
    {
        if (model.IdPaciente == 0)
            return Json(new { success = false, message = "ID de paciente no válido." });

        var existing = await _context.HistorialesClinicos
            .FirstOrDefaultAsync(h => h.IdPaciente == model.IdPaciente);

        if (existing == null)
        {
            model.FechaRegistro = DateTime.Now;
            _context.HistorialesClinicos.Add(model);
        }
        else
        {
            // Update fields
            existing.Alergias = model.Alergias;
            existing.AlergiasDetalle = model.AlergiasDetalle;
            existing.Diabetes = model.Diabetes;
            existing.TomaMedicamento = model.TomaMedicamento;
            existing.MedicamentoDetalle = model.MedicamentoDetalle;
            existing.Hipertension = model.Hipertension;
            existing.Embarazada = model.Embarazada;
            existing.Cardiacos = model.Cardiacos;
            existing.AntecedenteOncologico = model.AntecedenteOncologico;
            existing.OtrosPadecimientos = model.OtrosPadecimientos;
            existing.OtrosPadecimientosDetalle = model.OtrosPadecimientosDetalle;
            existing.ConsumeAlcohol = model.ConsumeAlcohol;
            existing.FumaCigarrillos = model.FumaCigarrillos;
            existing.RealizaEjercicio = model.RealizaEjercicio;
            existing.CirugiasEsteticas = model.CirugiasEsteticas;
            existing.CirugiasEsteticasDetalle = model.CirugiasEsteticasDetalle;
            existing.FechaActualizacion = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

}

