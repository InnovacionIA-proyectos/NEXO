using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// ¿La línea cae en una regla "Solo SICE-TAC"? Definición única para el motor de propuesta, la edición de
/// fletes (que la bloquea) y la Mesa de Costeo: si divergieran, la Mesa ofrecería editar lo que el servidor rechaza.
/// Función pura: las claves de las reglas las trae quien llama.
/// </summary>
public static class clsReglaSoloSicetacDeLinea
{
    /// <param name="claves">Claves origen|destino|tipoVehiculo de las reglas (clsTexto.ClaveRutaVehiculo).</param>
    /// <returns>
    /// <c>true</c> solo si hay código DANE en origen y destino (las reglas se definen por DANE) y la línea no es de
    /// Cupo sin vehículo (<see cref="clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta"/>).
    /// </returns>
    public static bool Tiene(
        IReadOnlySet<string> claves, ClaseViaje? claseViaje, string? origenDane, string? destinoDane, string? tipoVehiculo) =>
        !clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta(claseViaje, tipoVehiculo)
        && origenDane is not null && destinoDane is not null
        && claves.Contains(clsTexto.ClaveRutaVehiculo(origenDane, destinoDane, tipoVehiculo));
}
