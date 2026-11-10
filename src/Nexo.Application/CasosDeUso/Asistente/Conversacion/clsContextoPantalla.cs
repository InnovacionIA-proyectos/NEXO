using Nexo.Application.CasosDeUso.Asistente.Herramientas;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>La solicitud, cotización y línea que la persona tiene abiertas en pantalla.</summary>
public sealed record clsContextoPantalla(Guid SolicitudId, Guid? CotizacionId = null, Guid? LineaSolicitudId = null);

/// <summary>Valida el contexto de pantalla contra los permisos de la persona y lo convierte en el texto que se suma a las instrucciones.</summary>
public interface IResolutorContextoPantalla
{
    /// <exception cref="clsValidationFailedException"><c>CONTEXTO_INVALIDO</c>: la cotización o la línea no son de esa solicitud.</exception>
    /// <exception cref="clsForbiddenException"><c>SOLICITUD_AJENA</c>: la persona no puede ver esa solicitud.</exception>
    /// <exception cref="clsNotFoundException">La solicitud o la cotización no existen.</exception>
    Task<string> ResolverAsync(clsContextoPantalla contexto, CancellationToken ct);
}

public sealed class clsResolutorContextoPantalla(
    ISolicitudesPort solicitudes,
    ILineasSolicitudPort lineasSolicitud,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones,
    ICurrentUserPort usuario) : IResolutorContextoPantalla
{
    public async Task<string> ResolverAsync(clsContextoPantalla contexto, CancellationToken ct)
    {
        if (contexto.SolicitudId == Guid.Empty)
            throw new clsValidationFailedException("CONTEXTO_INVALIDO", "El contexto necesita el identificador de la solicitud.");

        var solicitud = await solicitudes.ObtenerBasicoAsync(contexto.SolicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");

        // Mismo aislamiento que las herramientas: un Comercial solo ve lo que él creó.
        if (usuario.Rol == RolUsuario.Comercial && solicitud.CreadoPor != usuario.Id)
            throw new clsForbiddenException("SOLICITUD_AJENA", "No tiene acceso a esta solicitud.");

        if (contexto.CotizacionId is { } cotizacionId)
        {
            var cotizacion = await cotizaciones.Handle(new clsObtenerCotizacionQuery(cotizacionId), ct);
            if (cotizacion.SolicitudId != contexto.SolicitudId)
                throw new clsValidationFailedException("CONTEXTO_INVALIDO", "La cotización no es de esa solicitud.");
        }

        string? descripcionDeLaLinea = null;
        if (contexto.LineaSolicitudId is { } lineaId)
        {
            var linea = await lineasSolicitud.ObtenerConRutaYVehiculoAsync(lineaId, contexto.SolicitudId, ct)
                ?? throw new clsValidationFailedException("CONTEXTO_INVALIDO", "La línea no es de esa solicitud.");
            descripcionDeLaLinea = clsDescripcionDeLinea.De(linea);
        }

        return clsInstruccionesAsistente.ContextoDePantalla(contexto, descripcionDeLaLinea);
    }
}
