using Moq;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// La recomendación de vehículo por contenedor y peso es la misma regla del costeo, por lote y en el mismo orden: el vehículo de la tabla, null
/// con el tope más alto cuando el peso lo supera, y sin vehículo cuando la clase no trae el tamaño o la tabla no está disponible.
/// </summary>
public class clsRecomendarVehiculoContenedorTests
{
    private static readonly clsRangoPesoContenedorDto[] Tabla =
    [
        new(20, "2S2", 24_000), new(20, "3S2", 28_000),
        new(40, "3S3", 34_000), new(40, "2S3", 30_000),
    ];

    private readonly Mock<IRangosPesoContenedorPort> _rangos = new();

    public clsRecomendarVehiculoContenedorTests()
    {
        _rangos.Setup(r => r.VehiculoParaAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pies, decimal peso, CancellationToken _) =>
                new clsVehiculoDeContenedor(true, clsRecomendadorDeVehiculoDeContenedor.Elegir(Tabla, pies, peso)));
        _rangos.Setup(r => r.PesoMaximoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pies, CancellationToken _) => Tabla.Where(t => t.Contenedor == pies).Select(t => (int?)t.PesoMaximo).Max());
    }

    private static clsRecomendacionVehiculoContenedorInput L(int indice, string? clase, decimal peso) => new(indice, clase, peso);

    private Task<IReadOnlyList<clsRecomendacionVehiculoContenedorDto>> Recomendar(params clsRecomendacionVehiculoContenedorInput[] lineas) =>
        new clsRecomendarVehiculoContenedorQueryHandler(_rangos.Object).Handle(new clsRecomendarVehiculoContenedorQuery(lineas));

    [Fact]
    public async Task Responde_en_el_mismo_orden_con_el_vehiculo_de_la_tabla_y_el_tope_del_tamano()
    {
        var r = await Recomendar(L(7, "CNT40.HC", 31_000), L(3, "CNT20.ST", 20_000), L(9, "40 HC", 29_000));

        Assert.Equal([7, 3, 9], r.Select(x => x.Indice));
        Assert.Equal(("3S3", 40, true, 34_000), (r[0].TipoVehiculo, r[0].ContenedorPies, r[0].TablaDisponible, r[0].PesoMaximoContenedor));
        Assert.Equal(("2S2", 20, true, 28_000), (r[1].TipoVehiculo, r[1].ContenedorPies, r[1].TablaDisponible, r[1].PesoMaximoContenedor));
        Assert.Equal("2S3", r[2].TipoVehiculo);
    }

    [Fact]
    public async Task El_tamano_se_acepta_como_clase_de_SAP_o_como_pies()
    {
        var r = await Recomendar(L(0, "20", 10_000), L(1, "CNT20.IT", 10_000), L(2, "cnt 40 hc", 10_000));

        Assert.Equal([20, 20, 40], r.Select(x => x.ContenedorPies));
    }

    [Fact]
    public async Task Un_peso_sobre_el_tope_deja_el_vehiculo_en_null_y_dice_el_tope_mas_alto()
    {
        var r = Assert.Single(await Recomendar(L(0, "CNT40.HC", 40_000)));

        Assert.Null(r.TipoVehiculo);
        Assert.True(r.TablaDisponible);
        Assert.Equal(34_000, r.PesoMaximoContenedor);
    }

    [Fact]
    public async Task El_peso_igual_al_tope_si_alcanza()
    {
        var r = Assert.Single(await Recomendar(L(0, "CNT40.HC", 34_000)));

        Assert.Equal("3S3", r.TipoVehiculo);
    }

    [Fact]
    public async Task Una_clase_sin_tamano_no_tiene_vehiculo_y_no_consulta_la_tabla()
    {
        var r = Assert.Single(await Recomendar(L(0, "granel", 10_000)));

        Assert.Equal((null, null, null), (r.TipoVehiculo, r.ContenedorPies, r.PesoMaximoContenedor));
        _rangos.Verify(x => x.VehiculoParaAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_la_tabla_responde_que_no_esta_disponible()
    {
        _rangos.Setup(r => r.VehiculoParaAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsVehiculoDeContenedor(false, null));
        _rangos.Setup(r => r.PesoMaximoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var r = Assert.Single(await Recomendar(L(0, "CNT40.HC", 10_000)));

        Assert.Equal((null, false, null), (r.TipoVehiculo, r.TablaDisponible, r.PesoMaximoContenedor));
    }

    [Fact]
    public async Task El_tope_de_cada_tamano_se_consulta_una_sola_vez_por_lote()
    {
        await Recomendar(L(0, "CNT40.HC", 10_000), L(1, "CNT40.RE", 20_000), L(2, "CNT20.ST", 5_000), L(3, "CNT20.IT", 5_000));

        _rangos.Verify(r => r.PesoMaximoAsync(40, It.IsAny<CancellationToken>()), Times.Once);
        _rangos.Verify(r => r.PesoMaximoAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_lote_vacio_demasiado_grande_o_con_peso_invalido_es_400()
    {
        var vacio = await Assert.ThrowsAsync<clsValidationFailedException>(() => Recomendar());
        Assert.Equal("SIN_LINEAS", vacio.Code);

        var grande = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Recomendar(Enumerable.Range(0, clsRecomendarVehiculoContenedorQueryHandler.MaxLineas + 1)
                .Select(i => new clsRecomendacionVehiculoContenedorInput(i, "CNT40.HC", 1_000)).ToArray()));
        Assert.Equal("DEMASIADAS_LINEAS", grande.Code);

        var peso = await Assert.ThrowsAsync<clsValidationFailedException>(() => Recomendar(L(4, "CNT40.HC", 0)));
        Assert.Equal("PESO_INVALIDO", peso.Code);
        Assert.Contains("4", peso.Message);
    }
}
