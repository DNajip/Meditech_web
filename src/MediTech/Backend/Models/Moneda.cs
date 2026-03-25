using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

[Table("MONEDAS", Schema = "CAT")]
public class Moneda
{
    [Key]
    [Column("ID_MONEDA")]
    public int IdMoneda { get; set; }

    [Column("CODIGO")]
    [Required]
    [StringLength(10)]
    public string Codigo { get; set; } = null!;

    [Column("NOMBRE")]
    [Required]
    [StringLength(50)]
    public string Nombre { get; set; } = null!;

    [Column("SIMBOLO")]
    [StringLength(5)]
    public string? Simbolo { get; set; }
}

