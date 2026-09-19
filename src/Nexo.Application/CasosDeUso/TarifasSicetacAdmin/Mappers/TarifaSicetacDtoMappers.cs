using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Mappers;

public static class TarifaSicetacDtoMappers
{
    public static TarifaSicetacDto ToDto(this TarifaSicetac t) =>
        new(t.Id, t.Periodo, t.OrigenDane, t.DestinoDane, t.NombreRuta, t.Configuracion,
            t.TipoCarga, t.UnidadTransporte, t.CondicionCarga, t.ValorPiso, t.ValorHora, t.HorasPactadas);
}
