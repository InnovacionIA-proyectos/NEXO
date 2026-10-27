using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Herramientas;

/// <summary>Estimación del viaje vacío que lleva el contenedor desde la ciudad de levante hasta el origen del cargue.</summary>
public sealed class clsConsultarAprovisionamientoHerramienta(IPropuestaTarifaPort propuestas) : clsHerramientaBase
{
    public override string Nombre => "consultar_aprovisionamiento";

    public override string Descripcion =>
        "Piso SICE-TAC del aprovisionamiento de un contenedor: el viaje vacío desde la ciudad donde se levanta el contenedor hasta el " +
        "origen del cargue. Solo aplica a cargas en contenedor.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["ciudadLevante","origen"],"properties":{
          "ciudadLevante":{"type":"string","description":"Ciudad donde se levanta el contenedor."},
          "origen":{"type":"string","description":"Ciudad de origen del cargue."},
          "configuracion":{"type":"string","description":"Configuración vehicular SICE-TAC. Opcional."},
          "carroceria":{"type":"string","description":"Tipo de carrocería. Opcional."}}}
        """;

    // Es la misma consulta del formulario de captura, que solo usan quienes crean solicitudes.
    public override IReadOnlyCollection<RolUsuario> Roles => [RolUsuario.Comercial, RolUsuario.Admin];

    private const string Fuente = "SICE-TAC (DWSice), viaje vacío de aprovisionamiento";
    protected override string FuenteNoDisponible => Fuente;

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        var candidato = await propuestas.EstimarAprovisionamientoAsync(
            entrada.Texto("ciudadLevante", requerido: true)!, entrada.Texto("origen", requerido: true)!,
            entrada.Texto("configuracion"), ct, entrada.Texto("carroceria"));

        if (candidato is not null) return clsResultadoHerramienta.Con(candidato, Fuente);

        var motivo = await propuestas.MotivoSicetacNoUsadoAsync(ct);
        return clsResultadoHerramienta.SinDatos(Fuente, motivo ?? "No hay piso SICE-TAC para ese viaje vacío.");
    }
}

/// <summary>
/// La propuesta de tarifa de UNA línea de una solicitud: valor propuesto, de qué fuente salió, los candidatos que se compararon y, si la
/// línea no se costea sola, el motivo de pendiente. Es la lectura de siempre del motor de propuesta (no persiste nada).
/// </summary>
public sealed class clsConsultarPropuestaLineaHerramienta(
    IQueryHandler<clsProponerPropuestaTarifaQuery, clsPropuestaSolicitudDto> propuestas,
    ISolicitudesPort solicitudes,
    ICurrentUserPort usuario) : clsHerramientaBase
{
    public override string Nombre => "consultar_propuesta_linea";

    public override string Descripcion =>
        "Propuesta de tarifa de una línea de solicitud: valor propuesto y su fuente (SICE-TAC, plaza o histórico), los candidatos comparados, la " +
        "justificación y el motivo por el que la línea queda pendiente de revisión, si lo hay. Si no hay valor propuesto, dilo; no lo estimes.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["solicitudId","lineaId"],"properties":{
          "solicitudId":{"type":"string","description":"Identificador (GUID) de la solicitud."},
          "lineaId":{"type":"string","description":"Identificador (GUID) de la línea dentro de la solicitud."}}}
        """;

    // Los mismos roles de la Mesa de Costeo; AdminBiblioteca no entra a solicitudes.
    public override IReadOnlyCollection<RolUsuario> Roles =>
        [RolUsuario.Comercial, RolUsuario.AdminTarifas, RolUsuario.ServiciosLogisticos, RolUsuario.LiderProceso, RolUsuario.Admin];

    private const string Fuente = "NEXO · motor de propuesta de tarifa (SICE-TAC, plaza y histórico SAP)";
    protected override string FuenteNoDisponible => Fuente;

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        var solicitudId = entrada.Identificador("solicitudId");
        var lineaId = entrada.Identificador("lineaId");

        var solicitud = await solicitudes.ObtenerBasicoAsync(solicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");

        // Aislamiento por comercial: un Comercial solo ve lo que él mismo creó, también a través del asistente.
        if (usuario.Rol == RolUsuario.Comercial && solicitud.CreadoPor != usuario.Id)
            throw new clsForbiddenException("SOLICITUD_AJENA", "No tiene acceso a esta solicitud.");

        var propuesta = await propuestas.Handle(new clsProponerPropuestaTarifaQuery(solicitudId), ct);
        var linea = propuesta.Lineas.FirstOrDefault(l => l.LineaId == lineaId)
            ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "La línea no existe en esa solicitud.");

        return clsResultadoHerramienta.Con(linea, Fuente, propuesta.Advertencias.Count > 0 ? string.Join(" ", propuesta.Advertencias) : null);
    }
}
