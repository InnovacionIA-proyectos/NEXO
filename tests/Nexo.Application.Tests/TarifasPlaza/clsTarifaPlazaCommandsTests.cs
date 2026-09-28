using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.TarifasPlaza;

public class clsTarifaPlazaCommandsTests
{
    private readonly Mock<ITarifasPlazaRepository> _tarifas = new();
    private readonly Mock<IMunicipiosRepository> _municipios = new();
    private readonly Mock<IRangosPesoRepository> _rangos = new();
    private readonly Mock<ILineasNegocioPort> _lineasNegocio = new();
    private readonly Mock<IClasesViajePort> _clasesViaje = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly clsMunicipio Origen = new() { Id = 1, CodigoDane = "05001000", Municipio = "Medellín", Departamento = "Antioquia", Pais = "CO" };
    private static readonly clsMunicipio Destino = new() { Id = 2, CodigoDane = "11001000", Municipio = "Bogotá D.C.", Departamento = "Cundinamarca", Pais = "CO", ZonaTarificacion = "ZTC_BOGOTA" };

    public clsTarifaPlazaCommandsTests()
    {
        _municipios.Setup(m => m.ListarAsync(default)).ReturnsAsync([Origen, Destino]);
        _lineasNegocio.Setup(l => l.ListarAsync(default)).ReturnsAsync([new clsLineaNegocioDto(1, "Oferta de Expedición Nacional")]);
        _clasesViaje.Setup(c => c.ListarAsync(default)).ReturnsAsync([new clsClaseViajeDto(1, "Carga Suelta")]);
        _usuarioActual.SetupGet(u => u.Id).Returns(Guid.NewGuid());
    }

    private clsCrearTarifaPlazaCommandHandler CrearHandler() =>
        new(_tarifas.Object, _municipios.Object, _rangos.Object, _lineasNegocio.Object, _clasesViaje.Object, _usuarioActual.Object, _uow.Object);

    private clsEditarTarifaPlazaCommandHandler EditarHandler() =>
        new(_tarifas.Object, _municipios.Object, _rangos.Object, _lineasNegocio.Object, _clasesViaje.Object, _uow.Object);

    private clsCrearTarifaPlazaCommand ComandoValido(string? tipoVehiculo = "3S3", int? pesoId = null) =>
        new("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, pesoId, tipoVehiculo, 1_500_000m, new DateOnly(2026, 12, 31), "Tarifa de prueba");

    [Fact]
    public async Task Crea_una_tarifa_valida_por_tipo_de_vehiculo()
    {
        _tarifas.Setup(t => t.ExisteAsync("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, null, "3S3", null, default)).ReturnsAsync(false);

        var dto = await CrearHandler().Handle(ComandoValido());

        Assert.Equal("Medellín", dto.Origen);
        Assert.Equal("Bogotá D.C.", dto.Destino);
        Assert.Equal("ZTC_BOGOTA", dto.ZonaDestino);
        Assert.Equal("3S3", dto.TipoVehiculo);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Crea_una_tarifa_valida_por_rango_de_peso()
    {
        var rango = new clsRangoPeso { Id = 1, PesoMinimo = 3, PesoMaximo = 15000 };
        _rangos.Setup(r => r.ObtenerAsync(1, default)).ReturnsAsync(rango);
        _tarifas.Setup(t => t.ExisteAsync("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, 1, null, null, default)).ReturnsAsync(false);

        var dto = await CrearHandler().Handle(ComandoValido(tipoVehiculo: null, pesoId: 1));

        Assert.Equal("3-15000", dto.RangoPeso);
        Assert.Null(dto.TipoVehiculo);
    }

    [Fact]
    public async Task Crea_una_tarifa_de_contenedor_con_rango_de_peso_y_tipo_de_vehiculo_a_la_vez()
    {
        // Verificado contra datos reales (S4-HU-10.1): una tarifa de contenedor SÍ trae
        // rango de peso Y tipo de vehículo a la vez (el tipo se deriva de la banda de
        // peso) — no son mutuamente excluyentes.
        var rango = new clsRangoPeso { Id = 1, PesoMinimo = 3, PesoMaximo = 15000 };
        _rangos.Setup(r => r.ObtenerAsync(1, default)).ReturnsAsync(rango);
        _tarifas.Setup(t => t.ExisteAsync("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, 1, "3S3", null, default)).ReturnsAsync(false);

        var dto = await CrearHandler().Handle(ComandoValido(tipoVehiculo: "3S3", pesoId: 1));

        Assert.Equal("3S3", dto.TipoVehiculo);
        Assert.Equal("3-15000", dto.RangoPeso);
    }

    [Fact]
    public async Task Sin_ninguna_dimension_lanza_TARIFA_PLAZA_INVALIDA()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            CrearHandler().Handle(ComandoValido(tipoVehiculo: null, pesoId: null)));

        Assert.Equal("TARIFA_PLAZA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Linea_de_negocio_fuera_del_catalogo_lanza_LINEA_NEGOCIO_INVALIDA()
    {
        var comando = ComandoValido() with { LineaNegocio = "No existe" };

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => CrearHandler().Handle(comando));

        Assert.Equal("LINEA_NEGOCIO_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Municipio_de_origen_inexistente_lanza_MUNICIPIO_INVALIDO()
    {
        var comando = ComandoValido() with { OrigenMunicipioId = 999 };

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => CrearHandler().Handle(comando));

        Assert.Equal("MUNICIPIO_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Combinacion_duplicada_lanza_TARIFA_PLAZA_DUPLICADA()
    {
        _tarifas.Setup(t => t.ExisteAsync("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, null, "3S3", null, default)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => CrearHandler().Handle(ComandoValido()));

        Assert.Equal("TARIFA_PLAZA_DUPLICADA", ex.Code);
    }

    [Fact]
    public async Task Editar_tarifa_inexistente_lanza_TARIFA_PLAZA_NO_ENCONTRADA()
    {
        _tarifas.Setup(t => t.ObtenerAsync(It.IsAny<Guid>(), default)).ReturnsAsync((clsTarifaPlaza?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            EditarHandler().Handle(new clsEditarTarifaPlazaCommand(Guid.NewGuid(), "Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, null, "3S3", 1m, new DateOnly(2026, 1, 1), "x")));

        Assert.Equal("TARIFA_PLAZA_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Editar_actualiza_valores_y_excluye_su_propio_id_del_chequeo_de_duplicado()
    {
        var id = Guid.NewGuid();
        var existente = new clsTarifaPlaza
        {
            Id = id, LineaNegocio = "Oferta de Expedición Nacional", ClaseViaje = "Carga Suelta",
            OrigenMunicipioId = 1, DestinoMunicipioId = 2, TipoVehiculo = "3S3",
            Valor = 1_000_000m, Vigencia = new DateOnly(2026, 1, 1), DetallesTarifa = "vieja",
        };
        _tarifas.Setup(t => t.ObtenerAsync(id, default)).ReturnsAsync(existente);
        _tarifas.Setup(t => t.ExisteAsync("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, null, "3S3", id, default)).ReturnsAsync(false);

        var dto = await EditarHandler().Handle(new clsEditarTarifaPlazaCommand(
            id, "Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, null, "3S3", 2_000_000m, new DateOnly(2026, 6, 1), "nueva"));

        Assert.Equal(2_000_000m, dto.Valor);
        Assert.Equal("nueva", dto.DetallesTarifa);
        _tarifas.Verify(t => t.ExisteAsync("Oferta de Expedición Nacional", "Carga Suelta", 1, 2, null, null, "3S3", id, default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Eliminar_tarifa_inexistente_devuelve_false()
    {
        _tarifas.Setup(t => t.EliminarAsync(It.IsAny<Guid>(), default)).ReturnsAsync(0);
        var handler = new clsEliminarTarifaPlazaCommandHandler(_tarifas.Object);

        Assert.False(await handler.Handle(new clsEliminarTarifaPlazaCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Listar_mapea_zona_y_rango_de_peso_resueltos()
    {
        var conRango = new clsTarifaPlaza
        {
            LineaNegocio = "Oferta de Expedición Nacional", ClaseViaje = "Contenedor",
            OrigenMunicipioId = 1, Origen = Origen, DestinoMunicipioId = 2, Destino = Destino,
            PesoId = 1, Peso = new clsRangoPeso { Id = 1, PesoMinimo = 10500, PesoMaximo = null },
            Valor = 1m, Vigencia = new DateOnly(2026, 1, 1), DetallesTarifa = "x",
        };
        _tarifas.Setup(t => t.ListarAsync(default)).ReturnsAsync([conRango]);
        var handler = new clsListarTarifasPlazaQueryHandler(_tarifas.Object);

        var lista = await handler.Handle(new clsListarTarifasPlazaQuery());

        Assert.Equal("10500+", lista[0].RangoPeso);
        Assert.Equal("ZTC_BOGOTA", lista[0].ZonaDestino);
    }
}
