using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;

namespace MediTech.Backend.Controllers
{
    [Authorize]
    [Route("Examenes")]
    public class ExamenesController(MediTechContext context) : Controller
    {
        private readonly MediTechContext _context = context;

        [HttpGet("GetExamenes/{idPaciente}")]
        public async Task<IActionResult> GetExamenes(int idPaciente, int page = 1, int pageSize = 6)
        {
            var query = _context.Examenes
                .Where(e => e.IdPaciente == idPaciente);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var examenes = await query
                .Include(e => e.Estado)
                .OrderByDescending(e => e.FechaOrden)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new {
                    idExamen = e.IdExamen,
                    nombreExamen = e.NombreExamen,
                    fechaOrden = e.FechaOrden.ToString("dd/MM/yyyy"),
                    fechaResultado = e.FechaResultado.HasValue ? e.FechaResultado.Value.ToString("dd/MM/yyyy") : null,
                    estado = e.Estado!.DescEstado,
                    idEstado = e.IdEstado,
                    comentarioMedico = e.ComentarioMedico,
                    tieneImagen = e.ImagenResultado != null
                })
                .ToListAsync();
            
            return Json(new { 
                success = true, 
                data = examenes,
                totalItems = totalItems,
                totalPages = totalPages,
                currentPage = page
            });
        }

        [HttpPost("GuardarExamen")]
        public async Task<IActionResult> GuardarExamen([FromBody] Examen model)
        {
            if (string.IsNullOrWhiteSpace(model.NombreExamen))
                return Json(new { success = false, message = "El nombre del examen es requerido." });

            model.FechaOrden = DateTime.Now;
            model.IdEstado = 1; // Ordenado

            _context.Examenes.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost("SubirResultadoExamen")]
        public async Task<IActionResult> SubirResultadoExamen([FromForm] int idExamen, [FromForm] string? comentario, IFormFile? file)
        {
            var examen = await _context.Examenes.FindAsync(idExamen);
            if (examen == null) return Json(new { success = false, message = "Examen no encontrado." });

            if (file != null && file.Length > 0)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                examen.ImagenResultado = ms.ToArray();
                examen.ContentType = file.ContentType;
            }

            if (!string.IsNullOrWhiteSpace(comentario))
            {
                examen.ComentarioMedico = comentario;
            }

            examen.FechaResultado = DateTime.Now;
            examen.IdEstado = 2; // Realizado/Con Resultado
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet("GetResultadoImage/{id}")]
        public async Task<IActionResult> GetResultadoImage(int id)
        {
            var examen = await _context.Examenes.FindAsync(id);
            if (examen == null || examen.ImagenResultado == null) return NotFound();

            return File(examen.ImagenResultado, examen.ContentType ?? "image/jpeg");
        }


        [HttpPost("EliminarExamen/{id}")]
        public async Task<IActionResult> EliminarExamen(int id)
        {
            var examen = await _context.Examenes.FindAsync(id);
            if (examen == null) return Json(new { success = false, message = "Examen no encontrado." });

            _context.Examenes.Remove(examen);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

