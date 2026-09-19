using System.Text.Json;
using Nexo.Mensajes.Cubicacion;
using Xunit;

namespace Nexo.Api.Tests.Cubicacion;

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
    public void PlanDeCargueGenerado_deserializa_el_JSON_enriquecido_que_publica_RabbitMqEventosPublisher()
    {
        // S3-HU-03 (issue #110/#111): copiado literal del shape que arma
        // ResultadoPlanDto.model_dump(mode="json", by_alias=True) del lado de Cubicador —
        // ya no {estado, puntaje, colocaciones: int}, ahora el resultado completo.
        const string json = """
            {"correlationId": "abc-123", "planId": "abc-123", "resultado": {
              "plan": {
                "id": "abc-123", "nombre": "Plan de prueba", "vehiculoId": "veh-1",
                "lineaSolicitudId": null, "numeroVehiculo": 1, "estado": "optimizado",
                "estrategia": "Combinada", "algoritmo": "bfd", "semilla": null,
                "pesoTotalKg": 500.0, "volumenUtilizadoM3": 1.2, "utilizacionVolumen": 45.0,
                "utilizacionPiso": 60.0, "puntaje": 75.1,
                "items": [{
                  "id": "item-1", "planCargueId": "abc-123", "sku": "SKU-1", "descripcion": "Pallet",
                  "pedido": null, "cliente": null, "destino": null, "secuenciaEntrega": 1,
                  "largoMm": 1200.0, "anchoMm": 1000.0, "altoMm": 1000.0, "pesoKg": 500.0,
                  "cantidad": 1, "color": null, "prioridad": 3, "tipoEmbalaje": null,
                  "apilable": true, "maxNiveles": 0, "maxPesoEncimaKg": -1.0, "rotable": true,
                  "mantenerVertical": false, "fragil": false, "peligroso": false, "refrigerado": false,
                  "grupoCompatibilidad": null, "observaciones": null
                }]
              },
              "vehiculo": {
                "id": "veh-1", "nombre": "Furgón 13.6m", "descripcion": null,
                "largoInternoMm": 13600.0, "anchoInternoMm": 2450.0, "altoInternoMm": 2700.0,
                "capacidadKg": 28000.0, "taraKg": 7000.0, "puertaAnchoMm": 2400.0, "puertaAltoMm": 2650.0,
                "puertaPosicion": "trasera", "ejes": [{"posicionMm": 1500.0, "capacidadKg": 8000.0}],
                "toleranciaParedesMm": 0.0, "toleranciaEntreUnidadesMm": 0.0, "esPreset": true
              },
              "colocaciones": [{
                "id": "col-1", "planCargueId": "abc-123", "itemPlanCargueId": "item-1", "indiceUnidad": 0,
                "cargada": true, "x": 0.0, "y": 0.0, "z": 0.0, "largoColocadoMm": 1200.0,
                "anchoColocadoMm": 1000.0, "altoColocadoMm": 1000.0, "orientacionIndice": 0,
                "secuenciaCargue": 1, "soporteColocacionId": null, "porcentajeSoporte": 100.0,
                "pesoEncimaKg": 0.0, "estadoEstabilidad": "estable", "accesibilidad": "accesible",
                "bloqueada": false, "ajusteManual": false, "codigoRechazo": null, "razonRechazo": null
              }],
              "metricas": {
                "volumenInternoM3": 90.0, "volumenUtilizadoM3": 1.2, "porcentajeVolumen": 1.3,
                "areaPisoM2": 33.0, "areaPisoUtilizadaM2": 1.2, "porcentajePiso": 3.6,
                "pesoMaximoKg": 28000.0, "pesoCargadoKg": 500.0, "porcentajePeso": 1.8,
                "unidadesTotales": 1, "unidadesCargadas": 1, "unidadesNoCargadas": 0,
                "pedidosCompletos": 1, "pedidosParciales": 0, "destinos": 1, "alturaMaximaMm": 1000.0,
                "centroGravedadMm": [600.0, 500.0, 500.0], "pesoPorEjeKg": [500.0],
                "diferenciaLadosKg": 0.0, "advertencias": 0, "violacionesCriticas": 0, "puntaje": 75.1,
                "puntajesParciales": {"volumen": 0.9}, "espacioVacioUtilM3": 0.0, "espacioFragmentadoM3": 0.0
              },
              "incidencias": []
            }}
            """;

        var evento = JsonSerializer.Deserialize<PlanDeCargueGenerado>(json, Opciones);

        Assert.NotNull(evento);
        Assert.Equal("abc-123", evento.PlanId);
        Assert.Equal("abc-123", evento.CorrelationId);
        Assert.Equal("optimizado", evento.Resultado.Plan.Estado);
        Assert.Equal(75.1m, evento.Resultado.Plan.Puntaje);
        Assert.Single(evento.Resultado.Plan.Items);
        Assert.Equal("Pallet", evento.Resultado.Plan.Items[0].Descripcion);
        Assert.Single(evento.Resultado.Colocaciones);
        Assert.True(evento.Resultado.Colocaciones[0].Cargada);
        Assert.Equal("Furgón 13.6m", evento.Resultado.Vehiculo.Nombre);
        Assert.NotNull(evento.Resultado.Metricas);
        Assert.Equal(1, evento.Resultado.Metricas!.UnidadesCargadas);
        Assert.Empty(evento.Resultado.Incidencias);
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

    private const string VehiculoJson = """
        {"id": "veh-1", "nombre": "Furgón 13.6m", "descripcion": null,
         "largoInternoMm": 13600.0, "anchoInternoMm": 2450.0, "altoInternoMm": 2700.0,
         "capacidadKg": 28000.0, "taraKg": 7000.0, "puertaAnchoMm": 2400.0, "puertaAltoMm": 2650.0,
         "puertaPosicion": "trasera", "ejes": [{"posicionMm": 1500.0, "capacidadKg": 8000.0}],
         "toleranciaParedesMm": 0.0, "toleranciaEntreUnidadesMm": 0.0, "esPreset": true}
        """;

    [Fact]
    public void VehiculoCreado_deserializa_el_JSON_real_que_publica_RabbitMqEventosPublisher()
    {
        // Copiado literal de RabbitMqEventosPublisher.publicar_vehiculo_creado (issue #110).
        var json = $$"""{"correlationId": "veh-1", "vehiculo": {{VehiculoJson}}}""";

        var evento = JsonSerializer.Deserialize<VehiculoCreado>(json, Opciones);

        Assert.NotNull(evento);
        Assert.Equal("veh-1", evento.CorrelationId);
        Assert.Equal("Furgón 13.6m", evento.Vehiculo.Nombre);
        Assert.True(evento.Vehiculo.EsPreset);
        Assert.Single(evento.Vehiculo.Ejes);
    }

    [Fact]
    public void VehiculoActualizado_deserializa_el_JSON_real_que_publica_RabbitMqEventosPublisher()
    {
        // Copiado literal de RabbitMqEventosPublisher.publicar_vehiculo_actualizado (issue #110).
        var json = $$"""{"correlationId": "veh-1", "vehiculo": {{VehiculoJson}}}""";

        var evento = JsonSerializer.Deserialize<VehiculoActualizado>(json, Opciones);

        Assert.NotNull(evento);
        Assert.Equal("veh-1", evento.CorrelationId);
        Assert.Equal("Furgón 13.6m", evento.Vehiculo.Nombre);
    }

    [Fact]
    public void VehiculoEliminado_deserializa_el_JSON_real_que_publica_RabbitMqEventosPublisher()
    {
        // Copiado literal de RabbitMqEventosPublisher.publicar_vehiculo_eliminado.
        // A diferencia de creado/actualizado, este NO lleva el vehículo completo: no hay
        // nada que proyectar, solo una fila que borrar.
        const string json = """{"correlationId": "veh-1", "vehiculoId": "veh-1"}""";

        var evento = JsonSerializer.Deserialize<VehiculoEliminado>(json, Opciones);

        Assert.NotNull(evento);
        Assert.Equal("veh-1", evento.CorrelationId);
        Assert.Equal("veh-1", evento.VehiculoId);
    }
}
