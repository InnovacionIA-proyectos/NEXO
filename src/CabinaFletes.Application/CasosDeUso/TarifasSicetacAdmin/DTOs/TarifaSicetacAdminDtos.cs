using CabinaFletes.Domain.BibliotecaTarifas.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Usuarios.Entidades;

namespace CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;

/// <summary>Filtros de consulta del piso SICE-TAC (caché local, cargada por Excel).</summary>
public record SicetacFiltro(
    string? Origen,
    string? Destino,
    string? Config,
    string? TipoCarga,
    string? Unidad,
    string? Condicion,
    int? Periodo);

public record TarifaSicetacDto(
    Guid Id,
    int Periodo,
    string OrigenDane,
    string DestinoDane,
    string? NombreRuta,
    string Configuracion,
    string TipoCarga,
    string UnidadTransporte,
    CondicionCarga CondicionCarga,
    decimal ValorPiso,
    decimal? ValorHora,
    decimal? HorasPactadas);

public static class TarifaSicetacDtoMappers
{
    public static TarifaSicetacDto ToDto(this TarifaSicetac t) =>
        new(t.Id, t.Periodo, t.OrigenDane, t.DestinoDane, t.NombreRuta, t.Configuracion,
            t.TipoCarga, t.UnidadTransporte, t.CondicionCarga, t.ValorPiso, t.ValorHora, t.HorasPactadas);
}

/// <summary>Resumen del resultado de importar un Excel SICE-TAC.</summary>
public record ImportResumenDto(
    int Procesadas,
    int Insertadas,
    int Actualizadas,
    int Rechazadas,
    IReadOnlyList<string> Errores,
    // Pisos importados que no se pudieron enlazar a una ruta local (NOMBRERUTA no
    // coincide con ninguna ruta capturada): no aparecerán en la Mesa hasta que la
    // ruta exista. Cero cuando todo se enlazó.
    int SinRutaLocal = 0);

/// <summary>Entrada del historial de cargas (derivada de audit_log).</summary>
public record ImportacionDto(
    Guid Id,
    string? Usuario,
    string? Detalle,
    DateTimeOffset Fecha);
