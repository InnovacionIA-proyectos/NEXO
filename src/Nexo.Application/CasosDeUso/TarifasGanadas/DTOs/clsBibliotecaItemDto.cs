using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;

/// <summary>Ítem de la Biblioteca de Tarifas Ganadas (o sugerencia para el costeo).</summary>
public record clsBibliotecaItemDto(
    Guid Id,
    string? clsRuta,
    string? Vehiculo,
    TipologiaCarga? Tipologia,
    decimal Valor,
    DateOnly Fecha,
    string Vigencia,   // "vigente" | "por_vencer" | "vencida"
    bool DeCotizacionGanada);
