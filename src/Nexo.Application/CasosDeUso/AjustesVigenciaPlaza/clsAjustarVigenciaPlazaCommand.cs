using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Mensajes.Costeo;

namespace Nexo.Application.CasosDeUso.AjustesVigenciaPlaza;

/// <summary>
/// Ajuste 1 a 1: fija (o reemplaza) la vigencia local de una tarifa de plaza.
///
/// NuevaVigencia es nullable a propósito, aunque el campo es obligatorio para el caso de
/// uso: <c>DateTime</c> (no nullable) con <c>[Required]</c> en el DTO de Api nunca dispara
/// esa validación (el model binder ya deja <c>default(DateTime)</c> antes de validar, así
/// que jamás es "null" para DataAnnotations) — eso dejaba pasar un body vacío como
/// 0001-01-01 sin error (bug confirmado en QA de producción, 2026-09-22). Mismo patrón que
/// <see cref="clsAjustarVigenciaPlazaMasivoCommand"/>: nullable + validación explícita acá,
/// no en el DTO.
/// </summary>
public sealed record clsAjustarVigenciaPlazaCommand(int TarifaPlazaId, DateTime? NuevaVigencia, string? Motivo) : ICommand<Unit>;

public sealed class clsAjustarVigenciaPlazaCommandHandler(
    IAjustesVigenciaPlazaRepository ajustes,
    IVigenciaSapPlazaPort vigenciaSap,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IPublicadorDeMensajes publicador,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsAjustarVigenciaPlazaCommand, Unit>
{
    public async Task<Unit> Handle(clsAjustarVigenciaPlazaCommand command, CancellationToken ct = default)
    {
        if (command.NuevaVigencia is null)
            throw new clsValidationFailedException("FECHA_REQUERIDA", "Debe indicar la nueva vigencia.");
        var nuevaVigencia = command.NuevaVigencia.Value.Date;

        var existente = await ajustes.ObtenerPorTarifaAsync(command.TarifaPlazaId, ct);

        // SAP solo si es un ajuste NUEVO (para guardar la vigencia original de referencia).
        var vigenciaSapOriginal = existente is null
            ? (await vigenciaSap.VigenciasAsync([command.TarifaPlazaId], ct)).GetValueOrDefault(command.TarifaPlazaId)
            : (DateTime?)null;

        if (existente is not null)
        {
            existente.VigenciaAjustada = nuevaVigencia;
            if (!string.IsNullOrWhiteSpace(command.Motivo)) existente.Motivo = command.Motivo.Trim();
            existente.AjustadoPor = usuarioActual.Id;
        }
        else
        {
            ajustes.Agregar(new Domain.Costeo.Entidades.clsAjusteVigenciaPlaza
            {
                TarifaPlazaId = command.TarifaPlazaId,
                VigenciaAjustada = nuevaVigencia,
                VigenciaOriginal = vigenciaSapOriginal == default ? null : vigenciaSapOriginal,
                Motivo = string.IsNullOrWhiteSpace(command.Motivo) ? null : command.Motivo.Trim(),
                AjustadoPor = usuarioActual.Id,
            });
        }

        auditoria.Registrar(usuarioActual.Id, "tarifas_plaza_vigencia", Guid.Empty, "ajustar",
            $"{{\"tarifa\":{command.TarifaPlazaId},\"vigencia\":\"{nuevaVigencia:yyyy-MM-dd}\"}}");
        // Extender una vigencia puede resolver líneas que estaban esperando esta tarifa.
        // Va por el Outbox, antes del commit: si el ajuste no confirma, no se dispara un
        // recosteo por una tarifa que nunca se extendió.
        await publicador.PublicarAsync(
            new clsTarifasActualizadas("Ajuste de vigencia de tarifa de plaza", clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        ajustes.InvalidarCache();
        return Unit.Value;
    }
}
