namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Los límites que la captura de una línea impone a sus campos (longitudes y rangos). Son los mismos que declara el cuerpo de
/// <c>POST /api/captura-solicitudes</c>; se repiten aquí porque la importación de un Excel arma las líneas sin pasar por ese cuerpo y no puede
/// aceptar una fila que la captura luego rechazaría. Una prueba compara estos valores con los atributos del cuerpo y falla si se separan.
/// </summary>
public static class clsLimitesDeLineaDeCaptura
{
    public const int MaxCiudad = 150;
    public const int MaxTipoVehiculo = 100;
    public const int MaxCarroceria = 100;
    public const int MaxLineaNegocio = 100;
    public const int MaxClaseViaje = 50;
    public const int MaxClaseContenedor = 20;

    public const decimal MaxHoras = 240m;
    public const decimal MaxPeso = 999_999.99m;
    public const decimal MaxValor = 9_999_999_999_999.99m;

    public const int MaxDescripcionPosicion = 200;
    public const int MinCantidad = 1;
    public const int MaxCantidad = 10_000;
    public const decimal MinDimensionCm = 0.1m;
    public const decimal MaxDimensionCm = 999_999m;
    public const decimal MinPesoPosicionKg = 0.01m;
    public const decimal MaxPesoPosicionKg = 999_999.99m;
    public const int MaxCodigoTipoMercancia = 40;
}
