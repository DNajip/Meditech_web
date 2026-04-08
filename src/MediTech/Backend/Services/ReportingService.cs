using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using System.Dynamic;

namespace MediTech.Backend.Services;

public class ReportingService(MediTechContext context)
{
    private readonly MediTechContext _context = context;

    #region Financial Reports
    
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

        var resumenMetodos = pagos.GroupBy(p => p.MetodoPago)
            .Select(g => { dynamic d = new ExpandoObject(); d.Metodo = g.Key; d.TotalBase = g.Sum(p => p.MontoBase ?? 0); return d; })
            .ToList();

        dynamic result = new ExpandoObject();
        result.Turno = turno;
        result.Pagos = pagos;
        result.ResumenMetodos = resumenMetodos;
        result.TotalRecaudadoBase = pagos.Sum(p => p.MontoBase ?? 0);
        result.Diferencia = turno.Diferencia ?? 0;
        return result;
    }

    public async Task<dynamic> GetIngresosPeriodo(DateTime inicio, DateTime fin)
    {
        var pagos = await _context.Pagos
            .Include(p => p.Moneda)
            .Where(p => p.FechaPago.Date >= inicio.Date && p.FechaPago.Date <= fin.Date)
            .ToListAsync();

        var porDia = pagos.GroupBy(p => p.FechaPago.Date)
            .Select(g => { dynamic d = new ExpandoObject(); d.Fecha = g.Key; d.Total = g.Sum(p => p.MontoBase ?? 0); return d; })
            .OrderBy(x => ((dynamic)x).Fecha)
            .ToList();

        dynamic result = new ExpandoObject();
        result.TotalGlobal = pagos.Sum(p => p.MontoBase ?? 0);
        result.DetallePorDia = porDia;
        result.ConteoPagos = pagos.Count;
        return result;
    }

    #endregion

    #region Management Reports

    public async Task<dynamic> GetEstadisticasCitas(DateTime inicio, DateTime fin)
    {
        var citas = await _context.Citas
            .Include(c => c.Estado)
            .Include(c => c.Medico).ThenInclude(m => m!.Persona)
            .Where(c => c.Fecha >= inicio.Date && c.Fecha <= fin.Date)
            .ToListAsync();

        var porEstado = citas.GroupBy(c => c.Estado?.DescEstado ?? "Indefinido")
            .Select(g => { dynamic d = new ExpandoObject(); d.Estado = g.Key; d.Cantidad = g.Count(); return d; })
            .ToList();

        var porMedico = citas.Where(c => c.IdMedico != null)
            .GroupBy(c => $"Dr. {c.Medico?.Persona?.PrimerNombre} {c.Medico?.Persona?.PrimerApellido}")
            .Select(g => { dynamic d = new ExpandoObject(); d.Medico = g.Key; d.Cantidad = g.Count(); return d; })
            .OrderByDescending(x => ((dynamic)x).Cantidad)
            .ToList();

        dynamic result = new ExpandoObject();
        result.TotalCitas = citas.Count;
        result.PorEstado = porEstado;
        result.PorMedico = porMedico;
        return result;
    }

    #endregion

    #region Inventory Reports

    public async Task<dynamic> GetProductosBajoStock(int umbral = 10)
    {
        var productos = await _context.Productos
            .Where(p => p.Activo && p.Stock <= umbral)
            .OrderBy(p => p.Stock)
            .ToListAsync();

        dynamic result = new ExpandoObject();
        result.Umbral = umbral;
        result.Productos = productos;
        result.TotalCriticos = productos.Count;
        return result;
    }

    public async Task<List<dynamic>> GetCitasRaw(DateTime inicio, DateTime fin)
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

        return citas.Select(c => {
            dynamic d = new ExpandoObject();
            d.Fecha = c.Fecha;
            d.Hora = DateTime.Today.Add(c.HoraInicio).ToString("hh:mm tt");
            d.Paciente = c.IdPaciente != null 
                ? $"{c.Paciente?.Persona?.PrimerNombre} {c.Paciente?.Persona?.PrimerApellido}"
                : $"[PROSPECTO] {c.PosiblePaciente?.PrimerNombre} {c.PosiblePaciente?.PrimerApellido}";
            d.Medico = c.IdMedico != null 
                ? $"Dr. {c.Medico?.Persona?.PrimerNombre} {c.Medico?.Persona?.PrimerApellido}"
                : "Sin asignar";
            d.Tratamiento = c.Tratamiento?.NombreTratamiento ?? "General";
            d.Estado = c.EstadoCita?.DescEstadoCita ?? "Programada";
            return d;
        }).ToList();
    }

    public async Task<List<dynamic>> GetPacientesRaw()
    {
        var pacientes = await _context.Pacientes
            .Include(p => p.Persona).ThenInclude(per => per!.Genero)
            .Include(p => p.Persona).ThenInclude(per => per!.TipoIdentificacion)
            .Where(p => p.IdEstado == 1)
            .OrderBy(p => p.Persona!.PrimerNombre)
            .ToListAsync();

        return pacientes.Select(p => {
            dynamic d = new ExpandoObject();
            d.NombreCompleto = $"{p.Persona?.PrimerNombre} {p.Persona?.SegundoNombre} {p.Persona?.PrimerApellido} {p.Persona?.SegundoApellido}".Replace("  ", " ").Trim();
            d.Identificacion = p.Persona?.NumIdentificacion;
            d.TipoId = p.Persona?.TipoIdentificacion?.DescTipo;
            d.Genero = p.Persona?.Genero?.DescGenero;
            d.Telefono = p.Persona?.Telefono;
            d.Email = p.Persona?.Email;
            d.FechaNacimiento = p.Persona?.FechaNacimiento;
            d.FechaRegistro = p.FechaRegistro;
            return d;
        }).ToList();
    }

    public async Task<List<dynamic>> GetInventarioRaw()
    {
        var productos = await _context.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        return productos.Select(p => {
            dynamic d = new ExpandoObject();
            d.Nombre = p.Nombre;
            d.Descripcion = p.Descripcion;
            d.Precio = p.Precio;
            d.Stock = p.Stock;
            d.StockMinimo = p.StockMinimo;
            d.Estado = p.Stock <= p.StockMinimo ? "STOCK BAJO" : "OK";
            return d;
        }).ToList();
    }
    #endregion
}
