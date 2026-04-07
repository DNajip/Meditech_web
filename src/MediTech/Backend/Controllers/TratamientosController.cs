using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.AspNetCore.Authorization;

namespace MediTech.Backend.Controllers;

[Authorize]
public class TratamientosController : Controller
{
    private readonly MediTechContext _context;
    private readonly ILogger<TratamientosController> _logger;

    public TratamientosController(MediTechContext context, ILogger<TratamientosController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Tratamientos
    public async Task<IActionResult> Index(int page = 1)
    {
        int pageSize = 15;
        var totalItems = await _context.Tratamientos.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var tratamientos = await _context.Tratamientos
            .Include(t => t.Estado)
            .OrderByDescending(t => t.IdTratamiento)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.PageSize = pageSize;

        // Modal context for creation - Only ACTIVO and INACTIVO
        ViewBag.IdEstado = new SelectList(await GetFilteredEstadosAsync(), "IdEstado", "DescEstado");

        return View(tratamientos);
    }

    // GET: Tratamientos/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var tratamiento = await _context.Tratamientos
            .Include(t => t.Estado)
            .FirstOrDefaultAsync(m => m.IdTratamiento == id);

        if (tratamiento == null) return NotFound();

        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;

        return PartialView("_DetailsPartial", tratamiento);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.IdEstado = new SelectList(await GetFilteredEstadosAsync(), "IdEstado", "DescEstado");
        return View();
    }

    // POST: Tratamientos/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Tratamiento tratamiento)
    {
        if (ModelState.IsValid)
        {
            try 
            {
                _context.Add(tratamiento);
                await _context.SaveChangesAsync();
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Tratamiento creado correctamente." });
                }

                TempData["SuccessMessage"] = "Tratamiento creado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear tratamiento.");
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error en la base de datos.", errors = new List<string> { ex.InnerException?.Message ?? ex.Message } });
                }
                
                ModelState.AddModelError("", "Ocurrió un error al guardar en la base de datos.");
            }
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => !string.IsNullOrEmpty(e.ErrorMessage) ? e.ErrorMessage : e.Exception?.Message ?? "Error desconocido")
                .ToList();
                
            return Json(new { success = false, message = "Datos inválidos.", errors });
        }

        ViewBag.IdEstado = new SelectList(await GetFilteredEstadosAsync(), "IdEstado", "DescEstado", tratamiento.IdEstado);
        return View(tratamiento);
    }

    // GET: Tratamientos/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var tratamiento = await _context.Tratamientos.FindAsync(id);
        if (tratamiento == null) return NotFound();

        ViewBag.IdEstado = new SelectList(await GetFilteredEstadosAsync(), "IdEstado", "DescEstado", tratamiento.IdEstado);
        return View(tratamiento);
    }

    // POST: Tratamientos/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Tratamiento tratamiento)
    {
        if (id != tratamiento.IdTratamiento) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                tratamiento.FechaActualizacion = DateTime.Now;
                _context.Update(tratamiento);
                await _context.SaveChangesAsync();
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Tratamiento actualizado correctamente." });
                }

                TempData["SuccessMessage"] = "Tratamiento actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TratamientoExists(tratamiento.IdTratamiento)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al actualizar tratamiento {id}.");
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error en la base de datos.", errors = new List<string> { ex.InnerException?.Message ?? ex.Message } });
                }
                
                ModelState.AddModelError("", "Ocurrió un error al actualizar en la base de datos.");
            }
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => !string.IsNullOrEmpty(e.ErrorMessage) ? e.ErrorMessage : e.Exception?.Message ?? "Error desconocido")
                .ToList();
                
            return Json(new { success = false, message = "Datos inválidos.", errors });
        }

        ViewBag.IdEstado = new SelectList(await GetFilteredEstadosAsync(), "IdEstado", "DescEstado", tratamiento.IdEstado);
        return View(tratamiento);
    }

    private async Task<List<Estado>> GetFilteredEstadosAsync()
    {
        return await _context.Estados
            .Where(e => e.DescEstado.Trim().ToUpper() == "ACTIVO" || e.DescEstado.Trim().ToUpper() == "INACTIVO")
            .ToListAsync();
    }

    private bool TratamientoExists(int id)
    {
        return _context.Tratamientos.Any(e => e.IdTratamiento == id);
    }
}

