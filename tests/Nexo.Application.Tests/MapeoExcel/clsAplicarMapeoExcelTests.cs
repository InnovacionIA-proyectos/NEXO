using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.MapeoExcel;

/// <summary>
/// Aplicar el mapeo confirmado a todas las filas: las conversiones declaradas, la traducción de valores con el diccionario del cliente, las
/// reglas de la captura y los errores por fila. No hay IA ni base de datos: el diccionario es un doble.
/// </summary>
public class clsAplicarMapeoExcelTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid OtroCliente = Guid.NewGuid();

    private readonly Mock<IDiccionarioClienteRepository> _dic = new();

    public clsAplicarMapeoExcelTests() => ConDiccionario();

    private clsAplicarMapeoExcelQueryHandler Manejador() => new(_dic.Object);

    private void ConDiccionario(params clsTerminoCliente[] terminos) =>
        _dic.Setup(d => d.ListarAsync(It.IsAny<Guid?>(), true, clsTerminoCliente.TipoValor, null, true, It.IsAny<CancellationToken>())).ReturnsAsync(terminos);

    private static clsTerminoCliente Valor(string texto, string contexto, string concepto, Guid? cliente = null) => new()
    {
        ClienteId = cliente, Termino = texto, TerminoNormalizado = clsResolucionDeCatalogos.Normalizar(texto),
        Tipo = clsTerminoCliente.TipoValor, Contexto = contexto, Concepto = concepto,
    };

    private static clsColumnaAAplicar Col(int indice, string campo, string? transformacion = null) => new(indice, campo, transformacion);

    private static IReadOnlyList<IReadOnlyList<string>> Filas(params string[][] filas) => filas.Select(f => (IReadOnlyList<string>)f).ToList();

    private static clsAplicarMapeoExcelQuery Consulta(clsColumnaAAplicar[] columnas, string[][] filas, Guid? cliente = null, int primeraFila = 2) =>
        new(cliente, columnas, Filas(filas), primeraFila);

    private static clsColumnaAAplicar[] Basico => [Col(0, "origen"), Col(1, "destino"), Col(2, "peso")];

    // ---------------------------------------------------------------- las conversiones declaradas

    [Fact]
    public async Task Una_fila_completa_sale_como_linea()
    {
        var r = await Manejador().Handle(Consulta(Basico, [["BOGOTA", "CALI", "8000"]]));

        var l = Assert.Single(r.Lineas);
        Assert.Equal((2, "BOGOTA", "CALI", 8000m), (l.Fila, l.Origen, l.Destino, l.Peso));
        Assert.Empty(r.Errores);
        Assert.Equal((1, 0, 0, 0), (r.TotalFilas, r.FilasVacias, r.FilasConErrores, r.TotalErrores));
    }

    [Fact]
    public async Task El_peso_en_toneladas_pasa_a_kilos()
    {
        var columnas = new[] { Col(0, "origen"), Col(1, "destino"), Col(2, "peso", "peso_toneladas_a_kg") };

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "4,5"], ["A", "B", "12"], ["A", "B", "250,5"]]));

        Assert.Equal([4500m, 12000m, 250500m], r.Lineas.Select(l => l.Peso));
    }

    [Fact]
    public async Task Una_sola_columna_origen_destino_se_separa()
    {
        var columnas = new[] { Col(0, "origen", "dividir_origen_destino"), Col(1, "peso") };

        var r = await Manejador().Handle(Consulta(columnas, [["BOGOTA - CALI", "8000"], ["Medellín / Cartagena", "500"]]));

        Assert.Equal([("BOGOTA", "CALI"), ("Medellín", "Cartagena")], r.Lineas.Select(l => (l.Origen, l.Destino)));
    }

    [Fact]
    public async Task Los_numeros_con_coma_decimal_se_leen()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "valorMercancia", "numero_coma_decimal"), Col(4, "horasCargue", "numero_coma_decimal")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "$ 1.234.567,50", "2,5"]]));

        var l = Assert.Single(r.Lineas);
        Assert.Equal((1234567.50m, 2.5m), (l.ValorMercancia, l.HorasCargue));
    }

    [Fact]
    public async Task La_fecha_limite_se_lee_y_va_en_la_linea()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "deadline", "fecha"), Col(4, "referencia")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "31/10/2026", "PO-123"]]));

        var l = Assert.Single(r.Lineas);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.FromHours(-5)), l.Deadline);
        Assert.Equal("PO-123", l.Referencia);
    }

    [Fact]
    public async Task Los_si_no_se_leen_y_las_columnas_ignoradas_no_cuentan()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "exclusivo"), Col(4, "ignorar"), Col(5, "consolidado")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "SI", "lo que sea", "no"]]));

        var l = Assert.Single(r.Lineas);
        Assert.Equal((true, false, null), (l.Exclusivo, l.Consolidado, (bool?)null));
    }

    [Fact]
    public async Task Las_celdas_vacias_son_nulas_y_una_fila_corta_no_falla()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "tipoVehiculo"), Col(4, "horasCargue")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000"], ["C", "D", "9000", "", "  "]]));

        Assert.Equal(2, r.Lineas.Count);
        Assert.All(r.Lineas, l => { Assert.Null(l.TipoVehiculo); Assert.Null(l.HorasCargue); });
    }

    // ---------------------------------------------------------------- el diccionario del cliente

    [Fact]
    public async Task Los_valores_del_cliente_se_traducen_y_lo_del_cliente_manda_sobre_lo_general()
    {
        ConDiccionario(
            Valor("FRIGO", "tipoVehiculo", "REFRIGERADO"),
            Valor("Camion grande", "tipoVehiculo", "TRACTOCAMION", Cliente),
            Valor("Camion grande", "tipoVehiculo", "SENCILLO"),
            Valor("Camion grande", "tipoVehiculo", "OTRO", OtroCliente));
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "tipoVehiculo")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "frigo"], ["A", "B", "8000", "CAMIÓN GRANDE"], ["A", "B", "8000", "Turbo"]], Cliente));

        Assert.Equal(["REFRIGERADO", "TRACTOCAMION", "Turbo"], r.Lineas.Select(l => l.TipoVehiculo));
    }

    [Fact]
    public async Task Sin_cliente_solo_valen_los_valores_generales()
    {
        ConDiccionario(Valor("Camion grande", "tipoVehiculo", "OTRO", OtroCliente), Valor("FRIGO", "tipoVehiculo", "REFRIGERADO"));
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "tipoVehiculo")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "frigo"], ["A", "B", "8000", "camion grande"]]));

        Assert.Equal(["REFRIGERADO", "camion grande"], r.Lineas.Select(l => l.TipoVehiculo));
    }

    [Fact]
    public async Task Un_valor_se_traduce_solo_en_el_campo_al_que_pertenece()
    {
        ConDiccionario(Valor("FRIGO", "carroceria", "FURGON REFRIGERADO"));
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "tipoVehiculo")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "FRIGO"]]));

        Assert.Equal("FRIGO", Assert.Single(r.Lineas).TipoVehiculo);
    }

    // ---------------------------------------------------------------- errores por fila

    [Fact]
    public async Task Cada_fila_con_errores_los_reporta_con_su_numero_y_no_sale_como_linea()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "valorMercancia")];

        var r = await Manejador().Handle(Consulta(columnas,
            [["A", "B", "8000", "1000"], ["C", "D", "mucho", "abc"], ["E", "F", "9000", "2000"]], primeraFila: 5));

        Assert.Equal([5, 7], r.Lineas.Select(l => l.Fila));
        Assert.Equal((1, 2), (r.FilasConErrores, r.TotalErrores));
        Assert.Equal([(6, "peso"), (6, "valorMercancia")], r.Errores.Select(e => (e.Fila, e.Campo)));
        Assert.All(r.Errores, e => Assert.Equal("NUMERO_INVALIDO", e.Codigo));
    }

    [Fact]
    public async Task La_pista_de_la_coma_decimal_solo_sale_cuando_hace_falta()
    {
        var r = await Manejador().Handle(Consulta(Basico, [["A", "B", "4,5"]]));

        var e = Assert.Single(r.Errores);
        Assert.Contains("numero_coma_decimal", e.Mensaje);
    }

    [Theory]
    [InlineData("1,5", "CANTIDAD_INVALIDA")]
    [InlineData("0", "FUERA_DE_RANGO")]
    [InlineData("10001", "FUERA_DE_RANGO")]
    public async Task La_cantidad_es_un_entero_dentro_del_rango_de_la_captura(string cantidad, string codigo)
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "cantidad", "numero_coma_decimal"), Col(4, "largoCm"), Col(5, "anchoCm"), Col(6, "altoCm"), Col(7, "pesoKg")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", cantidad, "100", "100", "100", "10"]]));

        Assert.Equal(codigo, Assert.Single(r.Errores).Codigo);
    }

    [Fact]
    public async Task Un_si_no_o_una_fecha_que_no_se_entienden_se_reportan()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "exclusivo"), Col(4, "deadline", "fecha")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "tal vez", "pronto"]]));

        Assert.Equal([("exclusivo", "SI_NO_INVALIDO"), ("deadline", "FECHA_INVALIDA")], r.Errores.Select(e => (e.Campo, e.Codigo)));
    }

    [Fact]
    public async Task Un_origen_destino_que_no_se_puede_separar_se_reporta()
    {
        var columnas = new[] { Col(0, "origen", "dividir_origen_destino"), Col(1, "peso") };

        var r = await Manejador().Handle(Consulta(columnas, [["BOGOTA", "8000"]]));

        Assert.Equal("ORIGEN_DESTINO_NO_SEPARABLE", Assert.Single(r.Errores).Codigo);
    }

    [Fact]
    public async Task Los_limites_de_longitud_y_rango_de_la_captura_se_aplican()
    {
        var columnas = new[] { Col(0, "origen"), Col(1, "destino"), Col(2, "peso", "peso_toneladas_a_kg"), Col(3, "claseContenedor"), Col(4, "horasCargue") };

        var r = await Manejador().Handle(Consulta(columnas, [[new string('x', 151), "B", "1000", "CONTENEDOR_DE_VEINTE_PIES_X", "241"]]));

        Assert.Equal(
            [("origen", "TEXTO_DEMASIADO_LARGO"), ("peso", "FUERA_DE_RANGO"), ("claseContenedor", "TEXTO_DEMASIADO_LARGO"), ("horasCargue", "FUERA_DE_RANGO")],
            r.Errores.Select(e => (e.Campo, e.Codigo)));
    }

    [Fact]
    public async Task Una_fila_sin_ningun_dato_se_salta_sin_error()
    {
        var r = await Manejador().Handle(Consulta(Basico, [["A", "B", "8000"], ["", "", ""], ["  ", "", " "], []]));

        Assert.Equal((4, 3, 0, 1), (r.TotalFilas, r.FilasVacias, r.FilasConErrores, r.Lineas.Count));
        Assert.Empty(r.Errores);
    }

    [Fact]
    public async Task Los_errores_devueltos_tienen_tope_pero_el_total_se_cuenta_completo()
    {
        var filas = Enumerable.Range(0, 150).Select(_ => new[] { "A", "B", "no" }).ToArray();

        var r = await Manejador().Handle(Consulta(Basico, filas));

        Assert.Equal(clsAplicarMapeoExcelQueryHandler.MaxErroresDevueltos, r.Errores.Count);
        Assert.Equal((150, 150), (r.FilasConErrores, r.TotalErrores));
        Assert.Empty(r.Lineas);
    }

    // ---------------------------------------------------------------- las reglas de la captura

    [Fact]
    public async Task Sin_origen_o_sin_peso_la_fila_no_pasa_las_reglas_de_la_captura()
    {
        var columnas = new[] { Col(0, "origen"), Col(1, "destino"), Col(2, "peso"), Col(3, "referencia") };

        var r = await Manejador().Handle(Consulta(columnas, [["", "CALI", "8000", "x"], ["A", "B", "", "x"], ["A", "B", "0", "x"]]));

        Assert.Equal([(2, "LINEA_INCOMPLETA"), (3, "PESO_REQUERIDO"), (4, "PESO_REQUERIDO")], r.Errores.Select(e => (e.Fila, e.Codigo)));
    }

    [Fact]
    public async Task La_carga_se_arma_con_la_cantidad_por_defecto_y_cuenta_para_el_peso()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "descripcion"), Col(4, "largoCm"), Col(5, "anchoCm"), Col(6, "altoCm"), Col(7, "pesoKg")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "", "Cajas", "100", "50", "40", "25"]]));

        var p = Assert.Single(Assert.Single(r.Lineas).Posiciones!);
        Assert.Equal(("Cajas", 1, 100m, 50m, 40m, 25m), (p.Descripcion, p.Cantidad, p.LargoCm, p.AnchoCm, p.AltoCm, p.PesoKg));
    }

    [Fact]
    public async Task El_peso_total_debe_coincidir_con_la_suma_de_la_carga()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "cantidad"), Col(4, "largoCm"), Col(5, "anchoCm"), Col(6, "altoCm"), Col(7, "pesoKg")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "1000", "10", "100", "50", "40", "20"], ["A", "B", "200", "10", "100", "50", "40", "20"]]));

        Assert.Equal([(2, "PESO_INCONSISTENTE")], r.Errores.Select(e => (e.Fila, e.Codigo)));
        Assert.Equal(3, Assert.Single(r.Lineas).Fila);
    }

    [Fact]
    public async Task La_carga_incompleta_se_reporta()
    {
        clsColumnaAAplicar[] columnas = [.. Basico, Col(3, "largoCm"), Col(4, "anchoCm")];

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "8000", "100", "50"]]));

        var e = Assert.Single(r.Errores);
        Assert.Equal(("altoCm", "POSICION_INCOMPLETA"), (e.Campo, e.Codigo));
        Assert.Contains("pesoKg", e.Mensaje);
    }

    // ---------------------------------------------------------------- el mapeo recibido

    [Theory]
    [InlineData("noExiste", null, "CAMPO_INVALIDO")]
    [InlineData("peso", "inventada", "TRANSFORMACION_INVALIDA")]
    [InlineData("origen", "peso_toneladas_a_kg", "TRANSFORMACION_NO_APLICA")]
    [InlineData("peso", "fecha", "TRANSFORMACION_NO_APLICA")]
    [InlineData("referencia", "numero_coma_decimal", "TRANSFORMACION_NO_APLICA")]
    public async Task Un_mapeo_con_campos_o_conversiones_que_no_existen_o_no_aplican_se_rechaza(string campo, string? transformacion, string codigo)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Manejador().Handle(Consulta([.. Basico, Col(3, campo, transformacion)], [["A", "B", "8000", "x"]])));

        Assert.Equal(codigo, ex.Code);
    }

    [Fact]
    public async Task Dos_columnas_para_el_mismo_campo_se_rechazan()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Manejador().Handle(Consulta([.. Basico, Col(3, "peso")], [["A", "B", "8000", "9"]])));

        Assert.Equal("CAMPO_DUPLICADO", ex.Code);
    }

    [Fact]
    public async Task Una_columna_origen_destino_ocupa_los_dos_campos()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Manejador().Handle(Consulta([Col(0, "origen", "dividir_origen_destino"), Col(1, "destino"), Col(2, "peso")], [["A - B", "C", "8000"]])));

        Assert.Equal("CAMPO_DUPLICADO", ex.Code);
    }

    [Fact]
    public async Task Sin_columna_de_origen_o_destino_se_rechaza()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Manejador().Handle(Consulta([Col(0, "origen"), Col(1, "peso")], [["A", "8000"]])));

        Assert.Equal("CAMPO_REQUERIDO", ex.Code);
        Assert.Contains("destino", ex.Message);
    }

    [Fact]
    public async Task Las_columnas_repetidas_o_sin_mapeo_se_rechazan()
    {
        var repetida = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Manejador().Handle(Consulta([Col(0, "origen"), Col(0, "destino")], [["A"]])));
        Assert.Equal("COLUMNA_INVALIDA", repetida.Code);

        var vacio = await Assert.ThrowsAsync<clsValidationFailedException>(() => Manejador().Handle(new clsAplicarMapeoExcelQuery(null, [], Filas())));
        Assert.Equal("MAPEO_REQUERIDO", vacio.Code);

        var nulo = await Assert.ThrowsAsync<clsValidationFailedException>(() => Manejador().Handle(new clsAplicarMapeoExcelQuery(null, null, null)));
        Assert.Equal("MAPEO_REQUERIDO", nulo.Code);
    }

    [Fact]
    public async Task Demasiadas_filas_se_rechazan()
    {
        var filas = Enumerable.Range(0, clsAplicarMapeoExcelQueryHandler.MaxFilas + 1).Select(_ => new[] { "A", "B", "8000" }).ToArray();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Manejador().Handle(Consulta(Basico, filas)));

        Assert.Equal("DEMASIADAS_FILAS", ex.Code);
    }

    [Fact]
    public async Task Sin_filas_el_resultado_es_vacio()
    {
        var r = await Manejador().Handle(Consulta(Basico, []));

        Assert.Equal((0, 0, 0, 0), (r.TotalFilas, r.Lineas.Count, r.Errores.Count, r.TotalErrores));
    }

    [Fact]
    public async Task El_nombre_del_campo_y_de_la_conversion_no_distingue_mayusculas()
    {
        var columnas = new[] { Col(0, "ORIGEN"), Col(1, "Destino"), Col(2, "peso", "PESO_TONELADAS_A_KG") };

        var r = await Manejador().Handle(Consulta(columnas, [["A", "B", "2"]]));

        Assert.Equal(2000m, Assert.Single(r.Lineas).Peso);
    }
}
