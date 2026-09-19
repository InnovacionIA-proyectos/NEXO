using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Costeo;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record ImportarTarifasSicetacExcelCommand(Stream Excel) : ICommand<ImportResumenDto>;

public sealed class ImportarTarifasSicetacExcelCommandHandler(
    ITarifasSicetacRepository repo,
    ICurrentUserPort usuarioActual,
    IPublicadorDeMensajes publicador,
    TimeProvider clock)
    : ICommandHandler<ImportarTarifasSicetacExcelCommand, ImportResumenDto>
{
    public async Task<ImportResumenDto> Handle(ImportarTarifasSicetacExcelCommand command, CancellationToken ct = default)
    {
        var resumen = await repo.ImportarExcelAsync(command.Excel, usuarioActual.Id, ct);

        // Solo si la importación dejó algo nuevo. Una carga que no insertó ni actualizó
        // nada no puede resolver ninguna línea, y disparar el recosteo sería revisar
        // todos los pendientes para nada.
        if (resumen.Insertadas > 0 || resumen.Actualizadas > 0)
            await publicador.PublicarAsync(
                new TarifasActualizadas(
                    $"Importación de la matriz SICE-TAC ({resumen.Insertadas} nuevas, {resumen.Actualizadas} actualizadas)",
                    clock.GetUtcNow()), ct);

        return resumen;
    }
}
