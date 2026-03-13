using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Models;

[Table("GOOGLE_TOKENS", Schema = "ADM")]
public class GoogleToken
{
    [Key]
    [Column("ID_TOKEN")]
    public int IdToken { get; set; }

    [Column("ID_USUARIO")]
    public int IdUsuario { get; set; }

    [Column("ACCESS_TOKEN")]
    [StringLength(2000)]
    public string? AccessToken { get; set; }

    [Column("REFRESH_TOKEN")]
    [StringLength(2000)]
    public string? RefreshToken { get; set; }

    [Column("TOKEN_EXPIRY")]
    public DateTime? TokenExpiry { get; set; }

    [Column("FECHA_CREACION")]
    public DateTime? FechaCreacion { get; set; }

    [Column("FECHA_ACTUALIZACION")]
    public DateTime? FechaActualizacion { get; set; }

    // Navigation
    [ForeignKey("IdUsuario")]
    public virtual Usuario? Usuario { get; set; }
}
