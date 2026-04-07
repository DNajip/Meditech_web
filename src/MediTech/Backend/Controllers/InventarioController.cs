using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MediTech.Backend.Controllers
{
    public class InventarioController : Controller
    {
        private readonly MediTechContext _context;
        private readonly IMemoryCache _cache;
        private const string TasaCacheKey = "ActiveExchangeRate";

        public InventarioController(MediTechContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: Inventario
        public async Task<IActionResult> Index(string searchString, int page = 1)
        {
            int pageSize = 10;
            
            // Obtener tasa de cambio con caché
            if (!_cache.TryGetValue(TasaCacheKey, out double tasaCambio))
            {
                var activeTasa = await _context.TasasCambio.FirstOrDefaultAsync(t => t.Activo);
                if (activeTasa == null)
                {
                    TempData["Error"] = "ERROR CRÍTICO: No hay una tasa de cambio activa definida. Por favor, configúrela para continuar.";
                    return RedirectToAction("Index", "Configuracion");
                }
                tasaCambio = (double)activeTasa.Valor;
                _cache.Set(TasaCacheKey, tasaCambio, TimeSpan.FromHours(1));
            }
            
            var query = _context.Productos.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.Nombre.Contains(searchString) || s.Descripcion!.Contains(searchString));
            }

            // Estadísticas globales sobre el conjunto filtrado
            var totalItems = await query.CountAsync();
            var allProducts = await query.ToListAsync(); // Cargamos en memoria para cálculos multimoneda precisos
            

            ViewBag.TotalItemsGlobal = totalItems;
            ViewBag.StockBajoCount = allProducts.Count(p => p.Stock <= p.StockMinimo && p.Activo);
            ViewBag.SinStockCount = allProducts.Count(p => p.Stock == 0 && p.Activo);

            // Cálculos de Valorización (Todo está en Moneda Base)
            decimal totalBase = 0;
            foreach (var p in allProducts)
            {
                if (p.Activo) totalBase += (p.Precio ?? 0) * p.Stock;
            }

            // Valorización Total
            ViewBag.ValorizacionTotalBase = totalBase;
            ViewBag.TasaCambio = tasaCambio;
            
            var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
            if (config != null && config.MonedaBase != null)
            {
                ViewBag.MonedaBase = config.MonedaBase;
            }
            else
            {
                // Un fallback básico para evitar errores de null reference en el Binder
                ViewBag.MonedaBase = new Moneda { Simbolo = "$", Nombre = "Base (No Configurada)", Codigo = "USD" };
            }

            // Datos paginados para la tabla
            var productos = allProducts
                .OrderByDescending(p => p.IdProducto)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.SearchString = searchString;

            return View(productos);
        }

        // GET: Inventario/Details/5
        public async Task<IActionResult> Details(int? id, int page = 1)
        {
            if (id == null) return NotFound();

            int pageSize = 10;
            var producto = await _context.Productos
                .FirstOrDefaultAsync(m => m.IdProducto == id);

            if (producto == null) return NotFound();



            // Obtener movimientos de forma separada para paginación eficiente
            var totalMovimientos = await _context.MovimientosInventario
                .Where(m => m.IdProducto == id)
                .CountAsync();

            var movimientos = await _context.MovimientosInventario
                .Where(m => m.IdProducto == id)
                .OrderByDescending(m => m.FechaMovimiento)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            producto.Movimientos = movimientos;

            var config = await _context.ConfiguracionesMoneda.Include(c => c.MonedaBase).FirstOrDefaultAsync();
            ViewBag.MonedaBase = config?.MonedaBase;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovimientos / pageSize);
            ViewBag.TotalCount = totalMovimientos;

            return View(producto);
        }

        // GET: Inventario/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Inventario/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Descripcion,Precio,Stock,StockMinimo,Activo")] Producto producto)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (ModelState.IsValid)
            {
                _context.Add(producto);
                await _context.SaveChangesAsync();

                if (isAjax)
                {
                    return Json(new { success = true, message = "Producto registrado con éxito en el inventario." });
                }

                return RedirectToAction(nameof(Index));
            }

            if (isAjax)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Error al registrar el producto.", errors });
            }

            return View(producto);
        }

        // GET: Inventario/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { 
                    idProducto = producto.IdProducto, 
                    nombre = producto.Nombre, 
                    descripcion = producto.Descripcion,
                    precio = producto.Precio,
                    stock = producto.Stock,
                    stockMinimo = producto.StockMinimo,
                    activo = producto.Activo,
                    fechaCreacion = producto.FechaCreacion
                });
            }

            return View(producto);
        }

        // POST: Inventario/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdProducto,Nombre,Descripcion,Precio,Stock,StockMinimo,Activo,FechaCreacion")] Producto producto)
        {
            if (id != producto.IdProducto)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error de validación: El ID del producto no coincide." });
                }
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = "Producto actualizado con éxito." });
                    }
                    TempData["Success"] = "Producto actualizado con éxito.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.IdProducto)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Error al actualizar el producto.", errors });
            }
            return View(producto);
        }

        // GET: Inventario/Ajustar/5
        public async Task<IActionResult> Ajustar(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { 
                    idProducto = producto.IdProducto, 
                    nombre = producto.Nombre, 
                    stock = producto.Stock 
                });
            }

            return View(producto);
        }

        // POST: Inventario/Ajustar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ajustar(int id, int cantidad, string tipo, string observacion)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            // Validación de Stock Negativo
            if (tipo == "AJUSTE_NEG")
            {
                var stockFinal = producto.Stock - cantidad;
                if (stockFinal < 0)
                {
                    var msg = "El stock no puede ser negativo. El stock de salida es mayor al inventario actual.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = msg });
                    }
                    return BadRequest(msg);
                }
            }

            var movimiento = new MovimientoInventario
            {
                IdProducto = id,
                Cantidad = cantidad,
                TipoMovimiento = tipo,
                Observacion = observacion
            };

            _context.Add(movimiento);
            // El stock se actualiza via Trigger SQL en la BD
            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Movimiento de inventario procesado con éxito." });
            }

            TempData["Success"] = "Movimiento de inventario procesado con éxito.";
            return RedirectToAction(nameof(Index));
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.IdProducto == id);
        }
    }
}

