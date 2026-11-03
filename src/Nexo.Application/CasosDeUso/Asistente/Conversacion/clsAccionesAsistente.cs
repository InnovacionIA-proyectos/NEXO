using System.Text.Json;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Asistente.Entidades;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>
/// La persona confirma un cambio que propuso el asistente y se aplica con los casos de uso de siempre, con su identidad y sus permisos:
/// las reglas del dominio mandan y, si rechazan el cambio, la acción queda <c>fallida</c> con el código y el motivo, la conversación lo
/// explica y se responde el mismo error del dominio (no se aplica nada). Solo quien tiene la conversación puede confirmar, una sola vez
/// y dentro de la vigencia.
/// </summary>
public sealed record clsConfirmarAccionAsistenteCommand(Guid AccionId) : ICommand<clsAccionAsistenteDto>;

public sealed class clsConfirmarAccionAsistenteCommandHandler(
    IConversacionesAsistenteRepository conversaciones,
    ICurrentUserPort usuario,
    TimeProvider reloj,
    ICommandHandler<clsAgregarLineaCotizacionCommand, clsCotizacionDto> agregarFlete,
    ICommandHandler<clsEditarLineaCotizacionCommand, clsCotizacionDto> editarFlete,
    ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto> agregarConexos,
    ICommandHandler<clsRechazarLineaCotizacionCommand, clsLineaRechazoDto> rechazarLinea,
    ICommandHandler<clsCambiarVehiculoLineaCommand, clsLineaVehiculoDto> cambiarVehiculo,
    ICommandHandler<clsGuardarPreferenciaAsistenteCommand, clsPreferenciaAsistenteDto> guardarPreferencia)
    : ICommandHandler<clsConfirmarAccionAsistenteCommand, clsAccionAsistenteDto>
{
    public async Task<clsAccionAsistenteDto> Handle(clsConfirmarAccionAsistenteCommand command, CancellationToken ct = default)
    {
        var accion = await ObtenerPropiaAsync(conversaciones, usuario, command.AccionId, ct);
        var ahora = reloj.GetUtcNow();

        if (!await conversaciones.TomarAccionAsync(accion.Id, ahora, ct))
        {
            var actual = (await conversaciones.ObtenerAccionAsync(accion.Id, ct))!;
            if (actual.Estado == clsAccionAsistente.EstadoPendiente)
            {
                await conversaciones.ResolverAccionAsync(
                    accion.Id, clsAccionAsistente.EstadoVencida, "ACCION_VENCIDA", "No se confirmó a tiempo.", ahora, CancellationToken.None);
                throw new clsConflictException("ACCION_VENCIDA", "La acción venció sin confirmarse. Pídele al asistente que la proponga de nuevo.");
            }
            throw new clsConflictException("ACCION_YA_RESUELTA", $"La acción ya está {actual.Estado}.");
        }

        try
        {
            // Sin el token de la petición: una vez tomada, el cambio se aplica o se rechaza, no queda a medias por una conexión cerrada.
            await EjecutarAsync(accion, CancellationToken.None);
        }
        catch (clsAppException ex)
        {
            await Cerrar(accion, clsAccionAsistente.EstadoFallida, ex.Code, ex.Message,
                $"No pude aplicar el cambio ({accion.Resumen}): {ex.Message} [{ex.Code}]. No se modificó nada.");
            throw;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await Cerrar(accion, clsAccionAsistente.EstadoFallida, "ERROR_INTERNO", "El cambio falló por un error interno.",
                $"No pude aplicar el cambio ({accion.Resumen}) por un error interno. Intenta de nuevo o hazlo en la pantalla de costeo.");
            throw;
        }

        await Cerrar(accion, clsAccionAsistente.EstadoEjecutada, "ok", null, $"Listo, apliqué el cambio: {accion.Resumen}");
        return clsAccionAsistenteDto.De((await conversaciones.ObtenerAccionAsync(accion.Id, ct))!);
    }

    private async Task Cerrar(clsAccionAsistente accion, string estado, string codigo, string? mensaje, string aviso)
    {
        await conversaciones.ResolverAccionAsync(accion.Id, estado, codigo, mensaje, reloj.GetUtcNow(), CancellationToken.None);
        await conversaciones.AgregarMensajeAsistenteAsync(accion.ConversacionId, aviso, CancellationToken.None);
    }

    internal static async Task<clsAccionAsistente> ObtenerPropiaAsync(
        IConversacionesAsistenteRepository conversaciones, ICurrentUserPort usuario, Guid id, CancellationToken ct)
    {
        var accion = await conversaciones.ObtenerAccionAsync(id, ct)
            ?? throw new clsNotFoundException("ACCION_NO_ENCONTRADA", "La acción no existe.");
        if (accion.UsuarioId != usuario.Id)
            throw new clsForbiddenException("ACCION_AJENA", "Esa acción es de otra persona.");
        return accion;
    }

    private async Task EjecutarAsync(clsAccionAsistente accion, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(accion.Parametros);
        var p = doc.RootElement;

        if (accion.Tipo == clsAccionAsistente.TipoGuardarPreferencia)
        {
            await guardarPreferencia.Handle(new clsGuardarPreferenciaAsistenteCommand(p.GetProperty("texto").GetString(), accion.ConversacionId), ct);
            return;
        }

        var cotizacionId = p.GetProperty("cotizacionId").GetGuid();

        switch (accion.Tipo)
        {
            case clsAccionAsistente.TipoAgregarFlete:
                await agregarFlete.Handle(new clsAgregarLineaCotizacionCommand(
                    cotizacionId, p.GetProperty("lineaSolicitudId").GetGuid(), p.GetProperty("valorFlete").GetDecimal(),
                    Numero(p, "valorRetornoContenedor"), p.GetProperty("fuente").GetString()!), ct);
                break;
            case clsAccionAsistente.TipoEditarFlete:
                // ConfirmarSinPiso nunca se activa desde el asistente: el piso SICE-TAC no se salta.
                await editarFlete.Handle(new clsEditarLineaCotizacionCommand(
                    cotizacionId, p.GetProperty("lineaSolicitudId").GetGuid(), p.GetProperty("valorFlete").GetDecimal(),
                    Numero(p, "valorRetornoContenedor"), ConfirmarSinPiso: false), ct);
                break;
            case clsAccionAsistente.TipoAgregarConexos:
                await agregarConexos.Handle(new clsAgregarConexosCotizacionCommand(
                    cotizacionId,
                    p.GetProperty("items").EnumerateArray().Select(i => new clsConexoItem(
                        i.GetProperty("lineaId").GetGuid(), i.GetProperty("servicioConexoId").GetGuid(), i.GetProperty("cantidad").GetDecimal(),
                        Numero(i, "valorAplicado"))).ToList()), ct);
                break;
            case clsAccionAsistente.TipoRechazarLinea:
                await rechazarLinea.Handle(new clsRechazarLineaCotizacionCommand(
                    cotizacionId, p.GetProperty("lineaSolicitudId").GetGuid(), p.GetProperty("motivo").GetString()), ct);
                break;
            case clsAccionAsistente.TipoCambiarVehiculo:
                await cambiarVehiculo.Handle(new clsCambiarVehiculoLineaCommand(
                    cotizacionId, p.GetProperty("lineaSolicitudId").GetGuid(), Identificador(p, "configVehiculoId"),
                    Texto(p, "tipoVehiculo"), Texto(p, "carroceria")), ct);
                break;
            default:
                throw new clsValidationFailedException("ACCION_DESCONOCIDA", $"Tipo de acción desconocido: {accion.Tipo}.");
        }
    }

    private static decimal? Numero(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static Guid? Identificador(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetGuid() : null;

    private static string? Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>La persona descarta un cambio propuesto. No se aplica nada y la acción ya no se puede confirmar.</summary>
public sealed record clsCancelarAccionAsistenteCommand(Guid AccionId) : ICommand<clsAccionAsistenteDto>;

public sealed class clsCancelarAccionAsistenteCommandHandler(
    IConversacionesAsistenteRepository conversaciones, ICurrentUserPort usuario, TimeProvider reloj)
    : ICommandHandler<clsCancelarAccionAsistenteCommand, clsAccionAsistenteDto>
{
    public async Task<clsAccionAsistenteDto> Handle(clsCancelarAccionAsistenteCommand command, CancellationToken ct = default)
    {
        var accion = await clsConfirmarAccionAsistenteCommandHandler.ObtenerPropiaAsync(conversaciones, usuario, command.AccionId, ct);

        if (!await conversaciones.CancelarAccionAsync(accion.Id, reloj.GetUtcNow(), ct))
        {
            var actual = (await conversaciones.ObtenerAccionAsync(accion.Id, ct))!;
            throw new clsConflictException("ACCION_YA_RESUELTA", $"La acción ya está {actual.Estado}.");
        }

        await conversaciones.AgregarMensajeAsistenteAsync(accion.ConversacionId, $"Se canceló el cambio propuesto: {accion.Resumen}", CancellationToken.None);
        return clsAccionAsistenteDto.De((await conversaciones.ObtenerAccionAsync(accion.Id, ct))!);
    }
}
