using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>Filtro por texto y/o departamento sobre el catálogo cacheado de ciudades.</summary>
public class clsBuscarCiudadesQueryTests
{
    private readonly Mock<ICiudadesDwPort> _ciudades = new();

    private clsBuscarCiudadesQueryHandler Handler() => new(_ciudades.Object);

    private void ConCatalogo(params clsCiudadDwDto[] ciudades) =>
        _ciudades.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ciudades);

    [Fact]
    public async Task Filtra_por_texto_sin_importar_mayusculas_ni_tildes_de_la_busqueda()
    {
        ConCatalogo(new clsCiudadDwDto("Medellín", "Antioquia"), new clsCiudadDwDto("Cali", "Valle del Cauca"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery(null, "medellin"));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Medellín", ciudad.Municipio);
    }

    [Fact]
    public async Task Filtra_por_departamento_cuando_hay_nombres_de_municipio_repetidos()
    {
        ConCatalogo(
            new clsCiudadDwDto("Sucre", "Sucre"),
            new clsCiudadDwDto("Sucre", "Santander"),
            new clsCiudadDwDto("Sucre", "Cauca"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery("Santander", null));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Santander", ciudad.Departamento);
    }

    [Fact]
    public async Task Combina_departamento_y_texto_a_la_vez()
    {
        ConCatalogo(
            new clsCiudadDwDto("Sucre", "Sucre"),
            new clsCiudadDwDto("Sincelejo", "Sucre"),
            new clsCiudadDwDto("Sucre", "Santander"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery("Sucre", "sin"));

        var ciudad = Assert.Single(resultado);
        Assert.Equal("Sincelejo", ciudad.Municipio);
    }

    [Fact]
    public async Task Sin_filtros_devuelve_todo_el_catalogo_ordenado()
    {
        ConCatalogo(new clsCiudadDwDto("Zipaquirá", "Cundinamarca"), new clsCiudadDwDto("Armenia", "Quindío"));

        var resultado = await Handler().Handle(new clsBuscarCiudadesQuery(null, null));

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Armenia", resultado[0].Municipio);
    }

    [Fact]
    public async Task ListarDepartamentos_devuelve_distintos_y_ordenados()
    {
        ConCatalogo(
            new clsCiudadDwDto("Sucre", "Sucre"),
            new clsCiudadDwDto("Sincelejo", "Sucre"),
            new clsCiudadDwDto("Medellín", "Antioquia"));

        var resultado = await new clsListarDepartamentosQueryHandler(_ciudades.Object)
            .Handle(new clsListarDepartamentosQuery());

        Assert.Equal(["Antioquia", "Sucre"], resultado);
    }
}
