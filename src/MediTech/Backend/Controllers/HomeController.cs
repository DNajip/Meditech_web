using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.AspNetCore.Authorization;

namespace MediTech.Backend.Controllers;

[Authorize]
public class HomeController(ILogger<HomeController> logger, MediTechContext context) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;
    private readonly MediTechContext _context = context;

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        ViewBag.TotalPacientes = await _context.Pacientes.CountAsync(p => p.IdEstado == 1);
        ViewBag.PacientesHoy = await _context.Pacientes.CountAsync(p => p.FechaRegistro.HasValue && p.FechaRegistro.Value.Date == today);

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

