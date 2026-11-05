namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Qué vehículo lleva un contenedor de 20 o 40 pies según su peso: un rango por tamaño y vehículo, con el peso máximo (kg).
/// El vehículo es el de NEXO (<see cref="clsMapeoVehiculoTarifario.TipoVehiculo"/>); para SICE-TAC o plaza se traduce con el mapeo.
/// </summary>
public class clsRangoPesoContenedor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tamaño del contenedor en pies: 20 o 40.</summary>
    public int Contenedor { get; set; }

    public string TipoVehiculo { get; set; } = null!;

    /// <summary>Peso máximo del contenedor (kg) que puede llevar ese vehículo.</summary>
    public int PesoMaximo { get; set; }

    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
