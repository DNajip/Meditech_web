using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("ROLES", Schema = "CAT")]
    public class Role
    {
        [Key]
        [Column("ID_ROL")]
        public int IdRol { get; set; }

        [Required]
        [Column("DESC_ROL")]
        [StringLength(80)]
        public string DescRol { get; set; } = string.Empty;

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }

        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    }
}

