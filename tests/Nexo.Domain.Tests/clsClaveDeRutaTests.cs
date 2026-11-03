using Nexo.Domain.Common;

namespace Nexo.Domain.Tests;

/// <summary>
/// La clave de ruta es lo que hace que una regla "solo SICE-TAC" se aplique a la línea
/// correcta. Si las dos puntas de la comparación no producen la misma cadena, la regla
/// no se aplica — y no falla, simplemente no ocurre.
///
/// Estas pruebas fijan la propiedad que importa: la misma ruta, escrita como la escriba
/// cada fuente, tiene que dar la misma clave. Y documentan el defecto que se corrigió,
/// para que no vuelva por otro lado.
/// </summary>
public class clsClaveDeRutaTests
{
    [Theory]
    [InlineData("Bogotá D.C.", "Cali")]
    [InlineData("BOGOTA D.C.", "CALI")]
    [InlineData("bogota d.c.", "cali")]
    [InlineData("  Bogotá   D.C.  ", "  cali ")]
    [InlineData("BOGOTÁ D.C.", "Calí")]
    public void La_misma_ruta_escrita_de_cualquier_forma_da_la_misma_clave(string origen, string destino)
        => Assert.Equal("BOGOTA D.C.|CALI", clsTexto.ClaveRuta(origen, destino));

    [Fact]
    public void Rutas_distintas_no_colisionan()
    {
        Assert.NotEqual(clsTexto.ClaveRuta("Bogotá", "Cali"), clsTexto.ClaveRuta("Cali", "Bogotá"));
        Assert.NotEqual(clsTexto.ClaveRuta("Bogotá", "Cali"), clsTexto.ClaveRuta("Bogotá", "Calarcá"));
    }

    /// <summary>
    /// EVIDENCIA DEL DEFECTO CORREGIDO EN LA ISSUE #16.
    ///
    /// El adaptador de propuesta comparaba los valores CRUDOS de la ruta contra las
    /// claves ya normalizadas de las reglas:
    ///
    ///     if (soloSice.Contains($"{linea.clsRuta.Origen}|{linea.clsRuta.Destino}"))
    ///
    /// La captura guarda la ruta con clsTexto.Limpiar, que conserva mayúsculas y tildes
    /// ("Bogotá D.C."). El administrador guardaba la regla con un normalizador propio
    /// de Application que pasaba a mayúsculas pero NO quitaba tildes ("BOGOTÁ D.C.").
    ///
    /// Resultado: tres formas distintas de la misma ruta, y una igualdad exacta entre
    /// dos de ellas que nunca coincidía. La regla no se aplicaba y la línea se costeaba
    /// con la comparación normal, que puede proponer la tarifa de plaza. Cotizar bajo
    /// el piso SICE-TAC es sanción legal.
    ///
    /// Esta prueba conserva el porqué: ninguna de las dos formas viejas sirve como
    /// clave, y solo la forma canónica de Domain coincide con ella misma.
    /// </summary>
    [Fact]
    public void El_defecto_estaba_en_comparar_formas_distintas_de_la_misma_ruta()
    {
        const string origenComoLoGuardaLaCaptura = "Bogotá D.C.";   // clsTexto.Limpiar
        const string origenComoLoGuardabaLaRegla = "BOGOTÁ D.C.";   // mayúsculas, con tilde
        const string destino = "Cali";

        var claveCanonica = clsTexto.ClaveRuta(origenComoLoGuardaLaCaptura, destino);

        // Ninguna de las dos formas crudas sirve como clave: por eso la igualdad exacta
        // fallaba en silencio.
        Assert.NotEqual($"{origenComoLoGuardaLaCaptura}|{destino}", claveCanonica);
        Assert.NotEqual($"{origenComoLoGuardabaLaRegla}|{destino}", claveCanonica);

        // Y las dos puntas, pasadas por la forma canónica, sí coinciden. Eso es la
        // corrección: un solo criterio para quien escribe la regla y quien la compara.
        Assert.Equal(claveCanonica, clsTexto.ClaveRuta(origenComoLoGuardabaLaRegla, destino));
    }

    /// <summary>
    /// EVIDENCIA DEL DEFECTO CORREGIDO EN LA ISSUE #17.
    ///
    /// La regla "solo SICE-TAC" se DEFINE con 3 llaves (Origen, Destino, TipoVehiculo -
    /// ver clsReglaSoloSicetac), pero clsPropuestaTarifaAdapter aplicaba la excepción
    /// comparando solo por clsTexto.ClaveRuta (2 llaves). Marcar UN tipo de vehículo
    /// apagaba la comparación contra plaza para TODOS los tipos de vehículo de esa
    /// ruta. clsTexto.ClaveRutaVehiculo agrega la tercera llave al mismo criterio canónico.
    /// </summary>
    [Fact]
    public void Distintos_tipos_de_vehiculo_en_la_misma_ruta_dan_claves_distintas()
    {
        var claveTurbo = clsTexto.ClaveRutaVehiculo("Bogotá", "Cali", "Turbo");
        var clave3S3 = clsTexto.ClaveRutaVehiculo("Bogotá", "Cali", "3S3");

        Assert.NotEqual(claveTurbo, clave3S3);
        // Sin la 3ra llave, ambas colapsarían a la misma clave de ruta - la causa del bug.
        Assert.Equal(clsTexto.ClaveRuta("Bogotá", "Cali"), clsTexto.ClaveRuta("Bogotá", "Cali"));
    }

    [Theory]
    [InlineData("3S3", "3s3")]
    [InlineData("Turbo", "TURBO")]
    [InlineData(" 2L3 ", "2L3")]
    public void El_tipo_de_vehiculo_se_normaliza_igual_que_la_ruta(string escrito, string otraForma)
        => Assert.Equal(
            clsTexto.ClaveRutaVehiculo("Bogotá", "Cali", escrito),
            clsTexto.ClaveRutaVehiculo("Bogotá", "Cali", otraForma));
}
