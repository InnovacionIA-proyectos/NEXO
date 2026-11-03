// Genera los 26 "casos dorados" ejecutando el motor C# REAL de
// cabinaFletes (via ProjectReference, ver GeneradorCasosDorados.csproj) y
// grabando su salida exacta en JSON. Estos JSON son el "esperado" que la
// suite de paridad en Python (tests/paridad/) compara contra su propio
// motor. No modifica nada de cabinaFletes - solo lo referencia para
// compilar y ejecutar sus clases tal cual.
//
// Cada caso replica EXACTAMENTE el setup del test correspondiente en
// cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs (mismo
// nombre, mismo orden 1-26) - no se reinventa nada, se copia el escenario.
//
// Uso: dotnet run [-- <directorio_salida>]  (por defecto ./salida)

using System.Text.Json;
using CabinaFletes.Api.Services.Cubicacion3D;

var dirSalida = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "salida");
Directory.CreateDirectory(dirSalida);

var opcionesJson = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

void Guardar(string numero, string caso, string tipo, params (object Input, object Esperado)[] casos)
{
    var fixture = new
    {
        caso,
        tipo,
        casos = casos.Select(c => new { input = c.Input, esperado = c.Esperado }).ToArray(),
    };
    var ruta = Path.Combine(dirSalida, $"caso_{numero}_{caso}.json");
    File.WriteAllText(ruta, JsonSerializer.Serialize(fixture, fixture.GetType(), opcionesJson));
    Console.WriteLine($"  caso_{numero}_{caso}.json");
}

// ------------------------------------------------------------- helpers (copiados de MotorCubicacion3DTests.cs)

static Contenedor ContenedorDemo(decimal capacidadKg = 28000) => new(
    LargoMm: 13600, AnchoMm: 2450, AltoMm: 2700,
    CapacidadKg: capacidadKg, TaraKg: 7000,
    PuertaAnchoMm: 2400, PuertaAltoMm: 2650, Puerta: PosicionPuerta.Trasera,
    Ejes: [new EjeVehiculo(1500, 8000), new EjeVehiculo(11000, 22000)]);

static ItemCarga Item(
    string id, decimal l, decimal w, decimal h, decimal peso,
    bool apilable = true, bool rotable = true, bool vertical = false,
    int secuencia = 1, decimal maxPesoEncima = -1, int maxNiveles = 0) =>
    new(id, $"SKU-{id}", $"Item {id}", "P1", "Cliente", "Destino", secuencia,
        l, w, h, peso, apilable, maxNiveles, maxPesoEncima, rotable, vertical);

static ResultadoCubicacion Correr(Contenedor c, params ItemCarga[] items) =>
    new MotorCubicacion3D().Optimizar(items, c, new ParametrosOptimizacion());

static Colocacion ColocacionEn(ItemCarga item, decimal x) => new()
{
    Item = item, X = x, Y = 0, Z = 0,
    Orientacion = new Orientacion(0, item.LargoMm, item.AnchoMm, item.AltoMm),
};

// Proyeccion de un ResultadoCubicacion a algo serializable y comparable
// (solo los campos que los 26 tests originales realmente verifican).
static object ProyectarResultado(ResultadoCubicacion r) => new
{
    colocaciones = r.Colocaciones.Select(c => new
    {
        itemId = c.Item.Id,
        x = c.X, y = c.Y, z = c.Z,
        orientacion = new { indice = c.Orientacion.Indice, largoMm = c.Orientacion.LargoMm, anchoMm = c.Orientacion.AnchoMm, altoMm = c.Orientacion.AltoMm },
        soporteId = c.SoporteId,
        porcentajeSoporte = c.PorcentajeSoporte,
        estadoEstabilidad = c.EstadoEstabilidad,
        accesibilidad = c.Accesibilidad,
    }).ToArray(),
    rechazos = r.Rechazos.Select(x => new { itemId = x.Item.Id, codigoRazon = x.CodigoRazon }).ToArray(),
    incidenciasCodigos = r.Incidencias.Select(i => i.CodigoRegla).Distinct().OrderBy(x => x).ToArray(),
};

Console.WriteLine("Generando casos dorados...");

// ------------------------------------------------------------- 1-5: colisiones

Guardar("01", "colision_unidades_separadas_no_colisionan", "colisiona",
    ((object)new { ax = 0m, ay = 0m, az = 0m, al = 100m, aw = 100m, ah = 100m, bx = 200m, by = 0m, bz = 0m, bl = 100m, bw = 100m, bh = 100m, tolerancia = 0m },
     (object)new { resultado = Validadores.Colisiona(0, 0, 0, 100, 100, 100, 200, 0, 0, 100, 100, 100) }));

Guardar("02", "colision_unidades_adyacentes_contacto_exacto_no_colisionan", "colisiona",
    ((object)new { ax = 0m, ay = 0m, az = 0m, al = 100m, aw = 100m, ah = 100m, bx = 100m, by = 0m, bz = 0m, bl = 100m, bw = 100m, bh = 100m, tolerancia = 0m },
     (object)new { resultado = Validadores.Colisiona(0, 0, 0, 100, 100, 100, 100, 0, 0, 100, 100, 100) }));

Guardar("03", "colision_parcialmente_superpuestas_colisionan", "colisiona",
    ((object)new { ax = 0m, ay = 0m, az = 0m, al = 100m, aw = 100m, ah = 100m, bx = 50m, by = 50m, bz = 0m, bl = 100m, bw = 100m, bh = 100m, tolerancia = 0m },
     (object)new { resultado = Validadores.Colisiona(0, 0, 0, 100, 100, 100, 50, 50, 0, 100, 100, 100) }));

Guardar("04", "colision_una_contenida_en_otra_colisionan", "colisiona",
    ((object)new { ax = 0m, ay = 0m, az = 0m, al = 500m, aw = 500m, ah = 500m, bx = 100m, by = 100m, bz = 100m, bl = 50m, bw = 50m, bh = 50m, tolerancia = 0m },
     (object)new { resultado = Validadores.Colisiona(0, 0, 0, 500, 500, 500, 100, 100, 100, 50, 50, 50) }));

Guardar("05", "colision_con_tolerancia_configurable_separacion_insuficiente_colisiona", "colisiona",
    ((object)new { ax = 0m, ay = 0m, az = 0m, al = 100m, aw = 100m, ah = 100m, bx = 110m, by = 0m, bz = 0m, bl = 100m, bw = 100m, bh = 100m, tolerancia = 20m },
     (object)new { resultado = Validadores.Colisiona(0, 0, 0, 100, 100, 100, 110, 0, 0, 100, 100, 100, tolerancia: 20) }),
    ((object)new { ax = 0m, ay = 0m, az = 0m, al = 100m, aw = 100m, ah = 100m, bx = 130m, by = 0m, bz = 0m, bl = 100m, bw = 100m, bh = 100m, tolerancia = 20m },
     (object)new { resultado = Validadores.Colisiona(0, 0, 0, 100, 100, 100, 130, 0, 0, 100, 100, 100, tolerancia: 20) }));

// ------------------------------------------------------------- 6-9: orientaciones

static object[] OrientacionesJson(IReadOnlyList<Orientacion> os) =>
    os.Select(o => (object)new { largoMm = o.LargoMm, anchoMm = o.AnchoMm, altoMm = o.AltoMm }).ToArray();

Guardar("06", "orientaciones_prisma_general_son_seis", "orientaciones",
    ((object)new { largoMm = 100m, anchoMm = 200m, altoMm = 300m, rotable = true, mantenerVertical = false },
     (object)new { orientaciones = OrientacionesJson(Orientaciones.De(Item("a", 100, 200, 300, 10))) }));

Guardar("07", "orientaciones_cubo_sin_duplicadas_es_una", "orientaciones",
    ((object)new { largoMm = 100m, anchoMm = 100m, altoMm = 100m, rotable = true, mantenerVertical = false },
     (object)new { orientaciones = OrientacionesJson(Orientaciones.De(Item("a", 100, 100, 100, 10))) }));

Guardar("08", "orientaciones_mantener_vertical_solo_rotacion_horizontal", "orientaciones",
    ((object)new { largoMm = 100m, anchoMm = 200m, altoMm = 300m, rotable = true, mantenerVertical = true },
     (object)new { orientaciones = OrientacionesJson(Orientaciones.De(Item("a", 100, 200, 300, 10, vertical: true))) }));

Guardar("09", "orientaciones_no_rotable_solo_la_original", "orientaciones",
    ((object)new { largoMm = 100m, anchoMm = 200m, altoMm = 300m, rotable = false, mantenerVertical = false },
     (object)new { orientaciones = OrientacionesJson(Orientaciones.De(Item("a", 100, 200, 300, 10, rotable: false))) }));

// ------------------------------------------------------------- 10-14: encaje y limites

{
    var c = new Contenedor(13600, 2450, 2700, 28000, 7000, 2450, 2700, PosicionPuerta.Trasera, []);
    var r = Correr(c, Item("a", 13600, 2450, 2700, 1000, rotable: false));
    Guardar("10", "unidad_que_cabe_exactamente_se_carga", "optimizar",
        ((object)new { contenedor = "seccion_completa_13600x2450x2700", item = "a_13600x2450x2700_no_rotable" },
         (object)ProyectarResultado(r)));
}

{
    (double l, double w, double h)[] variantes = [(14000, 2000, 2000), (2000, 14000, 2000), (2000, 2000, 3000)];
    var subcasos = variantes.Select(v =>
    {
        var r = Correr(ContenedorDemo(), Item("a", (decimal)v.l, (decimal)v.w, (decimal)v.h, 100, rotable: false));
        return ((object)new { largoMm = (decimal)v.l, anchoMm = (decimal)v.w, altoMm = (decimal)v.h }, (object)ProyectarResultado(r));
    }).ToArray();
    Guardar("11", "unidad_que_excede_una_dimension_sin_rotacion_se_rechaza", "optimizar", subcasos);
}

{
    var r = Correr(ContenedorDemo(), Item("a", 1000, 2600, 1000, 100, rotable: true));
    Guardar("12", "unidad_que_solo_cabe_rotada_se_carga_rotandola", "optimizar",
        ((object)new { item = "a_1000x2600x1000_rotable" }, (object)ProyectarResultado(r)));
}

{
    var r = Correr(ContenedorDemo(), Item("a", 1000, 2600, 1000, 100, rotable: false));
    Guardar("13", "unidad_que_solo_cabe_rotada_pero_no_rotable_se_rechaza", "optimizar",
        ((object)new { item = "a_1000x2600x1000_no_rotable" }, (object)ProyectarResultado(r)));
}

{
    var r = Correr(ContenedorDemo(),
        Item("a", 1200, 1000, 1000, 100, rotable: false),
        Item("b", 1200, 1000, 1000, 100, rotable: false));
    Guardar("14", "dos_unidades_caben_lado_a_lado_sin_colision", "optimizar",
        ((object)new { items = "a_y_b_1200x1000x1000_no_rotables" }, (object)ProyectarResultado(r)));
}

// ------------------------------------------------------------- 15-18: apilamiento y soporte

{
    var c = new Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.Trasera, []);
    var r = Correr(c, Item("base", 1200, 1000, 1000, 500, rotable: false), Item("arriba", 1200, 1000, 1000, 100, rotable: false));
    Guardar("15", "apilamiento_valido_segunda_encima_de_la_primera", "optimizar",
        ((object)new { items = "base_y_arriba_1200x1000x1000" }, (object)ProyectarResultado(r)));
}

{
    var c = new Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.Trasera, []);
    var r = Correr(c, Item("base", 1200, 1000, 1000, 500, apilable: false, rotable: false), Item("arriba", 1200, 1000, 1000, 100, rotable: false));
    Guardar("16", "apilamiento_sobre_no_apilable_se_rechaza", "optimizar",
        ((object)new { items = "base_no_apilable_y_arriba" }, (object)ProyectarResultado(r)));
}

{
    var c = new Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.Trasera, []);
    var r = Correr(c, Item("base", 1200, 1000, 1000, 500, rotable: false, maxPesoEncima: 50), Item("arriba", 1200, 1000, 1000, 100, rotable: false));
    Guardar("17", "apilamiento_excede_peso_soportado_se_rechaza", "optimizar",
        ((object)new { items = "base_max_peso_encima_50_y_arriba" }, (object)ProyectarResultado(r)));
}

{
    var c = new Contenedor(2100, 1300, 5000, 10000, 0, 1300, 4900, PosicionPuerta.Trasera, []);
    var r = Correr(c, Item("peq", 1100, 1200, 1000, 900, rotable: false), Item("grande", 2000, 1000, 600, 100, rotable: false));
    Guardar("18", "soporte_insuficiente_no_deja_unidades_flotando", "optimizar",
        ((object)new { items = "peq_y_grande" }, (object)ProyectarResultado(r)));
}

// ------------------------------------------------------------- 19: peso total

{
    var r = Correr(ContenedorDemo(capacidadKg: 500), Item("a", 1000, 1000, 1000, 400, rotable: false), Item("b", 1000, 1000, 1000, 300, rotable: false));
    Guardar("19", "exceso_de_peso_total_se_rechaza_con_razon", "optimizar",
        ((object)new { capacidadKg = 500m, items = "a_400kg_y_b_300kg" }, (object)ProyectarResultado(r)));
}

// ------------------------------------------------------------- 20-22: balance de ejes

{
    var c = ContenedorDemo();
    var r = Correr(c, Item("a", 2000, 2000, 1000, 10000, rotable: false));
    var porEje = Validadores.EstimarPesoPorEje(r.Colocaciones, c);
    Guardar("20", "balance_de_ejes_carga_centrada_estimacion_repartida", "estimar_peso_por_eje_tras_optimizar",
        ((object)new { item = "a_2000x2000x1000_10000kg" }, (object)new { sumaRedondeada = Math.Round(porEje.Sum()) }));
}

{
    var c = ContenedorDemo();
    var col = ColocacionEn(Item("a", 2000, 2000, 1000, 10000), x: 5250);
    var porEje = Validadores.EstimarPesoPorEje([col], c);
    Guardar("21", "balance_de_ejes_carga_entre_ejes_reparto_inverso_a_la_distancia", "estimar_peso_por_eje",
        ((object)new { itemXMm = 5250m }, (object)new { porEjeRedondeado = porEje.Select(x => Math.Round(x)).ToArray() }));
}

{
    var c = ContenedorDemo();
    var col = ColocacionEn(Item("a", 2000, 2000, 1000, 10000), x: 0);
    var porEje = Validadores.EstimarPesoPorEje([col], c);
    Guardar("22", "balance_de_ejes_voladizo_efecto_palanca", "estimar_peso_por_eje",
        ((object)new { itemXMm = 0m }, (object)new { porEjeRedondeado = porEje.Select(x => Math.Round(x)).ToArray() }));
}

// ------------------------------------------------------------- 23: puerta

{
    var r = Correr(ContenedorDemo(), Item("a", 1000, 2420, 2500, 100, rotable: false));
    Guardar("23", "unidad_que_no_pasa_por_la_puerta_se_rechaza", "optimizar",
        ((object)new { item = "a_1000x2420x2500_no_rotable" }, (object)ProyectarResultado(r)));
}

// ------------------------------------------------------------- 24-25: secuencia de entrega

{
    var cont = ContenedorDemo();
    var temprano = Item("a", 1000, 2450, 2700, 100, rotable: false, secuencia: 1);
    var tardio = Item("b", 1000, 2450, 2700, 100, rotable: false, secuencia: 2);
    var resultado = new ResultadoCubicacion
    {
        Contenedor = cont,
        Rechazos = [],
        Colocaciones =
        [
            new Colocacion { Item = temprano, X = 0, Y = 0, Z = 0, Orientacion = Orientaciones.De(temprano)[0] },
            new Colocacion { Item = tardio, X = 1000, Y = 0, Z = 0, Orientacion = Orientaciones.De(tardio)[0] },
        ],
    };
    var incidencias = new MotorCubicacion3D().Validar(resultado, new ParametrosOptimizacion());
    Guardar("24", "secuencia_entrega_temprana_al_fondo_genera_violacion", "validar",
        ((object)new { items = "a_secuencia1_en_x0_b_secuencia2_en_x1000" },
         (object)new
         {
             tieneSecuenciaBloqueada = incidencias.Any(i => i.CodigoRegla == "SECUENCIA_BLOQUEADA"),
             accesibilidadPrimeraColocacion = resultado.Colocaciones[0].Accesibilidad,
         }));
}

{
    var cont = ContenedorDemo();
    var items = new[]
    {
        Item("primera", 2000, 2400, 2000, 500, rotable: false, secuencia: 1),
        Item("ultima", 2000, 2400, 2000, 500, rotable: false, secuencia: 3),
    };
    var r = new MotorCubicacion3D().Optimizar(items, cont, new ParametrosOptimizacion(EstrategiaOptimizacion.PriorizarSecuenciaEntrega));
    var primera = r.Colocaciones.Single(c => c.Item.Id == "primera");
    var ultima = r.Colocaciones.Single(c => c.Item.Id == "ultima");
    Guardar("25", "secuencia_con_estrategia_de_secuencia_entrega_temprana_queda_cerca_de_la_puerta", "optimizar_estrategia",
        ((object)new { estrategia = "PriorizarSecuenciaEntrega", items = "primera_secuencia1_ultima_secuencia3" },
         (object)new
         {
             distanciaPrimeraMenorQueUltima = Validadores.DistanciaAPuerta(primera, cont) < Validadores.DistanciaAPuerta(ultima, cont),
             tieneSecuenciaBloqueada = r.Incidencias.Any(i => i.CodigoRegla == "SECUENCIA_BLOQUEADA"),
         }));
}

// ------------------------------------------------------------- 26: metricas

{
    var cont = new Contenedor(2000, 1000, 1000, 10000, 0, 1000, 1000, PosicionPuerta.Trasera, []);
    var items = new[] { Item("a", 1000, 1000, 1000, 100, rotable: false) };
    var motor = new MotorCubicacion3D();
    var r = motor.Optimizar(items, cont, new ParametrosOptimizacion());
    var m = motor.Metricas(r, items);
    Guardar("26", "metricas_utilizacion_volumetrica_es_volumen_cargado_sobre_util", "metricas",
        ((object)new { contenedor = "2000x1000x1000", item = "a_1000x1000x1000_no_rotable" },
         (object)new { porcentajeVolumen = m.PorcentajeVolumen, unidadesCargadas = m.UnidadesCargadas, pesoCargadoKg = m.PesoCargadoKg }));
}

Console.WriteLine($"Listo: 26 archivos en {Path.GetFullPath(dirSalida)}");
