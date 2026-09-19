namespace Nexo.Mensajes.Priorizacion;

// Un validador por mensaje. Fallo cerrado: un tipo sin validador no se publica, así
// que esta lista y la de mensajes crecen juntas por construcción.

public sealed class clsArrancarPriorizacionValidador : IValidadorDeMensaje<clsArrancarPriorizacion>
{
    public IReadOnlyList<string> Validar(clsArrancarPriorizacion m) =>
        clsMotivos.De(("CadenaId", m.CadenaId));
}

public sealed class clsTicDePriorizacionValidador : IValidadorDeMensaje<clsTicDePriorizacion>
{
    public IReadOnlyList<string> Validar(clsTicDePriorizacion m) =>
        clsMotivos.De(("CadenaId", m.CadenaId));
}

public sealed class clsParametrosDePriorizacionCambiaronValidador
    : IValidadorDeMensaje<clsParametrosDePriorizacionCambiaron>
{
    public IReadOnlyList<string> Validar(clsParametrosDePriorizacionCambiaron m)
    {
        // CambiadoPor es obligatorio: recalibrar el score reordena la bandeja de todo
        // el mundo, y tiene que quedar de quién fue.
        var motivos = clsMotivos.De(("CadenaId", m.CadenaId), ("CambiadoPor", m.CambiadoPor));
        if (m.CambiadoEn == default)
            motivos.Add("CambiadoEn es obligatorio: sin fecha no hay rastro del recalibrado.");
        return motivos;
    }
}

public sealed class clsRecalcularScoresSolicitadoValidador : IValidadorDeMensaje<clsRecalcularScoresSolicitado>
{
    public IReadOnlyList<string> Validar(clsRecalcularScoresSolicitado m)
    {
        var motivos = clsMotivos.De(("CadenaId", m.CadenaId));

        // El motivo es lo único que distingue un recálculo del reloj de uno disparado
        // por el Admin. Sin él, un recálculo repetido en el log no se puede explicar.
        if (string.IsNullOrWhiteSpace(m.Motivo))
            motivos.Add("Motivo es obligatorio: es lo que explica en el log por qué se recalculó.");

        return motivos;
    }
}
