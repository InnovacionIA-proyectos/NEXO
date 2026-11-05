using System.Globalization;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Corrige el valor de un flete ya agregado, modificando LA MISMA fila (reusar "agregar" duplicaría la línea e inflaría el total).
/// Reglas:
///  · Solo cotización Borrador; la línea tiene que existir en ESTA cotización; se edita un flete de cualquier fuente.
///  · Con regla Solo-SICE para la ruta y el vehículo NO se edita (409 FLETE_SOLO_SICETAC): gobierna el piso.
///  · Nunca por debajo del piso SICE-TAC (400 FLETE_BAJO_PISO_SICETAC, misma regla que al agregar).
///  · Si el piso NO se puede verificar (sin fila en DWSice, período distinto al del mes actual o consulta fallida): 409 PISO_SICETAC_NO_VERIFICABLE,
///    salvo <c>ConfirmarSinPiso</c>, que queda en la auditoría.
///  · Al guardar la fuente PASA A MANUAL (el valor ya lo fija una persona) y la auditoría guarda el valor y la fuente ANTERIORES.
/// </summary>
/// <param name="LineaSolicitudId">La línea del PLIEGO (clsLineaSolicitud), no el Id de la fila de la cotización; la misma que usa "agregar".</param>
/// <param name="ValorRetornoContenedor">null = no se toca el valor que ya tenía.</param>
/// <param name="ConfirmarSinPiso">true = quien edita acepta guardar aunque no se haya podido verificar el piso. Solo
/// sirve para el caso "no verificable": un valor bajo un piso que SÍ se encontró se rechaza siempre.</param>
public sealed record clsEditarLineaCotizacionCommand(
    Guid CotizacionId, Guid LineaSolicitudId, decimal ValorFlete, decimal? ValorRetornoContenedor,
    bool ConfirmarSinPiso = false)
    : ICommand<clsCotizacionDto>;

public sealed class clsEditarLineaCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    IClavesSoloSicetacPort reglasSoloSicetac,
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

        // Con regla Solo-SICE gobierna el piso y no se edita a mano (misma definición que el motor de propuesta y que tieneReglaSoloSicetac).
        // Va antes que el piso porque es más barato.
        var lineaSolicitud = await lineasSolicitud.ObtenerConRutaYVehiculoAsync(command.LineaSolicitudId, c.SolicitudId, ct);
        var claves = await reglasSoloSicetac.ClavesRutaVehiculoAsync(ct);
        if (lineaSolicitud is not null && clsReglaSoloSicetacDeLinea.Tiene(
                claves, lineaSolicitud.ClaseViaje,
                lineaSolicitud.clsRuta?.OrigenMunicipio?.CodigoDane, lineaSolicitud.clsRuta?.DestinoMunicipio?.CodigoDane,
                lineaSolicitud.clsConfiguracionVehiculo?.TipoVehiculo))
            throw new clsConflictException("FLETE_SOLO_SICETAC",
                "La ruta y el vehículo de esta línea tienen regla Solo SICE-TAC: el flete lo fija el piso SICE-TAC y no se puede " +
                "editar a mano. Un Administrador de Tarifas puede quitar la regla si hace falta.");

        // Mismo piso que al agregar: el servidor no confía en el bloqueo de pantalla.
        var propuesta = (await propuestas.ProponerAsync(c.SolicitudId, ct)).Lineas
            .FirstOrDefault(x => x.LineaId == command.LineaSolicitudId);

        var pisoVerificado = propuesta?.Sicetac?.Valor is not null;
        if (propuesta?.Sicetac?.Valor is { } piso && command.ValorFlete < piso)
            throw new clsValidationFailedException("FLETE_BAJO_PISO_SICETAC",
                $"El flete ({clsMoneda.Pesos(command.ValorFlete)}) está por debajo del piso SICE-TAC ({clsMoneda.Pesos(piso)}). No se puede guardar un valor por debajo del piso.");

        // Sin piso (ruta sin fila en DWSice, período distinto al del mes actual o consulta fallida) no se guarda en silencio: el que edita decide
        // con ConfirmarSinPiso y queda en la auditoría. No se distinguen los casos: en todos el piso no se verificó.
        if (!pisoVerificado && !command.ConfirmarSinPiso)
        {
            var motivo = string.IsNullOrWhiteSpace(propuesta?.MotivoPendiente) || !propuesta.MotivoPendiente.StartsWith("SICE-TAC no se usa")
                ? "no se encontró un piso SICE-TAC para esta ruta, o no se pudo consultar"
                : propuesta.MotivoPendiente;
            throw new clsConflictException("PISO_SICETAC_NO_VERIFICABLE",
                $"No se pudo verificar el piso SICE-TAC de esta línea ({motivo}). El flete ({clsMoneda.Pesos(command.ValorFlete)}) no se guardó: " +
                "confirme (confirmarSinPiso = true) si quiere guardarlo sin verificar el piso.");
        }

        var anterior = linea.ValorFlete;
        var fuenteAnterior = linea.Fuente;
        linea.ValorFlete = command.ValorFlete;
        // El valor ya no sale de su fuente original sino de una persona.
        linea.Fuente = FuenteFlete.Manual;
        if (command.ValorRetornoContenedor is { } retorno) linea.ValorRetornoContenedor = retorno;
        linea.UpdatedAt = DateTimeOffset.UtcNow;

        clsCotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "editar_flete",
            string.Create(CultureInfo.InvariantCulture,
                $"{{\"lineaSolicitudId\":\"{command.LineaSolicitudId}\",\"valorAnterior\":{anterior},\"fuenteAnterior\":\"{fuenteAnterior}\"," +
                $"\"valorNuevo\":{command.ValorFlete},\"fuenteNueva\":\"{FuenteFlete.Manual}\",\"confirmadoSinPiso\":{(pisoVerificado ? "false" : "true")}}}"));
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
