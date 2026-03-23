using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace MediTech.Controllers;

[Authorize(Roles = "ADMINISTRADOR")] 
public class ConfiguracionController : Controller
{
    private readonly MediTechContext _context;
    private readonly IMemoryCache _cache;
    private const string TasaCacheKey = "ActiveExchangeRate";

    public ConfiguracionController(MediTechContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var config = await _context.ConfiguracionesMoneda
            .Include(c => c.MonedaBase)
            .FirstOrDefaultAsync();

        var tasaActiva = await _context.TasasCambio
            .Include(t => t.MonedaOrigen)
            .Include(t => t.MonedaDestino)
            .FirstOrDefaultAsync(t => t.Activo);

        var historial = await _context.TasasCambio
            .OrderByDescending(t => t.Fecha)
            .Take(10)
            .ToListAsync();

        ViewBag.Monedas = await _context.Monedas.ToListAsync();
        ViewBag.TasaActiva = tasaActiva;
        ViewBag.Historial = historial;

        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarTasa(decimal valor)
    {
        if (valor <= 0)
        {
            TempData["Error"] = "La tasa debe ser mayor a cero.";
            return RedirectToAction(nameof(Index));
        }

        var tasaActual = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
        if (tasaActual != null && tasaActual.Valor == valor)
        {
            TempData["Info"] = "El valor es idéntico a la tasa actual.";
            return RedirectToAction(nameof(Index));
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Desactivar tasas anteriores
            var activas = await _context.TasasCambio.Where(t => t.Activo).ToListAsync();
            foreach (var t in activas) t.Activo = false;

            var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
            if (config == null) throw new Exception("Configuración de moneda no encontrada.");

            var monedaBaseId = config.IdMonedaBase;
            var monedaDestino = await _context.Monedas.FirstOrDefaultAsync(m => m.IdMoneda != monedaBaseId);

            if (monedaDestino == null) throw new Exception("No hay una moneda secundaria configurada para la tasa de cambio.");

            var nuevaTasa = new TasaCambio
            {
                IdMonedaOrigen = monedaBaseId,
                IdMonedaDestino = monedaDestino.IdMoneda,
                Valor = valor,
                Activo = true,
                Fecha = DateTime.Now,
                UsuarioModificacion = User.Identity?.Name ?? "SISTEMA"
            };

            _context.TasasCambio.Add(nuevaTasa);
            
            // También actualizar el campo legacy en configuracion por si acaso se usa en algun lugar aun no refactorizado
            config.TasaCambio = valor;
            config.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Invalidar caché
            _cache.Remove(TasaCacheKey);

            TempData["Success"] = "Tasa de cambio actualizada exitosamente.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Error al actualizar tasa: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarMonedaBase(int idMonedaBase)
    {
        var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
        
        // Restricción Crítica: No se puede cambiar si existen datos registrados
        bool tieneDatos = await _context.Productos.AnyAsync() || 
                          await _context.Cuentas.AnyAsync() || 
                          await _context.Pagos.AnyAsync();

        if (tieneDatos)
        {
            TempData["Error"] = "OPERACIÓN BLOQUEADA: No se puede cambiar la moneda base porque ya existen productos, cuentas o pagos registrados en el sistema.";
            return RedirectToAction(nameof(Index));
        }

        if (config == null)
        {
            config = new ConfiguracionMoneda { IdMonedaBase = idMonedaBase };
            _context.ConfiguracionesMoneda.Add(config);
        }
        else
        {
            config.IdMonedaBase = idMonedaBase;
            config.FechaActualizacion = DateTime.Now;
            config.UsuarioModificacion = User.Identity?.Name ?? "SISTEMA";
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Moneda base actualizada exitosamente.";
        return RedirectToAction(nameof(Index));
    }
}
