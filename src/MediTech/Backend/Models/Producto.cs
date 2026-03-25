using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("PRODUCTOS", Schema = "INV")]
    public class Producto
    {
        [Key]
        [Column("ID_PRODUCTO")]
        public int IdProducto { get; set; }

        [Column("NOMBRE")]
        [Required(ErrorMessage = "El nombre del producto es obligatorio")]
        [StringLength(120)]
        public string Nombre { get; set; } = null!;

        [Column("DESCRIPCION")]
        [StringLength(300)]
        public string? Descripcion { get; set; }

        [Column("PRECIO")]
        public decimal? Precio { get; set; }


        [Column("STOCK")]
        public int Stock { get; set; } = 0;

        [Column("STOCK_MINIMO")]
        public int StockMinimo { get; set; } = 0;

        [Column("ACTIVO")]
        public bool Activo { get; set; } = true;

        [Column("FECHA_CREACION")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Navigation properties

        public virtual ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    }
}

