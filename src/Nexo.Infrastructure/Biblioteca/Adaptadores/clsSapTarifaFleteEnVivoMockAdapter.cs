using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Common;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Dataset provisional de plaza (sin validar por negocio) para cuando no hay conexión SAP en vivo; solo se activa por configuración (Modo=Mock).
/// </summary>
public sealed class clsSapTarifaFleteEnVivoMockAdapter : ISapTarifaFleteEnVivoPort
{
    public bool Configurado => true;

    /// <summary>Clave: ruta normalizada (clsTexto.ClaveRuta). Valor: tarifa base en COP para un vehículo estándar y ~8 ton.</summary>
    private static readonly IReadOnlyDictionary<string, decimal> Dataset = new Dictionary<string, decimal>
    {
        [clsTexto.ClaveRuta("Bogota", "Medellin")] = 2_850_000m,
        [clsTexto.ClaveRuta("Medellin", "Bogota")] = 2_850_000m,
        [clsTexto.ClaveRuta("Bogota", "Cali")] = 3_100_000m,
        [clsTexto.ClaveRuta("Cali", "Bogota")] = 3_100_000m,
        [clsTexto.ClaveRuta("Bogota", "Barranquilla")] = 4_600_000m,
        [clsTexto.ClaveRuta("Barranquilla", "Bogota")] = 4_600_000m,
        [clsTexto.ClaveRuta("Medellin", "Cartagena")] = 3_900_000m,
        [clsTexto.ClaveRuta("Cartagena", "Medellin")] = 3_900_000m,
        [clsTexto.ClaveRuta("Bogota", "Bucaramanga")] = 2_400_000m,
        [clsTexto.ClaveRuta("Bucaramanga", "Bogota")] = 2_400_000m,
    };

    public Task<clsCandidatoTarifaDto?> ConsultarPlazaAsync(clsConsultaTarifarioSap consulta, CancellationToken ct = default)
    {
        var clave = clsTexto.ClaveRuta(consulta.Origen, consulta.Destino);
        if (!Dataset.TryGetValue(clave, out var baseCop))
            return Task.FromResult<clsCandidatoTarifaDto?>(null);

        // Escala lineal por peso respecto a ~8 ton, con piso de 60%: aproximación provisional, no una tarifa SAP.
        var factor = consulta.Peso is > 0
            ? Math.Clamp(consulta.Peso.Value / 8000m, 0.6m, 1.6m)
            : 1m;
        var valor = Math.Round(baseCop * factor, 0);

        return Task.FromResult<clsCandidatoTarifaDto?>(new clsCandidatoTarifaDto(
            "sap_en_vivo_mock", valor,
            $"{consulta.Origen} → {consulta.Destino} · dataset provisional, pendiente de validación formal por negocio",
            $"mock: ruta {clave}, factor de peso {factor:0.00}", 1));
    }
}
