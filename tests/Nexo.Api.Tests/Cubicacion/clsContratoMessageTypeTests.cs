using System.Text.RegularExpressions;
using Nexo.Infrastructure.Cubicacion;
using MassTransit;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// S3-HU-06.2 (issue #121): guarda del `messageType` compartido entre C# y Python.
///
/// <c>clsContratoCubicacionTests</c> (al lado) cubre el PAYLOAD — que `planId` se llame
/// `planId`. Esto cubre el SOBRE: el `messageType` del envelope de MassTransit
/// (<c>urn:message:Nexo.Mensajes.Cubicacion:clsPlanDeCargueGenerado</c>), que hasta
/// esta HU no verificaba nadie. El agujero era real: renombrar una clase o mover el
/// namespace dejaba pasar los 6 tests de payload —solo deserializan el contenido— y el
/// mensaje simplemente dejaba de entregarse, sin error de compilación en ningún lado.
/// Cubicador lo tiene escrito como una constante de texto
/// (<c>_NAMESPACE_MENSAJES_NEXO</c> en <c>rabbitmq.py</c>), así que el acoplamiento no
/// tiene ninguna red debajo salvo esta.
///
/// <b>Ningún lado lleva una lista escrita a mano</b>, a propósito: una lista manual
/// tiene el mismo problema que quiere resolver (nadie la actualiza). Los dos extremos se
/// derivan de su fuente real:
/// <list type="bullet">
/// <item>C#: por reflexión sobre las implementaciones de <c>IConsumer&lt;T&gt;</c> —los
/// tipos que NEXO consume SON los que tiene que poder deserializar—, y el URN se pide a
/// <see cref="MessageUrn"/>, que es quien lo genera de verdad en runtime (no una
/// reconstrucción del formato a mano, que podría diferir de MassTransit y pasar igual).</item>
/// <item>Python: leyendo el fuente real de <c>rabbitmq.py</c>.</item>
/// </list>
///
/// Por eso rompen solos cuatro cambios que hoy pasan desapercibidos: renombrar una clase,
/// mover el namespace, agregar un consumer que Cubicador no publica, o publicar desde
/// Cubicador algo que NEXO no consume.
///
/// <b>Asimetría deliberada en el sobre</b>: del `messageType` solo se verifica la
/// dirección Cubicador → NEXO. En la otra (<c>clsCubicacionSolicitada</c>), el worker solo
/// comprueba que EXISTA la clave `messageType` para desenvolver el sobre, sin mirar su
/// valor (<c>worker/main.py::_procesar</c>) — no hay acoplamiento por nombre que
/// proteger ahí.
///
/// <b>Pero esa dirección SÍ tiene su propio acoplamiento por texto</b>, y también se
/// cubre acá: el discriminador `tipo` del comando. NEXO decide qué puede publicar con
/// <c>TiposConocidos</c> y el worker despacha con <c>if tipo == "..."</c>. Es el mismo
/// problema con peor desenlace — un `tipo` que NEXO publica y el worker no conoce lanza
/// <c>ValueError</c>, gasta los 3 reintentos y muere en la DLQ sin que nada avise.
/// </summary>
public class clsContratoMessageTypeTests
{
    private const string RutaPublisherPython = "cubicador/infrastructure/mensajeria/rabbitmq.py";

    /// <summary>Los tipos que NEXO consume del dominio de cubicación, por reflexión.
    /// Excluye <c>Fault&lt;T&gt;</c> y los consumers genéricos (<c>clsAlertaDeFalloConsumer</c>)
    /// filtrando por namespace y descartando definiciones genéricas abiertas.</summary>
    private static IReadOnlyList<Type> TiposQueNexoConsume() =>
        typeof(clsPlanDeCargueGeneradoConsumer).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .Where(m => m.Namespace == "Nexo.Mensajes.Cubicacion")
            .Distinct()
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static string FuentePython()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "Nexo.Clean.sln")))
            directorio = directorio.Parent;

        Assert.True(directorio is not null, "No se encontró la raíz del repositorio (Nexo.Clean.sln).");

        var ruta = Path.Combine(directorio!.FullName, RutaPublisherPython.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(ruta), $"No existe {RutaPublisherPython} — ¿se movió el publisher de Cubicador?");
        return File.ReadAllText(ruta);
    }

    private static string NamespaceQueUsaCubicador()
    {
        var m = Regex.Match(FuentePython(), "_NAMESPACE_MENSAJES_NEXO\\s*=\\s*\"([^\"]+)\"");
        Assert.True(m.Success, "No se pudo leer _NAMESPACE_MENSAJES_NEXO de rabbitmq.py.");
        return m.Groups[1].Value;
    }

    /// <summary>Los nombres de clase C# que Cubicador pone en el `messageType`, leídos de
    /// las llamadas reales a `_sobre_masstransit(...)`.</summary>
    private static IReadOnlyList<string> TiposQueCubicadorPublica() =>
        Regex.Matches(FuentePython(), "_sobre_masstransit\\(\\s*\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void El_namespace_del_urn_es_el_mismo_en_los_dos_lados()
    {
        var enCSharp = typeof(Nexo.Mensajes.Cubicacion.clsPlanDeCargueGenerado).Namespace;

        Assert.Equal(enCSharp, NamespaceQueUsaCubicador());
    }

    [Fact]
    public void Cada_evento_que_NEXO_consume_lo_publica_Cubicador_con_el_mismo_nombre()
    {
        var consumidosPorNexo = TiposQueNexoConsume().Select(t => t.Name).ToList();
        var publicadosPorCubicador = TiposQueCubicadorPublica();

        // Las dos direcciones importan: uno de más en NEXO es un consumer que nunca
        // recibirá nada; uno de más en Cubicador es un evento que se descarta en silencio.
        Assert.Equal(consumidosPorNexo, publicadosPorCubicador);
    }

    [Fact]
    public void El_urn_que_arma_Cubicador_es_exactamente_el_que_MassTransit_espera()
    {
        // MessageUrn es quien genera el valor en runtime: compararlo contra él, y no
        // contra una reconstrucción del formato a mano, es lo que hace que este test
        // detecte también un cambio de convención del propio MassTransit.
        var ns = NamespaceQueUsaCubicador();

        foreach (var tipo in TiposQueNexoConsume())
        {
            var urnDeCubicador = $"urn:message:{ns}:{tipo.Name}";

            Assert.Equal(MessageUrn.ForTypeString(tipo), urnDeCubicador);
        }
    }

    // ------------------------------------------------------------- el `tipo` del comando

    /// <summary>Los `tipo` que NEXO se permite publicar, leídos del propio validador
    /// (que es quien decide qué sale al broker: un `tipo` fuera de este conjunto se
    /// rechaza antes de publicar). Se lee por reflexión y no del fuente para que sea el
    /// valor real en runtime.</summary>
    private static IReadOnlyList<string> TiposQueNexoPublica()
    {
        var campo = typeof(Nexo.Mensajes.Cubicacion.clsCubicacionSolicitadaValidador)
            .GetField("TiposConocidos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.True(campo is not null, "clsCubicacionSolicitadaValidador ya no tiene TiposConocidos: revisar este test.");

        var valores = (IEnumerable<string>)campo!.GetValue(null)!;
        return valores.OrderBy(t => t, StringComparer.Ordinal).ToList();
    }

    /// <summary>Los `tipo` que el worker sabe despachar, leídos de las ramas reales de
    /// `_ejecutar_solicitud`.</summary>
    private static IReadOnlyList<string> TiposQueCubicadorDespacha()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "Nexo.Clean.sln")))
            directorio = directorio.Parent;

        var ruta = Path.Combine(directorio!.FullName, "cubicador", "worker", "main.py");
        Assert.True(File.Exists(ruta), "No existe cubicador/worker/main.py — ¿se movió el worker?");

        return Regex.Matches(File.ReadAllText(ruta), "tipo\\s*==\\s*\"([a-z_]+)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void Todo_tipo_que_NEXO_puede_publicar_lo_sabe_despachar_el_worker()
    {
        // El `tipo` es el MISMO acoplamiento por texto que el messageType, y con peor
        // desenlace: un tipo que NEXO publica y el worker no conoce lanza ValueError,
        // gasta los 3 reintentos y termina en la DLQ — en silencio, sin que nada avise
        // que el par se desincronizó.
        //
        // Contención, NO igualdad: el worker despacha ADEMÁS `crear_desde_linea`, que
        // hoy solo publica la propia API de Cubicador (a NEXO le falta ese endpoint —
        // ver docs/mensajeria/CUBICACION.md §8.2). Que el worker sepa hacer de más es
        // legítimo; que NEXO publique algo que el worker no entiende, no.
        var sinDespachar = TiposQueNexoPublica().Except(TiposQueCubicadorDespacha()).ToList();

        Assert.True(
            sinDespachar.Count == 0,
            $"NEXO puede publicar tipos que worker/main.py no despacha: {string.Join(", ", sinDespachar)}. "
            + "Irían a la DLQ tras 3 reintentos, sin aviso.");
    }

    [Fact]
    public void Hay_al_menos_un_evento_cubierto()
    {
        // Red de la red: si la reflexión o el regex dejan de encontrar nada, los tests de
        // arriba pasarían comparando dos listas vacías y no protegerían absolutamente nada.
        Assert.NotEmpty(TiposQueNexoConsume());
        Assert.NotEmpty(TiposQueCubicadorPublica());
        Assert.NotEmpty(TiposQueNexoPublica());
        Assert.NotEmpty(TiposQueCubicadorDespacha());
    }
}
