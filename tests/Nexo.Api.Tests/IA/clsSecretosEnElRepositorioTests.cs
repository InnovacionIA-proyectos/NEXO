using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Nexo.Api.Tests.IA;

/// <summary>
/// La llave del proveedor de IA vive solo en el servidor (variables de entorno de Dokploy) y nunca en un archivo versionado.
/// Estas pruebas revisan los archivos que Git rastrea —no el <c>.env</c> local, que está ignorado— y fallan si aparece una
/// llave con la forma de una real o un valor asignado a <c>Anthropic__ApiKey</c> o <c>ANTHROPIC_API_KEY</c> en las plantillas de configuración.
/// </summary>
public class clsSecretosEnElRepositorioTests
{
    // La forma de una llave real: prefijo, tipo y un cuerpo largo. Las llaves falsas de las pruebas son más cortas y no
    // coinciden; el propio patrón tampoco, porque tras "sk-ant-" lleva un paréntesis y no letras.
    private static readonly Regex LlaveReal = new(@"sk-ant-(api|admin)\d{2}-[A-Za-z0-9_-]{40,}", RegexOptions.Compiled);

    // Anthropic__ApiKey= (NEXO) o ANTHROPIC_API_KEY= (Cubicador) seguido de un valor que no es vacío ni un marcador <como-este>.
    private static readonly Regex LlaveAsignada = new(@"^[ \t]*(Anthropic__ApiKey|ANTHROPIC_API_KEY)[ \t]*=[ \t]*(?!<|\s*$)\S+", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly HashSet<string> Binarios = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".docx", ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".zip", ".dll", ".exe", ".pptx", ".woff", ".woff2",
    };

    private static string? RaizDelRepositorio()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Nexo.Clean.sln"))) return dir.FullName;
        return null;
    }

    private static List<string>? ArchivosVersionados(string raiz)
    {
        try
        {
            var info = new ProcessStartInfo("git", "ls-files -z")
            {
                WorkingDirectory = raiz, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            using var proceso = Process.Start(info)!;
            var salida = proceso.StandardOutput.ReadToEnd();
            proceso.WaitForExit();
            return proceso.ExitCode == 0 ? [.. salida.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null; // sin git en el ambiente: no hay nada que revisar
        }
    }

    private static IEnumerable<(string Ruta, string Texto)> TextosVersionados()
    {
        var raiz = RaizDelRepositorio();
        if (raiz is null) yield break;
        var archivos = ArchivosVersionados(raiz);
        if (archivos is null) yield break;

        foreach (var relativa in archivos)
        {
            if (Binarios.Contains(Path.GetExtension(relativa))) continue;
            var ruta = Path.Combine(raiz, relativa);
            if (!File.Exists(ruta) || new FileInfo(ruta).Length > 2_000_000) continue;
            yield return (relativa, File.ReadAllText(ruta));
        }
    }

    [Fact]
    public void Ningun_archivo_versionado_contiene_una_llave_de_Anthropic()
    {
        var con = TextosVersionados().Where(t => LlaveReal.IsMatch(t.Texto)).Select(t => t.Ruta).ToList();

        Assert.True(con.Count == 0, $"Hay una llave de Anthropic en archivos versionados: {string.Join(", ", con)}");
    }

    [Fact]
    public void Las_plantillas_de_configuracion_no_traen_valor_para_la_llave_de_Anthropic()
    {
        var con = TextosVersionados().Where(t => LlaveAsignada.IsMatch(t.Texto)).Select(t => t.Ruta).ToList();

        Assert.True(con.Count == 0, $"La llave de Anthropic tiene un valor en: {string.Join(", ", con)}");
    }

    [Theory]
    [InlineData("sk-ant-api03-" + "AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-AbCdEfGhIjKlMnOpQrStUvWxYz012345", true)]
    [InlineData("sk-ant-LLAVE-FALSA-DE-PRUEBA", false)]
    [InlineData("Anthropic__ApiKey=", false)]
    [InlineData("Anthropic__ApiKey=\nAnthropic__Model=claude-sonnet-4-6", false)]
    [InlineData("Anthropic__ApiKey=<secreto>", false)]
    [InlineData("Anthropic__ApiKey=algo-con-valor", true)]
    [InlineData("ANTHROPIC_API_KEY=", false)]
    [InlineData("ANTHROPIC_API_KEY=<secreto>", false)]
    [InlineData("ANTHROPIC_API_KEY=\nANTHROPIC_MODEL=claude-sonnet-5-5", false)]
    [InlineData("ANTHROPIC_API_KEY=algo-con-valor", true)]
    public void Los_patrones_distinguen_una_llave_real_de_las_falsas_y_de_las_plantillas(string texto, bool debeDetectar)
    {
        Assert.Equal(debeDetectar, LlaveReal.IsMatch(texto) || LlaveAsignada.IsMatch(texto));
    }
}
