using System.Text.Json;
using Moq;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.MapeoExcel.Normalizacion;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.Tests.MapeoExcel;

/// <summary>
/// La línea de negocio en <c>normalizar-valores</c>: el diccionario del cliente manda, luego los pocos sinónimos inequívocos y solo después la IA,
/// que elige entre las seis líneas de NEXO. «Inbound» y «Outbound» no se traducen por regla.
/// </summary>
public class clsNormalizarLineaNegocioTests
{
    private static readonly Guid Cliente = Guid.NewGuid();

    private readonly Mock<IAsistenteIaPort> _ia = new();
    private readonly Mock<IDiccionarioClienteRepository> _dic = new();
    private readonly clsNormalizarValoresQueryHandler _manejador;
    private clsPeticionIa? _enviada;

    public clsNormalizarLineaNegocioTests()
    {
        var alias = new Mock<IAliasCatalogoPort>();
        alias.Setup(a => a.MapaActivoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, string>());
        var tipos = new Mock<ITiposVehiculoPort>();
        tipos.Setup(t => t.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var vehiculos = new Mock<IConfiguracionesVehiculoPort>();
        vehiculos.Setup(v => v.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var municipios = new Mock<IMunicipiosRepository>();
        municipios.Setup(m => m.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var usuario = new Mock<ICurrentUserPort>();
        usuario.SetupGet(u => u.Id).Returns(Guid.NewGuid());

        _ia.Setup(i => i.Habilitada(clsFuncionIa.ImportacionExcel)).Returns(true);
        ConDiccionario();
        IaResponde(Json());
        _manejador = new(_ia.Object, _dic.Object, alias.Object, tipos.Object, vehiculos.Object, municipios.Object, usuario.Object);
    }

    private void ConDiccionario(params clsTerminoCliente[] terminos) =>
        _dic.Setup(d => d.ListarAsync(It.IsAny<Guid?>(), true, clsTerminoCliente.TipoValor, null, true, It.IsAny<CancellationToken>())).ReturnsAsync(terminos);

    private static clsTerminoCliente Valor(string texto, string concepto) => new()
    {
        ClienteId = Cliente, Termino = texto, TerminoNormalizado = clsResolucionDeCatalogos.Normalizar(texto),
        Tipo = clsTerminoCliente.TipoValor, Contexto = "lineaNegocio", Concepto = concepto,
    };

    private void IaResponde(string json) =>
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .Callback<clsPeticionIa, CancellationToken>((p, _) => _enviada = p)
            .ReturnsAsync(new clsRespuestaIa(json, json, "modelo", 10, 5, TimeSpan.Zero));

    private static string Json(params object[] resultados) => JsonSerializer.Serialize(new { resultados });

    private Task<clsResultadoNormalizacionDto> Normalizar(params string[] textos) =>
        _manejador.Handle(new clsNormalizarValoresQuery(Cliente, textos.Select(t => new clsValorANormalizar("lineaNegocio", t)).ToList()));

    [Fact]
    public async Task El_diccionario_del_cliente_traduce_Outbound_sin_llamar_a_la_IA()
    {
        ConDiccionario(Valor("Outbound", "Interior a puerto"), Valor("Inbound", "puertoAInterior"));

        var r = await Normalizar("OUTBOUND", "inbound");

        Assert.Equal(
            [("resuelto", "interiorAPuerto", "Interior a puerto", "diccionario"), ("resuelto", "puertoAInterior", "Puerto a interior", "diccionario")],
            r.Valores.Select(v => (v.Estado, v.Codigo, v.Descripcion, v.Origen)));
        _ia.Verify(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_concepto_del_diccionario_que_no_es_una_linea_de_NEXO_no_se_usa()
    {
        ConDiccionario(Valor("Outbound", "Exportaciones"));

        var r = await Normalizar("Outbound");

        Assert.Equal("sin_resolver", Assert.Single(r.Valores).Estado);
    }

    [Fact]
    public async Task Los_sinonimos_inequivocos_y_los_nombres_de_NEXO_se_resuelven_por_regla()
    {
        var r = await Normalizar("IMPO", "Importación", "expo", "EXPORTACIÓN", "Nacional", "puerto a interior", "Devolución de contenedor", "andino");

        Assert.Equal(
            ["puertoAInterior", "puertoAInterior", "interiorAPuerto", "interiorAPuerto", "nacional", "puertoAInterior", "devolucionContenedor", "andino"],
            r.Valores.Select(v => v.Codigo));
        Assert.Equal(["regla", "regla", "regla", "regla", "catalogo", "catalogo", "catalogo", "catalogo"], r.Valores.Select(v => v.Origen));
        Assert.All(r.Valores, v => Assert.Equal(1.0, v.Confianza));
        Assert.False(r.IaUsada);
        Assert.Null(_enviada);
    }

    [Fact]
    public async Task Inbound_sin_diccionario_va_a_la_IA_con_las_seis_lineas_y_con_confianza_baja_queda_sin_resolver()
    {
        IaResponde(Json(new { indice = 0, clave = "puertoAInterior", confianza = 0.4, nota = "podría ser" }));

        var r = await Normalizar("Inbound");

        var v = Assert.Single(r.Valores);
        Assert.Equal("sin_resolver", v.Estado);
        Assert.Contains("no está segura", v.Nota);
        Assert.True(r.IaUsada);

        var catalogo = JsonDocument.Parse(Assert.Single(_enviada!.Mensajes).Contenido).RootElement.GetProperty("catalogos").GetProperty("lineaNegocio");
        Assert.Equal(
            ["nacional", "puertoAInterior", "interiorAPuerto", "devolucionContenedor", "andino", "licitaciones"],
            catalogo.EnumerateArray().Select(o => o.GetProperty("clave").GetString()));
    }

    [Fact]
    public async Task Lo_que_la_IA_elige_de_la_lista_con_confianza_suficiente_se_acepta_y_lo_que_no_esta_se_descarta()
    {
        IaResponde(Json(
            new { indice = 0, clave = "devolucionContenedor", confianza = 0.9, nota = "" },
            new { indice = 1, clave = "Importaciones", confianza = 0.95, nota = "" }));

        var r = await Normalizar("Retorno de vacíos", "Flujo entrante");

        Assert.Equal([("resuelto", "devolucionContenedor", "ia"), ("sin_resolver", null, "ninguno")], r.Valores.Select(v => (v.Estado, v.Codigo, v.Origen)));
        Assert.Contains("no está en el catálogo", r.Valores[1].Nota);
    }

    [Fact]
    public async Task Con_la_IA_apagada_lo_que_las_reglas_no_resuelven_queda_sin_resolver()
    {
        _ia.Setup(i => i.Habilitada(clsFuncionIa.ImportacionExcel)).Returns(false);

        var r = await Normalizar("Impo", "Outbound");

        Assert.Equal(["resuelto", "sin_resolver"], r.Valores.Select(v => v.Estado));
        Assert.Equal("IA_NO_DISPONIBLE", r.MotivoSinIa);
    }

    [Theory]
    [InlineData("nacional", true)]
    [InlineData("PuertoAInterior", true)]
    [InlineData("Interior a puerto", true)]
    [InlineData("Devolución de contenedor", true)]
    [InlineData("LICITACIONES", true)]
    [InlineData("Impo", false)]
    [InlineData("Outbound", false)]
    [InlineData("", false)]
    public void La_captura_solo_reconoce_las_seis_lineas(string texto, bool reconocida) =>
        Assert.Equal(reconocida, clsLineasDeNegocio.Reconocer(texto) is not null);
}
