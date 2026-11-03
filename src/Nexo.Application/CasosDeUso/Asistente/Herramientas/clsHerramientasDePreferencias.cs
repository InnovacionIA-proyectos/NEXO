using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.Common;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Herramientas;

/// <summary>
/// Propone guardar una regla o preferencia propia de la persona para sus próximas conversaciones. Como todo cambio, no se guarda hasta
/// que la persona lo confirma: así un texto ajeno que aparezca en una consulta no puede dejarle al asistente una "preferencia" que la
/// persona no pidió.
/// </summary>
public sealed class clsProponerGuardarPreferenciaHerramienta(
    IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto, ICurrentUserPort usuario,
    IPreferenciasAsistenteRepository preferencias, clsLimitesAsistente limites, TimeProvider reloj)
    : clsHerramientaPropuestaBase(conversaciones, contexto, usuario, limites, reloj)
{
    public override string Nombre => "proponer_guardar_preferencia";

    public override string Descripcion =>
        "Propone recordar una regla o preferencia propia de la persona para sus conversaciones futuras (por ejemplo avisarle si plaza e histórico " +
        "difieren más de 20 %). Úsala solo cuando la persona te pida recordar algo; escribe la preferencia con sus palabras y sin valores de dinero. " +
        "NO la guarda: queda pendiente hasta que la persona la confirme. Una preferencia nunca cambia las reglas del sistema.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["texto"],"properties":{
          "texto":{"type":"string","description":"La preferencia, en una frase clara (hasta 300 caracteres), sin valores de dinero."}}}
        """;

    protected override string Tipo => clsAccionAsistente.TipoGuardarPreferencia;

    protected override async Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        var texto = clsValidadorDePreferencias.Validar(entrada.Texto("texto", requerido: true));

        var actuales = await preferencias.ListarAsync(usuario.Id, ct);
        if (actuales.Any(p => string.Equals(p.Texto, texto, StringComparison.OrdinalIgnoreCase)))
            throw new clsConflictException("PREFERENCIA_REPETIDA", "Esa preferencia ya está guardada.");
        if (actuales.Count >= limites.MaxPreferenciasEfectivo)
            throw new clsConflictException("PREFERENCIAS_LLENAS",
                $"La persona ya tiene {limites.MaxPreferenciasEfectivo} preferencias guardadas; que borre alguna antes.");

        return (new { texto }, $"Guardar tu preferencia: «{texto}». El asistente la aplicará en tus conversaciones sin cambiar las reglas del sistema.");
    }
}

/// <summary>Las preferencias guardadas de la persona.</summary>
public sealed class clsListarPreferenciasHerramienta(IPreferenciasAsistenteRepository preferencias, ICurrentUserPort usuario) : clsHerramientaBase
{
    public override string Nombre => "listar_preferencias";

    public override string Descripcion =>
        "Lista las reglas y preferencias que la persona te pidió recordar. Úsala si pregunta qué tienes guardado.";

    public override string EsquemaEntradaJson => """{"type":"object","additionalProperties":false,"properties":{}}""";

    public override IReadOnlyCollection<RolUsuario> Roles => [RolUsuario.AdminTarifas, RolUsuario.Admin];

    private const string Fuente = "NEXO · preferencias de la persona";
    protected override string FuenteNoDisponible => Fuente;

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        var lista = await preferencias.ListarAsync(usuario.Id, ct);
        return lista.Count == 0
            ? clsResultadoHerramienta.SinDatos(Fuente, "La persona no tiene preferencias guardadas.")
            : clsResultadoHerramienta.Con(lista.Select(clsPreferenciaAsistenteDto.De).ToList(), Fuente);
    }
}
