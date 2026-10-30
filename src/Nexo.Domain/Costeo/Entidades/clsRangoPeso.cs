using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Banda de peso (kg) para el formulario de Tarifas de Plaza (S4-HU-10.1) — reemplaza
/// el número libre del sistema externo. Tabla propia de Nexo (<c>tbl_pesos</c>, creada y
/// poblada a mano en S4-HU-10.1 con las 8 bandas conocidas — ver
/// docs/Database/seeder/pesos.csv), CRUD completo: no es un catálogo cerrado, el sistema
/// externo real permite peso inferior/superior libres por tarifa puntual, así que puede
/// crecer con el tiempo.
/// </summary>
public class clsRangoPeso
{
    public int Id { get; set; }

    [Required]
    public int PesoMinimo { get; set; }

    /// <summary>Null = banda abierta, sin tope superior (ej. "PBV +10500").</summary>
    public int? PesoMaximo { get; set; }
}
