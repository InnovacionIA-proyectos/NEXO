using CabinaFletes.Application.Common.Mensajeria;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes;
using CabinaFletes.Mensajes.Solicitudes;
using Moq;

namespace CabinaFletes.Application.Tests.Common;

/// <summary>
/// El decorador que garantiza que nada llegue al broker sin validar.
///
/// Lo que se prueba aquí no es la validación de un mensaje concreto, sino la política:
/// que un mensaje inválido no salga, y —más importante— que un tipo SIN validador
/// tampoco. Sin lo segundo, "todo lo que viaja está validado" sería una intención y
/// no una garantía: bastaría con que alguien agregue un mensaje y olvide su validador.
/// </summary>
public class PublicadorValidadoTests
{
    private readonly Mock<IPublicadorDeMensajes> _interno = new();

    private static AutoCosteoSolicitado Valido() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 3, Guid.NewGuid(), DateTimeOffset.UtcNow);

    private IPublicadorDeMensajes ConValidador(IValidadorDeMensaje<AutoCosteoSolicitado>? validador) =>
        new PublicadorValidado(_interno.Object, new ProveedorDeUnSoloServicio(validador));

    /// <summary>
    /// Proveedor mínimo: devuelve el validador que se le dio, o null. No hace falta un
    /// contenedor de verdad para probar una política de tres líneas.
    /// </summary>
    private sealed class ProveedorDeUnSoloServicio(object? servicio) : IServiceProvider
    {
        public object? GetService(Type tipo) =>
            servicio is not null && tipo.IsInstanceOfType(servicio) ? servicio : null;
    }

    [Fact]
    public async Task Mensaje_valido_llega_al_publicador_interno()
    {
        var publicador = ConValidador(new AutoCosteoSolicitadoValidador());
        var mensaje = Valido();

        await publicador.PublicarAsync(mensaje);

        _interno.Verify(p => p.PublicarAsync(mensaje, default), Times.Once);
    }

    [Fact]
    public async Task Mensaje_invalido_no_sale_y_el_error_dice_todos_los_motivos()
    {
        var publicador = ConValidador(new AutoCosteoSolicitadoValidador());
        var invalido = new AutoCosteoSolicitado(Guid.Empty, Guid.Empty, 0, Guid.Empty, default);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => publicador.PublicarAsync(invalido));

        Assert.Equal("MENSAJE_INVALIDO", ex.Code);
        var motivos = Assert.IsAssignableFrom<IReadOnlyList<string>>(ex.Details);
        Assert.Equal(5, motivos.Count);
        _interno.Verify(p => p.PublicarAsync(It.IsAny<AutoCosteoSolicitado>(), default), Times.Never);
    }

    [Fact]
    public async Task Un_tipo_sin_validador_declarado_no_se_publica()
    {
        var publicador = ConValidador(null);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => publicador.PublicarAsync(Valido()));

        Assert.Equal("MENSAJE_SIN_VALIDADOR", ex.Code);
        _interno.Verify(p => p.PublicarAsync(It.IsAny<AutoCosteoSolicitado>(), default), Times.Never);
    }
}

/// <summary>Las invariantes del mensaje, una por una.</summary>
public class AutoCosteoSolicitadoValidadorTests
{
    private static readonly AutoCosteoSolicitadoValidador Validador = new();

    private static AutoCosteoSolicitado Base() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 3, Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public void Un_mensaje_bien_formado_no_tiene_motivos()
        => Assert.Empty(Validador.Validar(Base()));

    [Theory]
    [InlineData("trabajo")]
    [InlineData("solicitud")]
    [InlineData("lineas")]
    [InlineData("usuario")]
    [InlineData("fecha")]
    public void Cada_campo_ausente_da_exactamente_un_motivo(string campo)
    {
        var m = Base();
        var roto = campo switch
        {
            "trabajo"   => m with { TrabajoId = Guid.Empty },
            "solicitud" => m with { SolicitudId = Guid.Empty },
            "lineas"    => m with { TotalLineas = 0 },
            "usuario"   => m with { SolicitadoPor = Guid.Empty },
            _           => m with { SolicitadoEn = default },
        };

        Assert.Single(Validador.Validar(roto));
    }

    [Fact]
    public void Un_pliego_con_lineas_negativas_tampoco_pasa()
        => Assert.Single(Validador.Validar(Base() with { TotalLineas = -1 }));
}
