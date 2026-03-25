using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;

namespace MediTech.Backend.Controllers
{
    [Authorize]
    [Route("Galeria")]
    public class GaleriaController(MediTechContext context) : Controller
    {
        private readonly MediTechContext _context = context;

        [HttpGet("GetFotos/{id}")]
        public async Task<IActionResult> GetFotos(int id)
        {
            var fotos = await _context.FotosClinicas
                .Where(f => f.IdPaciente == id)
                .OrderByDescending(f => f.FechaRegistro)
                .Select(f => new {
                    idFoto = f.IdFoto,
                    titulo = f.Titulo,
                    fecha = f.FechaRegistro.ToString("dd/MM/yyyy HH:mm"),
                    contentType = f.ContentType
                })
                .ToListAsync();
            
            return Json(new { success = true, data = fotos });
        }

        [HttpPost("UploadFoto")]
        public async Task<IActionResult> UploadFoto([FromForm] int idPaciente, [FromForm] string? titulo, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Debe seleccionar un archivo." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var foto = new FotoClinica
            {
                IdPaciente = idPaciente,
                Titulo = titulo,
                Contenido = ms.ToArray(),
                ContentType = file.ContentType,
                FechaRegistro = DateTime.Now
            };

            _context.FotosClinicas.Add(foto);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet("GetFotoContent/{id}")]
        public async Task<IActionResult> GetFotoContent(int id)
        {
            var foto = await _context.FotosClinicas.FindAsync(id);
            if (foto == null) return NotFound();

            return File(foto.Contenido, foto.ContentType);
        }

        [HttpPost("DeleteFoto/{id}")]
        public async Task<IActionResult> DeleteFoto(int id)
        {
            var foto = await _context.FotosClinicas.FindAsync(id);
            if (foto == null) return Json(new { success = false, message = "Foto no encontrada." });

            _context.FotosClinicas.Remove(foto);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

