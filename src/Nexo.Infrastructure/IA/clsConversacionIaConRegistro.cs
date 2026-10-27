using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.IA.Entidades;

namespace Nexo.Infrastructure.IA;

/// <summary>
/// Envuelve la conversación con el modelo para controlar su uso, igual que <see cref="clsAsistenteIaConRegistro"/>: antes de empezar
/// aplica el tope diario de la persona y, al terminar (bien, con error o porque el usuario cerró la conexión), registra en
/// <c>tbl_ia_uso</c> UNA fila con la suma de todas las peticiones al proveedor que hizo el intercambio. No guarda el contenido.
/// </summary>
public sealed class clsConversacionIaConRegistro(
    IConversacionIaPort interno,
    IUsoIaRepository uso,
    IOptions<clsOpcionesAnthropic> opciones,
    TimeProvider reloj,
    ILogger<clsConversacionIaConRegistro> log) : IConversacionIaPort
{
    private readonly clsOpcionesAnthropic _opciones = opciones.Value;

    public async Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default)
    {
        await interno.VerificarAsync(funcion, usuarioId, ct);
        await clsTopeDiarioIa.VerificarAsync(uso, _opciones.LimiteTokensPorUsuarioDiaEfectivo, usuarioId, reloj, ct);
    }

    public async IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion,
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var cronometro = Stopwatch.StartNew();
        var resultado = clsUsoIa.ResultadoOk;
        var terminado = false;
        string modelo = _opciones.ModeloEfectivo;
        long entrada = 0, salida = 0, cacheLectura = 0, cacheEscritura = 0;

        var enumerador = interno.ConversarAsync(peticion, ejecutarHerramienta, ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                clsEventoConversacionIa evento;
                try
                {
                    if (!await enumerador.MoveNextAsync()) break;
                    evento = enumerador.Current;
                }
                catch (clsServiceUnavailableException ex)
                {
                    resultado = ex.Code;
                    throw;
                }

                switch (evento)
                {
                    case clsEventoConversacionIa.Uso u:
                        modelo = u.Tokens.Modelo;
                        entrada += u.Tokens.Entrada;
                        salida += u.Tokens.Salida;
                        cacheLectura += u.Tokens.CacheLectura;
                        cacheEscritura += u.Tokens.CacheEscritura;
                        break;
                    case clsEventoConversacionIa.Fin f when f.Motivo != "end_turn":
                        resultado = f.Motivo switch
                        {
                            "refusal" => "IA_RECHAZADA",
                            "max_tokens" => "IA_RESPUESTA_TRUNCADA",
                            _ => f.Motivo,
                        };
                        break;
                }

                yield return evento;
            }
            terminado = true;
        }
        finally
        {
            await enumerador.DisposeAsync();
            // Cerrar la conexión a mitad no es un error del proveedor, pero sí gastó tokens: queda anotado como cancelada.
            if (!terminado && resultado == clsUsoIa.ResultadoOk) resultado = "cancelada";
            await RegistrarAsync(peticion, new clsUsoDeTokens(modelo, entrada, salida, cacheLectura, cacheEscritura), resultado, cronometro);
        }
    }

    private async Task RegistrarAsync(clsPeticionConversacionIa peticion, clsUsoDeTokens tokens, string resultado, Stopwatch cronometro)
    {
        try
        {
            await uso.RegistrarAsync(new clsRegistroUsoIa(
                peticion.Funcion.ToString(), peticion.UsuarioId, tokens.Modelo,
                tokens.Entrada, tokens.Salida, tokens.CacheLectura, tokens.CacheEscritura,
                clsPreciosIa.Estimar(tokens.Modelo, tokens.Entrada, tokens.Salida, tokens.CacheLectura, tokens.CacheEscritura),
                (int)Math.Min(cronometro.ElapsedMilliseconds, int.MaxValue), resultado, reloj.GetUtcNow()),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Perder un renglón de uso es mejor que perder la respuesta que el usuario ya recibió; pero no debe pasar callado.
            log.LogError(ex, "No se pudo registrar el uso de IA ({Funcion}, {Resultado})", peticion.Funcion, resultado);
        }
    }
}
