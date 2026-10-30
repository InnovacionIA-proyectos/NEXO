using Nexo.Domain.Enums;
using Nexo.Domain.Common;

namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// Lo que una fuente encontró para una línea: cuánto y de dónde.
///
/// Es un objeto de valor de dominio y no un DTO de aplicación a propósito: la regla
/// del mayor es una invariante del negocio y no puede depender de la forma en que una
/// capa externa serializa sus datos.
/// </summary>
/// <param name="VehiculoRelajado">
/// true cuando la línea pide un vehículo y la fuente no tiene filas de ese vehículo, de modo que el valor salió de otro. Un candidato así
/// se muestra como referencia, pero no puede gobernar solo ni entrar al recomendado.
/// </param>
public sealed record clsCandidatoDeTarifa(string Fuente, decimal Valor, bool VehiculoRelajado = false);

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
///   2. Regla del mayor: propuesta = MAX(tarifa de plaza, histórico de plaza (últimos
///      3 meses), piso SICE-TAC). El histórico solo compite cuando hay piso vigente
///      con el cual validar que no está desactualizado (ver regla 6).
///   3. Si falta plaza y falta piso, gobierna el que haya.
///   4. Tarifa vencida se marca y no se propone: se descarta la plaza y gobierna el
///      piso (el histórico sigue compitiendo; no depende de la vigencia del tarifario).
///   5. Trazabilidad de fuente: toda propuesta dice de dónde salió.
///   6. Sin piso vigente, el histórico NUNCA compite ni se propone solo: no hay piso
///      contra el cual validar que no esté desactualizado. Se ofrece solo como
///      referencia para que una persona decida.
/// </summary>
public static class clsReglaDeCosteo
{
    public static clsResultadoDeCosteo Decidir(
        clsCandidatoDeTarifa? sicetac, clsCandidatoDeTarifa? plaza, clsCandidatoDeTarifa? historico)
    {
        if (sicetac is not null)
        {
            // Con piso vigente, plaza e histórico compiten por el mayor; el empate
            // entre ambos lo gana la plaza, por ser la tarifa comercial vigente.
            var mejorComercial = plaza is not null && historico is not null
                ? (plaza.Valor >= historico.Valor ? plaza : historico)
                : plaza ?? historico;

            if (mejorComercial is null)
                return new clsResultadoDeCosteo(sicetac.Valor, sicetac.Fuente,
                    "Solo se encontró piso SICE-TAC; sin tarifa de plaza ni histórico comparable para la ruta.");

            var mayor = mejorComercial.Valor >= sicetac.Valor ? mejorComercial : sicetac;
            var diferencia = Math.Abs(mejorComercial.Valor - sicetac.Valor);
            var porcentaje = sicetac.Valor > 0 ? Math.Round(diferencia / sicetac.Valor * 100, 1) : 0;
            // Fraseo idéntico al histórico de "El flete de plaza ... supera/es superado
            // por el piso SICE-TAC" cuando el comercial es la plaza (no romper el
            // fraseo ya validado contra datos reales, ver clsPliegoDatasetValidacionTests);
            // el histórico usa el mismo patrón con su propio sujeto.
            var (sujeto, sujetoObjeto) = mejorComercial.Fuente == "plaza"
                ? ("El flete de plaza", "el flete de plaza")
                : ("El histórico de plaza (últimos 3 meses)", "el histórico de plaza (últimos 3 meses)");

            var justificacion = mejorComercial.Valor >= sicetac.Valor
                ? $"{sujeto} ({clsMoneda.Pesos(mejorComercial.Valor)}) supera el piso SICE-TAC ({clsMoneda.Pesos(sicetac.Valor)}) en {porcentaje}%: se propone {mejorComercial.Fuente}."
                : $"El piso SICE-TAC ({clsMoneda.Pesos(sicetac.Valor)}) supera {sujetoObjeto} ({clsMoneda.Pesos(mejorComercial.Valor)}) en {porcentaje}%: se propone SICE-TAC para no cotizar bajo el piso.";

            return new clsResultadoDeCosteo(mayor.Valor, mayor.Fuente, justificacion);
        }

        if (plaza is not null)
            return new clsResultadoDeCosteo(plaza.Valor, plaza.Fuente,
                "Solo se encontró flete de plaza; sin piso SICE-TAC comparable para la ruta.");

        // Sin piso vigente, el histórico viaja como referencia, no como propuesta:
        // proponerlo sería costear sin saber si respeta el piso vigente.
        var referencia = historico is not null
            ? $" Referencia disponible: histórico SAP {clsMoneda.Pesos(historico.Valor)} (el Administrador de Fletes decide manualmente)."
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
        var aviso = "El tarifario está INACTIVO (vigencia vencida): se descarta la plaza; el tarifario vencido queda solo como referencia. ";

        // El histórico puede seguir compitiendo contra el piso (regla 4): si gana, la
        // justificación de Decidir ya lo explica y solo se antepone el aviso de plaza
        // vencida. Si nadie más que el piso queda en pie, se aclara que nunca cotiza
        // por debajo de él.
        var justificacion = sinPlaza.Fuente switch
        {
            "sicetac" => aviso + $"Se propone el piso SICE-TAC vigente ({clsMoneda.Pesos(sicetac!.Valor)}); nunca cotiza por debajo del piso legal.",
            null => "La tarifa del tarifario está INACTIVA (vigencia vencida) y no hay piso SICE-TAC vigente para la ruta: " +
                    "requiere revisión manual del Administrador de Fletes.",
            _ => aviso + sinPlaza.Justificacion,
        };

        return sinPlaza with { Justificacion = justificacion };
    }

    /// <summary>Motivo con el que queda pendiente una línea sin vehículo (S5-HU-20.5/#297).</summary>
    public const string MotivoLineaSinVehiculo = "Línea sin vehículo: requiere consulta completa";

    /// <summary>
    /// S5-HU-20.5/#297: <c>true</c> cuando la línea es de Cupo —o no trae clase de viaje— y llega sin
    /// vehículo. NO aplica a Contenedor (el vehículo se deduce por peso y clase de contenedor), ni a
    /// Carga suelta ni a Extradimensionado (decidido por Sara: solo Cupo).
    /// </summary>
    public static bool LineaSinVehiculoQueExigeConsultaCompleta(ClaseViaje? claseViaje, string? tipoVehiculo) =>
        (claseViaje is null or ClaseViaje.Cupo) && string.IsNullOrWhiteSpace(tipoVehiculo);

    /// <summary>
    /// Flujo de decisión del flete principal de una línea, en tres caminos:
    ///
    ///   1. Con regla Solo-SICETAC y valor SICE-TAC: se usa solo el piso (<see cref="ModoDecision.SoloSicetac"/>).
    ///   2. Sin regla, con piso y con plaza vigente estrictamente mayor que el piso: se usa solo la plaza
    ///      (<see cref="ModoDecision.PlazaVigenteMayor"/>); el histórico no se consulta.
    ///   3. Todo lo demás es proceso completo (<see cref="ModoDecision.ConsultaCompleta"/>): se comparan piso, plaza vigente
    ///      (o la vencida solo como referencia) e histórico, y el recomendado es el valor más alto (<see cref="Decidir"/>).
    ///
    /// Solo los candidatos que coinciden en vehículo pueden gobernar o ser recomendados: uno con el vehículo relajado
    /// (<see cref="clsCandidatoDeTarifa.VehiculoRelajado"/>) se anota en la justificación como referencia y no cuenta.
    /// Si el proceso completo no deja ningún valor, la línea queda sin recomendado.
    /// </summary>
    public static clsDecisionDeFlujo DecidirFlujo(
        bool hayReglaSoloSicetac, clsCandidatoDeTarifa? sicetac,
        clsCandidatoDeTarifa? plazaVigente, clsCandidatoDeTarifa? plazaVencida, clsCandidatoDeTarifa? historico,
        bool lineaSinVehiculoQueLoExige = false)
    {
        // Solo los candidatos que coinciden en vehículo compiten; los relajados quedan como referencia.
        static clsCandidatoDeTarifa? Exacto(clsCandidatoDeTarifa? c) => c is { VehiculoRelajado: false } ? c : null;
        var sicetacExacto = Exacto(sicetac);
        var plazaVigenteExacta = Exacto(plazaVigente);
        var plazaVencidaExacta = Exacto(plazaVencida);
        var historicoExacto = Exacto(historico);

        var referenciasRelajadas = new[] { sicetac, plazaVigente, plazaVencida, historico }
            .Where(c => c is { VehiculoRelajado: true }).Select(c => c!).ToList();
        string AnotarRelajados(string justificacion) => referenciasRelajadas.Count == 0
            ? justificacion
            : justificacion + " Sin coincidencia de vehículo (solo referencia, no se recomienda): " +
              string.Join("; ", referenciasRelajadas.Select(c => $"{c.Fuente} {clsMoneda.Pesos(c.Valor)}")) + ".";

        // Una línea de Cupo (o sin clase de viaje) sin vehículo nunca se autocostea: sin vehículo el piso SICE-TAC se
        // busca sin filtro y toma el menor valor, y la plaza cae al mayor valor vigente. Va directo a proceso completo,
        // con los candidatos como referencia, y queda pendiente con su motivo propio.
        if (lineaSinVehiculoQueLoExige)
        {
            var referencia = plazaVigenteExacta is not null
                ? Decidir(sicetacExacto, plazaVigenteExacta, historicoExacto)
                : DecidirConTarifario(sicetacExacto, plazaVencidaExacta, historicoExacto, tarifarioVencido: plazaVencidaExacta is not null);
            return new clsDecisionDeFlujo(
                referencia with { Justificacion = AnotarRelajados($"{MotivoLineaSinVehiculo}. Referencia: {referencia.Justificacion}") },
                ModoDecision.ConsultaCompleta, MotivoLineaSinVehiculo);
        }

        // Camino 1: con regla y con piso, el piso gobierna solo. Con regla pero sin piso no hay nada que proponer por esta
        // vía: pasa al proceso completo (nunca al camino 2, que es explícitamente "sin regla").
        if (hayReglaSoloSicetac && sicetacExacto is not null)
            return new clsDecisionDeFlujo(
                new clsResultadoDeCosteo(sicetacExacto.Valor, sicetacExacto.Fuente,
                    $"La ruta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({clsMoneda.Pesos(sicetacExacto.Valor)}) sin comparar con plaza ni histórico."),
                ModoDecision.SoloSicetac);

        // Camino 2: sin regla, con piso y con plaza vigente estrictamente mayor que el piso, la plaza gobierna sola.
        if (!hayReglaSoloSicetac && sicetacExacto is not null && plazaVigenteExacta is not null && plazaVigenteExacta.Valor > sicetacExacto.Valor)
            return new clsDecisionDeFlujo(
                new clsResultadoDeCosteo(plazaVigenteExacta.Valor, plazaVigenteExacta.Fuente,
                    AnotarRelajados($"La plaza vigente ({clsMoneda.Pesos(plazaVigenteExacta.Valor)}) supera el piso SICE-TAC ({clsMoneda.Pesos(sicetacExacto.Valor)}): se propone la plaza sin consultar el histórico.")),
                ModoDecision.PlazaVigenteMayor);

        // Camino 3: proceso completo. Regla del mayor entre los candidatos que coinciden en vehículo, con la plaza vigente
        // si la hay o la vencida solo como referencia.
        var resultado = plazaVigenteExacta is not null
            ? Decidir(sicetacExacto, plazaVigenteExacta, historicoExacto)
            : DecidirConTarifario(sicetacExacto, plazaVencidaExacta, historicoExacto, tarifarioVencido: plazaVencidaExacta is not null);
        return new clsDecisionDeFlujo(resultado with { Justificacion = AnotarRelajados(resultado.Justificacion) }, ModoDecision.ConsultaCompleta);
    }
}

/// <summary>Los 3 caminos del flujo de decisión — ver <see cref="clsReglaDeCosteo.DecidirFlujo"/>.</summary>
public enum ModoDecision { SoloSicetac, PlazaVigenteMayor, ConsultaCompleta }

/// <summary>
/// Qué se propuso y por qué camino.
///
/// <see cref="MotivoPendiente"/>: motivo con el que la línea NO se guarda sola aunque haya un valor propuesto (línea de Cupo sin
/// vehículo, período SICE-TAC no vigente, tramo vacío sin piso...). <see cref="ValorAprovisionamiento"/> y
/// <see cref="ValorRetornoContenedor"/> son los tramos vacíos de levante y de retorno del contenedor, ya calculados con SICE-TAC.
///
/// <see cref="Autocosteable"/>: el flete se guarda solo cuando hay un valor propuesto y ningún motivo de pendiente, sea cual
/// sea el camino de decisión. Sin valor (proceso completo sin ningún candidato) o con motivo, la línea queda pendiente.
/// </summary>
public sealed record clsDecisionDeFlujo(
    clsResultadoDeCosteo Resultado, ModoDecision Modo, string? MotivoPendiente = null,
    decimal? ValorAprovisionamiento = null, decimal? ValorRetornoContenedor = null)
{
    public bool Autocosteable => Resultado.Valor is not null && MotivoPendiente is null;
}
