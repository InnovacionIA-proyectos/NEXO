using System.Text.Json;
using Nexo.Application.Common.Dtos;

namespace Nexo.Application.Tests.Common;

public class clsCondicionesServicioDtoTests
{
    // S4-HU-18.10/#227: nuevas notas/observaciones comerciales, sin migración de esquema
    // (mismo mecanismo JSON ya usado por Referencia/TiemposLogisticos).
    [Fact]
    public void Notas_sobrevive_el_viaje_ida_y_vuelta_por_JSON()
    {
        var original = new clsCondicionesServicioDto("REF-123", clsTiemposLogisticosDto.PorDefecto, Notas: "Entregar solo en horario diurno");

        var json = JsonSerializer.Serialize(original, clsCondicionesServicioDto.Json);
        var recuperado = clsCondicionesServicioDto.Desde(json);

        Assert.Equal("Entregar solo en horario diurno", recuperado.Notas);
        Assert.Equal("REF-123", recuperado.Referencia); // no se pisan entre sí
    }

    [Fact]
    public void Sin_notas_Desde_devuelve_null_no_cadena_vacia()
    {
        var condiciones = clsCondicionesServicioDto.Desde(null);

        Assert.Null(condiciones.Notas);
    }

    [Fact]
    public void JSON_legado_sin_el_campo_notas_no_revienta_y_da_null()
    {
        // Condiciones guardadas ANTES de que existiera Notas (S4-HU-18.10/#227).
        var json = "{\"referencia\":\"REF-999\",\"tiemposLogisticos\":{\"horasCargue\":4,\"horasDescargue\":4}}";

        var condiciones = clsCondicionesServicioDto.Desde(json);

        Assert.Null(condiciones.Notas);
        Assert.Equal("REF-999", condiciones.Referencia);
    }
}
