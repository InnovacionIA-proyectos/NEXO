namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>
/// Puerto de LECTURA cacheada que consume el motor de propuesta de tarifa
/// (clsPropuestaTarifaAdapter, RN-01): el conjunto de rutas+vehículo "solo
/// SICE-TAC" con clave "ORIGEN|DESTINO|TIPOVEHICULO" (antes la
/// clave era solo "ORIGEN|DESTINO", así que marcar un único tipo de vehículo
/// apagaba la comparación contra plaza para TODOS los tipos de esa ruta).
/// </summary>
public interface IClavesSoloSicetacPort
{
    Task<IReadOnlySet<string>> ClavesRutaVehiculoAsync(CancellationToken ct = default);
}
