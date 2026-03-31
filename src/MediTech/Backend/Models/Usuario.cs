using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("USUARIOS", Schema = "ADM")]
    public class Usuario
    {
        [Key]
        [Column("ID_USUARIO")]
        public int IdUsuario { get; set; }

        [Required]
        [Column("USERNAME")]
        [StringLength(80)]
        public string Username { get; set; } = string.Empty;

        [Column("PASSWORD_HASH")]
        public byte[]? PasswordHash { get; set; }

        [Column("PASSWORD_SALT")]
        public byte[]? PasswordSalt { get; set; }

        [Column("ID_EMPLEADO")]
        public int? IdEmpleado { get; set; }

        [Column("ID_ROL")]
        public int? IdRol { get; set; }

        [Column("FECHA_CREACION")]
        public DateTime? FechaCreacion { get; set; } = DateTime.Now;

        [Column("ID_ESTADO")]
        public int? IdEstado { get; set; }

        [ForeignKey("IdEmpleado")]
        public Empleado? Empleado { get; set; }

        [ForeignKey("IdRol")]
        public Role? Role { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }

        public virtual ICollection<UsuarioModulo> UsuarioModulos { get; set; } = new List<UsuarioModulo>();
    }
}

