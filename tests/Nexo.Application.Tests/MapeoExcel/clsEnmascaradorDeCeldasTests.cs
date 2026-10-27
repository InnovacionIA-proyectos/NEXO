using Nexo.Application.CasosDeUso.MapeoExcel;

namespace Nexo.Application.Tests.MapeoExcel;

/// <summary>Lo que nunca debe salir hacia el modelo: NIT, nombres de personas, correos, teléfonos y valores comerciales.</summary>
public class clsEnmascaradorDeCeldasTests
{
    // ---------------------------------------------------------------- por el encabezado

    [Theory]
    [InlineData("NIT", "<NIT>")]
    [InlineData("NIT Cliente", "<NIT>")]
    [InlineData("Cédula del conductor", "<NIT>")]
    [InlineData("Correo electrónico", "<CORREO>")]
    [InlineData("E-MAIL", "<CORREO>")]
    [InlineData("e_mail contacto", "<CORREO>")]
    [InlineData("Teléfono contacto", "<TELEFONO>")]
    [InlineData("CELULAR", "<TELEFONO>")]
    [InlineData("Conductor", "<NOMBRE>")]
    [InlineData("Nombre del solicitante", "<NOMBRE>")]
    [InlineData("Cliente", "<NOMBRE>")]
    [InlineData("VALOR MERCANCIA", "<VALOR>")]
    [InlineData("Tarifa propuesta", "<VALOR>")]
    [InlineData("Flete", "<VALOR>")]
    // Pegado sin espacio, en cualquier combinación de mayúsculas.
    [InlineData("NombreContacto", "<NOMBRE>")]
    [InlineData("nombrecontacto", "<NOMBRE>")]
    [InlineData("NOMBRECONTACTO", "<NOMBRE>")]
    [InlineData("TelefonoCelular", "<TELEFONO>")]
    [InlineData("telefonocelular", "<TELEFONO>")]
    [InlineData("CorreoContacto", "<CORREO>")]
    [InlineData("NITCliente", "<NIT>")]
    [InlineData("ValorMercancia", "<VALOR>")]
    // Un sinónimo que no está en la lista de palabras.
    [InlineData("Quien recibe", "<NOMBRE>")]
    [InlineData("Quién entrega", "<NOMBRE>")]
    [InlineData("A cargo de", "<NOMBRE>")]
    [InlineData("Persona de contacto", "<NOMBRE>")]
    [InlineData("Atendido por", "<NOMBRE>")]
    [InlineData("Razón social", "<NOMBRE>")]
    [InlineData("Apellidos", "<NOMBRE>")]
    [InlineData("Representante legal", "<NOMBRE>")]
    [InlineData("Tel.", "<TELEFONO>")]
    [InlineData("Cel", "<TELEFONO>")]
    [InlineData("Nº de celular", "<TELEFONO>")]
    public void Un_encabezado_que_delata_datos_sensibles_marca_toda_la_columna(string encabezado, string marcador)
    {
        Assert.Equal(marcador, clsEnmascaradorDeCeldas.MarcadorDeColumna(encabezado));
    }

    [Theory]
    [InlineData("Origen")]
    [InlineData("CIUDAD DESTINO")]
    [InlineData("Peso total (kg)")]
    [InlineData("TIPO DE CAMION")]
    [InlineData("Cantidad")]
    [InlineData("E")]
    [InlineData("Descripción de la mercancía")]
    [InlineData("Peso unitario")]
    [InlineData("Fecha de entrega")]
    [InlineData("Unidad de medida")]
    [InlineData("Hotel")]
    [InlineData("Cargo")]
    [InlineData("Tipo de carrocería")]
    [InlineData("Ciudad de cargue")]
    [InlineData("Referencia")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_encabezado_normal_no_oculta_la_columna(string? encabezado)
    {
        Assert.Null(clsEnmascaradorDeCeldas.MarcadorDeColumna(encabezado));
    }

    [Fact]
    public void Una_celda_de_una_columna_sensible_se_reemplaza_por_su_marcador()
    {
        Assert.Equal("<NIT>", clsEnmascaradorDeCeldas.Celda("900123456-1", "<NIT>"));
        Assert.Equal("<VALOR>", clsEnmascaradorDeCeldas.Celda("12.500.000", "<VALOR>"));
    }

    // ---------------------------------------------------------------- por el contenido

    [Theory]
    [InlineData("Escribir a juan.perez@empresa.com.co hoy", "Escribir a <CORREO> hoy")]
    [InlineData("NIT 900123456-1 de BASF", "NIT <NIT> de BASF")]
    [InlineData("Cel 3101234567", "Cel <NUMERO>")]
    [InlineData("CC 1023456789", "CC <NUMERO>")]
    [InlineData("Llamar al 300 123 4567", "Llamar al <TELEFONO>")]
    [InlineData("+57 300-123-4567", "<TELEFONO>")]
    [InlineData("(601) 555 1234", "<TELEFONO>")]
    [InlineData("Tel 555-1234", "Tel <TELEFONO>")]
    public void Un_dato_sensible_escrito_en_una_columna_cualquiera_tambien_se_oculta(string celda, string esperado)
    {
        Assert.Equal(esperado, clsEnmascaradorDeCeldas.Celda(celda, null));
    }

    [Theory]
    [InlineData("8000")]
    [InlineData("1.234,56")]
    [InlineData("120x100x150")]
    [InlineData("BOGOTA - CALI")]
    [InlineData("3S3")]
    [InlineData("12345678")]
    [InlineData("100 150 200")]
    [InlineData("REF-123-4567")]
    [InlineData("Peso 12,5 t")]
    public void Los_datos_normales_del_archivo_pasan_sin_cambios(string celda)
    {
        Assert.Equal(celda, clsEnmascaradorDeCeldas.Celda(celda, null));
    }

    [Fact]
    public void Una_celda_larga_se_recorta_y_los_caracteres_de_control_se_quitan()
    {
        var larga = clsEnmascaradorDeCeldas.Celda(new string('a', 500), null);
        Assert.Equal(clsEnmascaradorDeCeldas.MaxCaracteresPorCelda, larga.Length);

        Assert.Equal("a b", clsEnmascaradorDeCeldas.Celda("a\u0000b", null));
    }

    [Fact]
    public void Una_celda_vacia_o_nula_queda_vacia_aunque_la_columna_sea_sensible()
    {
        Assert.Equal(string.Empty, clsEnmascaradorDeCeldas.Celda(null, "<NIT>"));
        Assert.Equal(string.Empty, clsEnmascaradorDeCeldas.Celda("   ", null));
    }

    // ---------------------------------------------------------------- nombres de personas escritos en una celda cualquiera

    [Theory]
    [InlineData("Juan Pérez")]
    [InlineData("JUAN PEREZ")]
    [InlineData("juan perez")]
    [InlineData("María del Carmen Gómez")]
    [InlineData("Carlos Andrés Ruiz Torres")]
    [InlineData("Sr. Ruiz")]
    [InlineData("Ing. Brayan Yeimy")]
    [InlineData("Pérez, Juan")]
    [InlineData("Entregar a Juan Pérez en bodega 3")]
    [InlineData("Brayan Pérez")]
    public void Un_nombre_de_persona_en_texto_libre_se_oculta(string celda)
    {
        Assert.Equal("<NOMBRE>", clsEnmascaradorDeCeldas.Celda(celda, null, detectarNombres: true));
    }

    [Theory]
    [InlineData("Santa Marta")]
    [InlineData("SAN JUAN")]
    [InlineData("San Pedro de los Milagros")]
    [InlineData("Puerto Colombia")]
    [InlineData("Barranquilla Atlántico")]
    [InlineData("Bogotá D.C.")]
    [InlineData("Camión Sencillo")]
    [InlineData("Furgón Refrigerado")]
    [InlineData("Tractocamión 3S3")]
    [InlineData("Cajas de cartón")]
    [InlineData("Buenaventura")]
    [InlineData("Juan")]
    [InlineData("Peso total en kilos")]
    public void Una_ciudad_un_vehiculo_o_una_mercancia_no_se_confunden_con_un_nombre(string celda)
    {
        Assert.Equal(celda, clsEnmascaradorDeCeldas.Celda(celda, null, detectarNombres: true));
    }

    [Fact]
    public void Sin_pedirlo_la_celda_no_busca_nombres_porque_una_columna_categorica_necesita_verse()
    {
        Assert.Equal("Santa Marta", clsEnmascaradorDeCeldas.Celda("Santa Marta", null));
        Assert.Equal("Juan Pérez", clsEnmascaradorDeCeldas.Celda("Juan Pérez", null));
    }

    // ---------------------------------------------------------------- la columna entera, por lo que trae

    [Fact]
    public void Una_columna_de_nombres_se_oculta_entera_aunque_algunos_nombres_no_esten_en_la_lista()
    {
        // "Brayan Yeimy" y "Yeimy Ortega" no se reconocen solos; la columna sí: la mayoría de sus celdas son nombres conocidos.
        string[] celdas = ["Juan Pérez", "Brayan Yeimy", "Carlos Ruiz", "Yeimy Ortega", "María Gómez"];
        Assert.Equal("<NOMBRE>", clsEnmascaradorDeCeldas.MarcadorPorContenido(celdas));
    }

    [Fact]
    public void Una_sola_ciudad_que_se_parece_a_un_nombre_no_oculta_una_columna_de_ciudades()
    {
        var ciudades = Enumerable.Range(0, 30).Select(i => i == 7 ? "Rafael Uribe" : "Cali").ToList();
        Assert.Null(clsEnmascaradorDeCeldas.MarcadorPorContenido(ciudades));
    }

    [Theory]
    [InlineData("Escribir a juan.perez@empresa.com.co", "<CORREO>")]
    [InlineData("900123456-1", "<NIT>")]
    [InlineData("300 123 4567", "<TELEFONO>")]
    public void Un_dato_sensible_en_una_celda_oculta_toda_la_columna(string celda, string marcador)
    {
        Assert.Equal(marcador, clsEnmascaradorDeCeldas.MarcadorPorContenido(["Cali", celda, "Bogotá"]));
    }

    [Fact]
    public void Una_columna_normal_no_se_oculta()
    {
        Assert.Null(clsEnmascaradorDeCeldas.MarcadorPorContenido(["Cali", "Bogotá", "Santa Marta", "8000", "", null]));
    }

    [Fact]
    public void Los_marcadores_combinan_el_encabezado_y_el_contenido_de_cada_columna()
    {
        string[] encabezados = ["Origen", "Quién lo recibe en destino", "Col 3", "Teléfono"]; // la 2 por el encabezado, la 3 por lo que trae, la 4 por el encabezado
        IReadOnlyList<IReadOnlyList<string>> filas =
        [
            ["Cali", "x", "Juan Pérez", "1"],
            ["Bogotá", "y", "Carlos Ruiz", "2"],
        ];

        var marcadores = clsEnmascaradorDeCeldas.MarcadoresDeColumnas(encabezados, filas);

        Assert.Equal([null, "<NOMBRE>", "<NOMBRE>", "<TELEFONO>"], marcadores);
    }
}
