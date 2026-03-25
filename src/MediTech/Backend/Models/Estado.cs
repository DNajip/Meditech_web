using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("ESTADOS", Schema = "CAT")]
    public class Estado
    {
        [Key]
        [Column("ID_ESTADO")]
        public int IdEstado { get; set; }

        [Required]
        [Column("DESC_ESTADO")]
        [StringLength(50)]
        public string DescEstado { get; set; } = string.Empty;

        [Column("FECHA_CREACION")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}

