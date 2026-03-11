using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Models
{
    [Table("pacientes")]
    public class Paciente
    {
        [Key]
        [Column("id_paciente")]
        public int IdPaciente { get; set; }

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

        [Column("numero_identificacion")]
        [StringLength(30)]
        public string? NumeroIdentificacion { get; set; }

        [Column("sexo")]
        [StringLength(1)]
        public string? Sexo { get; set; }

        [Column("fecha_nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("telefono")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [Column("direccion")]
        [StringLength(200)]
        public string? Direccion { get; set; }

        [Column("ocupacion")]
        [StringLength(100)]
        public string? Ocupacion { get; set; }

        [Column("fecha_registro")]
        public DateTime? FechaRegistro { get; set; }

        [Column("estado")]
        public bool? Estado { get; set; }
    }
}
