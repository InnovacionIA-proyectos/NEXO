using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

/// <summary>
/// Resolución compartida entre Crear/Editar (S4-HU-10.2) y el importador Excel: valida
/// línea de negocio y clase de viaje contra el catálogo real (tblSAPLineaNegocio/
/// tblSAPClaseViaje) y devuelve el texto CANÓNICO del catálogo, no el que escribió quien
/// captura — así dos formas de escribir la misma línea de negocio nunca producen dos
/// tarifas "distintas" para Nexo.
/// </summary>
public static class clsResolucionDeTarifaPlaza
{
    public static async Task<(string LineaNegocio, string ClaseViaje)> ResolverCatalogosAsync(
        string lineaNegocio, string claseViaje,
        ILineasNegocioPort lineasNegocio, IClasesViajePort clasesViaje, CancellationToken ct)
    {
        var ln = (await lineasNegocio.ListarAsync(ct))
            .FirstOrDefault(l => clsTexto.Normalizar(l.Descripcion) == clsTexto.Normalizar(lineaNegocio))
            ?? throw new clsValidationFailedException("LINEA_NEGOCIO_INVALIDA", $"'{lineaNegocio}' no existe en el catálogo de línea de negocio.");

        var cv = (await clasesViaje.ListarAsync(ct))
            .FirstOrDefault(c => clsTexto.Normalizar(c.Descripcion) == clsTexto.Normalizar(claseViaje))
            ?? throw new clsValidationFailedException("CLASE_VIAJE_INVALIDA", $"'{claseViaje}' no existe en el catálogo de clase de viaje.");

        return (ln.Descripcion, cv.Descripcion);
    }
}

public static class clsTarifaPlazaMapper
{
    public static clsTarifaPlazaDto ToDto(this clsTarifaPlaza t) => new(
        t.Id, t.LineaNegocio, t.ClaseViaje,
        t.OrigenMunicipioId, t.Origen?.Municipio ?? "", t.Origen?.ZonaTarificacion,
        t.DestinoMunicipioId, t.Destino?.Municipio ?? "", t.Destino?.ZonaTarificacion,
        t.ClaseContenedor, t.PesoId, t.Peso is null ? null : FormatoRango(t.Peso), t.TipoVehiculo,
        t.Valor, t.Vigencia, t.DetallesTarifa, t.CreatedAt);

    /// <summary>"3-15000" para una banda cerrada, "10500+" para una banda abierta (sin
    /// PesoMaximo) — mismo formato que espera el importador/exportador Excel.</summary>
    public static string FormatoRango(clsRangoPeso p) => p.PesoMaximo is { } max ? $"{p.PesoMinimo}-{max}" : $"{p.PesoMinimo}+";
}
