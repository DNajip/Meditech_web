using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("POSIBLES_PACIENTES", Schema = "CLI")]
public class PosiblePaciente
{
    [Key]
    [Column("ID_POSIBLE_PACIENTE")]
    public int IdPosiblePaciente { get; set; }

    [Column("PRIMER_NOMBRE")]
    [Required]
    [StringLength(40)]
    public string PrimerNombre { get; set; } = null!;

    [Column("SEGUNDO_NOMBRE")]
    [StringLength(40)]
    public string? SegundoNombre { get; set; }

    [Column("PRIMER_APELLIDO")]
    [Required]
    [StringLength(40)]
    public string PrimerApellido { get; set; } = null!;

    [Column("SEGUNDO_APELLIDO")]
    [StringLength(40)]
    public string? SegundoApellido { get; set; }

    [Column("TELEFONO")]
    [Required]
    [StringLength(20)]
    public string Telefono { get; set; } = null!;

    [Column("FECHA_CREACION")]
    public DateTime? FechaCreacion { get; set; } = DateTime.Now;

    [Column("ID_ESTADO")]
    public int? IdEstado { get; set; } = 1;

    [ForeignKey("IdEstado")]
    public virtual Estado? Estado { get; set; }
}

