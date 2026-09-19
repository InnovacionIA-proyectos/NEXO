namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// Lo que una fuente encontró para una línea: cuánto y de dónde.
///
/// Es un objeto de valor de dominio y no un DTO de aplicación a propósito: la regla
/// del mayor es una invariante del negocio y no puede depender de la forma en que una
/// capa externa serializa sus datos.
/// </summary>
public sealed record clsCandidatoDeTarifa(string Fuente, decimal Valor);

/// <summary>Qué se propone para una línea, con su trazabilidad.</summary>
/// <param name="Valor">Nulo cuando el servicio no puede resolverla solo.</param>
/// <param name="Fuente">Origen de la propuesta. Nulo si no hay propuesta.</param>
/// <param name="Justificacion">Por qué. Se muestra al usuario, no es para depurar.</param>
public sealed record clsResultadoDeCosteo(decimal? Valor, string? Fuente, string Justificacion)
{
    /// <summary>
    /// No hay propuesta automática: la línea necesita que el Administrador de Tarifas
    /// agregue o actualice la tarifa.
    ///
    /// NO es un fallo. El servicio hizo su trabajo y la respuesta es que aquí no hay
    /// tarifa válida. Se expone explícito y no como "valor nulo" porque de esta
    /// distinción depende que la línea NO se reintente: reintentar algo que espera a una
    /// persona quema reintentos y acaba disparando la alerta del equipo por nada.
    /// </summary>
    public bool RequiereTarifaManual => Valor is null;
}

/// <summary>
/// Las reglas inviolables del costeo por línea.
///
/// Función pura: entra qué encontró cada fuente, sale qué se propone y por qué. No
/// consulta nada, no conoce EF ni almacenes. Vive en Domain porque son invariantes del
/// negocio, y estaban en un adaptador de Infrastructure donde no se podían probar.
///
/// Las reglas que aplica:
///
///   1. Nunca por debajo del piso SICE-TAC vigente. Cotizar bajo el piso es sanción.
///   2. Regla del mayor: propuesta = MAX(tarifa de plaza, piso SICE-TAC).
///   3. Si falta una de las dos, gobierna la que haya.
///   4. Tarifa vencida se marca y no se propone: se descarta la plaza y gobierna el piso.
///   5. Trazabilidad de fuente: toda propuesta dice de dónde salió.
///   6. Sin plaza ni piso, la línea es de una persona. El histórico se ofrece como
///      referencia pero NO se propone solo.
/// </summary>
public static class clsReglaDeCosteo
{
    public static clsResultadoDeCosteo Decidir(
        clsCandidatoDeTarifa? sicetac, clsCandidatoDeTarifa? plaza, clsCandidatoDeTarifa? historico)
    {
        if (plaza is not null && sicetac is not null)
        {
            // El empate lo gana la plaza: es la tarifa comercial y respeta el piso.
            var mayor = plaza.Valor >= sicetac.Valor ? plaza : sicetac;
            var diferencia = Math.Abs(plaza.Valor - sicetac.Valor);
            var porcentaje = sicetac.Valor > 0 ? Math.Round(diferencia / sicetac.Valor * 100, 1) : 0;

            var justificacion = plaza.Valor >= sicetac.Valor
                ? $"El flete de plaza ({plaza.Valor:C0}) supera el piso SICE-TAC ({sicetac.Valor:C0}) en {porcentaje}%: se propone plaza."
                : $"El piso SICE-TAC ({sicetac.Valor:C0}) supera el flete de plaza ({plaza.Valor:C0}) en {porcentaje}%: se propone SICE-TAC para no cotizar bajo el piso.";

            return new clsResultadoDeCosteo(mayor.Valor, mayor.Fuente, justificacion);
        }

        if (plaza is not null)
            return new clsResultadoDeCosteo(plaza.Valor, plaza.Fuente,
                "Solo se encontró flete de plaza; sin piso SICE-TAC comparable para la ruta.");

        if (sicetac is not null)
            return new clsResultadoDeCosteo(sicetac.Valor, sicetac.Fuente,
                "Solo se encontró piso SICE-TAC; sin tarifa de plaza vigente para la ruta.");

        // El histórico viaja como referencia, no como propuesta: proponerlo sería
        // costear sin saber si respeta el piso vigente.
        var referencia = historico is not null
            ? $" Referencia disponible: histórico SAP {historico.Valor:C0} (el Administrador de Fletes decide manualmente)."
            : string.Empty;

        return new clsResultadoDeCosteo(null, null,
            "Sin flete de plaza ni piso SICE-TAC para la ruta: la línea requiere atención manual del " +
            "Administrador de Fletes en la bandeja de entrada, según las reglas de priorización." + referencia);
    }

    /// <summary>
    /// Igual que <see cref="Decidir"/>, pero descartando la plaza cuando su vigencia
    /// venció. Una tarifa vencida no se propone aunque sea la más alta.
    /// </summary>
    public static clsResultadoDeCosteo DecidirConTarifario(
        clsCandidatoDeTarifa? sicetac, clsCandidatoDeTarifa? plaza, clsCandidatoDeTarifa? historico, bool tarifarioVencido)
    {
        if (!tarifarioVencido) return Decidir(sicetac, plaza, historico);

        var sinPlaza = Decidir(sicetac, plaza: null, historico);

        var justificacion = sicetac is not null
            ? $"El tarifario está INACTIVO (vigencia vencida): se propone el piso SICE-TAC vigente ({sicetac.Valor:C0}); " +
              "nunca cotiza por debajo del piso legal. El tarifario vencido queda solo como referencia."
            : "La tarifa del tarifario está INACTIVA (vigencia vencida) y no hay piso SICE-TAC vigente para la ruta: " +
              "requiere revisión manual del Administrador de Fletes.";

        return sinPlaza with { Justificacion = justificacion };
    }
}
