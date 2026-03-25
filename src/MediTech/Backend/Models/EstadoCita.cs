using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("ESTADO_CITA", Schema = "CAT")]
    public class EstadoCita
    {
        [Key]
        [Column("ID_ESTADO_CITA")]
        public int IdEstadoCita { get; set; }

        [Column("DESC_ESTADO_CITA")]
        [StringLength(50)]
        [Required]
        public string DescEstadoCita { get; set; } = string.Empty;

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; } = 1;

        [ForeignKey("IdEstado")]
        public virtual Estado? EstadoGlobal { get; set; }
    }
}

