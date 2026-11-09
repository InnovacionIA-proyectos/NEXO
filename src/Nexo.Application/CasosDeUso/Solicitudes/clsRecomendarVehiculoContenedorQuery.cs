using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Un contenedor a recomendar: su clase (<c>CNT40.HC</c>, «40 HC») o su tamaño en pies, y el peso de la carga.</summary>
public sealed record clsRecomendacionVehiculoContenedorInput(int Indice, string? ClaseContenedor, decimal PesoKg);

/// <summary>
/// <paramref name="TipoVehiculo"/> va en null si el peso supera el tope del tamaño, si la clase no trae 20 ni 40 pies o si la tabla no está
/// disponible; <paramref name="PesoMaximoContenedor"/> es el tope más alto de ese tamaño.
/// </summary>
public sealed record clsRecomendacionVehiculoContenedorDto(
    int Indice, string? TipoVehiculo, int? ContenedorPies, bool TablaDisponible, int? PesoMaximoContenedor);

public sealed record clsRecomendarVehiculoContenedorQuery(IReadOnlyList<clsRecomendacionVehiculoContenedorInput> Lineas)
    : IQuery<IReadOnlyList<clsRecomendacionVehiculoContenedorDto>>;

/// <summary>El vehículo que el costeo escogería para cada contenedor según su tamaño y peso; es la misma regla, solo lectura.</summary>
public sealed class clsRecomendarVehiculoContenedorQueryHandler(IRangosPesoContenedorPort rangos)
    : IQueryHandler<clsRecomendarVehiculoContenedorQuery, IReadOnlyList<clsRecomendacionVehiculoContenedorDto>>
{
    public const int MaxLineas = 5000;

    public async Task<IReadOnlyList<clsRecomendacionVehiculoContenedorDto>> Handle(
        clsRecomendarVehiculoContenedorQuery query, CancellationToken ct = default)
    {
        if (query.Lineas.Count == 0)
            throw new clsValidationFailedException("SIN_LINEAS", "Debe enviar al menos una línea.");
        if (query.Lineas.Count > MaxLineas)
            throw new clsValidationFailedException("DEMASIADAS_LINEAS", $"Máximo {MaxLineas:N0} líneas por consulta.");
        var sinPeso = query.Lineas.FirstOrDefault(l => l.PesoKg <= 0);
        if (sinPeso is not null)
            throw new clsValidationFailedException("PESO_INVALIDO", $"La línea con índice {sinPeso.Indice} necesita un peso mayor que cero.");

        var topes = new Dictionary<int, int?>();
        var resultado = new List<clsRecomendacionVehiculoContenedorDto>(query.Lineas.Count);
        foreach (var l in query.Lineas)
        {
            if (clsRecomendadorDeVehiculoDeContenedor.Pies(l.ClaseContenedor) is not { } pies)
            {
                resultado.Add(new(l.Indice, null, null, true, null));
                continue;
            }

            if (!topes.TryGetValue(pies, out var tope))
                topes[pies] = tope = await rangos.PesoMaximoAsync(pies, ct);

            var vehiculo = await rangos.VehiculoParaAsync(pies, l.PesoKg, ct);
            resultado.Add(new(l.Indice, vehiculo.TipoVehiculo, pies, vehiculo.TablaDisponible, tope));
        }
        return resultado;
    }
}
