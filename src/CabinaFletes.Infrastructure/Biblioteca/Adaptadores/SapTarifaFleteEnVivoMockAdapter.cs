using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;
using CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Domain.Common;

namespace CabinaFletes.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// S2-HU-09.3 (#33): dataset PROVISIONAL de plaza mientras la conexión SAP en vivo
/// (S2-HU-09.1, #31) no exista. Valores representativos de fletes reales de Botero
/// Soto para las rutas más frecuentes — PENDIENTE de validación formal por negocio
/// (S2-HU-09.2): no son un promedio calculado, son estimaciones razonables a partir
/// de las tarifas ya sembradas en DataSeeder.cs para las mismas rutas.
///
/// Se activa SOLO por configuración (`SapTarifaFleteEnVivo:Modo=Mock`,
/// DependencyInjection.cs) — nunca reemplaza a <see cref="ITarifarioSapPort"/> (la
/// fuente REAL de plaza, vía DbSolucionesTi, que sigue intacta) salvo que un operador
/// lo encienda a propósito.
/// </summary>
public sealed class SapTarifaFleteEnVivoMockAdapter : ISapTarifaFleteEnVivoPort
{
    public bool Configurado => true;

    /// <summary>Clave: ruta normalizada (Texto.ClaveRuta). Valor: tarifa base en COP,
    /// para un vehículo estándar (Turbo/Sencillo) y peso de referencia (~8 ton).</summary>
    private static readonly IReadOnlyDictionary<string, decimal> Dataset = new Dictionary<string, decimal>
    {
        [Texto.ClaveRuta("Bogota", "Medellin")] = 2_850_000m,
        [Texto.ClaveRuta("Medellin", "Bogota")] = 2_850_000m,
        [Texto.ClaveRuta("Bogota", "Cali")] = 3_100_000m,
        [Texto.ClaveRuta("Cali", "Bogota")] = 3_100_000m,
        [Texto.ClaveRuta("Bogota", "Barranquilla")] = 4_600_000m,
        [Texto.ClaveRuta("Barranquilla", "Bogota")] = 4_600_000m,
        [Texto.ClaveRuta("Medellin", "Cartagena")] = 3_900_000m,
        [Texto.ClaveRuta("Cartagena", "Medellin")] = 3_900_000m,
        [Texto.ClaveRuta("Bogota", "Bucaramanga")] = 2_400_000m,
        [Texto.ClaveRuta("Bucaramanga", "Bogota")] = 2_400_000m,
    };

    public Task<CandidatoTarifaDto?> ConsultarPlazaAsync(ConsultaTarifarioSap consulta, CancellationToken ct = default)
    {
        var clave = Texto.ClaveRuta(consulta.Origen, consulta.Destino);
        if (!Dataset.TryGetValue(clave, out var baseCop))
            return Task.FromResult<CandidatoTarifaDto?>(null);

        // Ajuste simple por peso: la base es para ~8 ton, escala linealmente con piso de
        // 60% (un flete no baja de más de la mitad aunque la carga sea liviana) — es una
        // aproximación deliberadamente simple, PROVISIONAL, no una tarifa SAP real.
        var factor = consulta.Peso is > 0
            ? Math.Clamp(consulta.Peso.Value / 8000m, 0.6m, 1.6m)
            : 1m;
        var valor = Math.Round(baseCop * factor, 0);

        return Task.FromResult<CandidatoTarifaDto?>(new CandidatoTarifaDto(
            "sap_en_vivo_mock", valor,
            $"{consulta.Origen} → {consulta.Destino} · dataset provisional (S2-HU-09.3, pendiente de validación formal por negocio)",
            $"mock: ruta {clave}, factor de peso {factor:0.00}", 1));
    }
}
