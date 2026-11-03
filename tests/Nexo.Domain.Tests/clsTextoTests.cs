using Nexo.Domain.Common;

namespace Nexo.Domain.Tests;

public class clsTextoTests
{
    [Fact]
    public void ClaveRuta_ignora_tildes_y_mayusculas()
    {
        Assert.Equal(
            clsTexto.ClaveRuta("Bogotá", "Medellín"),
            clsTexto.ClaveRuta("BOGOTA", "medellin"));
    }

    [Theory]
    [InlineData("MEDELLÍN (ANT) - BOGOTÁ D.C. (CUN)", "MEDELLÍN (ANT)", "BOGOTÁ D.C. (CUN)")]
    [InlineData("Cúcuta → Cali", "Cúcuta", "Cali")]
    [InlineData("Barranquilla -> Medellín", "Barranquilla", "Medellín")]
    public void PartirRuta_separa_origen_y_destino(string nombre, string origen, string destino)
    {
        var partes = clsTexto.PartirRuta(nombre);
        Assert.NotNull(partes);
        Assert.Equal(origen, partes!.Value.Origen);
        Assert.Equal(destino, partes.Value.Destino);
    }

    [Theory]
    [InlineData("SIN SEPARADOR CLARO")]
    [InlineData("")]
    [InlineData(null)]
    public void PartirRuta_devuelve_null_sin_separador(string? nombre)
    {
        Assert.Null(clsTexto.PartirRuta(nombre));
    }
}
