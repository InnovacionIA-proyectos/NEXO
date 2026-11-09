using Moq;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// La ruta que resuelve el texto libre guarda y usa el municipio del catálogo: se busca primero por municipios, los ids vacíos se completan y
/// los que ya tiene nunca se cambian, y una ruta nueva con id toma el nombre del municipio.
/// </summary>
public class clsRutaConMunicipiosTests
{
    private static readonly clsMunicipio Cartagena = new() { Id = 10, CodigoDane = "13001000", Municipio = "CARTAGENA DE INDIAS", Departamento = "BOLIVAR", Pais = "CO" };
    private static readonly clsMunicipio Uribia = new() { Id = 20, CodigoDane = "44847000", Municipio = "URIBIA", Departamento = "LA GUAJIRA", Pais = "CO" };
    private static readonly clsMunicipio GuadalupeHuila = new() { Id = 30, CodigoDane = "41319000", Municipio = "GUADALUPE", Departamento = "HUILA", Pais = "CO" };
    private static readonly clsMunicipio GuadalupeSantander = new() { Id = 31, CodigoDane = "68320000", Municipio = "GUADALUPE", Departamento = "SANTANDER", Pais = "CO" };

    private static readonly IReadOnlyDictionary<int, clsMunicipio> Catalogo =
        new[] { Cartagena, Uribia, GuadalupeHuila, GuadalupeSantander }.ToDictionary(m => m.Id);

    private readonly Mock<IRutasCatalogoPort> _catalogo = new();

    private static Dictionary<string, clsRuta> Indexar(params clsRuta[] rutas) =>
        rutas.ToDictionary(r => clsResolucionDeCatalogos.ClaveRuta(r.Origen, r.Destino));

    [Fact]
    public void Una_ruta_nueva_con_ids_toma_el_nombre_del_municipio_del_catalogo()
    {
        var porClave = Indexar();

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta(
            "Cartagena", "uribia (guajira)", porClave, _catalogo.Object, Cartagena.Id, Uribia.Id, Catalogo);

        Assert.True(creada);
        Assert.Equal(("CARTAGENA DE INDIAS", "URIBIA"), (ruta.Origen, ruta.Destino));
        Assert.Equal((Cartagena.Id, Uribia.Id), (ruta.OrigenMunicipioId, ruta.DestinoMunicipioId));
        _catalogo.Verify(c => c.Agregar(ruta), Times.Once);
    }

    [Fact]
    public void Una_ruta_nueva_sin_ids_se_guarda_como_siempre()
    {
        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta("  Bogotá  ", "Medellín", Indexar(), _catalogo.Object);

        Assert.True(creada);
        Assert.Equal(("Bogotá", "Medellín"), (ruta.Origen, ruta.Destino));
        Assert.Null(ruta.OrigenMunicipioId);
    }

    [Fact]
    public void Con_ids_se_busca_primero_por_municipios_aunque_el_texto_sea_otro()
    {
        var existente = new clsRuta { Origen = "CARTAGENA BOLIVAR", Destino = "URIBIA", OrigenMunicipioId = Cartagena.Id, DestinoMunicipioId = Uribia.Id };

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta(
            "CARTAGENA DE INDIAS", "URIBIA", Indexar(existente), _catalogo.Object, Cartagena.Id, Uribia.Id, Catalogo);

        Assert.False(creada);
        Assert.Same(existente, ruta);
        _catalogo.Verify(c => c.Agregar(It.IsAny<clsRuta>()), Times.Never);
    }

    [Fact]
    public void Una_ruta_existente_por_texto_sin_municipios_los_completa()
    {
        var existente = new clsRuta { Origen = "CARTAGENA DE INDIAS", Destino = "URIBIA" };

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta(
            "Cartagena de Indias", "Uribia", Indexar(existente), _catalogo.Object, Cartagena.Id, Uribia.Id, Catalogo);

        Assert.False(creada);
        Assert.Same(existente, ruta);
        Assert.Equal((Cartagena.Id, Uribia.Id), (existente.OrigenMunicipioId, existente.DestinoMunicipioId));
    }

    [Fact]
    public void Con_un_solo_id_se_completa_solo_ese_lado()
    {
        var existente = new clsRuta { Origen = "CARTAGENA DE INDIAS", Destino = "URIBIA" };

        clsResolucionDeCatalogos.ResolverRuta("Cartagena de Indias", "Uribia", Indexar(existente), _catalogo.Object, Cartagena.Id, null, Catalogo);

        Assert.Equal((Cartagena.Id, (int?)null), (existente.OrigenMunicipioId, existente.DestinoMunicipioId));
    }

    [Fact]
    public void Una_ruta_existente_con_otro_municipio_no_se_pisa()
    {
        var existente = new clsRuta { Origen = "GUADALUPE", Destino = "URIBIA", OrigenMunicipioId = GuadalupeHuila.Id, DestinoMunicipioId = Uribia.Id };

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta(
            "Guadalupe", "Uribia", Indexar(existente), _catalogo.Object, GuadalupeSantander.Id, Uribia.Id, Catalogo);

        Assert.False(creada);
        Assert.Same(existente, ruta);
        Assert.Equal(GuadalupeHuila.Id, existente.OrigenMunicipioId);
        _catalogo.Verify(c => c.Agregar(It.IsAny<clsRuta>()), Times.Never);
    }

    [Fact]
    public void Sin_ids_una_ruta_existente_no_cambia_y_se_reutiliza_como_hoy()
    {
        var existente = new clsRuta { Origen = "BOGOTA", Destino = "MEDELLIN", OrigenMunicipioId = 1, DestinoMunicipioId = 2 };

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta("Bogotá", "Medellín", Indexar(existente), _catalogo.Object);

        Assert.False(creada);
        Assert.Equal((1, 2), (ruta.OrigenMunicipioId, ruta.DestinoMunicipioId));
    }

    [Fact]
    public void Dos_lineas_iguales_en_una_misma_carga_dan_la_misma_ruta_nueva()
    {
        var porClave = Indexar();

        var (primera, _) = clsResolucionDeCatalogos.ResolverRuta("Cartagena", "Uribia", porClave, _catalogo.Object, Cartagena.Id, Uribia.Id, Catalogo);
        var (segunda, creada) = clsResolucionDeCatalogos.ResolverRuta("CARTAGENA DE INDIAS", "URIBIA", porClave, _catalogo.Object, Cartagena.Id, Uribia.Id, Catalogo);

        Assert.False(creada);
        Assert.Same(primera, segunda);
        _catalogo.Verify(c => c.Agregar(It.IsAny<clsRuta>()), Times.Once);
    }

    // ---------------------------------------------------------------- cargar los municipios

    [Fact]
    public async Task Sin_ids_no_se_consulta_el_catalogo_de_municipios()
    {
        var repo = new Mock<IMunicipiosRepository>();

        var municipios = await clsResolucionDeCatalogos.CargarMunicipiosAsync(repo.Object, [(1, null, null), (2, null, null)], default);

        Assert.Null(municipios);
        repo.Verify(r => r.ListarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_id_que_no_esta_en_el_catalogo_es_400_con_el_numero_de_linea()
    {
        var repo = new Mock<IMunicipiosRepository>();
        repo.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Cartagena, Uribia]);

        var ok = await clsResolucionDeCatalogos.CargarMunicipiosAsync(repo.Object, [(1, Cartagena.Id, Uribia.Id)], default);
        Assert.Equal(2, ok!.Count);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => clsResolucionDeCatalogos.CargarMunicipiosAsync(repo.Object, [(1, Cartagena.Id, Uribia.Id), (2, 999999, null)], default));

        Assert.Equal("MUNICIPIO_NO_ENCONTRADO", ex.Code);
        Assert.Contains("Línea 2", ex.Message);
    }
}
