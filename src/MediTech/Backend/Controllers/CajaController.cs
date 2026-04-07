using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace MediTech.Backend.Controllers;

[Authorize]
public class CajaController : Controller
{
    private readonly MediTechContext _context;

    public CajaController(MediTechContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("Caja/GetFinanzasPaciente/{id}")]
    public async Task<IActionResult> GetFinanzasPaciente(int id)
    {
        try
        {
            var cuentas = await _context.Cuentas
                .AsNoTracking()
                .Where(c => c.IdPaciente == id)
                .Include(c => c.MonedaBase)
                .Include(c => c.Detalles)
                .Include(c => c.Pagos)
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();

            var totalFacturado = cuentas.Sum(c => c.TotalFinal ?? 0m);
            var totalPagado = cuentas.SelectMany(c => c.Pagos).Sum(p => p.MontoBase ?? 0m);
            var saldoPendiente = totalFacturado - totalPagado;

            var result = new
            {
                success = true,
                resumen = new
                {
                    totalFacturado,
                    totalPagado,
                    saldoPendiente
                },
                cuentas = cuentas.Select(c => new
                {
                    idCuenta = c.IdCuenta,
                    fecha = c.FechaCreacion.ToString("dd/MM/yyyy"),
                    totalBruto = c.TotalBruto,
                    descuento = c.Descuento,
                    totalFinal = c.TotalFinal,
                    simbolo = c.MonedaBase?.Simbolo ?? "$",
                    estado = (c.Pagos.Sum(p => p.MontoBase ?? 0m) >= (c.TotalFinal ?? 0m) - 0.05m && (c.TotalFinal ?? 0m) > 0) ? "PAGADA" : "PENDIENTE",
                    detalles = c.Detalles.Select(d => new
                    {
                        tipo = d.TipoItem,
                        descripcion = d.Descripcion,
                        cant = d.Cantidad,
                        precio = d.PrecioUnitario,
                        subtotal = d.Subtotal
                    })
                })
            };

            return Json(result);
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "Error al cargar finanzas." });
        }
    }

    // GET: Caja
    public async Task<IActionResult> Index(string? estado, DateTime? fecha, string? buscar, int page = 1)
    {
        int pageSize = 10;
        var hoy = DateTime.Today;

        var query = _context.Cuentas
            .AsNoTracking()
            .Include(c => c.Pagos)
            .Include(c => c.MonedaBase)
            .Include(c => c.Paciente)
                .ThenInclude(p => p.Persona)
            .AsQueryable();

        // Filtro por búsqueda de nombre o ID
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
        var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
        ViewBag.MonedaBase = config?.MonedaBase;

        ViewBag.IngresosHoy = await _context.Pagos
            .Where(p => p.FechaPago.Date == hoy)
            .SumAsync(p => p.MontoBase ?? 0);

        ViewBag.CuentasHoy = await _context.Cuentas
            .CountAsync(c => c.FechaCreacion.Date == hoy);

        ViewBag.CuentasPendientes = await _context.Cuentas
            .CountAsync(c => (c.TotalFinal ?? 0) > c.Pagos.Sum(p => p.MontoBase ?? 0) && (c.TotalFinal ?? 0) > 0);

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
        ViewBag.TotalItems = totalItems;
        ViewBag.EstadoFilter = estado;
        ViewBag.FechaFilter = fecha?.ToString("yyyy-MM-dd");
        ViewBag.BuscarFilter = buscar;

        return View(cuentas);
    }

    // GET: Caja/Create
    public async Task<IActionResult> Create()
    {
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

    // AJAX: Buscar Pacientes (Phase 3 #13 - Stable)
    [HttpGet]
    public async Task<IActionResult> GetPacientesSearch(string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2) return Json(new List<object>());

            var pattern = $"%{term}%";
            var query = _context.Pacientes
                .AsNoTracking()
                .Include(p => p.Persona)
                .Where(p => p.IdEstado == 1 && p.Persona != null);

            // Búsqueda robusta por múltiples campos
            query = query.Where(p =>
                EF.Functions.Like(p.Persona!.PrimerNombre, pattern) ||
                EF.Functions.Like(p.Persona.SegundoNombre ?? "", pattern) ||
                EF.Functions.Like(p.Persona.PrimerApellido, pattern) ||
                EF.Functions.Like(p.Persona.SegundoApellido ?? "", pattern) ||
                EF.Functions.Like(p.Persona.NumIdentificacion, pattern) ||
                EF.Functions.Like(p.Persona.Telefono ?? "", pattern)
            );

            var pacientes = await query
                .Take(10)
                .Select(p => new
                {
                    id = p.IdPaciente,
                    nombre = (p.Persona!.PrimerNombre + " " + p.Persona.PrimerApellido).Trim(),
                    detalle = (p.Persona.NumIdentificacion ?? "S/I") + " | " + (p.Persona.Telefono ?? "S/T")
                })
                .ToListAsync();

            return Json(pacientes);
        }
        catch (Exception ex)
        {
            // Log real en servidor para diagnóstico
            Console.WriteLine($"[CajaController] Search Error: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error interno de servidor." });
        }
    }

    // ViewModel para creación de cuenta
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

        var totalPagado = cuenta.Pagos.Sum(p => p.MontoBase ?? 0);
        ViewBag.TotalPagado = totalPagado;
        ViewBag.SaldoPendiente = (cuenta.TotalFinal ?? 0) - totalPagado;
        ViewBag.IdMonedaBase = ViewBag.MonedaBase?.IdMoneda ?? 0;

        return View(cuenta);
    }

    // POST: Caja/RegistrarPago (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(int idCuenta, decimal monto, int idMoneda, string metodoPago, decimal montoRecibido = 0)
    {
        try
        {
            var cuenta = await _context.Cuentas
                .Include(c => c.Pagos)
                .Include(c => c.MonedaBase)
                .FirstOrDefaultAsync(c => c.IdCuenta == idCuenta);

            if (cuenta == null) return Json(new { success = false, message = "Cuenta no encontrada" });

            var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
            var monedaPago = await _context.Monedas.FindAsync(idMoneda);
            var currentTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);

            if (config == null || monedaPago == null || currentTasa == null)
            {
                return Json(new { success = false, message = "Error de configuración de moneda o tasa activa." });
            }

            string codigoBase = config.MonedaBase?.Codigo ?? "NIO";
            string codigoPago = monedaPago.Codigo ?? "NIO";
            decimal tasa = currentTasa.Valor;

            // Lógica Inteligente de Conversión (Sincronizada con Frontend)
            decimal montoBase = monto;
            decimal montoRecibidoEnBase = montoRecibido;

            if (codigoBase != codigoPago)
            {
                if (codigoBase == "NIO" && codigoPago == "USD")
                {
                    montoBase = monto * tasa;
                    montoRecibidoEnBase = montoRecibido * tasa;
                }
                else if (codigoBase == "USD" && codigoPago == "NIO")
                {
                    montoBase = monto / tasa;
                    montoRecibidoEnBase = montoRecibido / tasa;
                }
                else
                {
                    if (tasa > 1)
                    {
                        montoBase = monto * tasa;
                        montoRecibidoEnBase = montoRecibido * tasa;
                    }
                    else
                    {
                        montoBase = monto / tasa;
                        montoRecibidoEnBase = montoRecibido / tasa;
                    }
                }
            }

            var pagosList = cuenta.Pagos ?? new List<Pago>();
            var totalPagadoPrevio = pagosList.Sum(p => p.MontoBase ?? 0);
            var saldoPendienteBase = (cuenta.TotalFinal ?? 0) - totalPagadoPrevio;

            // Validación con tolerancia de 0.05 para redondeo
            if (montoBase < saldoPendienteBase - 0.05m)
            {
                return Json(new { success = false, message = $"Monto insuficiente. Eq: {montoBase:N2}, Saldo: {saldoPendienteBase:N2}" });
            }

            // El vuelto es lo que sobra del recibido en moneda base
            decimal vuelto = 0;
            if (metodoPago == "EFECTIVO" && montoRecibidoEnBase > montoBase)
            {
                vuelto = montoRecibidoEnBase - montoBase;
            }

            var pago = new Pago
            {
                IdCuenta = idCuenta,
                Monto = monto,
                MontoBase = montoBase,
                MontoRecibido = montoRecibido,
                Vuelto = vuelto,
                IdMoneda = idMoneda,
                MetodoPago = metodoPago,
                TasaCambioAplicada = tasa,
                FechaPago = DateTime.Now
            };

            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Pago registrado exitosamente.", vuelto = vuelto });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error interno: " + ex.Message });
        }
    }

    // POST: Caja/AnularCuenta (AJAX)
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

        var detalles = await _context.CuentaDetalles.Where(d => d.IdCuenta == id && d.TipoItem == "PRODUCTO").ToListAsync();
        foreach (var det in detalles)
        {
            if (det.IdReferencia.HasValue)
            {
                var prod = await _context.Productos.FindAsync(det.IdReferencia.Value);
                if (prod != null)
                {
                    prod.Stock += det.Cantidad ?? 0;
                    _context.Productos.Update(prod);
                }
            }
        }

        cuenta.TotalBruto = 0;
        cuenta.TotalFinal = 0;
        cuenta.Descuento = 0;

        _context.CuentaDetalles.RemoveRange(_context.CuentaDetalles.Where(d => d.IdCuenta == id));
        _context.Cuentas.Update(cuenta);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Cuenta anulada correctamente y stock devuelto." });
    }
}
