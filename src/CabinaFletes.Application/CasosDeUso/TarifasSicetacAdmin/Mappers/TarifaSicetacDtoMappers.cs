using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using CabinaFletes.Domain.BibliotecaTarifas.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Usuarios.Entidades;

namespace CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.Mappers;

public static class TarifaSicetacDtoMappers
{
    public static TarifaSicetacDto ToDto(this TarifaSicetac t) =>
        new(t.Id, t.Periodo, t.OrigenDane, t.DestinoDane, t.NombreRuta, t.Configuracion,
            t.TipoCarga, t.UnidadTransporte, t.CondicionCarga, t.ValorPiso, t.ValorHora, t.HorasPactadas);
}
