using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("CONSULTAS", Schema = "CLI")]
    public class Consulta
    {
        [Key]
        [Column("ID_CONSULTA")]
        public int IdConsulta { get; set; }

        [Column("ID_CITA")]
        public int IdCita { get; set; }

        [Column("ID_MEDICO")]
        public int IdMedico { get; set; }

        [Column("MOTIVO_CONSULTA")]
        [StringLength(300)]
        public string? MotivoConsulta { get; set; }

        [Column("DIAGNOSTICO_PRINCIPAL")]
        [StringLength(500)]
        public string? DiagnosticoPrincipal { get; set; }

        [Column("OBSERVACIONES")]
        [StringLength(500)]
        public string? Observaciones { get; set; }

        [Column("FECHA_CONSULTA")]
        public DateTime FechaConsulta { get; set; } = DateTime.Now;

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; } = 1;

        // Navigation properties
        [ForeignKey("IdCita")]
        public virtual Cita? Cita { get; set; }

        [ForeignKey("IdMedico")]
        public virtual Empleado? Medico { get; set; }

        [ForeignKey("IdEstado")]
        public virtual Estado? Estado { get; set; }

        public virtual SignosVitales? SignosVitales { get; set; }
        public virtual ICollection<Diagnostico> Diagnosticos { get; set; } = new List<Diagnostico>();
        public virtual ICollection<ConsultaDetalle> ConsultaDetalles { get; set; } = new List<ConsultaDetalle>();
    }
}

