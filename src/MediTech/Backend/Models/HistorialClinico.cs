using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediTech.Backend.Models
{
    [Table("HISTORIAL_CLINICO", Schema = "CLI")]
    public class HistorialClinico
    {
        [Key]
        [Column("ID_HISTORIAL")]
        public int IdHistorial { get; set; }

        [Column("ID_PACIENTE")]
        public int IdPaciente { get; set; }

        // --- Estado de Salud ---
        [Column("ALERGIAS")]
        public bool Alergias { get; set; }

        [Column("ALERGIAS_DETALLE")]
        [StringLength(500)]
        public string? AlergiasDetalle { get; set; }

        [Column("DIABETES")]
        public bool Diabetes { get; set; }

        [Column("TOMA_MEDICAMENTO")]
        public bool TomaMedicamento { get; set; }

        [Column("MEDICAMENTO_DETALLE")]
        [StringLength(500)]
        public string? MedicamentoDetalle { get; set; }

        [Column("HIPERTENSION")]
        public bool Hipertension { get; set; }

        [Column("EMBARAZADA")]
        public bool Embarazada { get; set; }

        [Column("CARDIACOS")]
        public bool Cardiacos { get; set; }

        [Column("ANTECEDENTE_ONCOLOGICO")]
        public bool AntecedenteOncologico { get; set; }

        [Column("OTROS_PADECIMIENTOS")]
        public bool OtrosPadecimientos { get; set; }

        [Column("OTROS_PADECIMIENTOS_DETALLE")]
        [StringLength(500)]
        public string? OtrosPadecimientosDetalle { get; set; }

        // --- Hábitos Personales ---
        [Column("CONSUME_ALCOHOL")]
        public bool ConsumeAlcohol { get; set; }

        [Column("FUMA_CIGARRILLOS")]
        public bool FumaCigarrillos { get; set; }

        [Column("REALIZA_EJERCICIO")]
        public bool RealizaEjercicio { get; set; }


        // --- Antecedentes Estéticos ---
        [Column("CIRUGIAS_ESTETICAS")]
        public bool CirugiasEsteticas { get; set; }

        [Column("CIRUGIAS_ESTETICAS_DETALLE")]
        [StringLength(500)]
        public string? CirugiasEsteticasDetalle { get; set; }

        // --- Metadata ---
        [Column("FECHA_REGISTRO")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [Column("FECHA_ACTUALIZACION")]
        public DateTime? FechaActualizacion { get; set; }

        // Navigation
        [ForeignKey("IdPaciente")]
        public Paciente? Paciente { get; set; }
    }
}

