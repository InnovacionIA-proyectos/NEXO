using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Dtos;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Comportamiento POR DEFECTO (`SapTarifaFleteEnVivo:Modo=Deshabilitado`, o `Real`
/// mientras "el equipo de conexiones" no entregue el adaptador real todavía): nunca
/// hay candidato, `clsPropuestaTarifaAdapter` sigue exactamente la misma ruta de
/// siempre. Con este adaptador registrado, la plaza en vivo nunca se ejecuta y las
/// fuentes de lectura reales quedan sin cambios.
/// </summary>
public sealed class clsSapTarifaFleteEnVivoDeshabilitadoAdapter : ISapTarifaFleteEnVivoPort
{
    public bool Configurado => false;

    public Task<clsCandidatoTarifaDto?> ConsultarPlazaAsync(clsConsultaTarifarioSap consulta, CancellationToken ct = default) =>
        Task.FromResult<clsCandidatoTarifaDto?>(null);
}
