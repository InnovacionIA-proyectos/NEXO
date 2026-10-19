using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286: editar el flete de una línea modifica LA MISMA fila (no suma otra, no infla el total), solo en Borrador y
/// nunca bajo el piso.
/// S6-HU-26.1/#30: se puede editar un flete de CUALQUIER fuente (queda Manual); no si la ruta y el vehículo tienen regla
/// Solo-SICE (409 FLETE_SOLO_SICETAC); y si el piso no se puede verificar no se guarda en silencio (409 PISO_SICETAC_NO_VERIFICABLE).
/// </summary>
public class clsEditarLineaCotizacionCommandTests
{
    private const decimal Piso = 2_000_000m;

    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<ILineasSolicitudPort> _lineasSolicitud = new();
    private readonly Mock<IClavesSoloSicetacPort> _reglas = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsEditarLineaCotizacionCommandHandler Handler() =>
        new(_cotizaciones.Object, _lineasSolicitud.Object, _reglas.Object, _propuestas.Object, _auditoria.Object, _usuario.Object, _uow.Object);

    private const string DaneOrigen = "11001000";
    private const string DaneDestino = "05001000";
    private const string Vehiculo = "3S3";

    /// <summary>La línea del pliego con ruta (municipios con DANE) y vehículo, como la devuelve el puerto.</summary>
    private void LineaDelPliego(Guid solicitudId, Guid lineaSolicitudId, ClaseViaje? claseViaje = ClaseViaje.Cupo, string? vehiculo = Vehiculo) =>
        _lineasSolicitud.Setup(l => l.ObtenerConRutaYVehiculoAsync(lineaSolicitudId, solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaSolicitud
            {
                Id = lineaSolicitudId, SolicitudId = solicitudId, ClaseViaje = claseViaje,
                clsRuta = new clsRuta
                {
                    Origen = "Bogotá", Destino = "Medellín",
                    OrigenMunicipio = new clsMunicipio { CodigoDane = DaneOrigen },
                    DestinoMunicipio = new clsMunicipio { CodigoDane = DaneDestino },
                },
                clsConfiguracionVehiculo = vehiculo is null ? null : new clsConfiguracionVehiculo { TipoVehiculo = vehiculo },
            });

    /// <summary>Define qué rutas|vehículo tienen regla Solo-SICE (por defecto ninguna).</summary>
    private void ReglasSoloSice(params string[] claves) =>
        _reglas.Setup(r => r.ClavesRutaVehiculoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)claves.ToHashSet());

    private (clsCotizacion C, Guid LineaSolicitudId, clsCotizacionLinea Fila) Preparar(
        FuenteFlete fuente = FuenteFlete.Manual, EstadoCotizacion estado = EstadoCotizacion.Borrador, decimal? piso = Piso)
    {
        var solicitudId = Guid.NewGuid();
        var lineaSolicitudId = Guid.NewGuid();
        var c = new clsCotizacion { Id = Guid.NewGuid(), SolicitudId = solicitudId, Estado = estado };
        var fila = new clsCotizacionLinea
        {
            Id = Guid.NewGuid(), CotizacionId = c.Id, LineaSolicitudId = lineaSolicitudId, ValorFlete = 3_000_000m, Fuente = fuente,
            ValorRetornoContenedor = 400_000m,
        };
        c.Lineas.Add(fila);
        c.Lineas.Add(new clsCotizacionLinea { Id = Guid.NewGuid(), CotizacionId = c.Id, LineaSolicitudId = Guid.NewGuid(), ValorFlete = 500_000m, Fuente = FuenteFlete.Sicetac });
        c.Total = 3_900_000m;
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(c.Id, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        LineaDelPliego(solicitudId, lineaSolicitudId);
        ReglasSoloSice();
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId, [
                new clsPropuestaLineaDto(lineaSolicitudId, "A → B", "C2", 1000,
                    piso is null ? null : new clsCandidatoTarifaDto("sicetac", piso.Value, "", "", 1),
                    null, null, null, null, ""),
            ], []));
        return (c, lineaSolicitudId, fila);
    }

    [Fact]
    public async Task Edita_la_misma_linea_sin_duplicar_y_recalcula_el_total()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        var dto = await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        Assert.Equal(2, dto.Lineas.Count);                      // sigue habiendo 2, no 3
        Assert.Equal(3_500_000m, fila.ValorFlete);
        Assert.Equal(FuenteFlete.Manual, fila.Fuente);          // la fuente no cambia
        Assert.Equal(3_500_000m + 400_000m + 500_000m, dto.Total); // 3.5M + retorno + la otra línea
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_valor_de_retorno_no_toca_el_que_tenia_y_con_valor_lo_reemplaza()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_100_000m, null));
        Assert.Equal(400_000m, fila.ValorRetornoContenedor);

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_100_000m, 250_000m));
        Assert.Equal(250_000m, fila.ValorRetornoContenedor);
    }

    [Fact]
    public async Task Cotizacion_entregada_da_COTIZACION_NO_EDITABLE()
    {
        var (c, lineaSolicitudId, _) = Preparar(estado: EstadoCotizacion.Entregada);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null)));

        Assert.Equal("COTIZACION_NO_EDITABLE", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Linea_inexistente_en_la_cotizacion_da_LINEA_NO_ENCONTRADA()
    {
        var (c, _, _) = Preparar();

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, Guid.NewGuid(), 3_500_000m, null)));

        Assert.Equal("LINEA_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Cotizacion_inexistente_da_COTIZACION_NO_ENCONTRADA()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(Guid.NewGuid(), Guid.NewGuid(), 1m, null)));

        Assert.Equal("COTIZACION_NO_ENCONTRADA", ex.Code);
    }

    // S6-HU-26.1/#30: antes solo la fuente Manual (409 FLETE_NO_EDITABLE: "Reemplácelo eligiendo otro candidato"). Ahora una
    // tarifa se corrige en su sitio, de cualquier fuente, y al guardar la fuente PASA A MANUAL (decisión D4).
    [Theory]
    [InlineData(FuenteFlete.Tarifario)]   // plaza
    [InlineData(FuenteFlete.Sicetac)]
    [InlineData(FuenteFlete.Historica)]
    [InlineData(FuenteFlete.Manual)]
    public async Task Un_flete_de_cualquier_fuente_se_edita_y_la_fuente_pasa_a_Manual(FuenteFlete fuente)
    {
        var (c, lineaSolicitudId, fila) = Preparar(fuente);

        var dto = await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        Assert.Equal(3_500_000m, fila.ValorFlete);
        Assert.Equal(FuenteFlete.Manual, fila.Fuente);
        Assert.Equal(2, dto.Lineas.Count);                       // la misma fila, no una nueva
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // S6-HU-26.1/#30: con regla Solo-SICE para la ruta y el vehículo gobierna el piso y no se edita a mano.
    [Theory]
    [InlineData(FuenteFlete.Tarifario)]
    [InlineData(FuenteFlete.Manual)]   // también la manual: la regla manda sobre cualquier fuente
    public async Task Con_regla_Solo_SICE_responde_409_FLETE_SOLO_SICETAC_y_no_cambia(FuenteFlete fuente)
    {
        var (c, lineaSolicitudId, fila) = Preparar(fuente);
        ReglasSoloSice(clsTexto.ClaveRutaVehiculo(DaneOrigen, DaneDestino, Vehiculo));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null)));

        Assert.Equal("FLETE_SOLO_SICETAC", ex.Code);
        Assert.Equal(3_000_000m, fila.ValorFlete);
        Assert.Equal(fuente, fila.Fuente);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // La regla se revisa antes que el piso: ni se pide la propuesta.
        _propuestas.Verify(p => p.ProponerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] // La regla es por ruta Y vehículo: otro vehículo de la misma ruta no queda bloqueado.
    public async Task La_regla_de_otro_vehiculo_de_la_misma_ruta_no_bloquea()
    {
        var (c, lineaSolicitudId, fila) = Preparar(FuenteFlete.Tarifario);
        ReglasSoloSice(clsTexto.ClaveRutaVehiculo(DaneOrigen, DaneDestino, "C2"));

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        Assert.Equal(3_500_000m, fila.ValorFlete);
    }

    [Fact] // Una ruta sin municipio resuelto (sin DANE) no puede tener regla: las reglas se definen por DANE.
    public async Task Una_ruta_sin_DANE_no_tiene_regla_y_se_edita()
    {
        var (c, lineaSolicitudId, fila) = Preparar(FuenteFlete.Tarifario);
        _lineasSolicitud.Setup(l => l.ObtenerConRutaYVehiculoAsync(lineaSolicitudId, c.SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaSolicitud
            {
                Id = lineaSolicitudId, ClaseViaje = ClaseViaje.Cupo, clsRuta = new clsRuta { Origen = "A", Destino = "B" },
                clsConfiguracionVehiculo = new clsConfiguracionVehiculo { TipoVehiculo = Vehiculo },
            });
        ReglasSoloSice(clsTexto.ClaveRutaVehiculo(DaneOrigen, DaneDestino, Vehiculo));

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        Assert.Equal(3_500_000m, fila.ValorFlete);
    }

    [Fact]
    public async Task Un_valor_bajo_el_piso_da_FLETE_BAJO_PISO_SICETAC_y_no_cambia()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, Piso - 1, null)));

        Assert.Equal("FLETE_BAJO_PISO_SICETAC", ex.Code);
        Assert.Equal(3_000_000m, fila.ValorFlete);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2_000_000)]   // igual al piso
    [InlineData(2_000_001)]
    public async Task Un_valor_igual_o_mayor_al_piso_se_guarda(int valor)
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, valor, null));

        Assert.Equal(valor, fila.ValorFlete);
    }

    // S6-HU-26.1/#30 — el hueco que dejó #283: sin piso la validación se SALTABA y cualquier valor entraba (aquí, 100.000 contra
    // un flete de 3.000.000) sin aviso. Ahora el piso no verificable es un 409 y no se guarda.
    [Fact]
    public async Task Sin_piso_verificable_no_se_guarda_en_silencio_responde_409_PISO_SICETAC_NO_VERIFICABLE()
    {
        var (c, lineaSolicitudId, fila) = Preparar(piso: null);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 100_000m, null)));

        Assert.Equal("PISO_SICETAC_NO_VERIFICABLE", ex.Code);
        Assert.Equal(3_000_000m, fila.ValorFlete);
        Assert.Equal(FuenteFlete.Manual, fila.Fuente);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Cuando la causa es el período (no es el del mes actual), el aviso la dice con las palabras del motor de propuesta.
    [Fact]
    public async Task El_aviso_de_piso_no_verificable_trae_el_motivo_del_periodo_si_ese_es_el_caso()
    {
        var (c, lineaSolicitudId, _) = Preparar(piso: null);
        const string motivo = "SICE-TAC no se usa: el período más reciente cargado es 202610 y el del mes actual es 202611 (sin mes de gracia).";
        _propuestas.Setup(p => p.ProponerAsync(c.SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(c.SolicitudId, [
                new clsPropuestaLineaDto(lineaSolicitudId, "A → B", "C2", 1000, null, null, null, null, null, "", MotivoPendiente: motivo),
            ], []));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 100_000m, null)));

        Assert.Equal("PISO_SICETAC_NO_VERIFICABLE", ex.Code);
        Assert.Contains("202611", ex.Message);
    }

    // Quien edita puede aceptar guardar sin verificar el piso: se guarda, y la auditoría deja constancia.
    [Fact]
    public async Task Confirmando_sin_piso_se_guarda_y_la_auditoria_lo_registra()
    {
        var (c, lineaSolicitudId, fila) = Preparar(FuenteFlete.Tarifario, piso: null);

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 100_000m, null, ConfirmarSinPiso: true));

        Assert.Equal(100_000m, fila.ValorFlete);
        Assert.Equal(FuenteFlete.Manual, fila.Fuente);
        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid>(), "cotizaciones", c.Id, "editar_flete",
            It.Is<string>(d => d.Contains("\"confirmadoSinPiso\":true"))), Times.Once);
    }

    // La confirmación NO salta un piso que SÍ se encontró: bajo el piso es siempre 400.
    [Fact]
    public async Task Confirmar_sin_piso_no_permite_un_valor_bajo_un_piso_que_si_se_encontro()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, Piso - 1, null, ConfirmarSinPiso: true)));

        Assert.Equal("FLETE_BAJO_PISO_SICETAC", ex.Code);
        Assert.Equal(3_000_000m, fila.ValorFlete);
    }

    [Fact]
    public async Task Audita_el_valor_y_la_fuente_anteriores_y_los_nuevos()
    {
        var (c, lineaSolicitudId, _) = Preparar(FuenteFlete.Tarifario);

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid>(), "cotizaciones", c.Id, "editar_flete",
            It.Is<string>(d => d.Contains("\"valorAnterior\":3000000") && d.Contains("\"fuenteAnterior\":\"Tarifario\"")
                && d.Contains("\"valorNuevo\":3500000") && d.Contains("\"fuenteNueva\":\"Manual\"")
                && d.Contains("\"confirmadoSinPiso\":false"))), Times.Once);
    }
}
