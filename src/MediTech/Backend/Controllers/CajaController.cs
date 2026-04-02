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
    public async Task<IActionResult> Index(string? estado, DateTime? fecha, string? buscar, int page = 1)
    {
        int pageSize = 10;

        var query = _context.Cuentas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.MonedaBase)
            .Include(c => c.Pagos)
            .AsQueryable();

        // Filtro por búsqueda de nombre
        if (!string.IsNullOrEmpty(buscar))
        {
            query = query.Where(c => (c.Paciente!.Persona!.PrimerNombre + " " + c.Paciente.Persona.PrimerApellido).Contains(buscar) ||
                                     c.IdCuenta.ToString() == buscar);
        }

        // Filtro por fecha
        if (fecha.HasValue)
        {
            query = query.Where(c => c.FechaCreacion.Date == fecha.Value.Date);
        }

        // Estadísticas optimizadas (SQL)
        var hoy = DateTime.Today;
        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;

        // Calculamos stats directamente en SQL
        ViewBag.IngresosHoy = await _context.Pagos
            .Where(p => p.FechaPago.Date == hoy)
            .SumAsync(p => p.MontoBase ?? 0);

        ViewBag.CuentasHoy = await _context.Cuentas
            .CountAsync(c => c.FechaCreacion.Date == hoy);

        ViewBag.CuentasPendientes = await _context.Cuentas
            .CountAsync(c => c.Pagos.Sum(p => p.MontoBase ?? 0) < c.TotalFinal);

        ViewBag.TotalRecaudado = await _context.Pagos
            .SumAsync(p => p.MontoBase ?? 0);

        // Filtro por estado
        if (!string.IsNullOrEmpty(estado))
        {
            if (estado == "PENDIENTE")
                query = query.Where(c => c.Pagos.Sum(p => p.MontoBase ?? 0) < c.TotalFinal && (c.TotalFinal ?? 0) > 0);
            else if (estado == "PAGADA")
                query = query.Where(c => c.Pagos.Sum(p => p.MontoBase ?? 0) >= c.TotalFinal && (c.TotalFinal ?? 0) > 0);
            else if (estado == "CANCELADA")
                query = query.Where(c => (c.TotalFinal ?? 0) == 0);
        }

        var totalItems = await query.CountAsync();
        
        var cuentas = await query
            .OrderByDescending(c => c.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.TotalItems = totalItems; // Para el indicador "Mostrando X de Z"
        ViewBag.EstadoFilter = estado;
        ViewBag.FechaFilter = fecha?.ToString("yyyy-MM-dd");
        ViewBag.BuscarFilter = buscar;

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

    // ViewModel para creación de cuenta (Fix Binding Errors)
    public class CreateCuentaViewModel
    {
        public int IdPaciente { get; set; }
        public int? IdMonedaBase { get; set; }
        public decimal Descuento { get; set; }
        public string[] TipoItem { get; set; } = Array.Empty<string>();
        public int[] IdReferencia { get; set; } = Array.Empty<int>();
        public string[] DescripcionItem { get; set; } = Array.Empty<string>();
        public int[] Cantidad { get; set; } = Array.Empty<int>();
        public decimal[] PrecioUnitario { get; set; } = Array.Empty<decimal>();
    }

    // POST: Caja/Create (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreateCuentaViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join(" | ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            return Json(new { success = false, message = "Datos inválidos: " + errors });
        }

        try 
        {
            if (model.TipoItem == null || model.TipoItem.Length == 0)
            {
                return Json(new { success = false, message = "Debe agregar al menos un ítem a la cuenta." });
            }

            decimal totalBruto = 0;
            var detalles = new List<CuentaDetalle>();

            for (int i = 0; i < model.TipoItem.Length; i++)
            {
                var subtotal = model.PrecioUnitario[i] * model.Cantidad[i];
                totalBruto += subtotal;

                detalles.Add(new CuentaDetalle
                {
                    TipoItem = model.TipoItem[i],
                    IdReferencia = model.IdReferencia[i],
                    Descripcion = model.DescripcionItem[i],
                    Cantidad = model.Cantidad[i],
                    PrecioUnitario = model.PrecioUnitario[i],
                    Subtotal = subtotal
                });
            }

            var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
            if (config == null) return Json(new { success = false, message = "Configuración de moneda no encontrada." });

            var cuenta = new Cuenta
            {
                IdPaciente = model.IdPaciente,
                IdMonedaBase = config.IdMonedaBase,
                TotalBruto = totalBruto,
                Descuento = model.Descuento,
                TotalFinal = totalBruto - model.Descuento,
                FechaCreacion = DateTime.Now
            };

            _context.Cuentas.Add(cuenta);
            await _context.SaveChangesAsync();

            // Asignar IdCuenta y descontar stock
            foreach (var d in detalles)
            {
                d.IdCuenta = cuenta.IdCuenta;
                if (d.TipoItem == "PRODUCTO" && d.IdReferencia.HasValue)
                {
                    var producto = await _context.Productos.FindAsync(d.IdReferencia.Value);
                    if (producto != null)
                    {
                        producto.Stock -= d.Cantidad ?? 0;
                        if (producto.Stock < 0) producto.Stock = 0;
                        _context.Productos.Update(producto);
                    }
                }
            }
            
            _context.CuentaDetalles.AddRange(detalles);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cuenta creada exitosamente.", id = cuenta.IdCuenta });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error interno: " + ex.Message });
        }
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

    // POST: Caja/RegistrarPago (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(int idCuenta, decimal monto, int idMoneda, string metodoPago)
    {
        try 
        {
            var cuenta = await _context.Cuentas
                .Include(c => c.Pagos)
                .FirstOrDefaultAsync(c => c.IdCuenta == idCuenta);

            if (cuenta == null) return NotFound(new { success = false, message = "Cuenta no encontrada" });

            // Obtener tasa de cambio con caché
            if (!_cache.TryGetValue(TasaCacheKey, out decimal tasaCambio))
            {
                var activeTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
                if (activeTasa == null)
                {
                    return Json(new { success = false, message = "No hay tasa de cambio activa definida." });
                }
                tasaCambio = activeTasa.Valor;
                _cache.Set(TasaCacheKey, tasaCambio, TimeSpan.FromHours(1));
            }

            var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
            var currentTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);

            decimal montoBase = monto;
            if (idMoneda != config?.IdMonedaBase && currentTasa != null)
            {
                if (currentTasa.IdMonedaOrigen == idMoneda && currentTasa.IdMonedaDestino == config?.IdMonedaBase)
                    montoBase = monto * currentTasa.Valor;
                else if (currentTasa.IdMonedaOrigen == config?.IdMonedaBase && currentTasa.IdMonedaDestino == idMoneda)
                    montoBase = monto / currentTasa.Valor;
            }

            var pagosList = cuenta.Pagos ?? new List<Pago>();
            var totalPagadoPrevio = pagosList.Sum(p => p.MontoBase ?? 0);
            var saldoPendienteBase = (cuenta.TotalFinal ?? 0) - totalPagadoPrevio;

            if (montoBase < saldoPendienteBase - 0.01m)
            {
                return Json(new { success = false, message = $"El monto ({montoBase:N2}) es insuficiente para el saldo ({saldoPendienteBase:N2})." });
            }

            var pago = new Pago
            {
                IdCuenta = idCuenta,
                Monto = monto,
                MontoBase = montoBase,
                IdMoneda = idMoneda,
                MetodoPago = metodoPago,
                TasaCambioAplicada = currentTasa?.Valor ?? 1,
                FechaPago = DateTime.Now
            };

            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Pago registrado exitosamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error interno: " + ex.Message });
        }
    }

    // POST: Caja/AnularCuenta (Phase 3 #8)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnularCuenta(int id)
    {
        var cuenta = await _context.Cuentas.Include(c => c.Pagos).FirstOrDefaultAsync(c => c.IdCuenta == id);
        if (cuenta == null) return NotFound();

        if (cuenta.Pagos.Any())
        {
            return Json(new { success = false, message = "No se puede anular una cuenta que ya tiene pagos registrados." });
        }

        // Devolver stock si tenía productos (Phase 3 #8 + Reversión de #5)
        var detalles = await _context.CuentaDetalles.Where(d => d.IdCuenta == id && d.TipoItem == "PRODUCTO").ToListAsync();
        foreach (var det in detalles)
        {
            if (det.IdReferencia.HasValue)
            {
                var prod = await _context.Productos.FindAsync(det.IdReferencia.Value);
                if (prod != null) prod.Stock += det.Cantidad ?? 0;
            }
        }

        // "Anular" poniendo totales en 0 o eliminando detalles
        cuenta.TotalBruto = 0;
        cuenta.TotalFinal = 0;
        cuenta.Descuento = 0;
        
        // Opcional: Podríamos tener un campo ESTADO, pero aquí lo manejamos por totales/detalles
        _context.CuentaDetalles.RemoveRange(_context.CuentaDetalles.Where(d => d.IdCuenta == id));
        _context.Cuentas.Update(cuenta);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Cuenta anulada correctamente y stock devuelto." });
    }
}

