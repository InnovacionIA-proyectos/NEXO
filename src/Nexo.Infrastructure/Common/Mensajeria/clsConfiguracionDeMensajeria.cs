using Microsoft.Extensions.Configuration;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>Lecturas de configuración que usa la mensajería.</summary>
internal static class clsConfiguracionDeMensajeria
{
    /// <summary>
    /// Lee un entero de la configuración sin depender del paquete Binder.
    ///
    /// <c>GetValue</c> vive en Microsoft.Extensions.Configuration.Binder, que esta capa no
    /// referencia; y el <c>using MassTransit</c> trae una extensión homónima que tapa la
    /// otra y produce un error que no dice nada. Con esto no hay ambigüedad posible.
    /// </summary>
    internal static int EnteroDeConfig(IConfiguration config, string clave, int porDefecto) =>
        int.TryParse(config[clave], out var valor) ? valor : porDefecto;
}
