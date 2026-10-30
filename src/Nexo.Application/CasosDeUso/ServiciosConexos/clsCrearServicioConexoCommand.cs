using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>
/// Origen/Destino/Puerto/Ruta/TipoMaquina son opcionales: cada tipo de servicio usa solo
/// el subconjunto que le corresponde (Almacenamiento → Origen; ITR → Origen+Puerto+
/// TipoMaquina; adicional por ruta → Ruta; adicional por origen-destino → Origen+Destino;
/// valor único → ninguna) — las que no vienen se normalizan a "NO APLICA" (S4-HU-15.1/#223).
/// </summary>
public sealed record clsCrearServicioConexoCommand(
    string CodigoServicio, string TipoServicio, string? Origen, string? Destino, string? Puerto,
    string? Ruta, string? TipoMaquina, decimal CostoUnitario, string Unidad, DateOnly Vigencia,
    string? Categoria, bool Activo, decimal? PrecioVenta = null)
    : ICommand<clsServicioConexoDto>;

public sealed class clsCrearServicioConexoCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsCrearServicioConexoCommand, clsServicioConexoDto>
{
    public async Task<clsServicioConexoDto> Handle(clsCrearServicioConexoCommand command, CancellationToken ct = default)
    {
        var codigo = command.CodigoServicio.Trim();
        var tipo = command.TipoServicio.Trim();
        if (codigo.Length == 0 || tipo.Length == 0)
            throw new clsValidationFailedException("SERVICIO_INVALIDO", "Código y tipo de servicio son obligatorios.");

        var origen = Normalizar(command.Origen);
        var destino = Normalizar(command.Destino);
        var puerto = Normalizar(command.Puerto);
        var ruta = Normalizar(command.Ruta);
        var tipoMaquina = Normalizar(command.TipoMaquina);

        if (await repo.ExisteDimensionesAsync(codigo, origen, destino, puerto, ruta, tipoMaquina, null, ct))
            throw new clsConflictException("SERVICIO_DUPLICADO",
                $"Ya existe un servicio '{codigo}' con esa misma combinación de origen/destino/puerto/ruta/tipo de máquina.");

        var servicio = new clsServicioConexo
        {
            CodigoServicio = codigo,
            TipoServicio = tipo,
            Origen = origen,
            Destino = destino,
            Puerto = puerto,
            Ruta = ruta,
            TipoMaquina = tipoMaquina,
            CostoUnitario = command.CostoUnitario,
            PrecioVenta = command.PrecioVenta is { } pv ? Math.Round(pv, 2, MidpointRounding.AwayFromZero) : null,
            Unidad = command.Unidad.Trim(),
            Vigencia = command.Vigencia,
            Categoria = clsServicioConexoCategoria.Validada(command.Categoria),
            Activo = command.Activo,
            ActualizadoPor = usuarioActual.Id,
        };
        repo.Agregar(servicio);
        await uow.SaveChangesAsync(ct);
        return servicio.ToDto();
    }

    private static string Normalizar(string? dimension) =>
        string.IsNullOrWhiteSpace(dimension) ? clsServicioConexo.NoAplica : dimension.Trim();
}
