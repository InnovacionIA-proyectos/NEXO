using Nexo.Application.CasosDeUso.ReglasSoloSicetac;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Costeo.Entidades;
using Moq;

namespace Nexo.Application.Tests.ReglasSoloSicetac;

public class CrearReglaSoloSicetacCommandTests
{
    private readonly Mock<IReglasSoloSicetacRepository> _reglas = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CrearReglaSoloSicetacCommandHandler Handler() => new(_reglas.Object, _uow.Object);

    [Theory]
    [InlineData("", "Cali", "Turbo")]
    [InlineData("Bogotá", "", "Turbo")]
    [InlineData("Bogotá", "Cali", "")]
    [InlineData("   ", "Cali", "Turbo")]
    public async Task Campos_vacios_lanzan_ValidationFailedException(string origen, string destino, string tipoVehiculo)
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new CrearReglaSoloSicetacCommand(origen, destino, tipoVehiculo)));

        _reglas.Verify(r => r.Agregar(It.IsAny<ReglaSoloSicetac>()), Times.Never);
    }

    [Fact]
    public async Task Regla_duplicada_lanza_ConflictException()
    {
        // La forma canónica de Domain quita tildes: "Bogotá" queda "BOGOTA", así que la
        // segunda regla para la misma ruta se detecta como duplicada (PR #53).
        //
        // Ese arreglo cubre la ESCRITURA. La lectura se corrige en la issue #16: el
        // adaptador de propuesta comparaba el valor crudo de la ruta contra estas claves
        // ya normalizadas, y por eso una ruta marcada "solo SICE-TAC" no aplicaba la
        // regla al costear. Ver ClaveDeRutaTests en Domain.Tests.
        _reglas.Setup(r => r.ExisteAsync("BOGOTA", "CALI", "TURBO", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler().Handle(new CrearReglaSoloSicetacCommand("Bogotá", "Cali", "Turbo")));
    }

    [Fact]
    public async Task Regla_valida_se_normaliza_a_mayusculas_sin_tildes_ni_espacios_dobles()
    {
        _reglas.Setup(r => r.ExisteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default)).ReturnsAsync(false);
        ReglaSoloSicetac? guardada = null;
        _reglas.Setup(r => r.Agregar(It.IsAny<ReglaSoloSicetac>())).Callback<ReglaSoloSicetac>(r => guardada = r);

        var dto = await Handler().Handle(new CrearReglaSoloSicetacCommand("  bogotá   d.c.  ", "cali", "turbo con estacas"));

        Assert.NotNull(guardada);
        Assert.Equal("BOGOTA D.C.", guardada!.Origen);
        Assert.Equal("CALI", guardada.Destino);
        Assert.Equal("TURBO CON ESTACAS", guardada.TipoVehiculo);
        Assert.Equal(guardada.Id, dto.Id);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
