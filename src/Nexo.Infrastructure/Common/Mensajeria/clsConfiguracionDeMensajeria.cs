using Microsoft.Extensions.Configuration;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>Lecturas de configuración que usa la mensajería.</summary>
internal static class clsConfiguracionDeMensajeria
{
    /// <summary>Evita GetValue: vive en el paquete Binder (no referenciado) y choca con la extensión homónima de MassTransit.</summary>
    internal static int EnteroDeConfig(IConfiguration config, string clave, int porDefecto) =>
        int.TryParse(config[clave], out var valor) ? valor : porDefecto;
}
