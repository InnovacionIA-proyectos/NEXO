using System.Text.Json;
using CabinaFletes.Mensajes.Cubicacion;
using Xunit;

namespace CabinaFletes.Api.Tests.Cubicacion;

/// <summary>
/// Guarda del CONTRATO de mensajes con Cubicador (issue #28, S2-HU-075) - sin
/// broker, sin base de datos: solo que el JSON que produce/espera el lado C#
/// coincide exactamente con lo que worker/main.py y RabbitMqEventosPublisher
/// leen/escriben del otro lado (ver cubicador/CONTRATO.md sección 6).
///
/// Si alguno de los dos lados cambia un nombre de campo sin avisar al otro, este
/// test lo detecta aquí - antes de que solo se note contra un broker real. La
/// verificación de punta a punta con infraestructura real vive en
/// tests/QA/28-s2-hu-075-.../README.md (requiere Cubicador + RabbitMQ reales,
/// no vale la pena acoplar el runner de CI de NEXO a levantar Cubicador).
/// </summary>
public class ContratoCubicacionTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void CubicacionSolicitada_serializa_con_los_nombres_de_campo_que_lee_worker_main_py()
    {
        var mensaje = new CubicacionSolicitada(
            "optimizar", "11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111",
            new SolicitudDeCubicacion("Combinada", "bfd", 42, null, false));

        var json = JsonSerializer.Serialize(mensaje, Opciones);
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;

        // Mismos nombres que worker/main.py::_ejecutar_solicitud lee de `cuerpo`.
        Assert.Equal("optimizar", raiz.GetProperty("tipo").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", raiz.GetProperty("planId").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", raiz.GetProperty("correlationId").GetString());

        var solicitud = raiz.GetProperty("solicitud");
        Assert.Equal("Combinada", solicitud.GetProperty("estrategia").GetString());
        Assert.Equal("bfd", solicitud.GetProperty("algoritmo").GetString());
        Assert.Equal(42, solicitud.GetProperty("semilla").GetInt32());
        Assert.Equal(JsonValueKind.Null, solicitud.GetProperty("ponderaciones").ValueKind);
        Assert.False(solicitud.GetProperty("respetarBloqueadas").GetBoolean());
    }

    [Fact]
    public void PlanDeCargueGenerado_deserializa_el_JSON_real_que_publica_RabbitMqEventosPublisher()
    {
        // Copiado literal de RabbitMqEventosPublisher.publicar_plan_generado.
        const string json = """
            {"correlationId": "abc-123", "planId": "abc-123", "resultado": {"estado": "optimizado", "puntaje": 75.1, "colocaciones": 2}}
            """;

        var evento = JsonSerializer.Deserialize<PlanDeCargueGenerado>(json, Opciones);

        Assert.NotNull(evento);
        Assert.Equal("abc-123", evento.PlanId);
        Assert.Equal("abc-123", evento.CorrelationId);
        Assert.Equal("optimizado", evento.Resultado.Estado);
        Assert.Equal(75.1m, evento.Resultado.Puntaje);
        Assert.Equal(2, evento.Resultado.Colocaciones);
    }

    [Fact]
    public void CubicacionRechazada_deserializa_el_JSON_real_que_publica_RabbitMqEventosPublisher()
    {
        // Copiado literal de RabbitMqEventosPublisher.publicar_cubicacion_rechazada.
        const string json = """
            {"correlationId": "abc-123", "planId": "abc-123", "codigo": "PLAN_NO_ENCONTRADO", "mensaje": "Plan de cargue no encontrado."}
            """;

        var evento = JsonSerializer.Deserialize<CubicacionRechazada>(json, Opciones);

        Assert.NotNull(evento);
        Assert.Equal("abc-123", evento.PlanId);
        Assert.Equal("PLAN_NO_ENCONTRADO", evento.Codigo);
        Assert.Equal("Plan de cargue no encontrado.", evento.Mensaje);
    }
}
