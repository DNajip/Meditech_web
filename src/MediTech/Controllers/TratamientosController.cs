using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;
using Microsoft.AspNetCore.Authorization;

namespace MediTech.Controllers;

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
    public async Task<IActionResult> Index()
    {
        var tratamientos = await _context.Tratamientos
            .Include(t => t.Estado)
            .OrderBy(t => t.NombreTratamiento)
            .ToListAsync();
        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;

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

        return View(tratamiento);
    }

    // GET: Tratamientos/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.IdEstado = new SelectList(await _context.Estados.ToListAsync(), "IdEstado", "DescEstado");
        return View();
    }

    // POST: Tratamientos/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Tratamiento tratamiento)
    {
        if (ModelState.IsValid)
        {
            _context.Add(tratamiento);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Tratamiento creado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.IdEstado = new SelectList(await _context.Estados.ToListAsync(), "IdEstado", "DescEstado", tratamiento.IdEstado);
        return View(tratamiento);
    }

    // GET: Tratamientos/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var tratamiento = await _context.Tratamientos.FindAsync(id);
        if (tratamiento == null) return NotFound();

        ViewBag.IdEstado = new SelectList(await _context.Estados.ToListAsync(), "IdEstado", "DescEstado", tratamiento.IdEstado);
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
                TempData["SuccessMessage"] = "Tratamiento actualizado correctamente.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TratamientoExists(tratamiento.IdTratamiento)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewBag.IdEstado = new SelectList(await _context.Estados.ToListAsync(), "IdEstado", "DescEstado", tratamiento.IdEstado);
        return View(tratamiento);
    }

    private bool TratamientoExists(int id)
    {
        return _context.Tratamientos.Any(e => e.IdTratamiento == id);
    }
}
