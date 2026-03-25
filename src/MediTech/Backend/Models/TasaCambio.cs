using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("TASA_CAMBIO", Schema = "ADM")]
public class TasaCambio
{
    [Key]
    [Column("ID_TASA")]
    public int IdTasa { get; set; }

    [Column("ID_MONEDA_ORIGEN")]
    public int IdMonedaOrigen { get; set; }

    [Column("ID_MONEDA_DESTINO")]
    public int IdMonedaDestino { get; set; }

    [Column("VALOR", TypeName = "decimal(18, 6)")]
    public decimal Valor { get; set; }

    [Column("FECHA")]
    public DateTime Fecha { get; set; } = DateTime.Now;

    [Column("ACTIVO")]
    public bool Activo { get; set; } = true;

    [Column("USUARIO_MODIFICACION")]
    [StringLength(80)]
    public string? UsuarioModificacion { get; set; }

    // Navigation properties
    [ForeignKey("IdMonedaOrigen")]
    public virtual Moneda? MonedaOrigen { get; set; }

    [ForeignKey("IdMonedaDestino")]
    public virtual Moneda? MonedaDestino { get; set; }
}

