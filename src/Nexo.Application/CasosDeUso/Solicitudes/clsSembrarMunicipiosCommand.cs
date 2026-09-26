using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Siembra ÚNICA del catálogo de municipios (S4-HU-09.1) — no tiene parámetros porque
/// no es repetible: falla con conflicto si la tabla ya tiene filas. Toda la
/// transformación (filtro/match/excepciones verificados contra datos reales) vive en
/// el adapter de Infrastructure, no aquí — este handler solo orquesta el guard y el
/// guardado.
/// </summary>
public sealed record clsSembrarMunicipiosCommand : ICommand<clsResultadoSiembraMunicipiosDto>;

public sealed class clsSembrarMunicipiosCommandHandler(
    IMunicipiosSiembraPort fuente, IMunicipiosRepository municipios, IUnitOfWork uow)
    : ICommandHandler<clsSembrarMunicipiosCommand, clsResultadoSiembraMunicipiosDto>
{
    public async Task<clsResultadoSiembraMunicipiosDto> Handle(clsSembrarMunicipiosCommand command, CancellationToken ct = default)
    {
        if (!fuente.Configurado)
            throw new clsServiceUnavailableException("DB_SOLUCIONES_TI_NO_CONFIGURADA",
                "No se puede sembrar el catálogo de municipios sin ConnectionStrings:DbSolucionesTi.");

        if (await municipios.ExisteAlgunoAsync(ct))
            throw new clsConflictException("MUNICIPIOS_YA_SEMBRADOS",
                "El catálogo de municipios ya tiene datos — la siembra es de una sola vez (foto fija), no se repite.");

        var nuevos = await fuente.ConstruirCatalogoAsync(ct);

        municipios.AgregarRango(nuevos);
        await uow.SaveChangesAsync(ct);

        return new clsResultadoSiembraMunicipiosDto(
            TotalInsertados: nuevos.Count,
            Colombia: nuevos.Count(m => m.Pais == "CO"),
            Venezuela: nuevos.Count(m => m.Pais == "VE"),
            Ecuador: nuevos.Count(m => m.Pais == "EC"),
            ConNombreTarifasPlaza: nuevos.Count(m => m.NombreTarifasPlaza is not null),
            ConZonaTarificacion: nuevos.Count(m => m.ZonaTarificacion is not null),
            ZcSinRelacionOficial: nuevos.Count(m => m.NombreTarifasPlaza is not null && m.ZonaTarificacion is null));
    }
}
