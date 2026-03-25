using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("CITAS", Schema = "CLI")]
public class Cita
{
    [Key]
    [Column("ID_CITA")]
    public int IdCita { get; set; }

    [Column("ID_PACIENTE")]
    public int? IdPaciente { get; set; }

    [Column("ID_POSIBLE_PACIENTE")]
    public int? IdPosiblePaciente { get; set; }

    [Column("ID_TRATAMIENTO")]
    public int? IdTratamiento { get; set; }

    [Column("ID_ESTADO_CITA")]
    public int IdEstadoCita { get; set; } = 1;

    [Column("TELEFONO")]
    [Required(ErrorMessage = "El número de teléfono es obligatorio")]
    [StringLength(20)]
    public string Telefono { get; set; } = null!;

    // ... (rest of the properties)
    [Column("FECHA")]
    [Required(ErrorMessage = "La fecha es obligatoria")]
    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; }

    [Column("HORA_INICIO")]
    [Required(ErrorMessage = "La hora de inicio es obligatoria")]
    public TimeSpan HoraInicio { get; set; }

    [Column("HORA_FIN")]
    [Required(ErrorMessage = "La hora de fin es obligatoria")]
    public TimeSpan HoraFin { get; set; }

    [Column("OBSERVACIONES")]
    [StringLength(300)]
    public string? Observaciones { get; set; }

    [Column("FECHA_CREACION")]
    public DateTime? FechaCreacion { get; set; }

    [Column("ID_ESTADO")]
    public int? IdEstado { get; set; }

    // Navigation properties
    [ForeignKey("IdPaciente")]
    public virtual Paciente? Paciente { get; set; }

    [ForeignKey("IdPosiblePaciente")]
    public virtual PosiblePaciente? PosiblePaciente { get; set; }

    [ForeignKey("IdTratamiento")]
    public virtual Tratamiento? Tratamiento { get; set; }

    [ForeignKey("IdEstadoCita")]
    public virtual EstadoCita? EstadoCita { get; set; }

    [ForeignKey("IdEstado")]
    public virtual Estado? Estado { get; set; }
}

