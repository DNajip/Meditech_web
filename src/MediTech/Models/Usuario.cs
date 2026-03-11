using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Models
{
    [Table("usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("primer_nombre")]
        [StringLength(50)]
        public string? PrimerNombre { get; set; }

        [Column("segundo_nombre")]
        [StringLength(50)]
        public string? SegundoNombre { get; set; }

        [Column("primer_apellido")]
        [StringLength(50)]
        public string? PrimerApellido { get; set; }

        [Column("segundo_apellido")]
        [StringLength(50)]
        public string? SegundoApellido { get; set; }

        [Column("id_tipo_identificacion")]
        public int? IdTipoIdentificacion { get; set; }

        [Column("identificacion")]
        [StringLength(30)]
        public string? Identificacion { get; set; }

        [Column("id_genero")]
        public int? IdGenero { get; set; }

        [Column("fecha_nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("email")]
        [StringLength(100)]
        public string? Email { get; set; }

        [Column("password")]
        [StringLength(255)]
        public string? Password { get; set; }

        [Column("id_rol")]
        public int? IdRol { get; set; }

        [Column("estado")]
        public bool? Estado { get; set; }

        [Column("fecha_creacion")]
        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdRol")]
        public Role? Role { get; set; }
    }
}
