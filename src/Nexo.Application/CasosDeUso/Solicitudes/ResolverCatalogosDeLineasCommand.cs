using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Una línea del formulario de texto libre, antes de tener RutaId/ConfigVehiculoId.</summary>
public sealed record ResolverCatalogoLineaInput(string Origen, string Destino, string? TipoVehiculo, string? Carroceria);

/// <summary>Los IDs resueltos para una línea, en el mismo orden en que se enviaron.</summary>
public sealed record CatalogoLineaResueltoDto(
    int Indice, Guid RutaId, Guid? ConfigVehiculoId, bool RutaCreada, bool VehiculoCreado);

public sealed record ResolverCatalogosDeLineasCommand(IReadOnlyList<ResolverCatalogoLineaInput> Lineas)
    : ICommand<IReadOnlyList<CatalogoLineaResueltoDto>>;

/// <summary>
/// Resuelve texto libre (origen/destino/tipo de vehículo) a RutaId/ConfigVehiculoId,
/// creando lo que falte en el catálogo — SIN crear ninguna Solicitud.
///
/// Antes esta resolución solo existía dentro de CapturarSolicitudCommand, mezclada con
/// "crear la solicitud completa". lineas:bulk (CargarLineasBulkCommand) YA exige estos IDs
/// resueltos y no los resuelve por su cuenta: sin este caso de uso, un formulario de texto
/// libre no tenía cómo alimentarlo. Issue #131.
/// </summary>
public sealed class ResolverCatalogosDeLineasCommandHandler(
    IRutasCatalogoPort rutasCatalogo, IConfiguracionesVehiculoPort vehiculosCatalogo, IUnitOfWork uow)
    : ICommandHandler<ResolverCatalogosDeLineasCommand, IReadOnlyList<CatalogoLineaResueltoDto>>
{
    public async Task<IReadOnlyList<CatalogoLineaResueltoDto>> Handle(
        ResolverCatalogosDeLineasCommand command, CancellationToken ct = default)
    {
        if (command.Lineas.Count == 0)
            throw new ValidationFailedException("SIN_LINEAS", "Debe enviar al menos una línea a resolver.");

        var rutas = await rutasCatalogo.ListarAsync(ct);
        var rutasPorClave = rutas.GroupBy(r => ResolucionDeCatalogos.ClaveRuta(r.Origen, r.Destino))
            .ToDictionary(g => g.Key, g => g.First());

        var vehiculos = await vehiculosCatalogo.ListarAsync(ct);
        var vehiculosPorClave = vehiculos.GroupBy(v => ResolucionDeCatalogos.ClaveVehiculo(v.TipoVehiculo, v.Carroceria))
            .ToDictionary(g => g.Key, g => g.First());

        var resultado = new List<CatalogoLineaResueltoDto>(command.Lineas.Count);
        for (var i = 0; i < command.Lineas.Count; i++)
        {
            var lr = command.Lineas[i];
            if (string.IsNullOrWhiteSpace(lr.Origen) || string.IsNullOrWhiteSpace(lr.Destino))
                throw new ValidationFailedException(
                    "ORIGEN_DESTINO_REQUERIDOS", $"La línea {i + 1} necesita origen y destino.");

            var (ruta, rutaCreada) = ResolucionDeCatalogos.ResolverRuta(lr.Origen, lr.Destino, rutasPorClave, rutasCatalogo);
            var (vehiculo, vehiculoCreado) = ResolucionDeCatalogos.ResolverVehiculo(
                lr.TipoVehiculo, lr.Carroceria, vehiculosPorClave, vehiculosCatalogo);

            resultado.Add(new CatalogoLineaResueltoDto(i, ruta.Id, vehiculo?.Id, rutaCreada, vehiculoCreado));
        }

        await uow.SaveChangesAsync(ct);
        return resultado;
    }
}
