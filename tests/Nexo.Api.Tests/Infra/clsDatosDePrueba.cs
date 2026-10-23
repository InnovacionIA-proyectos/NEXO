using Microsoft.EntityFrameworkCore;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Limpieza de los datos que crean las pruebas contra la base real. Toda solicitud de prueba lleva la marca
/// <see cref="Marca"/> en su referencia, de modo que:
///   · cada prueba borra lo suyo al terminar (<see cref="Eliminar"/>): la solicitud, sus líneas y cotizaciones, y las rutas y
///     configuraciones de vehículo que la propia prueba creó;
///   · <see cref="BarrerRestos"/> borra cualquier solicitud marcada que haya quedado de una corrida interrumpida, así la base
///     no acumula datos de prueba aunque una ejecución se haya cortado a la mitad.
/// El borrado es de mejor esfuerzo: un bloqueo ajeno de la base compartida no debe tumbar una prueba que ya pasó.
/// </summary>
public sealed class clsDatosDePrueba(clsApiFixture fx)
{
    public const string Marca = "TEST-AUTO-";

    public static string NuevaReferencia() => $"{Marca}{Guid.NewGuid():N}";

    /// <summary>Borra las solicitudes marcadas que hayan quedado de una corrida anterior.</summary>
    public void BarrerRestos()
    {
        // Rutas y vehículos solo se borran si nacieron junto con la solicitud de prueba (poco antes de su creación): nunca un
        // registro del catálogo que ya existía y que la prueba solo reutilizó.
        var restos = fx.WithDb(db => db.Solicitudes
            .Where(s => s.CondicionesServicio != null && s.CondicionesServicio.Contains(Marca))
            .Select(s => new { s.Id, s.CreatedAt }).ToList());
        foreach (var r in restos) Eliminar(r.Id, r.CreatedAt.AddMinutes(-2));
    }

    /// <summary>
    /// Borra la solicitud, sus cotizaciones y líneas, y las rutas y configuraciones de vehículo que sus líneas usaban siempre
    /// que hayan nacido desde <paramref name="creadasDesde"/> y ya no las use ninguna otra línea ni tarifa histórica.
    /// </summary>
    public void Eliminar(Guid solicitudId, DateTimeOffset creadasDesde)
    {
        try
        {
            fx.WithDb(db =>
            {
                var rutas = db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).Select(l => l.RutaId).ToList();
                var vehiculos = db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId && l.ConfigVehiculoId != null)
                    .Select(l => l.ConfigVehiculoId!.Value).ToList();

                db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
                db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();

                db.Rutas.Where(r => rutas.Contains(r.Id) && r.CreatedAt >= creadasDesde
                    && !db.LineasSolicitud.Any(l => l.RutaId == r.Id) && !db.TarifasHistoricas.Any(h => h.RutaId == r.Id)).ExecuteDelete();
                db.ConfiguracionesVehiculo.Where(v => vehiculos.Contains(v.Id) && v.CreatedAt >= creadasDesde
                    && !db.LineasSolicitud.Any(l => l.ConfigVehiculoId == v.Id) && !db.TarifasHistoricas.Any(h => h.ConfigVehiculoId == v.Id)).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
    }
}
