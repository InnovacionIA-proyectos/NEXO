using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.TarifasGanadas.DTOs;

/// <summary>Ítem de la Biblioteca de Tarifas Ganadas (o sugerencia para el costeo).</summary>
public record BibliotecaItemDto(
    Guid Id,
    string? Ruta,
    string? Vehiculo,
    TipologiaCarga? Tipologia,
    decimal Valor,
    DateOnly Fecha,
    string Vigencia,   // "vigente" | "por_vencer" | "vencida"
    bool DeCotizacionGanada);
