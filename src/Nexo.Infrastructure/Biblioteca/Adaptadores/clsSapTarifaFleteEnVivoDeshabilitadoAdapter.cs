using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Dtos;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>Modo por defecto: nunca hay candidato en vivo, así que la propuesta usa solo las fuentes de lectura.</summary>
public sealed class clsSapTarifaFleteEnVivoDeshabilitadoAdapter : ISapTarifaFleteEnVivoPort
{
    public bool Configurado => false;

    public Task<clsCandidatoTarifaDto?> ConsultarPlazaAsync(clsConsultaTarifarioSap consulta, CancellationToken ct = default) =>
        Task.FromResult<clsCandidatoTarifaDto?>(null);
}
