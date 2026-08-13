using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.AjustesVigenciaPlaza.Ports;

namespace CabinaFletes.Application.AjustesVigenciaPlaza.Commands;

/// <summary>Quita el ajuste: la tarifa vuelve a regirse por la vigencia de SAP. False si no había ajuste (404).</summary>
public sealed record QuitarVigenciaPlazaCommand(int TarifaPlazaId) : ICommand<bool>;

public sealed class QuitarVigenciaPlazaCommandHandler(
    IAjustesVigenciaPlazaRepository ajustes, IAuditoriaPort auditoria, Common.ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<QuitarVigenciaPlazaCommand, bool>
{
    public async Task<bool> Handle(QuitarVigenciaPlazaCommand command, CancellationToken ct = default)
    {
        var existente = await ajustes.ObtenerPorTarifaAsync(command.TarifaPlazaId, ct);
        if (existente is null) return false;

        ajustes.Eliminar(existente);
        auditoria.Registrar(usuarioActual.Id, "tarifas_plaza_vigencia", Guid.Empty, "quitar", $"{{\"tarifa\":{command.TarifaPlazaId}}}");
        await uow.SaveChangesAsync(ct);
        ajustes.InvalidarCache();
        return true;
    }
}
