using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Costeo.Entidades;

/// <summary>Banda de peso (kg) de las tarifas de plaza. CRUD completo: no es un catálogo cerrado y puede crecer.</summary>
public class clsRangoPeso
{
    public int Id { get; set; }

    [Required]
    public int PesoMinimo { get; set; }

    /// <summary>Null = banda abierta, sin tope superior (ej. "PBV +10500").</summary>
    public int? PesoMaximo { get; set; }
}
