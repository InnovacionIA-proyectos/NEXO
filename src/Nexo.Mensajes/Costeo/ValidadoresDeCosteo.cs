namespace Nexo.Mensajes.Costeo;

// Un validador por mensaje. La regla es fallo cerrado: un tipo sin validador no se
// publica, así que esta lista y la de mensajes crecen juntas por construcción.

public sealed class clsRepartirLineasDeCosteoValidador : IValidadorDeMensaje<clsRepartirLineasDeCosteo>
{
    public IReadOnlyList<string> Validar(clsRepartirLineasDeCosteo m) =>
        clsMotivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId));
}

public sealed class clsPliegoRepartidoValidador : IValidadorDeMensaje<clsPliegoRepartido>
{
    public IReadOnlyList<string> Validar(clsPliegoRepartido m)
    {
        var motivos = clsMotivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId));
        // Cero líneas sí es válido: un pliego vacío se cierra completo sin repartir nada.
        // Negativo no: sería un error de conteo, y la saga nunca alcanzaría su total.
        if (m.TotalLineas < 0)
            motivos.Add("TotalLineas no puede ser negativo: la saga nunca alcanzaría su total.");
        return motivos;
    }
}

public sealed class clsCostearLineaSolicitadaValidador : IValidadorDeMensaje<clsCostearLineaSolicitada>
{
    public IReadOnlyList<string> Validar(clsCostearLineaSolicitada m) =>
        clsMotivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId), ("LineaId", m.LineaId));
}

public sealed class clsLineaCosteadaValidador : IValidadorDeMensaje<clsLineaCosteada>
{
    public IReadOnlyList<string> Validar(clsLineaCosteada m)
    {
        var motivos = clsMotivos.De(("TrabajoId", m.TrabajoId), ("LineaId", m.LineaId));

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

public sealed class clsLineaFalloTecnicoValidador : IValidadorDeMensaje<clsLineaFalloTecnico>
{
    public IReadOnlyList<string> Validar(clsLineaFalloTecnico m)
    {
        var motivos = clsMotivos.De(("TrabajoId", m.TrabajoId), ("LineaId", m.LineaId));
        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: una línea pendiente sin motivo no se puede explicar ni diagnosticar.");
        return motivos;
    }
}

public sealed class clsLineaRequiereTarifaManualValidador : IValidadorDeMensaje<clsLineaRequiereTarifaManual>
{
    public IReadOnlyList<string> Validar(clsLineaRequiereTarifaManual m)
    {
        var motivos = clsMotivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId), ("LineaId", m.LineaId));
        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: el Administrador de Tarifas necesita saber QUÉ falta para poder resolverlo.");
        return motivos;
    }
}

public sealed class clsTarifasActualizadasValidador : IValidadorDeMensaje<clsTarifasActualizadas>
{
    public IReadOnlyList<string> Validar(clsTarifasActualizadas m)
    {
        var motivos = new List<string>();
        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: sin él no se puede auditar qué disparó un recosteo masivo.");
        if (m.OcurridoEn == default)
            motivos.Add("OcurridoEn no puede ser la fecha por defecto.");
        return motivos;
    }
}

public sealed class clsRecostearLineaSolicitadaValidador : IValidadorDeMensaje<clsRecostearLineaSolicitada>
{
    public IReadOnlyList<string> Validar(clsRecostearLineaSolicitada m) =>
        clsMotivos.De(("TrabajoId", m.TrabajoId), ("SolicitudId", m.SolicitudId), ("LineaId", m.LineaId));
}

public sealed class clsTrabajoActualizadoValidador : IValidadorDeMensaje<clsTrabajoActualizado>
{
    public IReadOnlyList<string> Validar(clsTrabajoActualizado m) => clsMotivos.De(("TrabajoId", m.TrabajoId));
}

/// <summary>Los identificadores vacíos son el motivo que se repite en todos los mensajes.</summary>
