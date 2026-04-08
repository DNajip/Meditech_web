using System;
using System.Collections.Generic;

namespace MediTech.Backend.Dtos
{
    // Pilar 1: Financiero
    public class CajaAuditoriaDto
    {
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }
        public decimal TotalIngresosBase { get; set; }
        public int TotalTransacciones { get; set; }
        public decimal TotalDiferencias { get; set; }
        public List<MetodoPagoSummaryDto> DesglosePorMetodo { get; set; } = new();
        public List<TurnoSummaryDto> TurnosRecientes { get; set; } = new();
    }

    public class MetodoPagoSummaryDto
    {
        public string Metodo { get; set; } = "";
        public decimal Total { get; set; }
        public int Cantidad { get; set; }
        public double Porcentaje { get; set; }
    }

    public class TurnoSummaryDto
    {
        public int IdTurno { get; set; }
        public string Usuario { get; set; } = "";
        public DateTime Apertura { get; set; }
        public DateTime? Cierre { get; set; }
        public decimal MontoSistema { get; set; }
        public decimal MontoReal { get; set; }
        public decimal Diferencia { get; set; }
    }

    // Pilar 2: Operativo
    public class OperativoCoreDto
    {
        public int TotalCitas { get; set; }
        public int CitasAtendidas { get; set; }
        public int CitasCanceladas { get; set; }
        public double TasaNoShow { get; set; }
        public int ProspectosConvertidos { get; set; }
        public List<ProductividadMedicoDto> RendimientoMedicos { get; set; } = new();
        public List<EstatusCitaSummaryDto> DistribucionEstados { get; set; } = new();
    }

    public class ProductividadMedicoDto
    {
        public int IdMedico { get; set; }
        public string NombreMedico { get; set; } = "";
        public int CitasAtendidas { get; set; }
        public decimal IngresosGenerados { get; set; }
        public double Ocupacion { get; set; }
    }

    public class EstatusCitaSummaryDto
    {
        public string Estado { get; set; } = "";
        public int Cantidad { get; set; }
        public string Color { get; set; } = "";
    }

    // Pilar 3: Inventario y Ventas
    public class InventarioControlDto
    {
        public decimal ValorTotalStock { get; set; }
        public int ItemsCriticos { get; set; }
        public int TotalReferencias { get; set; }
        public List<TopServicioDto> TratamientosMasVendidos { get; set; } = new();
        public List<ProductoBajoStockDto> AlertasStock { get; set; } = new();
    }

    public class TopServicioDto
    {
        public string Nombre { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal TotalIngresos { get; set; }
    }

    public class ProductoBajoStockDto
    {
        public int IdProducto { get; set; }
        public string Nombre { get; set; } = "";
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
    }
}
