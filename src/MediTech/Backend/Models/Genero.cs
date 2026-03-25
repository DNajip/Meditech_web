using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("GENEROS", Schema = "CAT")]
    public class Genero
    {
        [Key]
        [Column("ID_GENERO")]
        public int IdGenero { get; set; }

        [Column("DESC_GENERO")]
        [StringLength(20)]
        public string? DescGenero { get; set; }

        [Column("ID_ESTADO")]
        public int? IdEstado { get; set; }

        [ForeignKey("IdEstado")]
        public Estado? Estado { get; set; }
    }
}

