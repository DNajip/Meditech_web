using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Models;

[Table("CITAS", Schema = "CLI")]
public class Cita
{
    [Key]
    [Column("ID_CITA")]
    public int IdCita { get; set; }

    [Column("ID_PACIENTE")]
    [Required(ErrorMessage = "El paciente es obligatorio")]
    public int IdPaciente { get; set; }

    [Column("ID_TRATAMIENTO")]
    public int? IdTratamiento { get; set; }

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

    [ForeignKey("IdTratamiento")]
    public virtual Tratamiento? Tratamiento { get; set; }

    [ForeignKey("IdEstado")]
    public virtual Estado? Estado { get; set; }
}
