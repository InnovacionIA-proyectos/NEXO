using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.AjustesVigenciaPlaza.Ports;

namespace CabinaFletes.Application.AjustesVigenciaPlaza.Commands;

/// <summary>Ajuste 1 a 1: fija (o reemplaza) la vigencia local de una tarifa de plaza.</summary>
public sealed record AjustarVigenciaPlazaCommand(int TarifaPlazaId, DateTime NuevaVigencia, string? Motivo) : ICommand<Unit>;

public sealed class AjustarVigenciaPlazaCommandHandler(
    IAjustesVigenciaPlazaRepository ajustes,
    IVigenciaSapPlazaPort vigenciaSap,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<AjustarVigenciaPlazaCommand, Unit>
{
    public async Task<Unit> Handle(AjustarVigenciaPlazaCommand command, CancellationToken ct = default)
    {
        var existente = await ajustes.ObtenerPorTarifaAsync(command.TarifaPlazaId, ct);

        // SAP solo si es un ajuste NUEVO (para guardar la vigencia original de referencia).
        var vigenciaSapOriginal = existente is null
            ? (await vigenciaSap.VigenciasAsync([command.TarifaPlazaId], ct)).GetValueOrDefault(command.TarifaPlazaId)
            : (DateTime?)null;

        if (existente is not null)
        {
            existente.VigenciaAjustada = command.NuevaVigencia.Date;
            if (!string.IsNullOrWhiteSpace(command.Motivo)) existente.Motivo = command.Motivo.Trim();
            existente.AjustadoPor = usuarioActual.Id;
        }
        else
        {
            ajustes.Agregar(new Domain.Entities.AjusteVigenciaPlaza
            {
                TarifaPlazaId = command.TarifaPlazaId,
                VigenciaAjustada = command.NuevaVigencia.Date,
                VigenciaOriginal = vigenciaSapOriginal == default ? null : vigenciaSapOriginal,
                Motivo = string.IsNullOrWhiteSpace(command.Motivo) ? null : command.Motivo.Trim(),
                AjustadoPor = usuarioActual.Id,
            });
        }

        auditoria.Registrar(usuarioActual.Id, "tarifas_plaza_vigencia", Guid.Empty, "ajustar",
            $"{{\"tarifa\":{command.TarifaPlazaId},\"vigencia\":\"{command.NuevaVigencia:yyyy-MM-dd}\"}}");
        await uow.SaveChangesAsync(ct);
        ajustes.InvalidarCache();
        return Unit.Value;
    }
}
