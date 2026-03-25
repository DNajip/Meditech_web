using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("FOTOS_CLINICAS", Schema = "CLI")]
    public class FotoClinica
    {
        [Key]
        [Column("ID_FOTO")]
        public int IdFoto { get; set; }

        [Column("ID_PACIENTE")]
        public int IdPaciente { get; set; }

        [Column("TITULO")]
        [StringLength(200)]
        public string? Titulo { get; set; }

        [Column("CONTENIDO")]
        public byte[] Contenido { get; set; } = null!;

        [Column("CONTENT_TYPE")]
        [StringLength(50)]
        public string ContentType { get; set; } = "image/jpeg";

        [Column("FECHA_REGISTRO")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("IdPaciente")]
        public Paciente? Paciente { get; set; }
    }
}

