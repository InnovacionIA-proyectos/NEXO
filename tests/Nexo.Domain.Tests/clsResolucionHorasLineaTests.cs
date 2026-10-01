using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

// S4-HU-15.5/#251
public class clsResolucionHorasLineaTests
{
    [Fact]
    public void Sin_override_y_sin_retorno_contenedor_usa_las_horas_de_la_solicitud()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: null, horasDescargueLinea: null, retornaContenedor: false,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(4m, cargue);
        Assert.Equal(4m, descargue);
    }

    [Fact]
    public void Retorna_contenedor_sin_override_usa_2_horas_totales_no_las_de_la_solicitud()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: null, horasDescargueLinea: null, retornaContenedor: true,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(2m, cargue + descargue);
    }

    [Fact]
    public void El_comercial_puede_sobreescribir_el_default_aunque_retorne_contenedor()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: 3m, horasDescargueLinea: 5m, retornaContenedor: true,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(3m, cargue);
        Assert.Equal(5m, descargue);
    }

    [Fact] // falta una de las dos: se trata como "no especificado", no una mezcla rara.
    public void Override_parcial_se_ignora_por_completo_y_cae_al_default()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: 3m, horasDescargueLinea: null, retornaContenedor: false,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(4m, cargue);
        Assert.Equal(4m, descargue);
    }
}
