namespace Nexo.Application.CasosDeUso.Cubicacion.DTOs;

/// <summary>
/// S5-HU-18.1/#273: espejo 1:1 del contrato HTTP de <c>POST /recomendaciones</c> del
/// Cubicador (<c>cubicador/api/dtos/recomendaciones.py</c>, <c>CONTRATO.md</c> §3.1).
/// Reemplaza por completo al contrato viejo de <c>POST /api/cubicacion</c> (retirado del
/// Cubicador en el mismo corte que lo publicó — no hay convivencia de los dos contratos).
/// A propósito NO hay mapeo de dominio adicional: NEXO reenvía la solicitud y devuelve la
/// respuesta tal cual — ver <see cref="Interfaces.ICubicacionRapidaPort"/> para por qué
/// esto es una excepción declarada al patrón "solo colas" del resto de Cubicación.
/// </summary>
public sealed record clsMercanciaSolicitadaInput(
    string CodigoMercancia, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm, decimal PesoKg);

/// <summary>
/// <c>OrigenSistema</c> NO viaja en el body a propósito: el Cubicador lo deriva del Bearer
/// (<c>api/dependencias.py::verificar_origen_sistema</c>) y responde 401 si el body lo
/// declara y no coincide (<c>api/routers/recomendaciones.py</c>). NEXO llama con su propia
/// llave (<c>Cubicador3D:RecomendacionesApiKey</c>) y deja que el Cubicador infiera "NEXO".
/// </summary>
public sealed record clsCrearRecomendacionInput(
    string IdLineaSolicitud, IReadOnlyList<clsMercanciaSolicitadaInput> Mercancias,
    string? ClaseViajeSeleccionada = null, string? UsuarioId = null);

public sealed record clsMercanciaDeVehiculoDto(
    string IdTipoMercancia, string CodigoMercancia, int Cantidad, decimal PesoKg, decimal VolumenM3);

/// <summary>
/// Cada campo es obligatorio y nunca llega null cuando <c>Resultado == "ASIGNADO"</c>
/// (doc del Cubicador). <c>PesoTotalKg</c> es el peso que Cotifly declaró al crear la
/// solicitud — el porcentaje de llenado no sirve para costear.
/// </summary>
public sealed record clsVehiculoRecomendadoDto(
    string IdRecomendacionVehiculo, int NumeroVehiculo, string CodigoVehiculo, string NombreVehiculo,
    string CodigoCarroceria, decimal PesoTotalKg, decimal VolumenTotalM3,
    decimal PorcentajeLlenadoVolumen, decimal PorcentajeLlenadoPeso,
    bool AlertaBajoLlenado, bool Heterogenea, IReadOnlyList<clsMercanciaDeVehiculoDto> Mercancias);

public sealed record clsMedidasEstandarDto(decimal LargoMaxCm, decimal AnchoMaxCm, decimal AltoMaxCm, decimal PesoMaxKg);

public sealed record clsPiezaFueraDeMedidaDto(string IdTipoMercancia, string CodigoMercancia, IReadOnlyList<string> Excesos);

public sealed record clsExtradimensionDto(
    bool HayExtradimension, clsMedidasEstandarDto MedidasEstandar, IReadOnlyList<clsPiezaFueraDeMedidaDto> Piezas);

/// <summary>
/// S5-HU-18.1/#273: <c>Resultado</c> es <c>"ASIGNADO"</c> | <c>"SIN_SELECCION_AUTOMATICA"</c>
/// — a propósito <c>string</c> y no un enum: el motor del Cubicador es quien es dueño de
/// este vocabulario, y un valor nuevo ahí no debe romper la deserialización en NEXO (mismo
/// criterio que <see cref="MotivoSinSeleccion"/> más abajo). Ambos valores llegan con
/// <b>200</b> — "lo que antes era error ahora es resultado" (doc del Cubicador):
/// <c>SIN_SELECCION_AUTOMATICA</c>, <c>Condicionada</c> y <c>Extradimension</c> NO son
/// fallos, son estados normales de la respuesta.
/// </summary>
public static class MotivoSinSeleccion
{
    public const string Extradimensionado = "EXTRADIMENSIONADO";
    public const string SinCandidatosDimensionales = "SIN_CANDIDATOS_DIMENSIONALES";
    public const string SuperaKMax = "SUPERA_K_MAX";
    public const string SinCarroceriaCompatible = "SIN_CARROCERIA_COMPATIBLE";

    /// <summary>
    /// S5-HU-18.1/#273: el buscador se quedó sin intentos de empaque antes de encontrar una
    /// flota — condición del ALGORITMO, distinta de <see cref="SuperaKMax"/> (no caber en
    /// k_max vehículos es una condición del NEGOCIO).
    /// </summary>
    public const string BusquedaAgotada = "BUSQUEDA_AGOTADA";
}

public sealed record clsRecomendacionDto(
    string IdRecomendacion, string IdLineaSolicitud, string OrigenSistema, string Resultado,
    string? MotivoSinSeleccion, bool Condicionada, IReadOnlyList<string> MercanciasCondicionadas,
    IReadOnlyList<clsVehiculoRecomendadoDto> Vehiculos, IReadOnlyList<string> Advertencias,
    clsExtradimensionDto? Extradimension);
