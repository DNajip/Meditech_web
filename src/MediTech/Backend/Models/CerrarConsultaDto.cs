using System.Collections.Generic;

namespace MediTech.Backend.Models
{
    public class CerrarConsultaDto
    {
        public int IdConsulta { get; set; }
        public string? DiagnosticoFinal { get; set; }
        public string? Observaciones { get; set; }
        public List<ItemCierreDto> Items { get; set; } = new();
    }

    public class ItemCierreDto
    {
        public string TipoItem { get; set; } = null!; // "TRATAMIENTO" o "PRODUCTO"
        public int IdReferencia { get; set; } // IdTratamiento o IdProducto
        public string Descripcion { get; set; } = null!;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}

