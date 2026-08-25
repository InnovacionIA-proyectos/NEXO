namespace CabinaFletes.Mensajes.Costeo;

// Un validador por mensaje. La regla es fallo cerrado: un tipo sin validador no se
// publica, así que esta lista y la de mensajes crecen juntas por construcción.

public sealed class RepartirLineasDeCosteoValidador : IValidadorDeMensaje<RepartirLineasDeCosteo>
{
    public IReadOnlyList<string> Validar(RepartirLineasDeCosteo m) =>
        Motivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId));
}

public sealed class PliegoRepartidoValidador : IValidadorDeMensaje<PliegoRepartido>
{
    public IReadOnlyList<string> Validar(PliegoRepartido m)
    {
        var motivos = Motivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId));
        // Cero líneas sí es válido: un pliego vacío se cierra completo sin repartir nada.
        // Negativo no: sería un error de conteo, y la saga nunca alcanzaría su total.
        if (m.TotalLineas < 0)
            motivos.Add("TotalLineas no puede ser negativo: la saga nunca alcanzaría su total.");
        return motivos;
    }
}

public sealed class CostearLineaSolicitadaValidador : IValidadorDeMensaje<CostearLineaSolicitada>
{
    public IReadOnlyList<string> Validar(CostearLineaSolicitada m) =>
        Motivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId), ("LineaId", m.LineaId));
}

public sealed class LineaCosteadaValidador : IValidadorDeMensaje<LineaCosteada>
{
    public IReadOnlyList<string> Validar(LineaCosteada m)
    {
        var motivos = Motivos.De(("TrabajoId", m.TrabajoId), ("LineaId", m.LineaId));

        // Regla inviolable: nunca se propone un flete por debajo del piso. Cero o
        // negativo no es "barato", es un cálculo roto, y dejarlo pasar contamina la
        // Biblioteca de tarifas ganadas si esa cotización se marca ganada.
        if (m.ValorPropuesto <= 0)
            motivos.Add("ValorPropuesto debe ser mayor que cero: un flete en cero es un cálculo roto, no una tarifa.");

        if (string.IsNullOrWhiteSpace(m.Fuente))
            motivos.Add("Fuente es obligatoria: toda propuesta debe poder mostrar de dónde salió (trazabilidad de fuente).");

        return motivos;
    }
}

public sealed class LineaFalloTecnicoValidador : IValidadorDeMensaje<LineaFalloTecnico>
{
    public IReadOnlyList<string> Validar(LineaFalloTecnico m)
    {
        var motivos = Motivos.De(("TrabajoId", m.TrabajoId), ("LineaId", m.LineaId));
        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: una línea pendiente sin motivo no se puede explicar ni diagnosticar.");
        return motivos;
    }
}

public sealed class LineaRequiereTarifaManualValidador : IValidadorDeMensaje<LineaRequiereTarifaManual>
{
    public IReadOnlyList<string> Validar(LineaRequiereTarifaManual m)
    {
        var motivos = Motivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId), ("LineaId", m.LineaId));
        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: el Administrador de Tarifas necesita saber QUÉ falta para poder resolverlo.");
        return motivos;
    }
}

/// <summary>Los identificadores vacíos son el motivo que se repite en todos los mensajes.</summary>
internal static class Motivos
{
    public static List<string> De(params (string Nombre, Guid Valor)[] campos)
    {
        var motivos = new List<string>();
        foreach (var (nombre, valor) in campos)
            if (valor == Guid.Empty)
                motivos.Add($"{nombre} no puede ser vacío.");
        return motivos;
    }
}
