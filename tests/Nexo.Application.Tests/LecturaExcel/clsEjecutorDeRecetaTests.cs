using Nexo.Application.CasosDeUso.LecturaExcel;
using Nexo.Application.Common.Exceptions;
using static Nexo.Application.Tests.LecturaExcel.clsHojasDePrueba;

namespace Nexo.Application.Tests.LecturaExcel;

/// <summary>
/// Cada prueba reproduce la forma de uno de los formatos reales de cliente (con datos inventados): lista con encabezado más abajo, escenario en una celda
/// suelta, títulos de dos filas con celdas combinadas, tabla cruzada con el origen en las columnas y el destino fuera de la tabla, dos niveles de columnas.
/// </summary>
public class clsEjecutorDeRecetaTests
{
    private static clsRecetaLectura Receta(params clsRecetaHoja[] hojas) => new(clsRecetaLectura.VersionActual, hojas);

    private static IReadOnlyList<string> Fila(clsLecturaResultado r, int tabla, int fila) => r.Tablas[tabla].Filas[fila];

    // ---------------------------------------------------------------- tabla simple

    [Fact]
    public void Una_lista_con_datos_de_la_empresa_arriba_se_lee_desde_su_encabezado_y_salta_las_filas_vacias()
    {
        var hoja = Hoja("Formulario", [
            "Formulario de oferta",
            "Razón social|ACME",
            "NIT|900",
            "",
            "Ruta|Secuencia|Tipo de vehículo|Km",
            "Ruta 1|Bello - Neiva|Sencillo|1295",
            "",
            "Ruta 1|Bello - Neiva|Tractomula|1295",
        ]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Formulario", FilasEncabezado: [5])));

        var t = Assert.Single(r.Tablas);
        Assert.Equal(["Ruta", "Secuencia", "Tipo de vehículo", "Km"], t.Encabezados);
        Assert.Equal(2, t.TotalFilas);
        Assert.Equal(["Ruta 1", "Bello - Neiva", "Tractomula", "1295"], t.Filas[1]);
        Assert.Equal(5, t.FilaEncabezado);
    }

    [Fact]
    public void Una_celda_suelta_y_el_nombre_de_la_hoja_se_repiten_como_columnas_en_todas_las_filas()
    {
        var hoja = Hoja("Escenarios", [
            "ESCENARIO 1",
            "",
            "Origen|Zona|Destino|Viajes",
            "Manizales|Antioquia|Medellin|2",
            "Manizales|Caribe|Santa Marta|1",
        ]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Escenarios", FilasEncabezado: [3],
            Constantes: [new clsRecetaConstante("Escenario", Celda: "A1"), new clsRecetaConstante("Hoja", Hoja: true)])));

        Assert.Equal(["Origen", "Zona", "Destino", "Viajes", "Escenario", "Hoja"], r.Tablas[0].Encabezados);
        Assert.Equal(["Manizales", "Caribe", "Santa Marta", "1", "ESCENARIO 1", "Escenarios"], Fila(r, 0, 1));
    }

    [Fact]
    public void Dos_filas_de_titulos_con_celdas_combinadas_se_unen_y_cada_columna_hereda_el_titulo_de_su_grupo()
    {
        var hoja = Hoja("Inbound", [
            "|||VOLUMEN||TARIFA",
            "Item|Origen|Destino|TURBO|MULA|TURBO|MULA",
            "I-1|CARTAGENA|FUNZA|5|2||",
        ], combinadas: ["D1:E1", "F1:G1"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Inbound", FilasEncabezado: [1, 2])));

        Assert.Equal(["Item", "Origen", "Destino", "VOLUMEN · TURBO", "VOLUMEN · MULA", "TARIFA · TURBO", "TARIFA · MULA"], r.Tablas[0].Encabezados);
    }

    [Fact]
    public void Una_etiqueta_repartida_en_celdas_combinadas_hacia_abajo_se_repite_en_cada_fila()
    {
        var hoja = Hoja("Rutas", [
            "Ruta|Vehículo|Tarifa",
            "Ruta 1|Sencillo|100",
            "|Mula|200",
            "Ruta 2|Sencillo|300",
        ], combinadas: ["A2:A3"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Rutas", FilasEncabezado: [1])));

        Assert.Equal("Ruta 1", Fila(r, 0, 1)[0]);
        Assert.Equal("Ruta 2", Fila(r, 0, 2)[0]);
    }

    [Fact]
    public void Los_datos_pueden_terminar_en_la_primera_fila_vacia_cuando_hay_notas_debajo()
    {
        var hoja = Hoja("Tabla", ["A|B", "1|2", "3|4", "", "Nota: las tarifas no incluyen IVA|"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Tabla", FilasEncabezado: [1], HastaPrimeraFilaVacia: true)));

        Assert.Equal(2, r.Tablas[0].TotalFilas);
    }

    [Fact]
    public void Se_pueden_limitar_las_filas_y_las_columnas_que_se_leen()
    {
        var hoja = Hoja("Tabla", ["A|B|C|D", "1|2|3|4", "5|6|7|8", "9|10|11|12"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Tabla", FilasEncabezado: [1], PrimeraFila: 3, UltimaFila: 3, Columnas: ["A", "C:D"])));

        Assert.Equal(["A", "C", "D"], r.Tablas[0].Encabezados);
        Assert.Equal(["5", "7", "8"], Fila(r, 0, 0));
        Assert.Equal(1, r.Tablas[0].TotalFilas);
    }

    // ---------------------------------------------------------------- secciones

    [Fact]
    public void Una_fila_con_solo_un_titulo_es_una_seccion_y_su_texto_va_a_las_filas_de_abajo()
    {
        var hoja = Hoja("Viajes", [
            "Origen|Destino|Viajes",
            "IMPORTACIÓN||",
            "Cartagena|Bogotá|3",
            "Buenaventura|Cali|1",
            "EXPORTACIÓN||",
            "Bogotá|Cartagena|2",
        ]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Viajes", FilasEncabezado: [1], Seccion: new clsRecetaSeccion("A", "Tipo de viaje"))));

        Assert.Equal(["Origen", "Destino", "Viajes", "Tipo de viaje"], r.Tablas[0].Encabezados);
        Assert.Equal(3, r.Tablas[0].TotalFilas);
        Assert.Equal(["Cartagena", "Bogotá", "3", "IMPORTACIÓN"], Fila(r, 0, 0));
        Assert.Equal(["Bogotá", "Cartagena", "2", "EXPORTACIÓN"], Fila(r, 0, 2));
    }

    // ---------------------------------------------------------------- tablas cruzadas

    [Fact]
    public void Una_tabla_cruzada_con_origenes_en_columnas_destino_fuera_de_la_tabla_y_filas_en_blanco_entre_bloques_se_despivota()
    {
        var hoja = Hoja("VALLE DE ABURRA", [
            "|||||||||",
            "Transporte de mercancías al área metropolitana|||||||||",
            "|||||||||",
            "TIPO DE SERVICIO||PUERTOS DE ORIGEN||||||||",
            "||CARTAGENA||BARRANQUILLA||SANTA MARTA||PONDERADO",
            "||||||||||",
            "Turbo hasta 4.5 ton||1000||1200||1300||1052",
            "Sencillo hasta 8.5 ton||1700||1500||||1656",
            "||||||||||",
            "Conten. x 20' hasta 8 ton||||||||||",
        ], combinadas: ["A4:B5", "C4:J4", "C5:D5", "E5:F5", "G5:H5", "A7:B7", "A8:B8", "A10:B10"]);

        var receta = Receta(new clsRecetaHoja("VALLE DE ABURRA",
            FilasEncabezado: [4],
            PrimeraFila: 6,
            Etiquetas: [new clsRecetaEtiqueta("A", "Servicio")],
            Valores: new clsRecetaValores(["C", "E", "G"], [new clsRecetaDimension(5, "Origen")], "Tarifa", OmitirVacios: false),
            Constantes: [new clsRecetaConstante("Destino", Celda: "A2")]));
        var r = clsEjecutorDeReceta.Ejecutar([hoja], receta);

        var t = Assert.Single(r.Tablas);
        Assert.Equal(["Servicio", "Origen", "Tarifa", "Destino"], t.Encabezados);
        // 3 servicios × 3 orígenes (el cuarto servicio no tiene precios, pero se quiere cotizar igual) = 9; la columna «PONDERADO» no se lee.
        Assert.Equal(9, t.TotalFilas);
        Assert.Equal(["Turbo hasta 4.5 ton", "CARTAGENA", "1000", "Transporte de mercancías al área metropolitana"], t.Filas[0]);
        Assert.Contains(t.Filas, f => f[0] == "Conten. x 20' hasta 8 ton" && f[1] == "SANTA MARTA" && f[2] == "");
        Assert.DoesNotContain(t.Filas, f => f.Contains("1052"));
    }

    [Fact]
    public void Dos_niveles_de_columnas_origen_con_vehiculos_debajo_dan_una_fila_por_destino_origen_y_vehiculo()
    {
        var hoja = Hoja("PROP ECONOM", [
            "ORIGEN|FUNZA||||TOCANCIPA||",
            "TIPO VEHICULO|PATINETA|VH 10 TON|PATINETA|VH 10 TON|PATINETA|VH 10 TON",
            "DESTINO FINAL|PROPUESTA|||||",
            "APARTADO|7318001|2843936|7318002|2843937|7618001|2895305",
            "RIONEGRO|4159622||4159623||4531102|",
        ], combinadas: ["B1:E1", "F1:G1"]);

        var receta = Receta(new clsRecetaHoja("PROP ECONOM", PrimeraFila: 4,
            Etiquetas: [new clsRecetaEtiqueta("A", "Destino")],
            Valores: new clsRecetaValores(["B:G"], [new clsRecetaDimension(1, "Origen"), new clsRecetaDimension(2, "Vehículo")], "Tarifa")));
        var r = clsEjecutorDeReceta.Ejecutar([hoja], receta);

        var t = r.Tablas[0];
        Assert.Equal(["Destino", "Origen", "Vehículo", "Tarifa"], t.Encabezados);
        Assert.Contains(t.Filas, f => f.SequenceEqual(["APARTADO", "FUNZA", "PATINETA", "7318001"]));
        Assert.Contains(t.Filas, f => f.SequenceEqual(["APARTADO", "TOCANCIPA", "VH 10 TON", "2895305"]));
        // OmitirVacios es verdadero por defecto: RIONEGRO solo tiene tres precios.
        Assert.Equal(6 + 3, t.TotalFilas);
    }

    [Fact]
    public void Una_tabla_cruzada_toma_el_titulo_de_la_etiqueta_de_las_filas_de_encabezado_si_no_se_le_da_nombre()
    {
        var hoja = Hoja("Hoja", ["Servicio|Cartagena|Santa Marta", "Turbo|10|20"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Hoja", FilasEncabezado: [1],
            Etiquetas: [new clsRecetaEtiqueta("A")], Valores: new clsRecetaValores(["B:C"], [new clsRecetaDimension(1, "Origen")]))));

        Assert.Equal(["Servicio", "Origen", "Valor"], r.Tablas[0].Encabezados);
    }

    // ---------------------------------------------------------------- trasponer, hojas y unión

    [Fact]
    public void Un_archivo_con_filas_y_columnas_intercambiadas_se_lee_trasponiendolo()
    {
        var hoja = Hoja("Tras", ["Origen|Bogotá|Cali", "Destino|Medellín|Pasto", "Viajes|4|2"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("Tras", Trasponer: true, FilasEncabezado: [1])));

        Assert.Equal(["Origen", "Destino", "Viajes"], r.Tablas[0].Encabezados);
        Assert.Equal(["Bogotá", "Medellín", "4"], Fila(r, 0, 0));
        Assert.Equal(["Cali", "Pasto", "2"], Fila(r, 0, 1));
    }

    [Fact]
    public void El_comodin_lee_las_hojas_visibles_no_nombradas_pero_nunca_las_ocultas()
    {
        var visible = Hoja("Uno", ["A|B|C", "1|2|3"]);
        var otra = Hoja("Dos", ["A|B|C", "4|5|6"]);
        var oculta = Hoja("TAGGED", ["A|B|C", "7|8|9"], oculta: true);

        var r = clsEjecutorDeReceta.Ejecutar([visible, otra, oculta], Receta(
            new clsRecetaHoja("Dos", Usar: false),
            new clsRecetaHoja("*", FilasEncabezado: [1])));

        Assert.Equal(["Uno"], r.Tablas.Select(t => t.Nombre));
    }

    [Fact]
    public void Una_hoja_oculta_se_lee_si_la_receta_la_nombra()
    {
        var oculta = Hoja("Datos", ["A|B|C", "1|2|3"], oculta: true);

        var r = clsEjecutorDeReceta.Ejecutar([oculta], Receta(new clsRecetaHoja("Datos", FilasEncabezado: [1])));

        Assert.Single(r.Tablas);
    }

    [Fact]
    public void Un_nombre_de_hoja_se_reconoce_sin_importar_mayusculas_ni_tildes_y_una_hoja_que_no_esta_se_avisa()
    {
        var hoja = Hoja("Importación", ["A|B|C", "1|2|3"]);

        var r = clsEjecutorDeReceta.Ejecutar([hoja], Receta(
            new clsRecetaHoja("IMPORTACION", FilasEncabezado: [1]),
            new clsRecetaHoja("Exportación", FilasEncabezado: [1])));

        Assert.Single(r.Tablas);
        Assert.Contains(r.Avisos, a => a.Contains("Exportación"));
    }

    [Fact]
    public void Unir_junta_las_hojas_en_una_sola_tabla_alineando_las_columnas_por_nombre()
    {
        var a = Hoja("Norte", ["Servicio|Tarifa", "Turbo|10"]);
        var b = Hoja("Sur", ["Servicio|Tarifa|Observación", "Mula|20|nota"]);

        var r = clsEjecutorDeReceta.Ejecutar([a, b], Receta(new clsRecetaHoja("*", FilasEncabezado: [1],
            Constantes: [new clsRecetaConstante("Zona", Hoja: true)])) with { Unir = true });

        var t = Assert.Single(r.Tablas);
        Assert.Equal(["Servicio", "Tarifa", "Zona", "Observación"], t.Encabezados);
        Assert.Equal(["Turbo", "10", "Norte", ""], t.Filas[0]);
        Assert.Equal(["Mula", "20", "Sur", "nota"], t.Filas[1]);
    }

    [Fact]
    public void Mas_filas_de_las_permitidas_se_cortan_y_se_avisa_el_total()
    {
        var filas = new List<string> { "A|B" };
        filas.AddRange(Enumerable.Range(1, clsEjecutorDeReceta.MaxFilasPorTabla + 10).Select(i => $"{i}|x"));

        var r = clsEjecutorDeReceta.Ejecutar([Hoja("Grande", [.. filas])], Receta(new clsRecetaHoja("Grande", FilasEncabezado: [1])));

        Assert.Equal(clsEjecutorDeReceta.MaxFilasPorTabla, r.Tablas[0].Filas.Count);
        Assert.Equal(clsEjecutorDeReceta.MaxFilasPorTabla + 10, r.Tablas[0].TotalFilas);
        Assert.Contains(r.Avisos, a => a.Contains("primeras"));
    }

    [Fact]
    public void Una_receta_invalida_no_se_ejecuta()
    {
        var hoja = Hoja("X", ["A|B"]);

        var error = Assert.Throws<clsValidationFailedException>(() =>
            clsEjecutorDeReceta.Ejecutar([hoja], Receta(new clsRecetaHoja("X", Columnas: ["1A"]))));

        Assert.Equal("RECETA_INVALIDA", error.Code);
    }
}
