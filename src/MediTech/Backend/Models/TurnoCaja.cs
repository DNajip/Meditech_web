using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("TURNOS_CAJA", Schema = "ADM")]
public class TurnoCaja
{
    [Key]
    [Column("ID_TURNO")]
    public int IdTurno { get; set; }

    [Column("ID_USUARIO")]
    public int IdUsuario { get; set; }

    [Column("FECHA_APERTURA")]
    public DateTime FechaApertura { get; set; }

    [Column("FECHA_CIERRE")]
    public DateTime? FechaCierre { get; set; }

    [Column("MONTO_INICIAL")]
    public decimal MontoInicial { get; set; }

    [Column("MONTO_FINAL_SISTEMA")]
    public decimal? MontoFinalSistema { get; set; }

    [Column("MONTO_FINAL_REAL")]
    public decimal? MontoFinalReal { get; set; }

    [Column("DIFERENCIA")]
    public decimal? Diferencia { get; set; }

    [Column("OBSERVACIONES")]
    [StringLength(500)]
    public string? Observaciones { get; set; }

    [Column("ID_ESTADO")]
    public int IdEstado { get; set; } = 1; // 1: ABIERTO, 2: CERRADO

    // Navigation properties
    [ForeignKey("IdUsuario")]
    public virtual Usuario? Usuario { get; set; }
}
