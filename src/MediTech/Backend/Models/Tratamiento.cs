using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models;

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


    [Column("DOSIS")]
    [StringLength(100)]
    public string? Dosis { get; set; }

    [Column("VIA_ADMINISTRACION")]
    [StringLength(100)]
    public string? ViaAdministracion { get; set; }

    [Column("FRECUENCIA")]
    [StringLength(100)]
    public string? Frecuencia { get; set; }

    [Column("DURACION_TRATAMIENTO")]
    [StringLength(100)]
    public string? DuracionTratamiento { get; set; }

    [Column("ID_ESTADO")]
    public int? IdEstado { get; set; }

    [Column("FECHA_ACTUALIZACION")]
    public DateTime? FechaActualizacion { get; set; }

    // Navigation properties
    [ForeignKey("IdEstado")]
    public virtual Estado? Estado { get; set; }

}

