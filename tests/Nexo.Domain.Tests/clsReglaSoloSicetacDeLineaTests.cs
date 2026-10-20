using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Tests;

// S6-HU-26.1/#30: una sola definición de "esta línea tiene regla Solo-SICE" para el motor, la edición del flete y la Mesa.
public class clsReglaSoloSicetacDeLineaTests
{
    private const string Origen = "11001000";
    private const string Destino = "05001000";

    private static IReadOnlySet<string> Reglas(params (string O, string D, string V)[] reglas) =>
        reglas.Select(r => clsTexto.ClaveRutaVehiculo(r.O, r.D, r.V)).ToHashSet();

    [Fact]
    public void Ruta_y_vehiculo_con_regla_la_tienen()
    {
        var claves = Reglas((Origen, Destino, "3S3"));

        Assert.True(clsReglaSoloSicetacDeLinea.Tiene(claves, ClaseViaje.Cupo, Origen, Destino, "3S3"));
    }

    [Fact]
    public void Otro_vehiculo_de_la_misma_ruta_no_la_tiene()
    {
        var claves = Reglas((Origen, Destino, "3S3"));

        Assert.False(clsReglaSoloSicetacDeLinea.Tiene(claves, ClaseViaje.Cupo, Origen, Destino, "C2"));
    }

    [Fact]
    public void Otra_ruta_no_la_tiene()
    {
        var claves = Reglas((Origen, Destino, "3S3"));

        Assert.False(clsReglaSoloSicetacDeLinea.Tiene(claves, ClaseViaje.Cupo, Destino, Origen, "3S3"));
    }

    [Theory] // las reglas se definen por DANE: sin DANE en alguno de los dos lados no hay clave que comparar
    [InlineData(null, Destino)]
    [InlineData(Origen, null)]
    [InlineData(null, null)]
    public void Sin_DANE_en_algun_lado_no_hay_regla(string? origen, string? destino)
    {
        var claves = Reglas((Origen, Destino, "3S3"));

        Assert.False(clsReglaSoloSicetacDeLinea.Tiene(claves, ClaseViaje.Cupo, origen, destino, "3S3"));
    }

    // Una línea de Cupo sin vehículo nunca se autocostea y salta la regla (S5-HU-20.5/#297): no se la bloquea.
    [Fact]
    public void Una_linea_de_Cupo_sin_vehiculo_no_cuenta_aunque_la_ruta_tenga_regla_para_algun_vehiculo()
    {
        var claves = Reglas((Origen, Destino, ""));

        Assert.False(clsReglaSoloSicetacDeLinea.Tiene(claves, ClaseViaje.Cupo, Origen, Destino, null));
    }

    [Fact]
    public void Sin_ninguna_regla_cargada_no_hay_regla()
    {
        Assert.False(clsReglaSoloSicetacDeLinea.Tiene(Reglas(), ClaseViaje.Cupo, Origen, Destino, "3S3"));
    }
}
