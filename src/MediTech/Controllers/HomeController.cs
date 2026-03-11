using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;
using Microsoft.AspNetCore.Authorization;

namespace MediTech.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MediTechContext _context;

    public HomeController(ILogger<HomeController> logger, MediTechContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        ViewBag.TotalPacientes = await _context.Pacientes.CountAsync(p => p.Estado == true);
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
