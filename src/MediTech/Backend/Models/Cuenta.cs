using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("CUENTAS", Schema = "CAJA")]
public class Cuenta
{
    [Key]
    [Column("ID_CUENTA")]
    public int IdCuenta { get; set; }

    [Column("ID_PACIENTE")]
    public int? IdPaciente { get; set; }

    [Column("ID_CONSULTA")]
    public int? IdConsulta { get; set; }

    [Column("TOTAL_BRUTO", TypeName = "decimal(12,2)")]
    public decimal? TotalBruto { get; set; }

    [Column("DESCUENTO", TypeName = "decimal(12,2)")]
    public decimal Descuento { get; set; } = 0;

    [Column("TOTAL_FINAL", TypeName = "decimal(12,2)")]
    public decimal? TotalFinal { get; set; }

    [Column("ID_MONEDA_BASE")]
    public int? IdMonedaBase { get; set; }

    [Column("FECHA_CREACION")]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // Navigation properties
    [ForeignKey("IdPaciente")]
    public virtual Paciente? Paciente { get; set; }

    [ForeignKey("IdConsulta")]
    public virtual Consulta? Consulta { get; set; }

    [ForeignKey("IdMonedaBase")]
    public virtual Moneda? MonedaBase { get; set; }

    public virtual ICollection<CuentaDetalle> Detalles { get; set; } = new List<CuentaDetalle>();
    public virtual ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

