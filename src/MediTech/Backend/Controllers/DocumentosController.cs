using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;

namespace MediTech.Backend.Controllers
{
    [Authorize]
    [Route("Documentos")]
    public class DocumentosController(MediTechContext context) : Controller
    {
        private readonly MediTechContext _context = context;

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/msword",
            "application/vnd.ms-excel",
            "image/jpeg",
            "image/png"
        };

        [HttpGet("GetDocumentos/{idPaciente}")]
        public async Task<IActionResult> GetDocumentos(int idPaciente)
        {
            var docs = await _context.DocumentosClinicos
                .Where(d => d.IdPaciente == idPaciente)
                .OrderByDescending(d => d.FechaRegistro)
                .Select(d => new
                {
                    idDocumento = d.IdDocumento,
                    nombreArchivo = d.NombreArchivo,
                    titulo = d.Titulo,
                    contentType = d.ContentType,
                    tamano = d.TamanoBytes,
                    fecha = d.FechaRegistro.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, data = docs });
        }

        [HttpPost("UploadDocumento")]
        public async Task<IActionResult> UploadDocumento([FromForm] int idPaciente, [FromForm] string? titulo, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Debe seleccionar un archivo." });

            if (!AllowedTypes.Contains(file.ContentType))
                return Json(new { success = false, message = "Formato no soportado. Use PDF, DOCX, XLSX, JPG o PNG." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var doc = new DocumentoClinico
            {
                IdPaciente = idPaciente,
                NombreArchivo = file.FileName,
                Titulo = titulo,
                Contenido = ms.ToArray(),
                ContentType = file.ContentType,
                TamanoBytes = file.Length,
                FechaRegistro = DateTime.Now
            };

            _context.DocumentosClinicos.Add(doc);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet("DownloadDocumento/{id}")]
        public async Task<IActionResult> DownloadDocumento(int id)
        {
            var doc = await _context.DocumentosClinicos.FindAsync(id);
            if (doc == null) return NotFound();

            return File(doc.Contenido, doc.ContentType, doc.NombreArchivo);
        }

        [HttpPost("DeleteDocumento/{id}")]
        public async Task<IActionResult> DeleteDocumento(int id)
        {
            var doc = await _context.DocumentosClinicos.FindAsync(id);
            if (doc == null) return Json(new { success = false, message = "Documento no encontrado." });

            _context.DocumentosClinicos.Remove(doc);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

