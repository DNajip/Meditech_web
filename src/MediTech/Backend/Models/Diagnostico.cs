using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("DIAGNOSTICOS", Schema = "CLI")]
    public class Diagnostico
    {
        [Key]
        [Column("ID_DIAGNOSTICO")]
        public int IdDiagnostico { get; set; }

        [Column("ID_CONSULTA")]
        public int IdConsulta { get; set; }

        [Column("DESCRIPCION")]
        [StringLength(250)]
        public string Descripcion { get; set; } = string.Empty;

        [Column("NOTAS")]
        public string? Notas { get; set; }

        [Column("FECHA_REGISTRO")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; } = 1;

        [ForeignKey("IdConsulta")]
        public virtual Consulta? Consulta { get; set; }

        [ForeignKey("IdEstado")]
        public virtual Estado? Estado { get; set; }
    }
}

