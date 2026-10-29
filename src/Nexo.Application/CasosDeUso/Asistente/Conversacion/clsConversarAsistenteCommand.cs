using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>
/// Un mensaje del usuario al asistente. Sin <paramref name="ConversacionId"/> empieza una conversación nueva.
/// Devuelve la respuesta como una secuencia de eventos: todo lo que puede fallar antes de responder (mensaje inválido, conversación
/// ajena, límite de turnos, función apagada, tope diario) falla aquí, antes de devolver la secuencia, para que sea un error HTTP normal.
/// </summary>
public sealed record clsConversarAsistenteCommand(Guid? ConversacionId, string? Mensaje) : ICommand<IAsyncEnumerable<clsEventoAsistente>>;

public sealed class clsConversarAsistenteCommandHandler(
    IConversacionIaPort ia,
    IConversacionesAsistenteRepository conversaciones,
    clsHerramientasDelAsistente herramientas,
    ICurrentUserPort usuario,
    clsLimitesAsistente limites)
    : ICommandHandler<clsConversarAsistenteCommand, IAsyncEnumerable<clsEventoAsistente>>
{
    private const int LargoDelTitulo = 60;

    public async Task<IAsyncEnumerable<clsEventoAsistente>> Handle(clsConversarAsistenteCommand command, CancellationToken ct = default)
    {
        var pregunta = command.Mensaje?.Trim();
        if (string.IsNullOrEmpty(pregunta))
            throw new clsValidationFailedException("MENSAJE_REQUERIDO", "Escribe un mensaje para el asistente.");
        if (pregunta.Length > limites.MaxCaracteresMensajeEfectivo)
            throw new clsValidationFailedException("MENSAJE_DEMASIADO_LARGO",
                $"El mensaje supera {limites.MaxCaracteresMensajeEfectivo:N0} caracteres.");

        var conversacion = command.ConversacionId is { } id
            ? await conversaciones.ObtenerAsync(id, ct) ?? throw new clsNotFoundException("CONVERSACION_NO_ENCONTRADA", "La conversación no existe.")
            : null;

        if (conversacion is not null && conversacion.UsuarioId != usuario.Id)
            throw new clsForbiddenException("CONVERSACION_AJENA", "Esa conversación es de otra persona.");

        if (conversacion is not null && conversacion.Turnos >= limites.MaxTurnosEfectivo)
            throw new clsLimiteExcedidoException(
                "ASISTENTE_LIMITE_TURNOS",
                $"La conversación llegó a su límite de {limites.MaxTurnosEfectivo} mensajes. Empieza una nueva.",
                new { maxTurnos = limites.MaxTurnosEfectivo });

        await ia.VerificarAsync(clsFuncionIa.AsistenteCotizacion, usuario.Id, ct);

        var previos = conversacion is null ? [] : await conversaciones.MensajesAsync(conversacion.Id, ct);
        conversacion ??= await conversaciones.CrearAsync(usuario.Id, Titulo(pregunta), ct);
        var respuestaId = await conversaciones.IniciarTurnoAsync(conversacion.Id, pregunta, ct);

        return Transmitir(conversacion.Id, respuestaId, pregunta, previos, ct);
    }

    private static string Titulo(string pregunta)
    {
        var linea = pregunta.ReplaceLineEndings(" ");
        return linea.Length <= LargoDelTitulo ? linea : linea[..LargoDelTitulo].TrimEnd() + "…";
    }

    private async IAsyncEnumerable<clsEventoAsistente> Transmitir(
        Guid conversacionId, Guid respuestaId, string pregunta, IReadOnlyList<clsMensajeAsistente> previos,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var respuesta = new StringBuilder();
        var terminado = false;
        try
        {
            yield return new clsEventoAsistente.Conversacion(conversacionId, respuestaId);

            // Solo lo que se entregó completo entra al contexto: una respuesta cortada o descartada no se le repite al modelo.
            var historial = previos
                .Where(m => m.Estado == clsMensajeAsistente.EstadoCompleto)
                .Select(m => new clsMensajeIa(m.Rol, m.Contenido))
                .Append(new clsMensajeIa(clsMensajeIa.RolUsuario, pregunta))
                .ToList();

            var definiciones = herramientas.Disponibles()
                .Select(d => new clsHerramientaIa(d.Nombre, d.Descripcion, d.EsquemaEntrada.GetRawText()))
                .ToList();

            var peticion = new clsPeticionConversacionIa(
                clsFuncionIa.AsistenteCotizacion, clsInstruccionesAsistente.Texto, historial, definiciones,
                limites.MaxIteracionesEfectivo, limites.MaxTokensRespuestaEfectivo, usuario.Id);

            string? motivo = null;
            var cortado = false;
            clsEventoAsistente.Error? error = null;
            var enumerador = ia.ConversarAsync(peticion, (llamada, token) => EjecutarHerramientaAsync(conversacionId, respuestaId, llamada, token), ct)
                .GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    clsEventoConversacionIa? evento = null;
                    Exception? fallo = null;
                    try
                    {
                        if (!await enumerador.MoveNextAsync()) break;
                        evento = enumerador.Current;
                    }
                    catch (Exception ex)
                    {
                        fallo = ex;
                    }

                    if (fallo is not null)
                    {
                        cortado = true;
                        error = ComoError(fallo, ct);
                        break;
                    }

                    switch (evento)
                    {
                        case clsEventoConversacionIa.Texto t:
                            respuesta.Append(t.Delta);
                            yield return new clsEventoAsistente.Fragmento(t.Delta);
                            break;
                        case clsEventoConversacionIa.HerramientaSolicitada s:
                            yield return new clsEventoAsistente.Herramienta(s.Llamada.Nombre, "ejecutando", null);
                            break;
                        case clsEventoConversacionIa.HerramientaTerminada f:
                            yield return new clsEventoAsistente.Herramienta(
                                f.Llamada.Nombre, f.Resultado.EsError ? "error" : "ok", f.Resultado.Fuente);
                            break;
                        case clsEventoConversacionIa.Fin f:
                            motivo = f.Motivo;
                            break;
                    }
                }
            }
            finally
            {
                await enumerador.DisposeAsync();
            }

            // Sin error que mostrar es que se cerró la conexión: no hay a quién avisarle, solo queda guardar lo generado.
            if (cortado && error is null)
            {
                await conversaciones.FinalizarMensajeAsync(respuestaId, respuesta.ToString(), clsMensajeAsistente.EstadoIncompleto, CancellationToken.None);
                terminado = true;
                yield break;
            }

            error ??= motivo switch
            {
                "refusal" => new clsEventoAsistente.Error("IA_RECHAZADA", "El modelo rechazó la petición."),
                "max_tokens" => new clsEventoAsistente.Error("IA_RESPUESTA_TRUNCADA", "La respuesta quedó incompleta."),
                "max_iteraciones" => new clsEventoAsistente.Error(
                    "ASISTENTE_DEMASIADAS_CONSULTAS", "El asistente necesitó demasiadas consultas para responder. Hazle una pregunta más concreta."),
                _ => null,
            };

            // Lo que se le entregó al usuario ya está entregado, pero lo que queda guardado (y se le repite al modelo) no puede traer
            // un precio sin origen.
            var texto = respuesta.ToString();
            var sinOrigen = texto.Length == 0 ? [] : await CifrasSinOrigenAsync(conversacionId, texto, historial, ct);
            if (sinOrigen.Count > 0)
            {
                var aviso = $"Descarté mi respuesta porque incluía cifras que no salen de ninguna consulta ({string.Join(", ", sinOrigen)}). " +
                    "Vuelve a preguntarme y consulto las fuentes antes de dar un valor.";
                await conversaciones.FinalizarMensajeAsync(respuestaId, aviso, clsMensajeAsistente.EstadoDescartado, CancellationToken.None);
                terminado = true;
                yield return new clsEventoAsistente.Reemplazo(aviso, sinOrigen);
                yield return new clsEventoAsistente.Fin(conversacionId, respuestaId, clsMensajeAsistente.EstadoDescartado);
                yield break;
            }

            var estado = error is null ? clsMensajeAsistente.EstadoCompleto : clsMensajeAsistente.EstadoIncompleto;
            await conversaciones.FinalizarMensajeAsync(respuestaId, texto, estado, CancellationToken.None);
            terminado = true;

            if (error is not null) yield return error;
            else yield return new clsEventoAsistente.Fin(conversacionId, respuestaId, estado);
        }
        finally
        {
            // Si el cliente se desconectó o algo falló antes de llegar aquí, lo que alcanzó a generarse queda como incompleto.
            if (!terminado)
                await conversaciones.FinalizarMensajeAsync(respuestaId, respuesta.ToString(), clsMensajeAsistente.EstadoIncompleto, CancellationToken.None);
        }
    }

    /// <summary>
    /// Ejecuta la herramienta con la identidad de la persona y deja constancia. Un rechazo del dominio (sin permiso, solicitud ajena,
    /// no existe) vuelve al modelo como error para que lo explique; no se esconde ni se reintenta por otro camino.
    /// </summary>
    private async Task<clsResultadoHerramientaIa> EjecutarHerramientaAsync(
        Guid conversacionId, Guid respuestaId, clsLlamadaHerramientaIa llamada, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        string? salida = null;
        string resultado;
        clsResultadoHerramienta? leido = null;
        string contenido;

        try
        {
            leido = await herramientas.EjecutarAsync(llamada.Nombre, llamada.Entrada, ct);
            salida = contenido = JsonSerializer.Serialize(leido, JsonSerializerOptions.Web);
            resultado = clsUsoHerramientaAsistente.ResultadoOk;
        }
        catch (clsAppException ex)
        {
            resultado = ex.Code;
            contenido = JsonSerializer.Serialize(new { error = ex.Code, mensaje = ex.Message }, JsonSerializerOptions.Web);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resultado = "HERRAMIENTA_ERROR";
            contenido = JsonSerializer.Serialize(
                new { error = resultado, mensaje = "La consulta falló por un error interno." }, JsonSerializerOptions.Web);
        }

        await conversaciones.RegistrarHerramientaAsync(new clsUsoHerramientaAsistente
        {
            ConversacionId = conversacionId, MensajeId = respuestaId, Herramienta = llamada.Nombre,
            Entrada = llamada.Entrada.GetRawText(), Salida = salida, Encontrado = leido?.Encontrado, Fuente = leido?.Fuente,
            Resultado = resultado, LatenciaMs = (int)Math.Min(reloj.ElapsedMilliseconds, int.MaxValue),
        }, CancellationToken.None);

        return new clsResultadoHerramientaIa(contenido, EsError: leido is null, leido?.Fuente, leido?.Encontrado);
    }

    private async Task<IReadOnlyList<string>> CifrasSinOrigenAsync(
        Guid conversacionId, string respuesta, IReadOnlyList<clsMensajeIa> historial, CancellationToken ct)
    {
        var fuentes = historial.Select(m => m.Contenido).ToList();
        fuentes.AddRange((await conversaciones.HerramientasAsync(conversacionId, ct)).Select(h => h.Salida).OfType<string>());
        return clsVerificadorDeCifras.CifrasSinOrigen(respuesta, fuentes);
    }

    /// <summary>El error que se le muestra al usuario; null si no hay a quién mostrárselo (se canceló la conexión).</summary>
    private static clsEventoAsistente.Error? ComoError(Exception ex, CancellationToken ct) => ex switch
    {
        _ when ct.IsCancellationRequested => null,
        clsAppException app => new clsEventoAsistente.Error(app.Code, app.Message),
        _ => new clsEventoAsistente.Error("ASISTENTE_ERROR", "No pude completar la respuesta."),
    };
}
