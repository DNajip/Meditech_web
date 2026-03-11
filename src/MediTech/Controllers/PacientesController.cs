using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;

namespace MediTech.Controllers;

[Authorize]
public class PacientesController : Controller
{
    private readonly MediTechContext _context;

    public PacientesController(MediTechContext context)
    {
        _context = context;
    }

    // GET: /Pacientes
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Pacientes.Where(p => p.Estado == true);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(p =>
                (p.PrimerNombre != null && p.PrimerNombre.ToLower().Contains(search)) ||
                (p.PrimerApellido != null && p.PrimerApellido.ToLower().Contains(search)) ||
                (p.NumeroIdentificacion != null && p.NumeroIdentificacion.Contains(search)) ||
                (p.Telefono != null && p.Telefono.Contains(search))
            );
        }

        var pacientes = await query.OrderByDescending(p => p.FechaRegistro).ToListAsync();
        ViewBag.Search = search;
        return View(pacientes);
    }

    // GET: /Pacientes/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == id);
        if (paciente == null) return NotFound();
        return View(paciente);
    }

    // GET: /Pacientes/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Pacientes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Paciente paciente)
    {
        if (!ModelState.IsValid)
        {
            return View(paciente);
        }

        paciente.FechaRegistro = DateTime.Now;
        paciente.Estado = true;

        _context.Pacientes.Add(paciente);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Paciente registrado exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Pacientes/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == id);
        if (paciente == null) return NotFound();
        return View(paciente);
    }

    // POST: /Pacientes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Paciente paciente)
    {
        if (id != paciente.IdPaciente) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(paciente);
        }

        var existing = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == id);
        if (existing == null) return NotFound();

        existing.PrimerNombre = paciente.PrimerNombre;
        existing.SegundoNombre = paciente.SegundoNombre;
        existing.PrimerApellido = paciente.PrimerApellido;
        existing.SegundoApellido = paciente.SegundoApellido;
        existing.NumeroIdentificacion = paciente.NumeroIdentificacion;
        existing.Sexo = paciente.Sexo;
        existing.FechaNacimiento = paciente.FechaNacimiento;
        existing.Telefono = paciente.Telefono;
        existing.Direccion = paciente.Direccion;
        existing.Ocupacion = paciente.Ocupacion;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Paciente actualizado exitosamente.";
        return RedirectToAction(nameof(Index));
    }
}
