using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("CONFIGURACION_MONEDA", Schema = "ADM")]
public class ConfiguracionMoneda
{
    [Key]
    [Column("ID_CONFIGURACION")]
    public int IdConfiguracion { get; set; }

    [Column("ID_MONEDA_BASE")]
    public int IdMonedaBase { get; set; }

    [Column("TASA_CAMBIO", TypeName = "decimal(18, 6)")]
    public decimal TasaCambio { get; set; }

    [Column("FECHA_ACTUALIZACION")]
    public DateTime FechaActualizacion { get; set; } = DateTime.Now;

    [Column("USUARIO_MODIFICACION")]
    [StringLength(80)]
    public string? UsuarioModificacion { get; set; }

    [ForeignKey("IdMonedaBase")]
    public virtual Moneda? MonedaBase { get; set; }
}

