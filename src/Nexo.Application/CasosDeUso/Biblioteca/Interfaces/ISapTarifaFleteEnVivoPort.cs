using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>
/// Contrato de la conexión SAP NUEVA (tarifa de plaza en vivo, equivalente a <c>getConsultarTarifaFlete</c>), que aún no existe: hoy solo se mockea.
/// No confundir con <see cref="ITarifarioSapPort"/>, la fuente REAL de plaza. Entrada: <see cref="clsConsultaTarifarioSap"/>; salida:
/// <see cref="clsCandidatoTarifaDto"/> con <c>Fuente</c> SIEMPRE iniciada por "sap_en_vivo" (nunca "plaza" a secas, para no confundir la trazabilidad)
/// y <c>Valor</c> en COP neto y positivo. Es siempre "al momento" (sin tarifa vencida ni filtro de vigencia); null = sin candidato, no es error.
/// El adaptador real se enchufa cambiando una línea en clsDependencyInjection.cs (<c>SapTarifaFleteEnVivo:Modo</c>).
/// </summary>
public interface ISapTarifaFleteEnVivoPort
{
    /// <summary>False cuando el modo es "Deshabilitado" (por defecto) o "Real" sin adaptador disponible: degrada, nunca falla.</summary>
    bool Configurado { get; }

    Task<clsCandidatoTarifaDto?> ConsultarPlazaAsync(clsConsultaTarifarioSap consulta, CancellationToken ct = default);
}
