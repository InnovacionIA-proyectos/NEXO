using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Asistente.Herramientas;

/// <summary>Catálogo de servicios logísticos conexos (cargue, descargue, estibas, escolta…) con su costo unitario vigente y su precio de venta (uso interno).</summary>
public sealed class clsConsultarServiciosConexosHerramienta(IQueryHandler<clsListarServiciosConexosQuery, clsPaginaServiciosDto> servicios)
    : clsHerramientaBase
{
    private const int TopPorDefecto = 10;
    private const int TopMaximo = 25;

    public override string Nombre => "consultar_servicios_conexos";

    public override string Descripcion =>
        "Servicios logísticos conexos activos del catálogo de NEXO (por ejemplo cargue, descargue o escolta) con costo unitario, precio de venta " +
        "(precioVenta; si viene vacío el servicio se cotiza a su costo), unidad, ciudad o ruta y vigencia. Los precios salen de aquí; no los estimes. " +
        "El costo es de uso interno: lo que se cotiza al cliente es el precio de venta.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"properties":{
          "q":{"type":"string","description":"Texto a buscar en el servicio, la ciudad, el puerto o la ruta."},
          "categoria":{"type":"string","description":"Categoría del servicio. Opcional."},
          "top":{"type":"integer","description":"Cuántas filas devolver (1 a 25, por defecto 10)."}}}
        """;

    private const string Fuente = "NEXO · catálogo de servicios logísticos conexos";
    protected override string FuenteNoDisponible => Fuente;

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        var top = entrada.Tope("top", TopPorDefecto, TopMaximo);
        var pagina = await servicios.Handle(
            new clsListarServiciosConexosQuery(SoloActivos: true, entrada.Texto("q"), entrada.Texto("categoria"), Page: 1, PageSize: top), ct);

        if (pagina.Items.Count == 0)
            return clsResultadoHerramienta.SinDatos(Fuente, "No hay servicios conexos activos con esos filtros.");

        return clsResultadoHerramienta.Con(
            new clsListadoAsistente<clsServicioConexoDto>(pagina.Total, pagina.Items), Fuente,
            pagina.Total > pagina.Items.Count ? $"Se muestran {pagina.Items.Count} de {pagina.Total}; afina los filtros para ver otros." : null);
    }
}

/// <summary>Ciudades del catálogo DANE con su código, departamento y la zona con la que se tarifa.</summary>
public sealed class clsBuscarCiudadesDaneHerramienta(IQueryHandler<clsBuscarCiudadesQuery, IReadOnlyList<clsCiudadBusquedaDto>> ciudades)
    : clsHerramientaBase
{
    private const int TopPorDefecto = 10;
    private const int TopMaximo = 25;

    public override string Nombre => "buscar_ciudades_dane";

    public override string Descripcion =>
        "Busca ciudades de Colombia en el catálogo DANE de NEXO y devuelve el código DANE, el departamento y la zona de tarificación. " +
        "Úsala para confirmar el nombre exacto de una ciudad antes de consultar tarifas.";

    public override string EsquemaEntradaJson => """
        {"type":"object","additionalProperties":false,"properties":{
          "q":{"type":"string","description":"Parte del nombre de la ciudad."},
          "departamento":{"type":"string","description":"Departamento para acotar la búsqueda."},
          "top":{"type":"integer","description":"Cuántas ciudades devolver (1 a 25, por defecto 10)."}}}
        """;

    private const string Fuente = "NEXO · catálogo de municipios DANE (tbl_municipios)";
    protected override string FuenteNoDisponible => Fuente;

    protected override async Task<clsResultadoHerramienta> LeerAsync(clsEntradaHerramienta entrada, CancellationToken ct)
    {
        entrada.ExigirAlgunoDe("q", "departamento");
        var top = entrada.Tope("top", TopPorDefecto, TopMaximo);

        var encontradas = await ciudades.Handle(new clsBuscarCiudadesQuery(entrada.Texto("departamento"), entrada.Texto("q")), ct);
        if (encontradas.Count == 0)
            return clsResultadoHerramienta.SinDatos(Fuente, "No hay ciudades con ese nombre o departamento.");

        var items = encontradas.Take(top).ToList();
        return clsResultadoHerramienta.Con(
            new clsListadoAsistente<clsCiudadBusquedaDto>(encontradas.Count, items), Fuente,
            encontradas.Count > items.Count ? $"Se muestran {items.Count} de {encontradas.Count}; afina la búsqueda." : null);
    }
}
