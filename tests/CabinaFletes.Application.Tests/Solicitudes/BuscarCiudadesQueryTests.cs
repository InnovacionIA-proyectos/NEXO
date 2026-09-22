using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using Moq;

namespace CabinaFletes.Application.Tests.Solicitudes;

/// <summary>HU-005 parte 3: filtro por texto y/o departamento sobre el catálogo cacheado.</summary>
public class BuscarCiudadesQueryTests
{
    private readonly Mock<ICiudadesDwPort> _ciudades = new();

    private BuscarCiudadesQueryHandler Handler() => new(_ciudades.Object);

    private void ConCatalogo(params CiudadDwDto[] ciudades) =>
        _ciudades.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ciudades);

    [Fact]
    public async Task Filtra_por_texto_sin_importar_mayusculas_ni_tildes_de_la_busqueda()
    {
        ConCatalogo(new CiudadDwDto("Medellín", "Antioquia"), new CiudadDwDto("Cali", "Valle del Cauca"));

        var resultado = await Handler().Handle(new BuscarCiudadesQuery(null, "medellin"));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Medellín", ciudad.Municipio);
    }

    [Fact]
    public async Task Filtra_por_departamento_cuando_hay_nombres_de_municipio_repetidos()
    {
        ConCatalogo(
            new CiudadDwDto("Sucre", "Sucre"),
            new CiudadDwDto("Sucre", "Santander"),
            new CiudadDwDto("Sucre", "Cauca"));

        var resultado = await Handler().Handle(new BuscarCiudadesQuery("Santander", null));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Santander", ciudad.Departamento);
    }

    [Fact]
    public async Task Combina_departamento_y_texto_a_la_vez()
    {
        ConCatalogo(
            new CiudadDwDto("Sucre", "Sucre"),
            new CiudadDwDto("Sincelejo", "Sucre"),
            new CiudadDwDto("Sucre", "Santander"));

        var resultado = await Handler().Handle(new BuscarCiudadesQuery("Sucre", "sin"));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Sincelejo", ciudad.Municipio);
    }

    [Fact]
    public async Task Sin_filtros_devuelve_todo_el_catalogo_ordenado()
    {
        ConCatalogo(new CiudadDwDto("Zipaquirá", "Cundinamarca"), new CiudadDwDto("Armenia", "Quindío"));

        var resultado = await Handler().Handle(new BuscarCiudadesQuery(null, null));

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Armenia", resultado[0].Municipio);
    }

    [Fact]
    public async Task ListarDepartamentos_devuelve_distintos_y_ordenados()
    {
        ConCatalogo(
            new CiudadDwDto("Sucre", "Sucre"),
            new CiudadDwDto("Sincelejo", "Sucre"),
            new CiudadDwDto("Medellín", "Antioquia"));

        var resultado = await new ListarDepartamentosQueryHandler(_ciudades.Object)
            .Handle(new ListarDepartamentosQuery());

        Assert.Equal(["Antioquia", "Sucre"], resultado);
    }
}
