namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>Lectura cacheada del conjunto de rutas+vehículo "solo SICE-TAC" con clave "ORIGEN|DESTINO|TIPOVEHICULO", que consume el motor de propuesta de tarifa.</summary>
public interface IClavesSoloSicetacPort
{
    Task<IReadOnlySet<string>> ClavesRutaVehiculoAsync(CancellationToken ct = default);
}
