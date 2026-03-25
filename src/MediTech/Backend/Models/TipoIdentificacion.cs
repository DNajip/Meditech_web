using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("TIPO_IDENTIFICACION", Schema = "CAT")]
    public class TipoIdentificacion
    {
        [Key]
        [Column("ID_TIPO_IDENTIFICACION")]
        public int IdTipoIdentificacion { get; set; }

        [Required]
        [Column("DESC_TIPO")]
        [StringLength(50)]
        public string DescTipo { get; set; } = string.Empty;

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }
    }
}

