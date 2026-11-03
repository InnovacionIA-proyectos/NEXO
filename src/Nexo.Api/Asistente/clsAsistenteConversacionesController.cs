using System.Text.Json;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Asistente;

/// <summary>Conversación con el asistente de cotización: respuesta en tiempo real, con herramientas de lectura y auditoría.</summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/asistente/conversaciones")]
public class clsAsistenteConversacionesController(
    ICommandHandler<clsConversarAsistenteCommand, IAsyncEnumerable<clsEventoAsistente>> conversar,
    IQueryHandler<clsListarConversacionesAsistenteQuery, IReadOnlyList<clsConversacionResumenDto>> listar,
    IQueryHandler<clsObtenerConversacionAsistenteQuery, clsConversacionDetalleDto> obtener) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Envía un mensaje y recibe la respuesta como <c>text/event-stream</c> (ver CONTRATO.md §3.23.1).</summary>
    [HttpPost("mensajes")]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task Mensajes([FromBody] clsEnviarMensajeRequest req, CancellationToken ct = default)
    {
        // Todo lo que puede fallar antes de responder falla aquí, como un error HTTP normal; después solo hay eventos.
        var eventos = await conversar.Handle(new clsConversarAsistenteCommand(req.ConversacionId, req.Mensaje), ct);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no"; // el proxy no debe retener los eventos
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            await foreach (var evento in eventos.WithCancellation(ct))
            {
                var (nombre, datos) = Describir(evento);
                await Response.WriteAsync($"event: {nombre}\ndata: {JsonSerializer.Serialize(datos, Json)}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // El cliente cerró la conexión: la respuesta parcial ya quedó guardada como incompleta.
        }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsConversacionResumenDto>>> Listar([FromQuery] int top = 50, CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarConversacionesAsistenteQuery(top), ct));

    /// <summary>La conversación con sus mensajes y las herramientas usadas en cada respuesta. La ve su dueño y el Admin.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsConversacionDetalleDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await obtener.Handle(new clsObtenerConversacionAsistenteQuery(id), ct));

    private static (string Nombre, object Datos) Describir(clsEventoAsistente evento) => evento switch
    {
        clsEventoAsistente.Conversacion e => ("conversacion", e),
        clsEventoAsistente.Preferencias e => ("preferencias", e.Aplicadas),
        clsEventoAsistente.Fragmento e => ("texto", e),
        clsEventoAsistente.Herramienta e => ("herramienta", e),
        clsEventoAsistente.Reemplazo e => ("reemplazo", e),
        clsEventoAsistente.Accion e => ("accion", e.Propuesta),
        clsEventoAsistente.Fin e => ("fin", e),
        clsEventoAsistente.Error e => ("error", e),
        _ => ("desconocido", evento),
    };
}

/// <param name="ConversacionId">Sin valor empieza una conversación nueva.</param>
public sealed record clsEnviarMensajeRequest(Guid? ConversacionId, string? Mensaje);
