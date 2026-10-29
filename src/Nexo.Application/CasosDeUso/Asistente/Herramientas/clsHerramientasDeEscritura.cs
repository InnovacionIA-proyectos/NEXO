using System.Globalization;
using System.Text.Json;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Herramientas;

/// <summary>
/// Lo que el modelo recibe al proponer un cambio: nada se modificó, la persona tiene que confirmarlo.
/// </summary>
public sealed record clsAccionPropuestaDto(Guid AccionId, string Tipo, string Resumen, string Estado, DateTimeOffset VenceAt);

/// <summary>
/// Base de las herramientas que proponen un cambio. NINGUNA escribe en NEXO: validan la forma de lo pedido, comprueban que exista lo que
/// se quiere tocar, y dejan una acción <c>pendiente</c> con parámetros fijos. Quien la aplica es la persona, al confirmarla, y entonces
/// mandan las reglas del dominio (el piso SICE-TAC, <c>FLETE_SOLO_SICETAC</c>, cotización entregada…): el asistente no las conoce ni las
/// evade. Un valor de dinero solo se acepta si salió de una consulta de esta conversación o lo escribió la persona.
/// </summary>
public abstract class clsHerramientaPropuestaBase(
    IConversacionesAsistenteRepository conversaciones,
    clsContextoConversacionAsistente contexto,
    ICurrentUserPort usuario,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones,
    clsLimitesAsistente limites,
    TimeProvider reloj) : clsHerramientaBase
{
    private const string FuentePropuesta = "NEXO · acción propuesta (aún sin aplicar)";

    // Escribir es de quien costea: los mismos roles que los endpoints de edición de la cotización.
    public override IReadOnlyCollection<RolUsuario> Roles => [RolUsuario.AdminTarifas, RolUsuario.Admin];

    protected override string FuenteNoDisponible => FuentePropuesta;

    protected abstract string Tipo { get; }

    /// <summary>Valida lo pedido y devuelve los parámetros que se guardarán (como objeto serializable) y el resumen para la persona.</summary>
    protected abstract Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta entrada, CancellationToken ct);

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        if (contexto.ConversacionId is not { } conversacionId || contexto.MensajeId is not { } mensajeId)
            throw new clsValidationFailedException("ASISTENTE_SIN_CONVERSACION", "Solo se puede proponer un cambio dentro de una conversación.");

        var (parametros, resumen) = await ArmarAsync(entrada, ct);

        if (await conversaciones.ContarAccionesDelMensajeAsync(mensajeId, ct) >= limites.MaxAccionesPorRespuestaEfectivo)
            throw new clsValidationFailedException("DEMASIADAS_ACCIONES",
                $"Ya propusiste {limites.MaxAccionesPorRespuestaEfectivo} cambios en esta respuesta; espera a que la persona los resuelva.");

        var ahora = reloj.GetUtcNow();
        var accion = await conversaciones.CrearAccionAsync(new clsAccionAsistente
        {
            ConversacionId = conversacionId, MensajeId = mensajeId, UsuarioId = usuario.Id, Tipo = Tipo,
            Parametros = JsonSerializer.Serialize(parametros, JsonSerializerOptions.Web), Resumen = resumen,
            VenceAt = ahora + limites.VigenciaAccionEfectiva, CreatedAt = ahora,
        }, ct);

        return clsResultadoHerramienta.Con(
            new clsAccionPropuestaDto(accion.Id, accion.Tipo, accion.Resumen, accion.Estado, accion.VenceAt), FuentePropuesta,
            "Nada se ha modificado. La persona debe confirmar el cambio en pantalla; no lo des por hecho ni digas que ya está aplicado.");
    }

    /// <summary>La cotización existe (si no, es un error para el modelo y no se deja nada pendiente).</summary>
    protected Task<clsCotizacionDto> CotizacionAsync(Guid id, CancellationToken ct) =>
        cotizaciones.Handle(new clsObtenerCotizacionQuery(id), ct);

    /// <summary>
    /// El valor de dinero que el modelo quiere aplicar tiene que salir de una consulta de esta conversación o de lo que escribió la persona:
    /// un precio calculado o inventado por el modelo no se propone.
    /// </summary>
    protected async Task ExigirOrigenDelValorAsync(decimal valor, string nombreDelCampo, CancellationToken ct)
    {
        if (contexto.ConversacionId is not { } conversacionId) return;

        var mensajes = await conversaciones.MensajesAsync(conversacionId, ct);
        var herramientas = await conversaciones.HerramientasAsync(conversacionId, ct);
        var fuentes = mensajes.Where(m => m.Rol == clsMensajeAsistente.RolUsuario).Select(m => m.Contenido)
            .Concat(herramientas.Select(h => h.Salida).OfType<string>());

        if (clsVerificadorDeCifras.CifrasSinOrigen("$" + valor.ToString("0.##", CultureInfo.InvariantCulture), fuentes).Count > 0)
            throw new clsValidationFailedException("VALOR_SIN_ORIGEN",
                $"El valor de \"{nombreDelCampo}\" ({Monto(valor)}) no salió de ninguna consulta de esta conversación ni lo escribió la persona. " +
                "Consulta la fuente primero o pídele a la persona el valor; no lo calcules.");
    }

    protected static string Monto(decimal valor) => "$" + valor.ToString("#,##0.##", CultureInfo.GetCultureInfo("es-CO"));

    protected static string Corto(Guid id) => id.ToString("N")[..8];
}

/// <summary>Agregar el flete de una línea a la cotización.</summary>
public sealed class clsProponerAgregarFleteHerramienta(
    IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto, ICurrentUserPort usuario,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones, clsLimitesAsistente limites, TimeProvider reloj)
    : clsHerramientaPropuestaBase(conversaciones, contexto, usuario, cotizaciones, limites, reloj)
{
    public override string Nombre => "proponer_agregar_flete";

    public override string Descripcion =>
        "Propone agregar a una línea de la cotización un flete con el valor indicado. NO lo aplica: deja el cambio pendiente hasta que la persona " +
        "lo confirme, y las reglas del sistema (piso SICE-TAC, reglas solo SICE-TAC) se evalúan al confirmar. El valor debe salir de una consulta " +
        "de esta conversación o de lo que escribió la persona.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["cotizacionId","lineaSolicitudId","valorFlete","fuente"],"properties":{
          "cotizacionId":{"type":"string","description":"Identificador (GUID) de la cotización."},
          "lineaSolicitudId":{"type":"string","description":"Identificador (GUID) de la línea de la solicitud."},
          "valorFlete":{"type":"number","description":"Valor del flete en pesos."},
          "valorRetornoContenedor":{"type":"number","description":"Valor del retorno del contenedor, si aplica. Opcional."},
          "fuente":{"type":"string","enum":["Sicetac","Historica","Manual","Tarifario"],"description":"De dónde sale el valor."}}}
        """;

    protected override string Tipo => clsAccionAsistente.TipoAgregarFlete;

    protected override async Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta e, CancellationToken ct)
    {
        var cotizacionId = e.Identificador("cotizacionId");
        var lineaId = e.Identificador("lineaSolicitudId");
        var valor = e.Decimal("valorFlete", 1, 1_000_000_000_000m) ?? throw Falta("valorFlete");
        var retorno = e.Decimal("valorRetornoContenedor", 0, 1_000_000_000_000m);
        var fuente = e.Enumeracion<FuenteFlete>("fuente") ?? throw Falta("fuente");

        await CotizacionAsync(cotizacionId, ct);
        await ExigirOrigenDelValorAsync(valor, "valorFlete", ct);
        if (retorno is { } r) await ExigirOrigenDelValorAsync(r, "valorRetornoContenedor", ct);

        var resumen = $"Agregar a la línea {Corto(lineaId)} de la cotización {Corto(cotizacionId)} un flete de {Monto(valor)} (fuente {fuente})" +
            (retorno is { } rr ? $" y un retorno de contenedor de {Monto(rr)}." : ".");
        return (new { cotizacionId, lineaSolicitudId = lineaId, valorFlete = valor, valorRetornoContenedor = retorno, fuente = fuente.ToString() }, resumen);
    }

    private static clsValidationFailedException Falta(string campo) => new("HERRAMIENTA_ENTRADA_INVALIDA", $"proponer_agregar_flete: falta \"{campo}\".");
}

/// <summary>Corregir el valor de un flete manual ya agregado.</summary>
public sealed class clsProponerEditarFleteHerramienta(
    IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto, ICurrentUserPort usuario,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones, clsLimitesAsistente limites, TimeProvider reloj)
    : clsHerramientaPropuestaBase(conversaciones, contexto, usuario, cotizaciones, limites, reloj)
{
    public override string Nombre => "proponer_editar_flete";

    public override string Descripcion =>
        "Propone corregir el valor del flete de una línea de la cotización. NO lo aplica: queda pendiente hasta que la persona lo confirme, y " +
        "el sistema puede rechazarlo al confirmar (flete que gobierna una regla solo SICE-TAC, valor bajo el piso, cotización ya entregada). " +
        "El valor debe salir de una consulta de esta conversación o de lo que escribió la persona.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["cotizacionId","lineaSolicitudId","valorFlete"],"properties":{
          "cotizacionId":{"type":"string","description":"Identificador (GUID) de la cotización."},
          "lineaSolicitudId":{"type":"string","description":"Identificador (GUID) de la línea de la solicitud."},
          "valorFlete":{"type":"number","description":"Nuevo valor del flete en pesos."},
          "valorRetornoContenedor":{"type":"number","description":"Nuevo valor del retorno del contenedor, si aplica. Opcional."}}}
        """;

    protected override string Tipo => clsAccionAsistente.TipoEditarFlete;

    protected override async Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta e, CancellationToken ct)
    {
        var cotizacionId = e.Identificador("cotizacionId");
        var lineaId = e.Identificador("lineaSolicitudId");
        var valor = e.Decimal("valorFlete", 1, 1_000_000_000_000m)
            ?? throw new clsValidationFailedException("HERRAMIENTA_ENTRADA_INVALIDA", "proponer_editar_flete: falta \"valorFlete\".");
        var retorno = e.Decimal("valorRetornoContenedor", 0, 1_000_000_000_000m);

        await CotizacionAsync(cotizacionId, ct);
        await ExigirOrigenDelValorAsync(valor, "valorFlete", ct);
        if (retorno is { } r) await ExigirOrigenDelValorAsync(r, "valorRetornoContenedor", ct);

        var resumen = $"Cambiar el flete de la línea {Corto(lineaId)} de la cotización {Corto(cotizacionId)} a {Monto(valor)}" +
            (retorno is { } rr ? $" y el retorno de contenedor a {Monto(rr)}." : ".");
        return (new { cotizacionId, lineaSolicitudId = lineaId, valorFlete = valor, valorRetornoContenedor = retorno }, resumen);
    }
}

/// <summary>Agregar servicios conexos a la cotización.</summary>
public sealed class clsProponerAgregarConexosHerramienta(
    IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto, ICurrentUserPort usuario,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones, clsLimitesAsistente limites, TimeProvider reloj)
    : clsHerramientaPropuestaBase(conversaciones, contexto, usuario, cotizaciones, limites, reloj)
{
    private const int MaxItems = 20;

    public override string Nombre => "proponer_agregar_conexos";

    public override string Descripcion =>
        "Propone agregar servicios conexos (cargue, escolta…) a líneas de la cotización, con la cantidad indicada. NO lo aplica: queda pendiente " +
        "hasta que la persona lo confirme. Sin valor aplicado se usa el costo del catálogo; si envías uno, debe salir de una consulta de esta " +
        "conversación o de lo que escribió la persona.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["cotizacionId","items"],"properties":{
          "cotizacionId":{"type":"string","description":"Identificador (GUID) de la cotización."},
          "items":{"type":"array","description":"Los conexos a agregar (1 a 20).","items":{"type":"object","additionalProperties":false,
            "required":["lineaId","servicioConexoId","cantidad"],"properties":{
              "lineaId":{"type":"string","description":"Identificador (GUID) de la línea de la cotización a la que se agrega."},
              "servicioConexoId":{"type":"string","description":"Identificador (GUID) del servicio del catálogo de conexos."},
              "cantidad":{"type":"number","description":"Cantidad del servicio."},
              "valorAplicado":{"type":"number","description":"Valor unitario a aplicar en lugar del costo del catálogo. Opcional."}}}}}}
        """;

    protected override string Tipo => clsAccionAsistente.TipoAgregarConexos;

    protected override async Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta e, CancellationToken ct)
    {
        var cotizacionId = e.Identificador("cotizacionId");
        List<object> items = [];
        foreach (var i in e.Objetos("items", MaxItems))
        {
            var cantidad = i.Decimal("cantidad", 0.01m, 1_000_000m)
                ?? throw new clsValidationFailedException("HERRAMIENTA_ENTRADA_INVALIDA", "proponer_agregar_conexos: falta \"cantidad\".");
            var valorAplicado = i.Decimal("valorAplicado", 0, 1_000_000_000_000m);
            items.Add(new { lineaId = i.Identificador("lineaId"), servicioConexoId = i.Identificador("servicioConexoId"), cantidad, valorAplicado });
        }

        await CotizacionAsync(cotizacionId, ct);
        foreach (var i in e.Objetos("items", MaxItems))
            if (i.Decimal("valorAplicado", 0, 1_000_000_000_000m) is { } v) await ExigirOrigenDelValorAsync(v, "valorAplicado", ct);

        var resumen = $"Agregar {items.Count} servicio(s) conexo(s) a la cotización {Corto(cotizacionId)}.";
        return (new { cotizacionId, items }, resumen);
    }
}

/// <summary>Rechazar una línea con un motivo.</summary>
public sealed class clsProponerRechazarLineaHerramienta(
    IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto, ICurrentUserPort usuario,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones, clsLimitesAsistente limites, TimeProvider reloj)
    : clsHerramientaPropuestaBase(conversaciones, contexto, usuario, cotizaciones, limites, reloj)
{
    public override string Nombre => "proponer_rechazar_linea";

    public override string Descripcion =>
        "Propone rechazar una línea de la cotización con un motivo (la línea queda sin precio y no suma al total). NO lo aplica: queda pendiente " +
        "hasta que la persona lo confirme.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["cotizacionId","lineaSolicitudId","motivo"],"properties":{
          "cotizacionId":{"type":"string","description":"Identificador (GUID) de la cotización."},
          "lineaSolicitudId":{"type":"string","description":"Identificador (GUID) de la línea de la solicitud."},
          "motivo":{"type":"string","description":"Motivo del rechazo, claro para quien lea la cotización."}}}
        """;

    protected override string Tipo => clsAccionAsistente.TipoRechazarLinea;

    protected override async Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta e, CancellationToken ct)
    {
        var cotizacionId = e.Identificador("cotizacionId");
        var lineaId = e.Identificador("lineaSolicitudId");
        var motivo = e.Texto("motivo", requerido: true)!;

        await CotizacionAsync(cotizacionId, ct);
        return (new { cotizacionId, lineaSolicitudId = lineaId, motivo },
            $"Rechazar la línea {Corto(lineaId)} de la cotización {Corto(cotizacionId)} con el motivo: {motivo}");
    }
}

/// <summary>Cambiar el vehículo de una línea.</summary>
public sealed class clsProponerCambiarVehiculoHerramienta(
    IConversacionesAsistenteRepository conversaciones, clsContextoConversacionAsistente contexto, ICurrentUserPort usuario,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> cotizaciones, clsLimitesAsistente limites, TimeProvider reloj)
    : clsHerramientaPropuestaBase(conversaciones, contexto, usuario, cotizaciones, limites, reloj)
{
    public override string Nombre => "proponer_cambiar_vehiculo";

    public override string Descripcion =>
        "Propone cambiar el vehículo de una línea de la cotización (por código o texto, por ejemplo C3 o tractomula). Al aplicarse se descarta el " +
        "flete que la línea tuviera y vuelve a quedar por costear. NO lo aplica: queda pendiente hasta que la persona lo confirme.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["cotizacionId","lineaSolicitudId"],"properties":{
          "cotizacionId":{"type":"string","description":"Identificador (GUID) de la cotización."},
          "lineaSolicitudId":{"type":"string","description":"Identificador (GUID) de la línea de la solicitud."},
          "tipoVehiculo":{"type":"string","description":"Código o texto del vehículo nuevo. Envía este o configVehiculoId."},
          "configVehiculoId":{"type":"string","description":"Identificador (GUID) de la configuración de vehículo. Envía este o tipoVehiculo."},
          "carroceria":{"type":"string","description":"Carrocería, si cambia. Opcional."}}}
        """;

    protected override string Tipo => clsAccionAsistente.TipoCambiarVehiculo;

    protected override async Task<(object Parametros, string Resumen)> ArmarAsync(clsEntradaHerramienta e, CancellationToken ct)
    {
        e.ExigirAlgunoDe("tipoVehiculo", "configVehiculoId");
        var cotizacionId = e.Identificador("cotizacionId");
        var lineaId = e.Identificador("lineaSolicitudId");
        var tipo = e.Texto("tipoVehiculo");
        Guid? config = e.Texto("configVehiculoId") is null ? null : e.Identificador("configVehiculoId");
        var carroceria = e.Texto("carroceria");

        await CotizacionAsync(cotizacionId, ct);
        var vehiculo = tipo ?? $"configuración {Corto(config!.Value)}";
        return (new { cotizacionId, lineaSolicitudId = lineaId, tipoVehiculo = tipo, configVehiculoId = config, carroceria },
            $"Cambiar el vehículo de la línea {Corto(lineaId)} de la cotización {Corto(cotizacionId)} a {vehiculo}. El flete que tenga se descarta.");
    }
}
