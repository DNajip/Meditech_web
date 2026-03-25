using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("PERSONAS", Schema = "ADM")]
    public class Persona
    {
        [Key]
        [Column("ID_PERSONA")]
        public int IdPersona { get; set; }

        [Required(ErrorMessage = "El primer nombre es requerido.")]
        [Column("PRIMER_NOMBRE")]
        [StringLength(40, ErrorMessage = "El nombre no puede exceder los 40 caracteres.")]
        public string PrimerNombre { get; set; } = string.Empty;

        [Column("SEGUNDO_NOMBRE")]
        [StringLength(40, ErrorMessage = "El segundo nombre no puede exceder los 40 caracteres.")]
        public string? SegundoNombre { get; set; }

        [Required(ErrorMessage = "El primer apellido es requerido.")]
        [Column("PRIMER_APELLIDO")]
        [StringLength(40, ErrorMessage = "El apellido no puede exceder los 40 caracteres.")]
        public string PrimerApellido { get; set; } = string.Empty;

        [Column("SEGUNDO_APELLIDO")]
        [StringLength(40, ErrorMessage = "el segundo apellido no puede exceder los 40 caracteres.")]
        public string? SegundoApellido { get; set; }

        [Required(ErrorMessage = "El tipo de identificación es requerido.")]
        [Column("ID_TIPO_IDENTIFICACION")]
        public int? IdTipoIdentificacion { get; set; }

        [Required(ErrorMessage = "El número de identificación es requerido.")]
        [Column("NUM_IDENTIFICACION")]
        [StringLength(25, ErrorMessage = "El número de identificación no puede exceder los 25 caracteres.")]
        public string NumIdentificacion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El género es requerido.")]
        [Column("ID_GENERO")]
        public int? IdGenero { get; set; }

        [Required(ErrorMessage = "La fecha de nacimiento es requerida.")]
        [Column("FECHA_NACIMIENTO")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("EMAIL")]
        [StringLength(120, ErrorMessage = "El email no puede exceder los 120 caracteres.")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "El teléfono es requerido.")]
        [Column("TELEFONO")]
        [StringLength(20)]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "El teléfono debe tener exactamente 8 dígitos.")]
        public string Telefono { get; set; } = string.Empty;

        [Column("DIRECCION")]
        [StringLength(250, ErrorMessage = "La dirección no puede exceder los 250 caracteres.")]
        public string? Direccion { get; set; }

        [Column("FECHA_CREACION")]
        public DateTime? FechaCreacion { get; set; } = DateTime.Now;

        [Column("ID_ESTADO")]
        public int IdEstado { get; set; }

        [ForeignKey("IdTipoIdentificacion")]
        public TipoIdentificacion? TipoIdentificacion { get; set; }

        [ForeignKey("IdGenero")]
        public Genero? Genero { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }

        // Navigation properties
        public virtual ICollection<Paciente> Pacientes { get; set; } = new List<Paciente>();
        public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    }
}

