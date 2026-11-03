using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

// S4-HU-15.5/#251; #300 (horas por tramo: solo la devolución arranca con 1h+1h)
public class clsResolucionHorasLineaTests
{
    [Fact]
    public void Sin_override_y_sin_devolucion_usa_las_horas_de_la_solicitud()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: null, horasDescargueLinea: null, esDevolucionContenedor: false,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(4m, cargue);
        Assert.Equal(4m, descargue);
    }

    // #300: devolución de contenedor sin horas = 1 + 1 (antes eran 2 h totales SOLO si marcaban "retorna contenedor").
    [Fact]
    public void Devolucion_sin_override_queda_en_1_mas_1_no_en_las_de_la_solicitud()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: null, horasDescargueLinea: null, esDevolucionContenedor: true,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(1m, cargue);
        Assert.Equal(1m, descargue);
    }

    // #300: lo que usan los tramos vacíos (levante y retorno) es la misma constante, y suma 2 h.
    [Fact]
    public void Las_horas_de_un_tramo_vacio_son_1_mas_1_y_suman_2()
    {
        Assert.Equal((1m, 1m), clsResolucionHorasLinea.HorasTramoVacio);
        Assert.Equal(2m, clsResolucionHorasLinea.HorasTotalesTramoVacio);
    }

    [Fact]
    public void El_comercial_puede_sobreescribir_el_default_aunque_sea_devolucion()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: 3m, horasDescargueLinea: 5m, esDevolucionContenedor: true,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(3m, cargue);
        Assert.Equal(5m, descargue);
    }

    [Fact] // falta una de las dos: se trata como "no especificado", no una mezcla rara.
    public void Override_parcial_se_ignora_por_completo_y_cae_al_default()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: 3m, horasDescargueLinea: null, esDevolucionContenedor: false,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(4m, cargue);
        Assert.Equal(4m, descargue);
    }

    // #300: el viaje cargado y el tramo vacío se calculan por separado — el vacío NO cambia las horas del cargado.
    [Fact]
    public void El_tramo_vacio_no_cambia_las_horas_del_viaje_cargado()
    {
        var (cargue, descargue) = clsResolucionHorasLinea.Resolver(
            horasCargueLinea: null, horasDescargueLinea: null, esDevolucionContenedor: false,
            horasCargueSolicitud: 4m, horasDescargueSolicitud: 4m);

        Assert.Equal(8m, cargue + descargue);                                     // cargado: 4+4
        Assert.Equal(2m, clsResolucionHorasLinea.HorasTotalesTramoVacio);         // vacío: 1+1, aparte
    }
}
