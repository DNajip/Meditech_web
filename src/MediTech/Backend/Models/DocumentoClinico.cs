using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("DOCUMENTOS_CLINICOS", Schema = "DOC")]
    public class DocumentoClinico
    {
        [Key]
        [Column("ID_DOCUMENTO")]
        public int IdDocumento { get; set; }

        [Column("ID_PACIENTE")]
        public int IdPaciente { get; set; }

        [Column("NOMBRE_ARCHIVO")]
        [StringLength(200)]
        public string NombreArchivo { get; set; } = null!;

        [Column("TITULO")]
        [StringLength(200)]
        public string? Titulo { get; set; }

        [Column("CONTENIDO")]
        public byte[] Contenido { get; set; } = null!;

        [Column("CONTENT_TYPE")]
        [StringLength(100)]
        public string ContentType { get; set; } = "application/octet-stream";

        [Column("TAMANO_BYTES")]
        public long TamanoBytes { get; set; }

        [Column("FECHA_REGISTRO")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("IdPaciente")]
        public Paciente? Paciente { get; set; }
    }
}

