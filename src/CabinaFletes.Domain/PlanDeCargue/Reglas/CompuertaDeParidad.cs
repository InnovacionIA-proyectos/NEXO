namespace CabinaFletes.Domain.PlanDeCargue.Reglas;

/// <summary>
/// S2-HU-08.2 (#30): la función de plan de cargue solo se habilita cuando los golden
/// tests de paridad del motor están 26/26 (S2-HU-07.3). Función pura: no lee
/// configuración ni hace I/O — solo decide, dados los tres campos, si la evidencia
/// alcanza. El adaptador que sí lee `IConfiguration` vive en Infrastructure
/// (ParidadCubicadorAdapter).
///
/// NO es un booleano suelto: <paramref name="verificada"/> por sí solo no habilita nada.
/// Tiene que venir acompañado de a QUÉ evidencia concreta se refiere
/// (<paramref name="evidencia"/>: típicamente la corrida de CI de
/// `cubicador-ci.yml` que confirmó 26/26, o el commit de los golden tests) y CUÁNDO se
/// confirmó (<paramref name="fecha"/>). Sin los tres, la compuerta queda cerrada — fail
/// closed, igual criterio que el resto de guardas de Producción de Nexo.
/// </summary>
public static class CompuertaDeParidad
{
    public static bool EstaHabilitada(bool verificada, string? evidencia, DateOnly? fecha) =>
        verificada && !string.IsNullOrWhiteSpace(evidencia) && fecha is not null;
}
