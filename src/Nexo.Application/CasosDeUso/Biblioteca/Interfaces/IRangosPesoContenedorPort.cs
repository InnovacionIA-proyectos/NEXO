using System.Text.RegularExpressions;

namespace Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>Un rango de la tabla: el vehículo de NEXO que lleva un contenedor de <paramref name="Contenedor"/> pies hasta <paramref name="PesoMaximo"/> kg.</summary>
public sealed record clsRangoPesoContenedorDto(int Contenedor, string TipoVehiculo, int PesoMaximo);

/// <summary>
/// El vehículo que corresponde a un contenedor según su tamaño y su peso. <paramref name="TablaDisponible"/> en false significa que la tabla no se
/// pudo leer o está vacía (el script aún no se aplicó): quien llama usa entonces la regla que tenía antes. Con la tabla disponible y
/// <paramref name="TipoVehiculo"/> en null, ningún vehículo aplica (el peso supera el tope más alto de ese tamaño).
/// </summary>
public readonly record struct clsVehiculoDeContenedor(bool TablaDisponible, string? TipoVehiculo);

/// <summary>Lectura de <c>tbl_rangos_peso_contenedor</c>, que reemplaza las reglas de las tablas de SAP para escoger el vehículo de un contenedor.</summary>
public interface IRangosPesoContenedorPort
{
    Task<clsVehiculoDeContenedor> VehiculoParaAsync(int contenedorPies, decimal pesoKg, CancellationToken ct = default);
}

/// <summary>Las reglas puras del recomendador, separadas de la lectura de la tabla para poder probarlas con todos los pesos.</summary>
public static partial class clsRecomendadorDeVehiculoDeContenedor
{
    /// <summary>
    /// Entre los rangos del tamaño de contenedor, el de MENOR peso máximo que alcance para el peso. Los topes de cada tamaño van subiendo sin
    /// repetirse, así que no hace falta un peso mínimo. Por encima del tope más alto no hay vehículo (null).
    /// </summary>
    public static string? Elegir(IEnumerable<clsRangoPesoContenedorDto> rangos, int contenedorPies, decimal pesoKg) =>
        rangos.Where(r => r.Contenedor == contenedorPies && r.PesoMaximo >= pesoKg)
            .OrderBy(r => r.PesoMaximo)
            .Select(r => r.TipoVehiculo)
            .FirstOrDefault();

    [GeneratedRegex(@"(?<![0-9])(20|40)(?![0-9])")]
    private static partial Regex PiesRegex();

    /// <summary>El tamaño en pies de una clase de contenedor escrita como <c>CNT40.HC</c> o <c>CNT20.IT</c> (o "40 HC"); null si no trae 20 ni 40.</summary>
    public static int? Pies(string? claseContenedor)
    {
        if (string.IsNullOrWhiteSpace(claseContenedor)) return null;
        var m = PiesRegex().Match(claseContenedor);
        return m.Success ? int.Parse(m.Value) : null;
    }
}
