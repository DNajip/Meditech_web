using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("PACIENTES", Schema = "CLI")]
    public class Paciente
    {
        [Key]
        [Column("ID_PACIENTE")]
        public int IdPaciente { get; set; }

        [Column("ID_PERSONA")]
        public int IdPersona { get; set; }

        [Column("OCUPACION")]
        [StringLength(100)]
        public string? Ocupacion { get; set; }

        [Column("CONTACTO_EMERGENCIA")]
        [StringLength(120)]
        public string? ContactoEmergencia { get; set; }

        [Column("TELEFONO_EMERGENCIA")]
        [StringLength(20)]
        public string? TelefonoEmergencia { get; set; }

        [Column("FECHA_REGISTRO")]
        public DateTime? FechaRegistro { get; set; } = DateTime.Now;

        [Column("ID_ESTADO")]
        public int? IdEstado { get; set; } = 1;

        [ForeignKey("IdPersona")]
        public Persona? Persona { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }
    }
}

