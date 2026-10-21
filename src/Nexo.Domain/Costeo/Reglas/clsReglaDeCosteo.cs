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
    /// S5-HU-19.2/#282: los 3 pasos del flujo de decisión (sección "Flujo objetivo" del
    /// milestone S5-HU-19) — reemplaza la llamada directa a <see cref="DecidirConTarifario"/>
    /// desde el adaptador. <see cref="Decidir"/>/<see cref="DecidirConTarifario"/> NO
    /// cambian: siguen siendo exactamente la regla del Paso 4 (consulta completa), ahora
    /// invocada desde acá en vez de directo.
    /// </summary>
    public static clsDecisionDeFlujo DecidirFlujo(
        bool hayReglaSoloSicetac, clsCandidatoDeTarifa? sicetac,
        clsCandidatoDeTarifa? plazaVigente, clsCandidatoDeTarifa? plazaVencida, clsCandidatoDeTarifa? historico,
        bool lineaSinVehiculoQueLoExige = false)
    {
        // S5-HU-20.5/#297: una línea de Cupo (o sin clase de viaje) SIN vehículo nunca se autocostea.
        // Sin vehículo el piso SICE-TAC se busca sin filtro y toma el menor valor, y la plaza cae al
        // "mayor valor vigente" solo con una advertencia: el precio sale bajo y nadie lo mira. Se
        // salta la regla Solo-SICETAC (Paso 1) y la plaza que gana sola (Paso 2) y va directo a
        // consulta completa: los candidatos siguen saliendo como referencia, y la línea queda
        // pendiente de que el Administrador de Tarifas elija o ingrese el valor (nunca bajo el piso).
        if (lineaSinVehiculoQueLoExige)
        {
            var referencia = plazaVigente is not null
                ? Decidir(sicetac, plazaVigente, historico)
                : DecidirConTarifario(sicetac, plazaVencida, historico, tarifarioVencido: plazaVencida is not null);
            return new clsDecisionDeFlujo(
                referencia with { Justificacion = $"{MotivoLineaSinVehiculo}. Referencia: {referencia.Justificacion}" },
                ModoDecision.ConsultaCompleta, MotivoLineaSinVehiculo);
        }

        // Paso 1: con regla y con piso, el piso gobierna solo — ni plaza ni histórico se
        // miran. Con regla pero SIN piso, no hay nada que proponer por esta vía: sigue al
        // paso 4 directo (nunca al paso 2 — el paso 2 es explícitamente "sin regla").
        if (hayReglaSoloSicetac && sicetac is not null)
            return new clsDecisionDeFlujo(
                new clsResultadoDeCosteo(sicetac.Valor, sicetac.Fuente,
                    $"La ruta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({clsMoneda.Pesos(sicetac.Valor)}) sin comparar con plaza ni histórico."),
                ModoDecision.SoloSicetac);

        // Paso 2: sin regla, con piso Y con plaza vigente ESTRICTAMENTE mayor que el
        // piso, la plaza gobierna sola — el histórico ni se consulta (ver el adaptador,
        // que ya se ahorra la consulta cuando esta condición se puede anticipar).
        if (!hayReglaSoloSicetac && sicetac is not null && plazaVigente is not null && plazaVigente.Valor > sicetac.Valor)
            return new clsDecisionDeFlujo(
                new clsResultadoDeCosteo(plazaVigente.Valor, plazaVigente.Fuente,
                    $"La plaza vigente ({clsMoneda.Pesos(plazaVigente.Valor)}) supera el piso SICE-TAC ({clsMoneda.Pesos(sicetac.Valor)}): se propone la plaza sin consultar el histórico."),
                ModoDecision.PlazaVigenteMayor);

        // Paso 3/4: todo lo demás (sin plaza vigente, plaza vigente que no supera el
        // piso, plaza vencida, o sin piso) va a consulta completa — la regla del mayor de
        // siempre, con la plaza vigente si la hay, o la vencida como referencia si no.
        var resultado = plazaVigente is not null
            ? Decidir(sicetac, plazaVigente, historico)
            : DecidirConTarifario(sicetac, plazaVencida, historico, tarifarioVencido: plazaVencida is not null);
        return new clsDecisionDeFlujo(resultado, ModoDecision.ConsultaCompleta);
    }
}

/// <summary>Los 3 modos del flujo de decisión — ver <see cref="clsReglaDeCosteo.DecidirFlujo"/>.</summary>
public enum ModoDecision { SoloSicetac, PlazaVigenteMayor, ConsultaCompleta }

/// <summary>
/// Qué se propuso y por qué camino. <see cref="Modo"/> es lo que decide si el auto-costeo
/// guarda el flete (SoloSicetac/PlazaVigenteMayor) o deja la línea pendiente de un
/// humano (ConsultaCompleta) — ver clsCapturarSolicitudCommand/clsCostearLineaCommand.
/// <see cref="MotivoPendiente"/> (S5-HU-20.5/#297): motivo específico con el que queda pendiente
/// la línea cuando no es el genérico de consulta completa (hoy: línea de Cupo sin vehículo).
/// </summary>
public sealed record clsDecisionDeFlujo(clsResultadoDeCosteo Resultado, ModoDecision Modo, string? MotivoPendiente = null);
