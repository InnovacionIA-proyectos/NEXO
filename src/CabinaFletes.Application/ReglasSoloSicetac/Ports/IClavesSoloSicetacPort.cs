namespace CabinaFletes.Application.ReglasSoloSicetac.Ports;

/// <summary>
/// Puerto de LECTURA cacheada que consumirá el módulo Biblioteca (motor de
/// propuesta de tarifa, aún no migrado — ver PENDIENTES.md): el conjunto de
/// rutas "solo SICE-TAC" con clave "ORIGEN|DESTINO".
/// </summary>
public interface IClavesSoloSicetacPort
{
    Task<IReadOnlySet<string>> ClavesRutaAsync(CancellationToken ct = default);
}
