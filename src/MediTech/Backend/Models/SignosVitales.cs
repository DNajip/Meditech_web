using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("SIGNOS_VITALES", Schema = "CLI")]
    public class SignosVitales
    {
        [Key]
        [Column("ID_SIGNOS")]
        public int IdSignos { get; set; }

        [Column("ID_CONSULTA")]
        public int IdConsulta { get; set; }

        [Column("PRESION_ARTERIAL")]
        [StringLength(15)]
        public string? PresionArterial { get; set; }

        [Column("TEMPERATURA")]
        public decimal? Temperatura { get; set; }

        [Column("FRECUENCIA_CARDIACA")]
        public int? FrecuenciaCardiaca { get; set; }

        [Column("SATURACION_OXIGENO")]
        public int? SaturacionOxigeno { get; set; }

        [Column("PESO")]
        public decimal? Peso { get; set; }

        [Column("ALTURA")]
        public decimal? Altura { get; set; }

        [ForeignKey("IdConsulta")]
        public virtual Consulta? Consulta { get; set; }

        // Calculado
        [NotMapped]
        public decimal? BMI 
        { 
            get 
            {
                if (Peso > 0 && Altura > 0)
                {
                    // Altura en metros para la formula
                    decimal alturaMetros = Altura.Value > 3 ? Altura.Value / 100 : Altura.Value;
                    return Peso.Value / (alturaMetros * alturaMetros);
                }
                return null;
            }
        }
    }
}

