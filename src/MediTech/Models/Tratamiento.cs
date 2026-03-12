using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Models;

[Table("TRATAMIENTOS", Schema = "CAT")]
public class Tratamiento
{
    [Key]
    [Column("ID_TRATAMIENTO")]
    public int IdTratamiento { get; set; }

    [Column("NOMBRE_TRATAMIENTO")]
    [StringLength(200)]
    public string? NombreTratamiento { get; set; }

    [Column("PRECIO")]
    public decimal? Precio { get; set; }

    [Column("ID_ESTADO")]
    public int? IdEstado { get; set; }

    [ForeignKey("IdEstado")]
    public virtual Estado? Estado { get; set; }
}
