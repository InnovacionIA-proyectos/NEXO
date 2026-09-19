using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Dtos;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Comportamiento POR DEFECTO de S2-HU-09 (`SapTarifaFleteEnVivo:Modo=Deshabilitado`,
/// o `Real` mientras "el equipo de conexiones" no entregue el adaptador real todavía):
/// nunca hay candidato, `PropuestaTarifaAdapter` sigue exactamente la misma ruta que
/// tenía antes de esta HU. Esto es lo que garantiza "las 4 fuentes de lectura que hoy
/// operan siguen usando el dato REAL, sin cambios" — con este adaptador registrado, el
/// código nuevo de S2-HU-09 no se ejecuta nunca.
/// </summary>
public sealed class SapTarifaFleteEnVivoDeshabilitadoAdapter : ISapTarifaFleteEnVivoPort
{
    public bool Configurado => false;

    public Task<CandidatoTarifaDto?> ConsultarPlazaAsync(ConsultaTarifarioSap consulta, CancellationToken ct = default) =>
        Task.FromResult<CandidatoTarifaDto?>(null);
}
