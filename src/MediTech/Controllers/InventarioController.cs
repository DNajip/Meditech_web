using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;

namespace MediTech.Controllers
{
    public class InventarioController : Controller
    {
        private readonly MediTechContext _context;

        public InventarioController(MediTechContext context)
        {
            _context = context;
        }

        // GET: Inventario
        public async Task<IActionResult> Index(string searchString, int page = 1)
        {
            int pageSize = 10;
            
            // Obtener tasa de cambio desde base de datos
            var config = await _context.ConfiguracionesMoneda.FirstOrDefaultAsync();
            double tasaCambio = config != null ? (double)config.TasaCambio : 36.62;
            
            var query = _context.Productos
                .Include(p => p.Moneda)
                .AsQueryable();

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

            // Cálculos de Valorización Bimonetaria
            double totalNio = 0;
            double totalUsd = 0;

            foreach (var p in allProducts)
            {
                double monto = (double)(p.Precio ?? 0) * p.Stock;
                if (p.Moneda?.Codigo == "USD")
                {
                    totalUsd += monto;
                    totalNio += monto * tasaCambio;
                }
                else // Asumimos NIO por defecto
                {
                    totalNio += monto;
                    totalUsd += monto / tasaCambio;
                }
            }

            ViewBag.ValorizacionTotalNio = totalNio;
            ViewBag.ValorizacionTotalUsd = totalUsd;
            ViewBag.TasaCambio = tasaCambio;

            // Datos paginados para la tabla
            var productos = allProducts
                .OrderBy(p => p.Nombre)
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
                .Include(p => p.Moneda)
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

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovimientos / pageSize);
            ViewBag.TotalCount = totalMovimientos;

            return View(producto);
        }

        // GET: Inventario/Create
        public IActionResult Create()
        {
            ViewData["IdMoneda"] = new SelectList(_context.Monedas, "IdMoneda", "Codigo");
            return View();
        }

        // POST: Inventario/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Descripcion,Precio,IdMoneda,Stock,StockMinimo,Activo")] Producto producto)
        {
            if (ModelState.IsValid)
            {
                _context.Add(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdMoneda"] = new SelectList(_context.Monedas, "IdMoneda", "Codigo", producto.IdMoneda);
            return View(producto);
        }

        // GET: Inventario/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            ViewData["IdMoneda"] = new SelectList(_context.Monedas, "IdMoneda", "Codigo", producto.IdMoneda);
            return View(producto);
        }

        // POST: Inventario/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdProducto,Nombre,Descripcion,Precio,IdMoneda,Stock,StockMinimo,Activo,FechaCreacion")] Producto producto)
        {
            if (id != producto.IdProducto) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.IdProducto)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdMoneda"] = new SelectList(_context.Monedas, "IdMoneda", "Codigo", producto.IdMoneda);
            return View(producto);
        }

        // GET: Inventario/Ajustar/5
        public async Task<IActionResult> Ajustar(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();
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
                    return BadRequest("el stock no puede ser negativo. El stock de salida es mayor al inventario actual");
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

            return RedirectToAction(nameof(Index));
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.IdProducto == id);
        }
    }
}
