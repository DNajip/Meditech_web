using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("PAGOS", Schema = "CAJA")]
public class Pago
{
    [Key]
    [Column("ID_PAGO")]
    public int IdPago { get; set; }

    [Column("ID_CUENTA")]
    public int IdCuenta { get; set; }

    [Column("MONTO", TypeName = "decimal(12,2)")]
    public decimal? Monto { get; set; }

    [Column("MONTO_BASE", TypeName = "decimal(12,2)")]
    public decimal? MontoBase { get; set; }

    [Column("ID_MONEDA")]
    public int? IdMoneda { get; set; }

    [Column("METODO_PAGO")]
    [StringLength(30)]
    public string? MetodoPago { get; set; } // EFECTIVO, TARJETA, TRANSFERENCIA

    [Column("MONTO_RECIBIDO", TypeName = "decimal(12,2)")]
    public decimal? MontoRecibido { get; set; }

    [Column("VUELTO", TypeName = "decimal(12,2)")]
    public decimal? Vuelto { get; set; }

    [Column("TASA_CAMBIO_APLICADA", TypeName = "decimal(18, 2)")]
    public decimal? TasaCambioAplicada { get; set; }

    [Column("FECHA_PAGO")]
    public DateTime FechaPago { get; set; } = DateTime.Now;

    // Navigation
    [ForeignKey("IdCuenta")]
    public virtual Cuenta? Cuenta { get; set; }

    [ForeignKey("IdMoneda")]
    public virtual Moneda? Moneda { get; set; }
}

