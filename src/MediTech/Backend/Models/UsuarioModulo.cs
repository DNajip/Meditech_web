using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("USUARIO_MODULOS", Schema = "ADM")]
    public class UsuarioModulo
    {
        [Column("ID_USUARIO")]
        public int IdUsuario { get; set; }

        [Column("ID_MODULO")]
        public int IdModulo { get; set; }

        [ForeignKey("IdUsuario")]
        public virtual Usuario? Usuario { get; set; }

        [ForeignKey("IdModulo")]
        public virtual Modulo? Modulo { get; set; }
    }
}
