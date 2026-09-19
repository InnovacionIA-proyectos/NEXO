using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Una línea del formulario de texto libre, antes de tener RutaId/ConfigVehiculoId.</summary>
public sealed record clsResolverCatalogoLineaInput(string Origen, string Destino, string? TipoVehiculo, string? Carroceria);

/// <summary>Los IDs resueltos para una línea, en el mismo orden en que se enviaron.</summary>
public sealed record clsCatalogoLineaResueltoDto(
    int Indice, Guid RutaId, Guid? ConfigVehiculoId, bool RutaCreada, bool clsVehiculoCreado);

public sealed record clsResolverCatalogosDeLineasCommand(IReadOnlyList<clsResolverCatalogoLineaInput> Lineas)
    : ICommand<IReadOnlyList<clsCatalogoLineaResueltoDto>>;

/// <summary>
/// Resuelve texto libre (origen/destino/tipo de vehículo) a RutaId/ConfigVehiculoId,
/// creando lo que falte en el catálogo — SIN crear ninguna clsSolicitud.
///
/// Antes esta resolución solo existía dentro de clsCapturarSolicitudCommand, mezclada con
/// "crear la solicitud completa". lineas:bulk (clsCargarLineasBulkCommand) YA exige estos IDs
/// resueltos y no los resuelve por su cuenta: sin este caso de uso, un formulario de texto
/// libre no tenía cómo alimentarlo. Issue #131.
/// </summary>
public sealed class clsResolverCatalogosDeLineasCommandHandler(
    IRutasCatalogoPort rutasCatalogo, IConfiguracionesVehiculoPort vehiculosCatalogo, IUnitOfWork uow)
    : ICommandHandler<clsResolverCatalogosDeLineasCommand, IReadOnlyList<clsCatalogoLineaResueltoDto>>
{
    public async Task<IReadOnlyList<clsCatalogoLineaResueltoDto>> Handle(
        clsResolverCatalogosDeLineasCommand command, CancellationToken ct = default)
    {
        if (command.Lineas.Count == 0)
            throw new clsValidationFailedException("SIN_LINEAS", "Debe enviar al menos una línea a resolver.");

        var rutas = await rutasCatalogo.ListarAsync(ct);
        var rutasPorClave = rutas.GroupBy(r => clsResolucionDeCatalogos.ClaveRuta(r.Origen, r.Destino))
            .ToDictionary(g => g.Key, g => g.First());

        var vehiculos = await vehiculosCatalogo.ListarAsync(ct);
        var vehiculosPorClave = vehiculos.GroupBy(v => clsResolucionDeCatalogos.ClaveVehiculo(v.TipoVehiculo, v.Carroceria))
            .ToDictionary(g => g.Key, g => g.First());

        var resultado = new List<clsCatalogoLineaResueltoDto>(command.Lineas.Count);
        for (var i = 0; i < command.Lineas.Count; i++)
        {
            var lr = command.Lineas[i];
            if (string.IsNullOrWhiteSpace(lr.Origen) || string.IsNullOrWhiteSpace(lr.Destino))
                throw new clsValidationFailedException(
                    "ORIGEN_DESTINO_REQUERIDOS", $"La línea {i + 1} necesita origen y destino.");

            var (ruta, rutaCreada) = clsResolucionDeCatalogos.ResolverRuta(lr.Origen, lr.Destino, rutasPorClave, rutasCatalogo);
            var (vehiculo, vehiculoCreado) = clsResolucionDeCatalogos.ResolverVehiculo(
                lr.TipoVehiculo, lr.Carroceria, vehiculosPorClave, vehiculosCatalogo);

            resultado.Add(new clsCatalogoLineaResueltoDto(i, ruta.Id, vehiculo?.Id, rutaCreada, vehiculoCreado));
        }

        await uow.SaveChangesAsync(ct);
        return resultado;
    }
}
