using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Asistente.Herramientas;

/// <summary>La línea como la ve la persona: «Cali → Barranquilla · Turbo».</summary>
public static class clsDescripcionDeLinea
{
    /// <summary>Null si la línea no trae ruta.</summary>
    public static string? De(clsLineaSolicitud? linea)
    {
        if (linea?.clsRuta is not { } ruta) return null;

        var vehiculo = linea.clsConfiguracionVehiculo?.TipoVehiculo;
        return $"{ruta.Origen} → {ruta.Destino}" + (string.IsNullOrWhiteSpace(vehiculo) ? string.Empty : $" · {vehiculo}");
    }
}
