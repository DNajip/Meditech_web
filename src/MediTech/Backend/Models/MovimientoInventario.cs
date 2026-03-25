using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("MOVIMIENTOS_INVENTARIO", Schema = "INV")]
    public class MovimientoInventario
    {
        [Key]
        [Column("ID_MOVIMIENTO")]
        public int IdMovimiento { get; set; }

        [Column("ID_PRODUCTO")]
        public int IdProducto { get; set; }

        [Column("TIPO_MOVIMIENTO")]
        [StringLength(20)]
        public string TipoMovimiento { get; set; } = null!; // COMPRA, VENTA, AJUSTE, INGRESO, AJUSTE_POS, AJUSTE_NEG

        [Column("CANTIDAD")]
        public int Cantidad { get; set; }

        [Column("FECHA_MOVIMIENTO")]
        public DateTime FechaMovimiento { get; set; } = DateTime.Now;

        [Column("OBSERVACION")]
        [StringLength(200)]
        public string? Observacion { get; set; }

        // Navigation properties
        [ForeignKey("IdProducto")]
        public virtual Producto? Producto { get; set; }
    }
}

