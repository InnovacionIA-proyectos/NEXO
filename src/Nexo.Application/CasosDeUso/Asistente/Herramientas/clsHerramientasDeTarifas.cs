using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Herramientas;

/// <summary>Una página de resultados con el total que cumplía el filtro, para que el asistente sepa si lo que ve es todo o una muestra.</summary>
public sealed record clsListadoAsistente<T>(int Total, IReadOnlyList<T> Items);

/// <summary>Una tarifa de SAP TM sin las columnas de auditoría y de código crudo: lo que hace falta para citar el valor y su alcance.</summary>
public sealed record clsTarifaSapAsistenteDto(
    string? LineaNegocio, string? Origen, string? Destino, decimal? Valor, string ValorTexto, DateTime Vigencia,
    string? TipoVehiculo, string? TipoCarroceria, string? ClaseViaje, string? ClaseContenedor, string? TamanoContenedor,
    int? RangoPesoInf, int? RangoPesoSup, string? PesoLimite, bool? Devolucion, bool? Consolidado, bool? Exclusivo, string? Detalles);

/// <summary>Una tarifa de plaza propia de NEXO (<c>tbl_tarifas_plaza</c>), con los nombres ya resueltos.</summary>
public sealed record clsTarifaPlazaNexoAsistenteDto(
    string LineaNegocio, string ClaseViaje, string Origen, string? ZonaOrigen, string Destino, string? ZonaDestino,
    string? ClaseContenedor, string? RangoPeso, string? TipoVehiculo, decimal Valor, DateOnly Vigencia, string Detalles);

/// <summary>Piso SICE-TAC vigente de una ruta (el mismo que usa la Mesa de Costeo).</summary>
public sealed class clsConsultarPisoSicetacHerramienta(IPropuestaTarifaPort propuestas) : clsHerramientaBase
{
    public override string Nombre => "consultar_piso_sicetac";

    public override string Descripcion =>
        "Piso SICE-TAC vigente (matriz oficial del Ministerio de Transporte) de una ruta de carga, con su desglose de horas. " +
        "Origen y destino van como nombre de ciudad; si no estás seguro de cómo se llaman, búscalos antes con buscar_ciudades_dane. " +
        "Si no devuelve valor, no hay piso para esa ruta y no debes estimar uno.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"required":["origen","destino"],"properties":{
          "origen":{"type":"string","description":"Ciudad de origen del viaje."},
          "destino":{"type":"string","description":"Ciudad de destino del viaje."},
          "vehiculo":{"type":"string","description":"Configuración vehicular SICE-TAC, por ejemplo C2, C3, 3S2, 3S3. Opcional."},
          "carroceria":{"type":"string","description":"Tipo de carrocería, por ejemplo FURGON. Opcional."},
          "tipologia":{"type":"string","enum":["Contenedor","CargaSuelta","GranelSolido","GranelLiquido","Alimentos","Refrigerados","CargaSeca"],"description":"Tipología de la carga. Opcional."}}}
        """;

    protected override string FuenteNoDisponible => "SICE-TAC (DWSice)";

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        var origen = entrada.Texto("origen", requerido: true)!;
        var destino = entrada.Texto("destino", requerido: true)!;

        var piso = await propuestas.PisoSicetacVigenteAsync(
            origen, destino, entrada.Texto("vehiculo"), entrada.Enumeracion<TipologiaCarga>("tipologia"), entrada.Texto("carroceria"), ct);

        if (piso is not null)
            return clsResultadoHerramienta.Con(piso, $"SICE-TAC (DWSice), periodo {piso.Periodo}");

        var motivo = await propuestas.MotivoSicetacNoUsadoAsync(ct);
        return clsResultadoHerramienta.SinDatos("SICE-TAC (DWSice)", motivo ?? "No hay piso SICE-TAC vigente para esa ruta y configuración.");
    }
}

/// <summary>Base de las dos consultas a SAP TM (plaza vigente e histórico): mismos filtros, misma forma de respuesta.</summary>
public abstract class clsHerramientaSapBase : clsHerramientaBase
{
    private const int TopPorDefecto = 10;
    private const int TopMaximo = 25;

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"properties":{
          "q":{"type":"string","description":"Texto de la ruta: parte del nombre de la ciudad de origen o de destino."},
          "lineaNegocio":{"type":"integer","description":"Código de la línea de negocio SAP. Opcional."},
          "tipoVehiculo":{"type":"string","description":"Código del tipo de vehículo del tarifario SAP. Opcional."},
          "tipoCarroceria":{"type":"string","description":"Código del tipo de carrocería. Opcional."},
          "claseViaje":{"type":"integer","description":"Código de la clase de viaje SAP. Opcional."},
          "top":{"type":"integer","description":"Cuántas filas devolver (1 a 25, por defecto 10)."}}}
        """;

    protected abstract string Fuente { get; }
    protected override string FuenteNoDisponible => Fuente;

    protected abstract Task<clsSapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroceria, int? claseViaje, int top, CancellationToken ct);

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        entrada.ExigirAlgunoDe("q", "lineaNegocio", "tipoVehiculo", "tipoCarroceria", "claseViaje");
        var top = entrada.Tope("top", TopPorDefecto, TopMaximo);

        var resultado = await BuscarAsync(
            entrada.Texto("q"), entrada.Entero("lineaNegocio", 0, int.MaxValue), entrada.Texto("tipoVehiculo"),
            entrada.Texto("tipoCarroceria"), entrada.Entero("claseViaje", 0, int.MaxValue), top, ct);

        if (resultado.Items.Count == 0)
            return clsResultadoHerramienta.SinDatos(Fuente, "No hay tarifas con esos filtros.");

        var items = resultado.Items.Select(Resumir).ToList();
        return clsResultadoHerramienta.Con(
            new clsListadoAsistente<clsTarifaSapAsistenteDto>(resultado.Total, items), Fuente,
            resultado.Total > items.Count ? $"Se muestran {items.Count} de {resultado.Total}; afina los filtros para ver otras." : null);
    }

    private static clsTarifaSapAsistenteDto Resumir(clsSapTarifaHistoricoItemDto t) => new(
        t.LineaNegocioNombre, t.OrigenNombre ?? t.Origen, t.DestinoNombre ?? t.Destino, t.Valor, t.ValorTexto, t.VigenciaAjustada ?? t.Vigencia,
        t.TipoVehiculoNombre ?? t.TipoVehiculo, t.TipoCarroseriaNombre ?? t.TipoCarroseria, t.ClaseViajeNombre, t.ClaseContenedor?.ToString(),
        t.TamanoContenedor, t.RangoPesoInf, t.RangoPesoSup, t.PesoLimite, t.Devolucion, t.Consolidado, t.Exclusivo, t.DetallesTarifa);
}

/// <summary>Tarifas de plaza vigentes de SAP TM.</summary>
public sealed class clsConsultarTarifaPlazaSapHerramienta(IQueryHandler<clsBuscarSapPlazaQuery, clsSapHistoricoResultDto> plaza)
    : clsHerramientaSapBase
{
    public override string Nombre => "consultar_tarifa_plaza_sap";

    public override string Descripcion =>
        "Tarifas de plaza vigentes de SAP TM (el flete que la empresa tiene pactado para una ruta). Devuelve valor, vigencia y el alcance de cada " +
        "tarifa (vehículo, carrocería, clase de viaje, rango de peso). Es una consulta por filtros, no una propuesta: para el valor propuesto de " +
        "una línea usa consultar_propuesta_linea.";

    protected override string Fuente => "SAP TM · tarifas de plaza vigentes (tblSAPTarifas)";

    protected override Task<clsSapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroceria, int? claseViaje, int top, CancellationToken ct) =>
        plaza.Handle(new clsBuscarSapPlazaQuery(q, lineaNegocio, tipoVehiculo, tipoCarroceria, claseViaje, top), ct);
}

/// <summary>Histórico de cambios de tarifas de SAP TM: lo que se pagó o se pactó antes.</summary>
public sealed class clsConsultarHistoricoSapHerramienta(IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto> historico)
    : clsHerramientaSapBase
{
    public override string Nombre => "consultar_historico_sap";

    public override string Descripcion =>
        "Histórico de tarifas de SAP TM (log de cambios): valores anteriores de una ruta. Sirve de referencia para comparar, no es una " +
        "tarifa vigente. Cada fila trae su vigencia.";

    protected override string Fuente => "SAP TM · histórico de tarifas (tblSAPTarifasLog)";

    protected override Task<clsSapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroceria, int? claseViaje, int top, CancellationToken ct) =>
        historico.Handle(new clsBuscarSapHistoricoQuery(q, lineaNegocio, tipoVehiculo, tipoCarroceria, claseViaje, top), ct);
}

/// <summary>Tarifas de plaza que gobierna NEXO (<c>tbl_tarifas_plaza</c>). Solo las ven los roles que administran tarifas.</summary>
public sealed class clsConsultarTarifaPlazaNexoHerramienta(IQueryHandler<clsListarTarifasPlazaQuery, IReadOnlyList<clsTarifaPlazaDto>> tarifas)
    : clsHerramientaBase
{
    private const int TopPorDefecto = 10;
    private const int TopMaximo = 25;

    public override string Nombre => "consultar_tarifa_plaza_nexo";

    public override string Descripcion =>
        "Tarifas de plaza propias de NEXO (las que administra el equipo de tarifas), con origen, destino, zona, rango de peso o vehículo, " +
        "valor y vigencia.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"properties":{
          "q":{"type":"string","description":"Parte del nombre de la ciudad o zona de origen o de destino."},
          "lineaNegocio":{"type":"string","description":"Nombre de la línea de negocio. Opcional."},
          "tipoVehiculo":{"type":"string","description":"Tipo de vehículo de la tarifa. Opcional."},
          "top":{"type":"integer","description":"Cuántas filas devolver (1 a 25, por defecto 10)."}}}
        """;

    public override IReadOnlyCollection<RolUsuario> Roles => [RolUsuario.AdminTarifas, RolUsuario.AdminBiblioteca, RolUsuario.Admin];

    private const string Fuente = "NEXO · tarifas de plaza (tbl_tarifas_plaza)";
    protected override string FuenteNoDisponible => Fuente;

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        entrada.ExigirAlgunoDe("q", "lineaNegocio", "tipoVehiculo");
        var q = entrada.Texto("q");
        var linea = entrada.Texto("lineaNegocio");
        var vehiculo = entrada.Texto("tipoVehiculo");
        var top = entrada.Tope("top", TopPorDefecto, TopMaximo);

        var coinciden = (await tarifas.Handle(new clsListarTarifasPlazaQuery(), ct))
            .Where(t => q is null || Contiene(q, t.Origen, t.ZonaOrigen, t.Destino, t.ZonaDestino))
            .Where(t => linea is null || Contiene(linea, t.LineaNegocio))
            .Where(t => vehiculo is null || Contiene(vehiculo, t.TipoVehiculo))
            .ToList();

        if (coinciden.Count == 0)
            return clsResultadoHerramienta.SinDatos(Fuente, "No hay tarifas de plaza de NEXO con esos filtros.");

        var items = coinciden.Take(top).Select(t => new clsTarifaPlazaNexoAsistenteDto(
            t.LineaNegocio, t.ClaseViaje, t.Origen, t.ZonaOrigen, t.Destino, t.ZonaDestino, t.ClaseContenedor, t.RangoPeso, t.TipoVehiculo,
            t.Valor, t.Vigencia, t.DetallesTarifa)).ToList();

        return clsResultadoHerramienta.Con(
            new clsListadoAsistente<clsTarifaPlazaNexoAsistenteDto>(coinciden.Count, items), Fuente,
            coinciden.Count > items.Count ? $"Se muestran {items.Count} de {coinciden.Count}; afina los filtros para ver otras." : null);
    }

    /// <summary>Coincidencia sin tildes ni mayúsculas, como el resto de comparaciones de texto del sistema.</summary>
    private static bool Contiene(string buscado, params string?[] campos)
    {
        var clave = clsTexto.Normalizar(buscado);
        return campos.Any(c => c is not null && clsTexto.Normalizar(c).Contains(clave, StringComparison.Ordinal));
    }
}
