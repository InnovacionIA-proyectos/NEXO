namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>Límites de los campos de una línea de captura; se repiten de <c>POST /api/captura-solicitudes</c> porque la importación no pasa por ese cuerpo. Una prueba verifica que no se separen.</summary>
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
