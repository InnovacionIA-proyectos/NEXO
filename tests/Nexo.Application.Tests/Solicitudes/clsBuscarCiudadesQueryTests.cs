using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>Filtro por texto y/o departamento sobre el catálogo de tbl_municipios (S5-HU-16.1/#254).</summary>
public class clsBuscarCiudadesQueryTests
{
    private readonly Mock<IMunicipiosRepository> _municipios = new();

    private clsBuscarCiudadesQueryHandler Handler() => new(_municipios.Object);

    private static clsMunicipio Municipio(string municipio, string departamento, string pais = "CO", string codigoDane = "00000000") =>
        new() { Id = municipio.GetHashCode(), CodigoDane = codigoDane, Municipio = municipio, Departamento = departamento, Pais = pais };

    private void ConCatalogo(params clsMunicipio[] municipios) =>
        _municipios.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(municipios);

    [Fact]
    public async Task Filtra_por_texto_sin_importar_mayusculas_ni_tildes_de_la_busqueda()
    {
        ConCatalogo(Municipio("Medellín", "Antioquia"), Municipio("Cali", "Valle del Cauca"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery(null, "medellin"));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Medellín", ciudad.Municipio);
    }

    [Fact]
    public async Task Filtra_por_departamento_cuando_hay_nombres_de_municipio_repetidos()
    {
        ConCatalogo(
            Municipio("Sucre", "Sucre"),
            Municipio("Sucre", "Santander"),
            Municipio("Sucre", "Cauca"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery("Santander", null));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Santander", ciudad.Departamento);
    }

    [Fact]
    public async Task Combina_departamento_y_texto_a_la_vez()
    {
        ConCatalogo(
            Municipio("Sucre", "Sucre"),
            Municipio("Sincelejo", "Sucre"),
            Municipio("Sucre", "Santander"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery("Sucre", "sin"));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Sincelejo", ciudad.Municipio);
    }

    [Fact]
    public async Task Sin_filtros_devuelve_todo_el_catalogo_ordenado()
    {
        ConCatalogo(Municipio("Zipaquirá", "Cundinamarca"), Municipio("Armenia", "Quindío"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery(null, null));

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Armenia", resultado[0].Municipio);
    }

    [Fact] // S5-HU-16.1/#254: Venezuela/Ecuador no se ofrecen para Origen/Destino.
    public async Task Excluye_municipios_que_no_son_de_Colombia()
    {
        ConCatalogo(Municipio("Caracas", "Distrito Capital", pais: "VE"), Municipio("Bogotá", "Cundinamarca"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery(null, null));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Bogotá", ciudad.Municipio);
    }

    [Fact] // S5-HU-16.1/#254: la base del match por DANE/ZC/ZTC de #255/#256.
    public async Task Expone_Id_CodigoDane_y_zonas_de_tarifa()
    {
        ConCatalogo(new clsMunicipio
        {
            Id = 42, CodigoDane = "05001000", Municipio = "Medellín", Departamento = "Antioquia",
            Pais = "CO", NombreTarifasPlaza = "ZC_MEDELLIN", ZonaTarificacion = "ZTC_VABURRA",
        });

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery(null, null));

        var ciudad = Assert.Single(resultado);
        Assert.Equal(42, ciudad.Id);
        Assert.Equal("05001000", ciudad.CodigoDane);
        Assert.Equal("ZC_MEDELLIN", ciudad.NombreTarifasPlaza);
        Assert.Equal("ZTC_VABURRA", ciudad.ZonaTarificacion);
    }

    [Fact]
    public async Task ListarDepartamentos_devuelve_distintos_ordenados_y_solo_Colombia()
    {
        ConCatalogo(
            Municipio("Sucre", "Sucre"),
            Municipio("Sincelejo", "Sucre"),
            Municipio("Medellín", "Antioquia"),
            Municipio("Caracas", "Distrito Capital", pais: "VE"));

        var resultado = await new clsListarDepartamentosQueryHandler(_municipios.Object)
            .Handle(new clsListarDepartamentosQuery());

        Assert.Equal(["Antioquia", "Sucre"], resultado);
    }
}
