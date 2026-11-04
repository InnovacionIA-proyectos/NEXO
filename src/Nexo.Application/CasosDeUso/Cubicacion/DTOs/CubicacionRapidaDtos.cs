namespace Nexo.Application.CasosDeUso.Cubicacion.DTOs;

/// <summary>Espejo 1:1 del contrato HTTP de <c>POST /recomendaciones</c> del Cubicador (CONTRATO.md §3.1); NEXO reenvía y devuelve tal cual, sin mapeo de dominio.</summary>
public sealed record clsMercanciaSolicitadaInput(
    string CodigoMercancia, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm, decimal PesoKg);

/// <summary><c>OrigenSistema</c> no viaja en el body: el Cubicador lo deriva del Bearer y responde 401 si el body lo declara y no coincide.</summary>
public sealed record clsCrearRecomendacionInput(
    string IdLineaSolicitud, IReadOnlyList<clsMercanciaSolicitadaInput> Mercancias,
    string? ClaseViajeSeleccionada = null, string? UsuarioId = null);

/// <summary>
/// <paramref name="IndicePosicion"/>: índice base 0 en <c>mercancias</c> del request; una fila por POSICIÓN, no por código, así que quien agrupe por código debe SUMAR.
/// Null al reconsultar corridas guardadas agrupadas por código (no es error).
/// </summary>
public sealed record clsMercanciaDeVehiculoDto(
    int? IndicePosicion, string IdTipoMercancia, string CodigoMercancia, int Cantidad, decimal PesoKg, decimal VolumenM3);

/// <summary>Cada campo llega no nulo cuando <c>Resultado == "ASIGNADO"</c>. <c>PesoTotalKg</c> es el peso declarado en la solicitud; el porcentaje de llenado no sirve para costear.</summary>
public sealed record clsVehiculoRecomendadoDto(
    string IdRecomendacionVehiculo, int NumeroVehiculo, string CodigoVehiculo, string NombreVehiculo,
    string CodigoCarroceria, decimal PesoTotalKg, decimal VolumenTotalM3,
    decimal PorcentajeLlenadoVolumen, decimal PorcentajeLlenadoPeso,
    bool AlertaBajoLlenado, bool Heterogenea, IReadOnlyList<clsMercanciaDeVehiculoDto> Mercancias);

public sealed record clsMedidasEstandarDto(decimal LargoMaxCm, decimal AnchoMaxCm, decimal AltoMaxCm, decimal PesoMaxKg);

/// <summary><paramref name="IndicePosicion"/>: ver <see cref="clsMercanciaDeVehiculoDto"/>.</summary>
public sealed record clsPiezaFueraDeMedidaDto(int? IndicePosicion, string IdTipoMercancia, string CodigoMercancia, IReadOnlyList<string> Excesos);

public sealed record clsExtradimensionDto(
    bool HayExtradimension, clsMedidasEstandarDto MedidasEstandar, IReadOnlyList<clsPiezaFueraDeMedidaDto> Piezas);

/// <summary>
/// <c>Resultado</c>: <c>"ASIGNADO"</c> | <c>"SIN_SELECCION_AUTOMATICA"</c>; es string y no enum porque el Cubicador es dueño del vocabulario y un valor nuevo no debe romper la deserialización.
/// Ambos llegan con 200: SIN_SELECCION_AUTOMATICA, Condicionada y Extradimension son estados normales, no fallos.
/// </summary>
public static class MotivoSinSeleccion
{
    public const string Extradimensionado = "EXTRADIMENSIONADO";
    public const string SinCandidatosDimensionales = "SIN_CANDIDATOS_DIMENSIONALES";
    public const string SuperaKMax = "SUPERA_K_MAX";
    public const string SinCarroceriaCompatible = "SIN_CARROCERIA_COMPATIBLE";

    /// <summary>El buscador se quedó sin intentos de empaque: condición del ALGORITMO, distinta de <see cref="SuperaKMax"/> (condición del NEGOCIO).</summary>
    public const string BusquedaAgotada = "BUSQUEDA_AGOTADA";
}

public sealed record clsRecomendacionDto(
    string IdRecomendacion, string IdLineaSolicitud, string OrigenSistema, string Resultado,
    string? MotivoSinSeleccion, bool Condicionada, IReadOnlyList<string> MercanciasCondicionadas,
    IReadOnlyList<clsVehiculoRecomendadoDto> Vehiculos, IReadOnlyList<string> Advertencias,
    clsExtradimensionDto? Extradimension);

/// <summary>El texto que explica en lenguaje natural por qué el Cubicador eligió esa flota; null cuando no hay explicación.</summary>
public sealed record clsExplicacionRecomendacionDto(string? Explicacion);
