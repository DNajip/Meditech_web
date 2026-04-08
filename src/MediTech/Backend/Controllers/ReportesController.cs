using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediTech.Backend.Services;
using MediTech.Backend.Models;
using MediTech.Backend.Dtos;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MediTech.Backend.Controllers;

[Authorize]
public class ReportesController(ReportingService reportingService) : Controller
{
    private readonly ReportingService _reportingService = reportingService;

    public async Task<IActionResult> Index()
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        
        // Initial load with current month
        ViewBag.Caja = await _reportingService.GetAuditoriaCaja(inicioMes, hoy);
        ViewBag.Operativo = await _reportingService.GetOperativoCore(inicioMes, hoy);
        ViewBag.Inventario = await _reportingService.GetInventarioControl();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetReportData(DateTime inicio, DateTime fin)
    {
        try
        {
            var caja = await _reportingService.GetAuditoriaCaja(inicio, fin);
            var operativo = await _reportingService.GetOperativoCore(inicio, fin);
            var inventario = await _reportingService.GetInventarioControl();

            return Json(new { success = true, caja, operativo, inventario });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    #region Export Actions

    [HttpGet]
    public async Task<IActionResult> ExportarIngresosExcel(DateTime inicio, DateTime fin)
    {
        var data = await _reportingService.GetAuditoriaCaja(inicio, fin);
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Auditoria Caja");
            worksheet.Cell(1, 1).Value = "AUDITORÍA DE CAJA Y FINANZAS";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(2, 1).Value = $"Periodo: {inicio:dd/MM/yyyy} - {fin:dd/MM/yyyy}";

            worksheet.Cell(4, 1).Value = "Resumen General";
            worksheet.Cell(4, 1).Style.Font.Bold = true;
            worksheet.Cell(5, 1).Value = "Total Ingresos (Base):";
            worksheet.Cell(5, 2).Value = data.TotalIngresosBase;
            worksheet.Cell(5, 2).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(6, 1).Value = "Transacciones:";
            worksheet.Cell(6, 2).Value = data.TotalTransacciones;

            var currentRow = 8;
            worksheet.Cell(currentRow, 1).Value = "Desglose por Método";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            currentRow++;
            worksheet.Cell(currentRow, 1).Value = "Método";
            worksheet.Cell(currentRow, 2).Value = "Monto";
            worksheet.Cell(currentRow, 3).Value = "Cantidad";
            worksheet.Range(currentRow, 1, currentRow, 3).Style.Font.Bold = true;
            worksheet.Range(currentRow, 1, currentRow, 3).Style.Fill.BackgroundColor = XLColor.LightGray;

            foreach (var item in data.DesglosePorMetodo)
            {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = item.Metodo;
                worksheet.Cell(currentRow, 2).Value = item.Total;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "$#,##0.00";
                worksheet.Cell(currentRow, 3).Value = item.Cantidad;
            }

            worksheet.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Auditoria_Caja_{inicio:yyyyMMdd}.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCitasExcel(DateTime inicio, DateTime fin)
    {
        var data = await _reportingService.GetCitasRaw(inicio, fin);
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Agenda");
            worksheet.Cell(1, 1).Value = "CONTROL DE OPERACIONES Y AGENDA";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(2, 1).Value = $"Rango: {inicio:dd/MM/yyyy} - {fin:dd/MM/yyyy}";

            var currentRow = 4;
            string[] headers = { "Fecha", "Hora", "Paciente", "Médico", "Servicio/Tratamiento", "Estado" };
            for (int i = 0; i < headers.Length; i++) {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.AirForceBlue;
                worksheet.Cell(currentRow, i + 1).Style.Font.FontColor = XLColor.White;
            }

            foreach (var item in data) {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = item.Fecha.ToShortDateString();
                worksheet.Cell(currentRow, 2).Value = item.Hora;
                worksheet.Cell(currentRow, 3).Value = item.Paciente;
                worksheet.Cell(currentRow, 4).Value = item.Medico;
                worksheet.Cell(currentRow, 5).Value = item.Tratamiento;
                worksheet.Cell(currentRow, 6).Value = item.Estado;
            }

            worksheet.Columns().AdjustToContents();
            using (var stream = new MemoryStream()) {
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Reporte_Operativo.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarInventarioExcel()
    {
        var data = await _reportingService.GetInventarioRaw();
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Control Activos");
            worksheet.Cell(1, 1).Value = "VALORIZACIÓN Y CONTROL DE ACTIVOS";
            worksheet.Cell(1, 1).Style.Font.Bold = true;

            var currentRow = 3;
            string[] headers = { "Producto", "Stock Actual", "Stock Mínimo", "Precio", "Estado" };
            for (int i = 0; i < headers.Length; i++) {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.DarkOliveGreen;
                worksheet.Cell(currentRow, i + 1).Style.Font.FontColor = XLColor.White;
            }

            foreach (var item in data) {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = item.Nombre;
                worksheet.Cell(currentRow, 2).Value = item.Stock;
                worksheet.Cell(currentRow, 3).Value = item.StockMinimo;
                worksheet.Cell(currentRow, 4).Value = item.Precio;
                worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "$#,##0.00";
                worksheet.Cell(currentRow, 5).Value = item.Estado;
            }

            worksheet.Columns().AdjustToContents();
            using (var stream = new MemoryStream()) {
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Control_Inventario.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCierrePDF(int idTurno)
    {
        var result = await _reportingService.GetCierreCajaData(idTurno);
        if (result == null) return NotFound();

        TurnoCaja turno = result.Turno;
        List<Pago> pagos = result.Pagos;
        decimal totalRecaudadoBase = result.TotalRecaudadoBase;
        decimal diferencia = result.Diferencia;
        string username = turno.Usuario?.Username ?? "N/A";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.Header().Row(row => {
                    row.RelativeItem().Column(col => {
                        col.Item().Text("MediTech Solutions").FontSize(24).SemiBold().FontColor(Colors.Indigo.Medium);
                        col.Item().Text("Reporte Profesional de Cierre de Caja").FontSize(12).Italic();
                    });
                    row.RelativeItem().AlignRight().Column(col => {
                        col.Item().Text($"Cierre N°: {idTurno}").Bold();
                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
                    });
                });
                
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(20);
                    
                    // Resumen General
                    col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c => {
                        c.Item().Text("RESUMEN DEL TURNO").Bold();
                        c.Item().Row(r => {
                            r.RelativeItem().Text($"Cajero: {username}");
                            r.RelativeItem().Text($"Apertura: {turno.FechaApertura:dd/MM HH:mm}");
                            r.RelativeItem().Text($"Cierre: {turno.FechaCierre:dd/MM HH:mm}");
                        });
                    });

                    // Tabla de Balances
                    col.Item().Table(table => {
                        table.ColumnsDefinition(columns => {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header => {
                            header.Cell().Text("CONCEPTO").Bold();
                            header.Cell().AlignRight().Text("MONTO (BASE)").Bold();
                        });

                        table.Cell().Text("Monto Inicial");
                        table.Cell().AlignRight().Text($"{turno.MontoInicial:C2}");

                        table.Cell().Text("Ventas Registradas");
                        table.Cell().AlignRight().Text($"{totalRecaudadoBase:C2}");

                        table.Cell().Text("Total Esperado (Sistema)");
                        table.Cell().AlignRight().Text($"{turno.MontoFinalSistema:C2}");

                        table.Cell().BorderTop(1).Text("Monto Reportado (Real)").Bold();
                        table.Cell().BorderTop(1).AlignRight().Text($"{turno.MontoFinalReal:C2}").Bold();

                        table.Cell().Text("Diferencia / Desfase").FontColor(diferencia < 0 ? Colors.Red.Medium : Colors.Green.Medium);
                        table.Cell().AlignRight().Text($"{diferencia:C2}").FontColor(diferencia < 0 ? Colors.Red.Medium : Colors.Green.Medium);
                    });

                    col.Item().PaddingTop(10).Text("DETALLE DE PAGOS RECIBIDOS").FontSize(14).SemiBold();

                    // Lista de Pagos
                    col.Item().Table(table => {
                        table.ColumnsDefinition(columns => {
                            columns.ConstantColumn(80);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header => {
                            header.Cell().Text("F. PAGO").Bold();
                            header.Cell().Text("PACIENTE").Bold();
                            header.Cell().Text("MÉTODO").Bold();
                            header.Cell().AlignRight().Text("MONTO").Bold();
                        });

                        foreach (var p in pagos) {
                            table.Cell().Text(p.FechaPago.ToString("dd/MM HH:mm"));
                            table.Cell().Text(p.Cuenta?.Paciente?.Persona?.PrimerNombre + " " + p.Cuenta?.Paciente?.Persona?.PrimerApellido);
                            table.Cell().Text(p.MetodoPago);
                            table.Cell().AlignRight().Text($"{p.MontoBase:C2}");
                        }
                    });

                    if (!string.IsNullOrEmpty(turno.Observaciones)) {
                        col.Item().Column(c => {
                            c.Item().Text("Observaciones:").Bold();
                            c.Item().Text(turno.Observaciones);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x => {
                    x.Span("MediTech v2.0 - Reporte de Auditoría Interna - Página ");
                    x.CurrentPageNumber();
                });
            });
        });

        return File(document.GeneratePdf(), "application/pdf", $"Cierre_Caja_{idTurno}.pdf");
    }

    #endregion
}
