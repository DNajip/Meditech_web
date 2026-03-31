using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("MODULOS", Schema = "ADM")]
    public class Modulo
    {
        [Key]
        [Column("ID_MODULO")]
        public int IdModulo { get; set; }

        [Required]
        [Column("NOMBRE")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Column("ICONO")]
        [StringLength(50)]
        public string? Icono { get; set; }

        [Required]
        [Column("CONTROLLER")]
        [StringLength(100)]
        public string Controller { get; set; } = string.Empty;

        [Column("ORDEN")]
        public int Orden { get; set; } = 0;

        [Column("ID_ESTADO")]
        public int? IdEstado { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }

        public virtual ICollection<UsuarioModulo> UsuarioModulos { get; set; } = new List<UsuarioModulo>();
    }
}
