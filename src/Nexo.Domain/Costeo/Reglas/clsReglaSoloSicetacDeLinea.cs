using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// S6-HU-26.1/#30: ¿esta línea cae en una regla "Solo SICE-TAC" (tbl_reglas_solo_sicetac)? Era una condición inline del
/// motor de propuesta (<c>clsPropuestaTarifaAdapter</c>) y ahora la necesitan también la edición de un flete (que la bloquea con
/// 409 FLETE_SOLO_SICETAC) y la Mesa de Costeo (que la expone como <c>tieneReglaSoloSicetac</c> para saber cuándo ofrecer
/// la edición). Una sola definición para los tres: si divergieran, la Mesa ofrecería editar lo que el servidor rechaza.
///
/// Función pura: las claves de las reglas las trae quien llama (IClavesSoloSicetacPort).
/// </summary>
public static class clsReglaSoloSicetacDeLinea
{
    /// <param name="claves">Claves origen|destino|tipoVehiculo de las reglas (clsTexto.ClaveRutaVehiculo).</param>
    /// <param name="origenDane">Código DANE del municipio de origen de la ruta; null si la ruta no resolvió municipio.</param>
    /// <param name="destinoDane">Ídem destino.</param>
    /// <returns>
    /// <c>true</c> solo si hay DANE en los dos lados (las reglas se definen por código DANE: sin él no hay clave que comparar)
    /// y la línea NO es una de Cupo sin vehículo (esa nunca se autocostea y ya salta la regla — ver
    /// <see cref="clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta"/>).
    /// </returns>
    public static bool Tiene(
        IReadOnlySet<string> claves, ClaseViaje? claseViaje, string? origenDane, string? destinoDane, string? tipoVehiculo) =>
        !clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta(claseViaje, tipoVehiculo)
        && origenDane is not null && destinoDane is not null
        && claves.Contains(clsTexto.ClaveRutaVehiculo(origenDane, destinoDane, tipoVehiculo));
}
