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
}
