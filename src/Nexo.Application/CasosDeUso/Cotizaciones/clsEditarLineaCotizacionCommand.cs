using System.Globalization;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286: corregir el valor de un flete MANUAL ya agregado a la cotización.
///
/// Existe porque <see cref="clsAgregarLineaCotizacionCommand"/> SIEMPRE agrega una fila nueva:
/// editar desde el front reusando "agregar" duplicaría la línea e inflaría el total
/// (<see cref="clsCotizacionCalculos.Recalcular"/> suma ambas). Acá se modifica LA MISMA fila.
///
/// Reglas: solo cotización Borrador; la línea tiene que existir en ESTA cotización; solo se
/// editan fletes de fuente Manual (un flete de Tarifario/SICE-TAC/Histórico sale de una
/// fuente y se cambia reemplazándolo por otro candidato, no escribiéndole un número encima);
/// nunca por debajo del piso SICE-TAC (misma regla que al agregar, #283). No recibe Fuente:
/// editar no puede cambiar de dónde salió el valor.
/// </summary>
/// <param name="LineaSolicitudId">La línea del PLIEGO (clsLineaSolicitud), no el Id de la fila de la cotización:
/// es la que el front ya conoce y la misma que usa "agregar".</param>
/// <param name="ValorRetornoContenedor">null = no se toca el valor que ya tenía.</param>
public sealed record clsEditarLineaCotizacionCommand(
    Guid CotizacionId, Guid LineaSolicitudId, decimal ValorFlete, decimal? ValorRetornoContenedor)
    : ICommand<clsCotizacionDto>;

public sealed class clsEditarLineaCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    IPropuestaTarifaPort propuestas,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsEditarLineaCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsEditarLineaCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var linea = c.Lineas.FirstOrDefault(l => l.LineaSolicitudId == command.LineaSolicitudId)
            ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "La línea no tiene un flete en esta cotización.");
        if (linea.Fuente != FuenteFlete.Manual)
            throw new clsConflictException("FLETE_NO_EDITABLE",
                $"Solo se puede editar un flete de fuente Manual; este viene de {linea.Fuente}. Reemplácelo eligiendo otro candidato.");

        // Mismo piso que al agregar (#283): el servidor no confía en que la pantalla lo haya
        // bloqueado — llamar la API directo lo saltaría.
        var propuesta = (await propuestas.ProponerAsync(c.SolicitudId, ct)).Lineas
            .FirstOrDefault(x => x.LineaId == command.LineaSolicitudId);
        if (propuesta?.Sicetac?.Valor is { } piso && command.ValorFlete < piso)
            throw new clsValidationFailedException("FLETE_BAJO_PISO_SICETAC",
                $"El flete ({command.ValorFlete:C0}) está por debajo del piso SICE-TAC ({piso:C0}). No se puede guardar un valor por debajo del piso.");

        var anterior = linea.ValorFlete;
        linea.ValorFlete = command.ValorFlete;
        if (command.ValorRetornoContenedor is { } retorno) linea.ValorRetornoContenedor = retorno;
        linea.UpdatedAt = DateTimeOffset.UtcNow;

        clsCotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "editar_flete",
            string.Create(CultureInfo.InvariantCulture,
                $"{{\"lineaSolicitudId\":\"{command.LineaSolicitudId}\",\"valorAnterior\":{anterior},\"valorNuevo\":{command.ValorFlete}}}"));
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
