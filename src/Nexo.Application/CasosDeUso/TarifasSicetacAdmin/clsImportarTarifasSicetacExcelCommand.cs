using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Costeo;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record clsImportarTarifasSicetacExcelCommand(Stream Excel) : ICommand<clsImportResumenDto>;

public sealed class clsImportarTarifasSicetacExcelCommandHandler(
    ITarifasSicetacRepository repo,
    ICurrentUserPort usuarioActual,
    IPublicadorDeMensajes publicador,
    TimeProvider clock)
    : ICommandHandler<clsImportarTarifasSicetacExcelCommand, clsImportResumenDto>
{
    public async Task<clsImportResumenDto> Handle(clsImportarTarifasSicetacExcelCommand command, CancellationToken ct = default)
    {
        var resumen = await repo.ImportarExcelAsync(command.Excel, usuarioActual.Id, ct);

        // Solo si la importación dejó algo nuevo. Una carga que no insertó ni actualizó
        // nada no puede resolver ninguna línea, y disparar el recosteo sería revisar
        // todos los pendientes para nada.
        if (resumen.Insertadas > 0 || resumen.Actualizadas > 0)
            await publicador.PublicarAsync(
                new clsTarifasActualizadas(
                    $"Importación de la matriz SICE-TAC ({resumen.Insertadas} nuevas, {resumen.Actualizadas} actualizadas)",
                    clock.GetUtcNow()), ct);

        return resumen;
    }
}
