using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.IA.Entidades;

namespace Nexo.Infrastructure.IA;

/// <summary>Envuelve al adaptador del proveedor: aplica el tope diario de tokens por persona (día de Colombia, UTC-5) y registra cada
/// llamada en <c>tbl_ia_uso</c>, sin guardar el contenido. El tope es BLANDO (dos llamadas simultáneas pueden pasarlo); 0 lo desactiva
/// y sin usuario no hay tope. Lo que nunca llegó al proveedor no se registra; si guardar falla, solo se avisa en el log.</summary>
public sealed class clsAsistenteIaConRegistro(
    IAsistenteIaPort interno,
    IUsoIaRepository uso,
    IOptions<clsOpcionesAnthropic> opciones,
    TimeProvider reloj,
    ILogger<clsAsistenteIaConRegistro> log) : IAsistenteIaPort
{
    private readonly clsOpcionesAnthropic _opciones = opciones.Value;

    public bool Habilitada(clsFuncionIa funcion) => interno.Habilitada(funcion);

    public async Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
    {
        // Si el adaptador va a negarse sin llamar al proveedor, que lo diga él: no hay nada que topar ni registrar.
        if (!interno.Habilitada(peticion.Funcion))
            return await interno.CompletarAsync(peticion, ct);

        await VerificarTopeAsync(peticion, ct);

        var cronometro = Stopwatch.StartNew();
        try
        {
            var respuesta = await interno.CompletarAsync(peticion, ct);
            await RegistrarAsync(peticion, new clsUsoDeTokens(
                respuesta.Modelo, respuesta.TokensEntrada, respuesta.TokensSalida, respuesta.TokensCacheLectura, respuesta.TokensCacheEscritura),
                clsUsoIa.ResultadoOk, cronometro);
            return respuesta;
        }
        catch (clsIaRespuestaNoUtilException ex)
        {
            await RegistrarAsync(peticion, ex.Uso, ex.Code, cronometro);
            throw;
        }
        catch (clsServiceUnavailableException ex) when (!ct.IsCancellationRequested)
        {
            // El proveedor falló antes de responder: no hay cifras de tokens, pero el intento y su causa quedan anotados.
            await RegistrarAsync(peticion, new clsUsoDeTokens(_opciones.ModeloEfectivo, 0, 0, 0, 0), ex.Code, cronometro);
            throw;
        }
    }

    private Task VerificarTopeAsync(clsPeticionIa peticion, CancellationToken ct) =>
        clsTopeDiarioIa.VerificarAsync(uso, _opciones.LimiteTokensPorUsuarioDiaEfectivo, peticion.UsuarioId, reloj, ct);

    /// <summary>Medianoche de Colombia (UTC-5) del día al que pertenece <paramref name="ahora"/>.</summary>
    internal static DateTimeOffset InicioDelDia(DateTimeOffset ahora) => clsTopeDiarioIa.InicioDelDia(ahora);

    private async Task RegistrarAsync(clsPeticionIa peticion, clsUsoDeTokens tokens, string resultado, Stopwatch cronometro)
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
            // Perder un renglón de uso es mejor que perder la respuesta que el usuario ya esperó; pero no debe pasar callado.
            log.LogError(ex, "No se pudo registrar el uso de IA ({Funcion}, {Resultado})", peticion.Funcion, resultado);
        }
    }
}
