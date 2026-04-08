using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using MediTech.Backend.Dtos;

namespace MediTech.Backend.Services;

public class ReportingService(MediTechContext context)
{
    private readonly MediTechContext _context = context;

    #region Pilar 1: Auditoría Financiera

    public async Task<CajaAuditoriaDto> GetAuditoriaCaja(DateTime inicio, DateTime fin)
    {
        var pagos = await _context.Pagos
            .Include(p => p.Moneda)
            .Where(p => p.FechaPago.Date >= inicio.Date && p.FechaPago.Date <= fin.Date)
            .ToListAsync();

        var turnos = await _context.TurnosCaja
            .Include(t => t.Usuario)
            .Where(t => t.FechaApertura.Date >= inicio.Date && (t.FechaCierre == null || (t.FechaCierre.HasValue && t.FechaCierre.Value.Date <= fin.Date)))
            .OrderByDescending(t => t.FechaApertura)
            .Take(10)
            .ToListAsync();

        var totalIngresos = pagos.Sum(p => p.MontoBase ?? 0);
        
        var desglose = pagos.GroupBy(p => p.MetodoPago ?? "OTROS")
            .Select(g => new MetodoPagoSummaryDto
            {
                Metodo = g.Key,
                Total = g.Sum(p => p.MontoBase ?? 0),
                Cantidad = g.Count(),
                Porcentaje = totalIngresos > 0 ? (double)(g.Sum(p => p.MontoBase ?? 0) / totalIngresos * 100) : 0
            }).ToList();

        return new CajaAuditoriaDto
        {
            Inicio = inicio,
            Fin = fin,
            TotalIngresosBase = totalIngresos,
            TotalTransacciones = pagos.Count,
            TotalDiferencias = turnos.Sum(t => t.Diferencia ?? 0),
            DesglosePorMetodo = desglose,
            TurnosRecientes = turnos.Select(t => new TurnoSummaryDto 
            {
                IdTurno = t.IdTurno,
                Usuario = t.Usuario?.Username ?? "N/A",
                Apertura = t.FechaApertura,
                Cierre = t.FechaCierre,
                MontoSistema = t.MontoFinalSistema ?? 0,
                MontoReal = t.MontoFinalReal ?? 0,
                Diferencia = t.Diferencia ?? 0
            }).ToList()
        };
    }

    public async Task<dynamic> GetCierreCajaData(int idTurno)
    {
        var turno = await _context.TurnosCaja
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.IdTurno == idTurno);

        if (turno == null) return null!;

        var pagos = await _context.Pagos
            .Include(p => p.Moneda)
            .Include(p => p.Cuenta).ThenInclude(c => c!.Paciente).ThenInclude(p => p!.Persona)
            .Where(p => p.IdUsuario == turno.IdUsuario && p.FechaPago >= turno.FechaApertura && (turno.FechaCierre == null || p.FechaPago <= turno.FechaCierre))
            .ToListAsync();

        return new {
            Turno = turno,
            Pagos = pagos,
            TotalRecaudadoBase = pagos.Sum(p => p.MontoBase ?? 0),
            Diferencia = turno.Diferencia ?? 0
        };
    }

    #endregion

    #region Pilar 2: Operativo Core

    public async Task<OperativoCoreDto> GetOperativoCore(DateTime inicio, DateTime fin)
    {
        var citas = await _context.Citas
            .Include(c => c.EstadoCita)
            .Include(c => c.Medico).ThenInclude(m => m!.Persona)
            .Where(c => c.Fecha >= inicio.Date && c.Fecha <= fin.Date)
            .ToListAsync();

        var atendidas = citas.Count(c => c.IdEstadoCita == 2); // 2: Atendida
        var canceladas = citas.Count(c => c.IdEstadoCita == 4); // 4: Cancelada
        
        var rendimientoMedicos = citas
            .Where(c => c.IdMedico != null)
            .GroupBy(c => new { c.IdMedico, Nombre = $"Dr. {c.Medico?.Persona?.PrimerNombre} {c.Medico?.Persona?.PrimerApellido}" })
            .Select(g => new ProductividadMedicoDto
            {
                IdMedico = g.Key.IdMedico!.Value,
                NombreMedico = g.Key.Nombre,
                CitasAtendidas = g.Count(c => c.IdEstadoCita == 2),
                IngresosGenerados = 0 
            }).OrderByDescending(m => m.CitasAtendidas).ToList();

        var distribucion = citas.GroupBy(c => c.EstadoCita?.DescEstadoCita ?? "Indefinido")
            .Select(g => new EstatusCitaSummaryDto
            {
                Estado = g.Key,
                Cantidad = g.Count(),
                Color = GetColorForEstado(g.Key)
            }).ToList();

        return new OperativoCoreDto
        {
            TotalCitas = citas.Count,
            CitasAtendidas = atendidas,
            CitasCanceladas = canceladas,
            TasaNoShow = citas.Count > 0 ? (double)canceladas / citas.Count * 100 : 0,
            ProspectosConvertidos = await _context.Pacientes.CountAsync(p => p.FechaRegistro != null && p.FechaRegistro.Value.Date >= inicio.Date && p.FechaRegistro.Value.Date <= fin.Date),
            RendimientoMedicos = rendimientoMedicos,
            DistribucionEstados = distribucion
        };
    }

    private string GetColorForEstado(string estado) => estado.ToUpper() switch
    {
        "PROGRAMADA" => "#3B82F6",
        "ATENDIDA" => "#10B981",
        "CANCELADA" => "#EF4444",
        _ => "#94A3B8"
    };

    #endregion

    #region Pilar 3: Control de Activos (Inventario)

    public async Task<InventarioControlDto> GetInventarioControl()
    {
        var productos = await _context.Productos.Where(p => p.Activo).ToListAsync();
        
        var topServicios = await _context.Citas
            .Include(c => c.Tratamiento)
            .Where(c => c.IdEstadoCita == 2)
            .GroupBy(c => c.Tratamiento!.NombreTratamiento)
            .Select(g => new TopServicioDto
            {
                Nombre = g.Key,
                Cantidad = g.Count(),
                TotalIngresos = 0 
            })
            .OrderByDescending(s => s.Cantidad)
            .Take(5)
            .ToListAsync();

        return new InventarioControlDto
        {
            ValorTotalStock = productos.Sum(p => (decimal)p.Stock * (p.Precio ?? 0)),
            ItemsCriticos = productos.Count(p => p.Stock <= p.StockMinimo),
            TotalReferencias = productos.Count,
            TratamientosMasVendidos = topServicios,
            AlertasStock = productos.Where(p => p.Stock <= p.StockMinimo)
                .Select(p => new ProductoBajoStockDto
                {
                    IdProducto = p.IdProducto,
                    Nombre = p.Nombre,
                    StockActual = p.Stock,
                    StockMinimo = p.StockMinimo
                }).ToList()
        };
    }

    #endregion

    #region Raw Exports (Excel compatible)

    public async Task<List<CitaExportDto>> GetCitasRaw(DateTime inicio, DateTime fin)
    {
        var citas = await _context.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Persona)
            .Include(c => c.PosiblePaciente)
            .Include(c => c.Tratamiento)
            .Include(c => c.Medico).ThenInclude(m => m!.Persona)
            .Include(c => c.EstadoCita)
            .Where(c => c.Fecha >= inicio.Date && c.Fecha <= fin.Date)
            .OrderBy(c => c.Fecha).ThenBy(c => c.HoraInicio)
            .ToListAsync();

        return citas.Select(c => new CitaExportDto
        {
            Fecha = c.Fecha,
            Hora = DateTime.Today.Add(c.HoraInicio).ToString("hh:mm tt"),
            Paciente = c.IdPaciente != null 
                ? $"{c.Paciente?.Persona?.PrimerNombre} {c.Paciente?.Persona?.PrimerApellido}"
                : $"[PROSPECTO] {c.PosiblePaciente?.PrimerNombre} {c.PosiblePaciente?.PrimerApellido}",
            Medico = c.IdMedico != null 
                ? $"Dr. {c.Medico?.Persona?.PrimerNombre} {c.Medico?.Persona?.PrimerApellido}"
                : "Sin asignar",
            Tratamiento = c.Tratamiento?.NombreTratamiento ?? "General",
            Estado = c.EstadoCita?.DescEstadoCita ?? "Programada"
        }).ToList();
    }

    public async Task<List<PacienteExportDto>> GetPacientesRaw()
    {
        var pacientes = await _context.Pacientes
            .Include(p => p.Persona).ThenInclude(per => per!.Genero)
            .Include(p => p.Persona).ThenInclude(per => per!.TipoIdentificacion)
            .Where(p => p.IdEstado == 1)
            .OrderBy(p => p.Persona!.PrimerNombre)
            .ToListAsync();

        return pacientes.Select(p => new PacienteExportDto
        {
            NombreCompleto = $"{p.Persona?.PrimerNombre} {p.Persona?.SegundoNombre} {p.Persona?.PrimerApellido} {p.Persona?.SegundoApellido}".Replace("  ", " ").Trim(),
            Identificacion = p.Persona?.NumIdentificacion,
            TipoId = p.Persona?.TipoIdentificacion?.DescTipo,
            Genero = p.Persona?.Genero?.DescGenero,
            Telefono = p.Persona?.Telefono,
            Email = p.Persona?.Email,
            FechaNacimiento = p.Persona?.FechaNacimiento,
            FechaRegistro = p.FechaRegistro
        }).ToList();
    }

    public async Task<List<InventarioExportDto>> GetInventarioRaw()
    {
        var productos = await _context.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        return productos.Select(p => new InventarioExportDto
        {
            Nombre = p.Nombre,
            Precio = p.Precio ?? 0,
            Stock = p.Stock,
            StockMinimo = p.StockMinimo,
            Estado = p.Stock <= p.StockMinimo ? "STOCK BAJO" : "OK"
        }).ToList();
    }

    #endregion
}

public class CitaExportDto {
    public DateTime Fecha { get; set; }
    public string Hora { get; set; } = "";
    public string Paciente { get; set; } = "";
    public string Medico { get; set; } = "";
    public string Tratamiento { get; set; } = "";
    public string Estado { get; set; } = "";
}

public class PacienteExportDto {
    public string NombreCompleto { get; set; } = "";
    public string? Identificacion { get; set; }
    public string? TipoId { get; set; }
    public string? Genero { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public DateTime? FechaRegistro { get; set; }
}

public class InventarioExportDto {
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public string Estado { get; set; } = "";
}
