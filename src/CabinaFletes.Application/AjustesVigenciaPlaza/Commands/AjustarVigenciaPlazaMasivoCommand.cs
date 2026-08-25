using CabinaFletes.Application.AjustesVigenciaPlaza.Dtos;
using CabinaFletes.Application.AjustesVigenciaPlaza.Ports;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Costeo.Entidades;

namespace CabinaFletes.Application.AjustesVigenciaPlaza.Commands;

/// <summary>
/// Ajuste MASIVO de vigencia sobre un conjunto de tarifas de plaza (por Id). Modo
/// "fecha": fija la misma NuevaVigencia a todas. Modo "extender": suma Cantidad de
/// Unidad ("dias"|"meses") a la vigencia efectiva de cada una.
/// </summary>
public sealed record AjustarVigenciaPlazaMasivoCommand(
    IReadOnlyList<int> TarifaIds, string Modo, DateTime? NuevaVigencia, int? Cantidad, string? Unidad, string? Motivo)
    : ICommand<AjusteVigenciaResultDto>;

public sealed class AjustarVigenciaPlazaMasivoCommandHandler(
    IAjustesVigenciaPlazaRepository ajustes,
    IVigenciaSapPlazaPort vigenciaSap,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<AjustarVigenciaPlazaMasivoCommand, AjusteVigenciaResultDto>
{
    public async Task<AjusteVigenciaResultDto> Handle(AjustarVigenciaPlazaMasivoCommand command, CancellationToken ct = default)
    {
        var ids = command.TarifaIds.Distinct().ToList();
        var modo = command.Modo.Trim().ToLowerInvariant();

        if (modo == "fecha" && command.NuevaVigencia is null)
            throw new ValidationFailedException("FECHA_REQUERIDA", "En modo 'fecha' debe indicar la nueva vigencia.");
        if (modo == "extender")
        {
            if (command.Cantidad is not > 0)
                throw new ValidationFailedException("CANTIDAD_INVALIDA", "En modo 'extender' la cantidad debe ser mayor a 0.");
            if (string.IsNullOrWhiteSpace(command.Unidad))
                throw new ValidationFailedException("UNIDAD_REQUERIDA", "En modo 'extender' debe indicar la unidad (dias|meses).");
        }

        var existentes = await ajustes.ObtenerPorTarifasAsync(ids, ct);
        var porId = existentes.ToDictionary(kv => kv.Key, kv => kv.Value);
        // SAP solo para los ids SIN override (base de extensión / vigencia original de
        // referencia); evita consultar el DW cuando ya hay ajuste (y no bloquear si está caído).
        var idsSinOverride = ids.Where(id => !porId.ContainsKey(id)).ToList();
        var vigenciasSap = await vigenciaSap.VigenciasAsync(idsSinOverride, ct);

        var aplicados = 0;
        foreach (var id in ids)
        {
            DateTime? nueva;
            if (modo == "fecha")
            {
                nueva = command.NuevaVigencia!.Value.Date;
            }
            else
            {
                DateTime? baseVig = porId.GetValueOrDefault(id)?.VigenciaAjustada;
                if (baseVig is null && vigenciasSap.TryGetValue(id, out var sapVig)) baseVig = sapVig;
                if (baseVig is null) continue; // sin base conocida: no se puede extender
                nueva = command.Unidad!.Trim().ToLowerInvariant() == "meses"
                    ? baseVig.Value.AddMonths(command.Cantidad!.Value)
                    : baseVig.Value.AddDays(command.Cantidad!.Value);
            }

            if (porId.TryGetValue(id, out var existente))
            {
                existente.VigenciaAjustada = nueva!.Value.Date;
                if (!string.IsNullOrWhiteSpace(command.Motivo)) existente.Motivo = command.Motivo.Trim();
                existente.AjustadoPor = usuarioActual.Id;
            }
            else
            {
                var nuevo = new Domain.Costeo.Entidades.AjusteVigenciaPlaza
                {
                    TarifaPlazaId = id,
                    VigenciaAjustada = nueva!.Value.Date,
                    VigenciaOriginal = vigenciasSap.TryGetValue(id, out var sap) ? sap : null,
                    Motivo = string.IsNullOrWhiteSpace(command.Motivo) ? null : command.Motivo.Trim(),
                    AjustadoPor = usuarioActual.Id,
                };
                ajustes.Agregar(nuevo);
                porId[id] = nuevo;
            }
            aplicados++;
        }

        auditoria.Registrar(usuarioActual.Id, "tarifas_plaza_vigencia", Guid.Empty, "ajustar_masivo",
            $"{{\"modo\":\"{modo}\",\"solicitadas\":{ids.Count},\"aplicadas\":{aplicados}}}");
        await uow.SaveChangesAsync(ct);
        ajustes.InvalidarCache();
        return new AjusteVigenciaResultDto(aplicados, ids.Count);
    }
}
