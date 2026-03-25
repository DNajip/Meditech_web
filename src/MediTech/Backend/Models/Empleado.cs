using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("EMPLEADOS", Schema = "ADM")]
    public class Empleado
    {
        [Key]
        [Column("ID_EMPLEADO")]
        public int IdEmpleado { get; set; }

        [Column("ID_PERSONA")]
        public int IdPersona { get; set; }

        [Column("ID_ROL")]
        public int IdRol { get; set; }

        [Column("FECHA_CONTRATACION")]
        public DateTime? FechaContratacion { get; set; }

        [Column("ID_ESTADO")]
        public int? IdEstado { get; set; }

        [ForeignKey("IdPersona")]
        public Persona? Persona { get; set; }

        [ForeignKey("IdRol")]
        public Role? Role { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }

        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}

