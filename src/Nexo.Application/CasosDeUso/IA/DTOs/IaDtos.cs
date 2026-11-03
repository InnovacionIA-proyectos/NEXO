namespace Nexo.Application.CasosDeUso.IA.DTOs;

/// <summary>Una llamada ya terminada, lista para guardarse. No lleva contenido: solo cifras y resultado.</summary>
public sealed record clsRegistroUsoIa(
    string Funcion,
    Guid? UsuarioId,
    string Modelo,
    long TokensEntrada,
    long TokensSalida,
    long TokensCacheLectura,
    long TokensCacheEscritura,
    decimal? CostoEstimadoUsd,
    int LatenciaMs,
    string Resultado,
    DateTimeOffset Fecha);

/// <summary>
/// Gasto de una función en un mes. <paramref name="CostoEstimadoUsd"/> suma solo las llamadas con precio conocido;
/// <paramref name="LlamadasSinCosto"/> dice cuántas quedaron fuera, para no leer el total como completo cuando faltan.
/// </summary>
/// <param name="Mes">Mes calendario en hora de Colombia, formato <c>AAAA-MM</c>.</param>
/// <param name="Errores">Llamadas cuyo resultado no fue <c>ok</c>.</param>
public sealed record clsGastoIaDto(
    string Mes,
    string Funcion,
    int Llamadas,
    int Errores,
    long TokensEntrada,
    long TokensSalida,
    long TokensCacheLectura,
    long TokensCacheEscritura,
    decimal CostoEstimadoUsd,
    int LlamadasSinCosto,
    int LatenciaPromedioMs);
