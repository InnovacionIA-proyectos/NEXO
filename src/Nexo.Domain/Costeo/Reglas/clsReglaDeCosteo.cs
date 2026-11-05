using Nexo.Domain.Enums;
using Nexo.Domain.Common;

namespace Nexo.Domain.Costeo.Reglas;

/// <summary>Lo que una fuente encontró para una línea: cuánto y de dónde.</summary>
/// <param name="VehiculoRelajado">
/// true cuando la fuente no tiene filas del vehículo pedido y el valor salió de otro. Se muestra como
/// referencia, pero no puede gobernar solo ni entrar al recomendado.
/// </param>
public sealed record clsCandidatoDeTarifa(string Fuente, decimal Valor, bool VehiculoRelajado = false);

/// <summary>Qué se propone para una línea, con su trazabilidad.</summary>
/// <param name="Valor">Nulo cuando el servicio no puede resolverla solo.</param>
/// <param name="Fuente">Origen de la propuesta. Nulo si no hay propuesta.</param>
/// <param name="Justificacion">Por qué. Se muestra al usuario, no es para depurar.</param>
public sealed record clsResultadoDeCosteo(decimal? Valor, string? Fuente, string Justificacion)
{
    /// <summary>
    /// No hay propuesta automática: la línea espera que el Administrador de Tarifas agregue o actualice
    /// la tarifa. No es un fallo y no debe reintentarse.
    /// </summary>
    public bool RequiereTarifaManual => Valor is null;
}

/// <summary>
/// Reglas del costeo por línea (función pura: entra lo que encontró cada fuente, sale la propuesta y su justificación):
///   1. Nunca por debajo del piso SICE-TAC vigente (cotizar bajo el piso es sanción).
///   2. Regla del mayor: MAX(plaza, histórico de plaza de los últimos 3 meses, piso SICE-TAC).
///   3. Si falta plaza y piso, gobierna el que haya.
///   4. Tarifa vencida no se propone: se descarta la plaza y gobierna el piso; el histórico sigue compitiendo.
///   5. Toda propuesta dice de qué fuente salió.
///   6. Sin piso vigente el histórico nunca se propone solo (no hay con qué validar que esté al día): es solo referencia.
/// </summary>
public static class clsReglaDeCosteo
{
    public static clsResultadoDeCosteo Decidir(
        clsCandidatoDeTarifa? sicetac, clsCandidatoDeTarifa? plaza, clsCandidatoDeTarifa? historico)
    {
        if (sicetac is not null)
        {
            // Plaza e histórico compiten por el mayor; el empate lo gana la plaza.
            var mejorComercial = plaza is not null && historico is not null
                ? (plaza.Valor >= historico.Valor ? plaza : historico)
                : plaza ?? historico;

            if (mejorComercial is null)
                return new clsResultadoDeCosteo(sicetac.Valor, sicetac.Fuente,
                    "Solo se encontró piso SICE-TAC; sin tarifa de plaza ni histórico comparable para la ruta.");

            var mayor = mejorComercial.Valor >= sicetac.Valor ? mejorComercial : sicetac;
            var diferencia = Math.Abs(mejorComercial.Valor - sicetac.Valor);
            var porcentaje = sicetac.Valor > 0 ? Math.Round(diferencia / sicetac.Valor * 100, 1) : 0;
            // El fraseo para la plaza está validado contra datos reales: no cambiarlo.
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

        // Sin piso vigente el histórico es solo referencia: proponerlo no garantiza respetar el piso.
        var referencia = historico is not null
            ? $" Referencia disponible: histórico SAP {clsMoneda.Pesos(historico.Valor)} (el Administrador de Fletes decide manualmente)."
            : string.Empty;

        return new clsResultadoDeCosteo(null, null,
            "Sin flete de plaza ni piso SICE-TAC para la ruta: la línea requiere atención manual del " +
            "Administrador de Fletes en la bandeja de entrada, según las reglas de priorización." + referencia);
    }

    /// <summary>Como <see cref="Decidir"/>, pero descarta la plaza vencida aunque sea la más alta.</summary>
    public static clsResultadoDeCosteo DecidirConTarifario(
        clsCandidatoDeTarifa? sicetac, clsCandidatoDeTarifa? plaza, clsCandidatoDeTarifa? historico, bool tarifarioVencido)
    {
        if (!tarifarioVencido) return Decidir(sicetac, plaza, historico);

        var sinPlaza = Decidir(sicetac, plaza: null, historico);
        var aviso = "El tarifario está INACTIVO (vigencia vencida): se descarta la plaza; el tarifario vencido queda solo como referencia. ";

        // Si el histórico gana, la justificación de Decidir ya lo explica y solo se antepone el aviso.
        var justificacion = sinPlaza.Fuente switch
        {
            "sicetac" => aviso + $"Se propone el piso SICE-TAC vigente ({clsMoneda.Pesos(sicetac!.Valor)}); nunca cotiza por debajo del piso legal.",
            null => "La tarifa del tarifario está INACTIVA (vigencia vencida) y no hay piso SICE-TAC vigente para la ruta: " +
                    "requiere revisión manual del Administrador de Fletes.",
            _ => aviso + sinPlaza.Justificacion,
        };

        return sinPlaza with { Justificacion = justificacion };
    }

    /// <summary>Motivo con el que queda pendiente una línea sin vehículo.</summary>
    public const string MotivoLineaSinVehiculo = "Línea sin vehículo: requiere consulta completa";

    /// <summary>
    /// <c>true</c> cuando la línea es de Cupo (o no trae clase de viaje) y llega sin vehículo. No aplica a
    /// Contenedor (el vehículo se deduce por peso y clase de contenedor), Carga suelta ni Extradimensionado.
    /// </summary>
    public static bool LineaSinVehiculoQueExigeConsultaCompleta(ClaseViaje? claseViaje, string? tipoVehiculo) =>
        (claseViaje is null or ClaseViaje.Cupo) && string.IsNullOrWhiteSpace(tipoVehiculo);

    /// <summary>
    /// Flujo de decisión del flete principal de una línea:
    ///   1. Con regla Solo-SICETAC y valor SICE-TAC: solo el piso (<see cref="ModoDecision.SoloSicetac"/>).
    ///   2. Sin regla, con plaza vigente estrictamente mayor que el piso: solo la plaza (<see cref="ModoDecision.PlazaVigenteMayor"/>).
    ///   3. Todo lo demás: proceso completo (<see cref="ModoDecision.ConsultaCompleta"/>), gana el valor más alto.
    /// Solo gobiernan los candidatos que coinciden en vehículo; los relajados se anotan como referencia.
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

        // Sin vehículo el piso se busca sin filtro (toma el menor valor) y la plaza cae al mayor vigente: nunca se autocostea.
        if (lineaSinVehiculoQueLoExige)
        {
            var referencia = plazaVigenteExacta is not null
                ? Decidir(sicetacExacto, plazaVigenteExacta, historicoExacto)
                : DecidirConTarifario(sicetacExacto, plazaVencidaExacta, historicoExacto, tarifarioVencido: plazaVencidaExacta is not null);
            return new clsDecisionDeFlujo(
                referencia with { Justificacion = AnotarRelajados($"{MotivoLineaSinVehiculo}. Referencia: {referencia.Justificacion}") },
                ModoDecision.ConsultaCompleta, MotivoLineaSinVehiculo);
        }

        // Camino 1: con regla y piso, el piso gobierna solo. Con regla y sin piso pasa al proceso completo, no al camino 2.
        if (hayReglaSoloSicetac && sicetacExacto is not null)
            return new clsDecisionDeFlujo(
                new clsResultadoDeCosteo(sicetacExacto.Valor, sicetacExacto.Fuente,
                    $"La ruta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({clsMoneda.Pesos(sicetacExacto.Valor)}) sin comparar con plaza ni histórico."),
                ModoDecision.SoloSicetac);

        // Camino 2: sin regla, con plaza vigente > piso.
        if (!hayReglaSoloSicetac && sicetacExacto is not null && plazaVigenteExacta is not null && plazaVigenteExacta.Valor > sicetacExacto.Valor)
            return new clsDecisionDeFlujo(
                new clsResultadoDeCosteo(plazaVigenteExacta.Valor, plazaVigenteExacta.Fuente,
                    AnotarRelajados($"La plaza vigente ({clsMoneda.Pesos(plazaVigenteExacta.Valor)}) supera el piso SICE-TAC ({clsMoneda.Pesos(sicetacExacto.Valor)}): se propone la plaza sin consultar el histórico.")),
                ModoDecision.PlazaVigenteMayor);

        // Camino 3: proceso completo, con la plaza vencida solo como referencia.
        var resultado = plazaVigenteExacta is not null
            ? Decidir(sicetacExacto, plazaVigenteExacta, historicoExacto)
            : DecidirConTarifario(sicetacExacto, plazaVencidaExacta, historicoExacto, tarifarioVencido: plazaVencidaExacta is not null);
        return new clsDecisionDeFlujo(resultado with { Justificacion = AnotarRelajados(resultado.Justificacion) }, ModoDecision.ConsultaCompleta);
    }
}

/// <summary>Los 3 caminos de <see cref="clsReglaDeCosteo.DecidirFlujo"/>.</summary>
public enum ModoDecision { SoloSicetac, PlazaVigenteMayor, ConsultaCompleta }

/// <summary>
/// Qué se propuso y por qué camino. <see cref="MotivoPendiente"/>: motivo por el que la línea no se guarda sola
/// aunque haya valor (Cupo sin vehículo, período SICE-TAC no vigente, tramo vacío sin piso...).
/// <see cref="ValorAprovisionamiento"/>/<see cref="ValorRetornoContenedor"/>: tramos vacíos de levante y retorno del contenedor, calculados con SICE-TAC.
/// </summary>
public sealed record clsDecisionDeFlujo(
    clsResultadoDeCosteo Resultado, ModoDecision Modo, string? MotivoPendiente = null,
    decimal? ValorAprovisionamiento = null, decimal? ValorRetornoContenedor = null)
{
    public bool Autocosteable => Resultado.Valor is not null && MotivoPendiente is null;
}
