using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediTech.Backend.Services;
using MediTech.Backend.Models;
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
        
        ViewBag.StatsCitas = await _reportingService.GetEstadisticasCitas(inicioMes, hoy);
        ViewBag.IngresosMes = await _reportingService.GetIngresosPeriodo(inicioMes, hoy);
        ViewBag.StockBajo = await _reportingService.GetProductosBajoStock();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardStats(DateTime? inicio, DateTime? fin)
    {
        var start = inicio ?? DateTime.Today.AddDays(-30);
        var end = fin ?? DateTime.Today;

        var citas = await _reportingService.GetEstadisticasCitas(start, end);
        var ingresos = await _reportingService.GetIngresosPeriodo(start, end);

        return Json(new { success = true, citas, ingresos });
    }

    #region Export Actions

    [HttpGet]
    public async Task<IActionResult> ExportarIngresosExcel(DateTime inicio, DateTime fin)
    {
        var data = await _reportingService.GetIngresosPeriodo(inicio, fin);
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Ingresos");
            worksheet.Cell(1, 1).Value = "REPORTE DE INGRESOS";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(2, 1).Value = $"Periodo: {inicio:dd/MM/yyyy} - {fin:dd/MM/yyyy}";

            var currentRow = 4;
            worksheet.Cell(currentRow, 1).Value = "Fecha";
            worksheet.Cell(currentRow, 2).Value = "Monto Total (Base)";
            worksheet.Range(currentRow, 1, currentRow, 2).Style.Font.Bold = true;
            worksheet.Range(currentRow, 1, currentRow, 2).Style.Fill.BackgroundColor = XLColor.LightGray;

            var detalle = (List<dynamic>)data.DetallePorDia;
            foreach (var item in detalle)
            {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = item.Fecha.ToShortDateString();
                worksheet.Cell(currentRow, 2).Value = item.Total;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "$#,##0.00";
            }

            currentRow += 2;
            worksheet.Cell(currentRow, 1).Value = "TOTAL GLOBAL:";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Value = data.TotalGlobal;
            worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "$#,##0.00";

            worksheet.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Reporte_Ingresos_{inicio:yyyyMMdd}.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCitasExcel(DateTime inicio, DateTime fin)
    {
        var data = await _reportingService.GetCitasRaw(inicio, fin);
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Citas");
            worksheet.Cell(1, 1).Value = "CONTROL DE CITAS MÉDICAS";
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
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Reporte_Citas.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarPacientesExcel()
    {
        var data = await _reportingService.GetPacientesRaw();
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Pacientes");
            worksheet.Cell(1, 1).Value = "DIRECTORIO DE PACIENTES";
            worksheet.Cell(1, 1).Style.Font.Bold = true;

            var currentRow = 3;
            string[] headers = { "Nombre Completo", "Identificación", "Tipo", "Género", "Teléfono", "Email", "F. Nacimiento", "F. Registro" };
            for (int i = 0; i < headers.Length; i++) {
                worksheet.Cell(currentRow, i + 1).Value = headers[i];
                worksheet.Cell(currentRow, i + 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, i + 1).Style.Fill.BackgroundColor = XLColor.DavysGrey;
                worksheet.Cell(currentRow, i + 1).Style.Font.FontColor = XLColor.White;
            }

            foreach (var item in data) {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = item.NombreCompleto;
                worksheet.Cell(currentRow, 2).Value = item.Identificacion;
                worksheet.Cell(currentRow, 3).Value = item.TipoId;
                worksheet.Cell(currentRow, 4).Value = item.Genero;
                worksheet.Cell(currentRow, 5).Value = item.Telefono;
                worksheet.Cell(currentRow, 6).Value = item.Email;
                worksheet.Cell(currentRow, 7).Value = item.FechaNacimiento?.ToShortDateString();
                worksheet.Cell(currentRow, 8).Value = item.FechaRegistro?.ToShortDateString();
            }

            worksheet.Columns().AdjustToContents();
            using (var stream = new MemoryStream()) {
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Directorio_Pacientes.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarInventarioExcel()
    {
        var data = await _reportingService.GetInventarioRaw();
        
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Inventario");
            worksheet.Cell(1, 1).Value = "ESTADO DE INVENTARIO";
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
                if (item.Estado == "STOCK BAJO") worksheet.Cell(currentRow, 5).Style.Font.FontColor = XLColor.Red;
            }

            worksheet.Columns().AdjustToContents();
            using (var stream = new MemoryStream()) {
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Estado_Inventario.xlsx");
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCierrePDF(int idTurno)
    {
        var result = await _reportingService.GetCierreCajaData(idTurno);
        if (result == null) return NotFound();

        // Cast explicitly to avoid dynamic dispatch issues in QuestPDF lambdas
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
