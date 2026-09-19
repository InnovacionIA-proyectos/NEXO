using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// S3-HU-02 (issue #103): cobertura de los 8 tipos nuevos de <see cref="CubicacionSolicitada"/>
/// — payload válido no reporta motivos, payload inválido reporta el motivo esperado.
/// "optimizar" ya estaba cubierto indirectamente por <c>SolicitarOptimizacionCommandTests</c>;
/// acá se agrega cobertura directa del validador para los 8 comandos nuevos.
///
/// [S3-HU-04.3, issue #78] Se suma "crear_desde_linea" (el décimo tipo).
/// </summary>
public class CubicacionSolicitadaValidadorTests
{
    private static readonly Guid Id = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");
    private static readonly CubicacionSolicitadaValidador Validador = new();

    private static CubicacionSolicitada Mensaje(string tipo, object solicitud, string? planId = null) =>
        new(tipo, planId ?? Id.ToString(), planId ?? Id.ToString(), solicitud);

    // ------------------------------------------------------------- generales

    [Fact]
    public void Rechaza_un_planid_que_no_es_guid()
    {
        var motivos = Validador.Validar(Mensaje("eliminar_plan", new EliminarPlanInput(), planId: "no-es-guid"));

        Assert.Contains(motivos, m => m.Contains("PlanId"));
    }

    [Fact]
    public void Rechaza_un_tipo_desconocido()
    {
        var motivos = Validador.Validar(Mensaje("tipo_inventado", new EliminarPlanInput()));

        Assert.Contains(motivos, m => m.Contains("desconocido"));
    }

    [Fact]
    public void Rechaza_cuando_el_payload_no_corresponde_al_tipo()
    {
        var motivos = Validador.Validar(Mensaje("crear_plan", new EliminarPlanInput()));

        Assert.Contains(motivos, m => m.Contains("CrearPlanInput"));
    }

    // ------------------------------------------------------------- crear_plan

    [Fact]
    public void Crear_plan_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("crear_plan", new CrearPlanInput("Plan de prueba", Guid.NewGuid().ToString())));

        Assert.Empty(motivos);
    }

    [Theory]
    [InlineData("", "11111111-1111-1111-1111-111111111111")]
    [InlineData("Plan de prueba", "no-es-guid")]
    public void Crear_plan_invalido_es_rechazado(string nombre, string vehiculoId)
    {
        var motivos = Validador.Validar(Mensaje("crear_plan", new CrearPlanInput(nombre, vehiculoId)));

        Assert.NotEmpty(motivos);
    }

    // ------------------------------------------------------------- crear_plan_demo

    [Fact]
    public void Crear_plan_demo_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("crear_plan_demo", new CrearPlanDemoInput()));

        Assert.Empty(motivos);
    }

    // ------------------------------------------------------------- crear_desde_linea [issue #78]

    [Fact]
    public void Crear_desde_linea_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("crear_desde_linea", new CrearPlanDesdeLineaInput(Guid.NewGuid().ToString())));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Crear_desde_linea_con_lineaid_no_guid_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("crear_desde_linea", new CrearPlanDesdeLineaInput("no-es-guid")));

        Assert.Contains(motivos, m => m.Contains("LineaSolicitudId"));
    }

    [Fact]
    public void Crear_desde_linea_con_solicitud_de_otro_tipo_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("crear_desde_linea", new CrearPlanDemoInput()));

        Assert.Contains(motivos, m => m.Contains("CrearPlanDesdeLineaInput"));
    }

    // ------------------------------------------------------------- guardar_vehiculo

    private static GuardarVehiculoInput VehiculoValido(string? id = null) => new(
        Id: id, Nombre: "Turbo con estacas", Descripcion: null,
        LargoInternoMm: 6200, AnchoInternoMm: 2200, AltoInternoMm: 2000,
        CapacidadKg: 6300, TaraKg: 4200,
        PuertaAnchoMm: 2200, PuertaAltoMm: 1950, PuertaPosicion: "trasera",
        Ejes: new[] { new EjeVehiculoInput(1000, 3500), new EjeVehiculoInput(4600, 7000) });

    [Fact]
    public void Guardar_vehiculo_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("guardar_vehiculo", VehiculoValido()));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Guardar_vehiculo_con_dimensiones_invalidas_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("guardar_vehiculo", VehiculoValido() with { LargoInternoMm = 0 }));

        Assert.Contains(motivos, m => m.Contains("dimensiones"));
    }

    [Fact]
    public void Guardar_vehiculo_con_puerta_posicion_invalida_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("guardar_vehiculo", VehiculoValido() with { PuertaPosicion = "arriba" }));

        Assert.Contains(motivos, m => m.Contains("PuertaPosicion"));
    }

    [Fact]
    public void Guardar_vehiculo_con_id_no_guid_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("guardar_vehiculo", VehiculoValido(id: "no-es-guid")));

        Assert.Contains(motivos, m => m.Contains("Id"));
    }

    // ------------------------------------------------------------- guardar_item

    private static GuardarItemInput ItemValido(string? id = null) => new(
        Id: id, Sku: "SKU-1", Descripcion: "Pallet industrial", Pedido: null, Cliente: null, Destino: null,
        SecuenciaEntrega: 1, LargoMm: 1200, AnchoMm: 1000, AltoMm: 1400, PesoKg: 800, Cantidad: 10,
        Color: null, Prioridad: 3, TipoEmbalaje: null, Apilable: true, MaxNiveles: 0, MaxPesoEncimaKg: -1,
        Rotable: true, MantenerVertical: false, Fragil: false, Peligroso: false, Refrigerado: false,
        GrupoCompatibilidad: null, Observaciones: null);

    [Fact]
    public void Guardar_item_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("guardar_item", ItemValido()));

        Assert.Empty(motivos);
    }

    [Theory]
    [InlineData(0, 1)]      // peso inválido
    [InlineData(800, 0)]    // cantidad inválida
    [InlineData(800, 2001)] // cantidad fuera de rango
    public void Guardar_item_invalido_es_rechazado(decimal pesoKg, int cantidad)
    {
        var motivos = Validador.Validar(
            Mensaje("guardar_item", ItemValido() with { PesoKg = pesoKg, Cantidad = cantidad }));

        Assert.NotEmpty(motivos);
    }

    // ------------------------------------------------------------- importar_items

    [Fact]
    public void Importar_items_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("importar_items", new ImportarItemsInput(new[] { ItemValido(), ItemValido() })));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Importar_items_vacio_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("importar_items", new ImportarItemsInput(Array.Empty<GuardarItemInput>())));

        Assert.Contains(motivos, m => m.Contains("entre 1 y 2.000"));
    }

    [Fact]
    public void Importar_items_con_un_item_invalido_es_rechazado()
    {
        var motivos = Validador.Validar(
            Mensaje("importar_items", new ImportarItemsInput(new[] { ItemValido(), ItemValido() with { Descripcion = "" } })));

        Assert.Contains(motivos, m => m.Contains("Items[1]"));
    }

    // ------------------------------------------------------------- eliminar_item

    [Fact]
    public void Eliminar_item_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("eliminar_item", new EliminarItemInput(Guid.NewGuid().ToString())));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Eliminar_item_con_itemid_invalido_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("eliminar_item", new EliminarItemInput("no-es-guid")));

        Assert.Contains(motivos, m => m.Contains("ItemId"));
    }

    // ------------------------------------------------------------- eliminar_plan

    [Fact]
    public void Eliminar_plan_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("eliminar_plan", new EliminarPlanInput()));

        Assert.Empty(motivos);
    }

    // ------------------------------------------------------------- eliminar_vehiculo

    [Fact]
    public void Eliminar_vehiculo_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje("eliminar_vehiculo", new EliminarVehiculoInput()));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Eliminar_vehiculo_con_solicitud_de_otro_tipo_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje("eliminar_vehiculo", new EliminarPlanInput()));

        Assert.Contains(motivos, m => m.Contains("EliminarVehiculoInput"));
    }

    // ------------------------------------------------------------- ajustar_colocacion

    [Fact]
    public void Ajustar_colocacion_valido_no_reporta_motivos()
    {
        var motivos = Validador.Validar(Mensaje(
            "ajustar_colocacion",
            new AjustarColocacionInput(Guid.NewGuid().ToString(), 100, 200, 300, 1, null, null, null)));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Ajustar_colocacion_sin_ningun_ajuste_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje(
            "ajustar_colocacion",
            new AjustarColocacionInput(Guid.NewGuid().ToString(), null, null, null, null, null, null, null)));

        Assert.Contains(motivos, m => m.Contains("ningún ajuste"));
    }

    [Fact]
    public void Ajustar_colocacion_retirar_solo_es_valido()
    {
        var motivos = Validador.Validar(Mensaje(
            "ajustar_colocacion",
            new AjustarColocacionInput(Guid.NewGuid().ToString(), null, null, null, null, null, true, null)));

        Assert.Empty(motivos);
    }

    [Fact]
    public void Ajustar_colocacion_con_colocacionid_invalido_es_rechazado()
    {
        var motivos = Validador.Validar(Mensaje(
            "ajustar_colocacion",
            new AjustarColocacionInput("no-es-guid", 1, null, null, null, null, null, null)));

        Assert.Contains(motivos, m => m.Contains("ColocacionId"));
    }
}
