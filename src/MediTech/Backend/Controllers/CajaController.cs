using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MediTech.Backend.Controllers;

public class CajaController : Controller
{
    private readonly MediTechContext _context;
    private readonly IMemoryCache _cache;
    private const string TasaCacheKey = "ActiveExchangeRate";

    public CajaController(MediTechContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // GET: Caja
    public async Task<IActionResult> Index(string? estado, DateTime? fecha, int page = 1)
    {
        int pageSize = 10;

        var query = _context.Cuentas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.MonedaBase)
            .Include(c => c.Pagos)
            .AsQueryable();

        // Filtro por fecha
        if (fecha.HasValue)
        {
            query = query.Where(c => c.FechaCreacion.Date == fecha.Value.Date);
        }

        // Estadísticas globales
        var allCuentas = await query.ToListAsync();
        
        var hoy = DateTime.Today;
        var cuentasHoy = allCuentas.Where(c => c.FechaCreacion.Date == hoy).ToList();
        ViewBag.IngresosHoy = cuentasHoy.Sum(c => c.Pagos.Sum(p => p.Monto ?? 0));
        ViewBag.CuentasHoy = cuentasHoy.Count;
        ViewBag.CuentasPendientes = allCuentas.Count(c => c.Pagos.Sum(p => p.Monto ?? 0) < (c.TotalFinal ?? 0));
        ViewBag.TotalRecaudado = allCuentas.Sum(c => c.Pagos.Sum(p => p.Monto ?? 0));

        // Filtro por estado
        if (!string.IsNullOrEmpty(estado))
        {
            if (estado == "PENDIENTE")
                allCuentas = allCuentas.Where(c => c.Pagos.Sum(p => p.Monto ?? 0) < (c.TotalFinal ?? 0) && (c.TotalFinal ?? 0) > 0).ToList();
            else if (estado == "PAGADA")
                allCuentas = allCuentas.Where(c => c.Pagos.Sum(p => p.Monto ?? 0) >= (c.TotalFinal ?? 0) && (c.TotalFinal ?? 0) > 0).ToList();
        }

        var totalItems = allCuentas.Count;
        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;

        var cuentas = allCuentas
            .OrderByDescending(c => c.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.EstadoFilter = estado;
        ViewBag.FechaFilter = fecha?.ToString("yyyy-MM-dd");

        return View(cuentas);
    }

    // GET: Caja/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Pacientes = await _context.Pacientes
            .Include(p => p.Persona)
            .Where(p => p.IdEstado == 1)
            .OrderBy(p => p.Persona!.PrimerApellido)
            .ToListAsync();

        ViewBag.Tratamientos = await _context.Tratamientos
            .Where(t => t.IdEstado == 1)
            .OrderBy(t => t.NombreTratamiento)
            .ToListAsync();

        ViewBag.Productos = await _context.Productos
            .Where(p => p.Activo && p.Stock > 0)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;
        ViewBag.Monedas = await _context.Monedas.ToListAsync();

        return View();
    }

    // POST: Caja/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int idPaciente,
        int? idMonedaBase,
        decimal descuento,
        string[] tipoItem,
        int[] idReferencia,
        string[] descripcionItem,
        int[] cantidad,
        decimal[] precioUnitario)
    {
        if (tipoItem.Length == 0)
        {
            TempData["Error"] = "Debe agregar al menos un ítem a la cuenta.";
            return RedirectToAction(nameof(Create));
        }

        decimal totalBruto = 0;
        var detalles = new List<CuentaDetalle>();

        for (int i = 0; i < tipoItem.Length; i++)
        {
            var subtotal = precioUnitario[i] * cantidad[i];
            totalBruto += subtotal;

            detalles.Add(new CuentaDetalle
            {
                TipoItem = tipoItem[i],
                IdReferencia = idReferencia[i],
                Descripcion = descripcionItem[i],
                Cantidad = cantidad[i],
                PrecioUnitario = precioUnitario[i],
                Subtotal = subtotal
            });
        }

        var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
        if (config == null) throw new Exception("Configuración de moneda no encontrada.");

        var cuenta = new Cuenta
        {
            IdPaciente = idPaciente,
            IdMonedaBase = config.IdMonedaBase,
            TotalBruto = totalBruto,
            Descuento = descuento,
            TotalFinal = totalBruto - descuento,
            FechaCreacion = DateTime.Now
        };

        _context.Cuentas.Add(cuenta);
        await _context.SaveChangesAsync();

        // Asignar IdCuenta a cada detalle y guardar
        foreach (var d in detalles)
        {
            d.IdCuenta = cuenta.IdCuenta;
        }
        _context.CuentaDetalles.AddRange(detalles);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Cuenta creada exitosamente.";
        return RedirectToAction(nameof(Details), new { id = cuenta.IdCuenta });
    }

    // GET: Caja/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var cuenta = await _context.Cuentas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.Consulta)
            .Include(c => c.MonedaBase)
            .Include(c => c.Detalles)
            .Include(c => c.Pagos).ThenInclude(p => p.Moneda)
            .FirstOrDefaultAsync(c => c.IdCuenta == id);

        if (cuenta == null) return NotFound();

        // Moneda Base: Si la cuenta no la tiene (legacy), usar la global
        if (cuenta.MonedaBase != null)
        {
            ViewBag.MonedaBase = cuenta.MonedaBase;
        }
        else
        {
            var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
            ViewBag.MonedaBase = config?.MonedaBase ?? new Moneda { Codigo = "USD", Simbolo = "$", Nombre = "Base (Default)" };
        }
        
        ViewBag.Monedas = await _context.Monedas.ToListAsync();

        // Tasa de cambio con caché
        var activeTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
        if (activeTasa == null)
        {
            TempData["Error"] = "ERROR CRÍTICO: No hay una tasa de cambio activa definida.";
            ViewBag.TasaCambio = 0;
        }
        else
        {
            ViewBag.TasaCambio = activeTasa.Valor;
            ViewBag.TasaOrigenId = activeTasa.IdMonedaOrigen;
            ViewBag.TasaDestinoId = activeTasa.IdMonedaDestino;
        }

        // Saldo pendiente
        var totalPagado = cuenta.Pagos.Sum(p => p.MontoBase ?? 0);
        ViewBag.TotalPagado = totalPagado;
        ViewBag.SaldoPendiente = (cuenta.TotalFinal ?? 0) - totalPagado;
        ViewBag.IdMonedaBase = ViewBag.MonedaBase?.IdMoneda ?? 0;

        return View(cuenta);
    }

    // POST: Caja/RegistrarPago
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(int idCuenta, decimal monto, int idMoneda, string metodoPago)
    {
        var cuenta = await _context.Cuentas
            .Include(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.IdCuenta == idCuenta);

        if (cuenta == null) return NotFound();

        // Obtener tasa de cambio con caché (Obligatorio para nuevos pagos)
        if (!_cache.TryGetValue(TasaCacheKey, out decimal tasaCambio))
        {
            var activeTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
            if (activeTasa == null)
            {
                throw new Exception("No existe una tasa de cambio activa definida. No se puede proceder con el pago.");
            }
            tasaCambio = activeTasa.Valor;
            _cache.Set(TasaCacheKey, tasaCambio, TimeSpan.FromHours(1));
        }

        var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
        if (config == null) throw new Exception("Configuración de moneda no encontrada.");

        var currentTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
        if (currentTasa == null) throw new Exception("No hay tasa de cambio activa.");

        decimal montoBase = monto;
        if (idMoneda != config.IdMonedaBase)
        {
            // Si la moneda pagada es el Origen de la tasa y la Base es el Destino -> Multiplicar
            if (currentTasa.IdMonedaOrigen == idMoneda && currentTasa.IdMonedaDestino == config.IdMonedaBase)
            {
                montoBase = monto * currentTasa.Valor;
            }
            // Si la moneda Base es el Origen y la pagada es el Destino -> Dividir
            else if (currentTasa.IdMonedaOrigen == config.IdMonedaBase && currentTasa.IdMonedaDestino == idMoneda)
            {
                montoBase = monto / currentTasa.Valor;
            }
        }

        // Validación "No fiamos" - El pago debe cubrir el saldo total pendiente
        var totalPagadoPrevio = cuenta.Pagos.Sum(p => p.MontoBase ?? 0);
        var saldoPendienteBase = (cuenta.TotalFinal ?? 0) - totalPagadoPrevio;

        if (montoBase < saldoPendienteBase - 0.01m)
        {
            TempData["Error"] = $"ERROR: El monto ingresado ({montoBase:N2} {config.MonedaBase?.Codigo}) es menor al saldo pendiente ({saldoPendienteBase:N2} {config.MonedaBase?.Codigo}). En este centro no se permite el fiado.";
            return RedirectToAction("Details", new { id = idCuenta });
        }

        var pago = new Pago
        {
            IdCuenta = idCuenta,
            Monto = monto,
            MontoBase = montoBase,
            IdMoneda = idMoneda,
            MetodoPago = metodoPago,
            TasaCambioAplicada = currentTasa.Valor,
            FechaPago = DateTime.Now
        };

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Pago registrado exitosamente.";
        return RedirectToAction(nameof(Details), new { id = idCuenta });
    }
}

