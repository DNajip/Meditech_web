using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("CUENTA_DETALLE", Schema = "CAJA")]
public class CuentaDetalle
{
    [Key]
    [Column("ID_DETALLE")]
    public int IdDetalle { get; set; }

    [Column("ID_CUENTA")]
    public int IdCuenta { get; set; }

    [Column("TIPO_ITEM")]
    [StringLength(20)]
    public string? TipoItem { get; set; } // TRATAMIENTO / PRODUCTO

    [Column("ID_REFERENCIA")]
    public int? IdReferencia { get; set; }

    [Column("DESCRIPCION")]
    [StringLength(200)]
    public string? Descripcion { get; set; }

    [Column("CANTIDAD")]
    public int? Cantidad { get; set; }

    [Column("PRECIO_UNITARIO", TypeName = "decimal(12,2)")]
    public decimal? PrecioUnitario { get; set; }

    [Column("SUBTOTAL", TypeName = "decimal(12,2)")]
    public decimal? Subtotal { get; set; }

    // Navigation
    [ForeignKey("IdCuenta")]
    public virtual Cuenta? Cuenta { get; set; }
}

