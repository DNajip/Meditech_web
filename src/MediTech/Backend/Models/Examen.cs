using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("EXAMENES", Schema = "CLI")]
    public class Examen
    {
        [Key]
        [Column("ID_EXAMEN")]
        public int IdExamen { get; set; }

        [Column("ID_PACIENTE")]
        public int IdPaciente { get; set; }

        [Column("ID_CONSULTA")]
        public int? IdConsulta { get; set; }

        [Required]
        [Column("NOMBRE_EXAMEN")]
        [MaxLength(200)]
        public string NombreExamen { get; set; } = string.Empty;

        [Column("FECHA_ORDEN")]
        public DateTime FechaOrden { get; set; } = DateTime.Now;

        [Column("FECHA_RESULTADO")]
        public DateTime? FechaResultado { get; set; }

        [Column("IMAGEN_RESULTADO")]
        public byte[]? ImagenResultado { get; set; }


        [Column("CONTENT_TYPE")]
        [MaxLength(50)]
        public string? ContentType { get; set; }

        [Column("COMENTARIO_MEDICO")]

        public string? ComentarioMedico { get; set; }

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; } = 1; // 1: Ordenado, 2: Realizado, 3: Revisado

        // Navigation properties
        [ForeignKey("IdPaciente")]
        public virtual Paciente? Paciente { get; set; }

        [ForeignKey("IdConsulta")]
        public virtual Consulta? Consulta { get; set; }

        [ForeignKey("IdEstado")]
        public virtual Estado? Estado { get; set; }
    }
}

