using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Cubicacion.Entidades;

/// <summary>
/// S5-HU-18.5/#277: proyección del catálogo de tipos de mercancía del Cubicador
/// (<c>GET /tipos-mercancia</c>, <c>cubicador/api/dtos/catalogo.py::TipoMercanciaDto</c>),
/// mantenida al día por <c>MercanciaCreada</c>/<c>MercanciaActualizada</c>/
/// <c>MercanciaEliminada</c> — mismo patrón que <see cref="PlanDeCargue.Entidades.clsVehiculoCubicacion"/>
/// para el catálogo de vehículos (S5-HU-17). Alimenta el desplegable de tipo de mercancía
/// en Captura (S5-HU-18.2/#274).
///
/// <see cref="Id"/> es el id tal como lo asigna el Cubicador (string, no necesariamente
/// UUID), así que NEXO no le inventa un id propio. S5-HU-17.7: en el contrato de cubicación
/// (<c>POST /api/cubicacion</c>, §3.14 de CONTRATO.md) ya NO viaja el id sino el
/// <see cref="CodigoMercancia"/> (<c>codigoMercancia</c>), que es también lo que la Captura persiste
/// (<c>CodigoTipoMercancia</c>: estable, sobrevive un reseed y es legible en logs). La entidad conserva
/// los dos campos y sigue alimentando el desplegable; la respuesta del Cubicador también lleva los dos.
/// <see cref="CarroceriaPrincipal"/>/<see cref="CarroceriaSecundaria"/> llegan ya resueltas
/// a CÓDIGO (no id) desde el evento — igual que en la respuesta de
/// <c>GET /tipos-mercancia</c>.
/// </summary>
public class clsTipoMercanciaCatalogo
{
    [MaxLength(64)]
    public string Id { get; set; } = null!;

    [Required, MaxLength(40)]
    public string CodigoMercancia { get; set; } = null!;

    [Required, MaxLength(150)]
    public string NombreMercancia { get; set; } = null!;

    [MaxLength(100)]
    public string Categoria { get; set; } = null!;

    public bool Cubicable { get; set; }
    public bool CondicionadaCubicacion { get; set; }

    [MaxLength(40)]
    public string? CarroceriaPrincipal { get; set; }

    [MaxLength(40)]
    public string? CarroceriaSecundaria { get; set; }

    public bool CarroceriaSecundariaCondicionada { get; set; }

    /// <summary>NINGUNO | REFRIGERADO | VENTILADO_CONDICIONAL | CLIMATIZADO_CONDICIONAL | REFRIGERADO_O_CLIMATIZADO | VENTILADO_O_REFRIGERADO</summary>
    [Required, MaxLength(40)]
    public string ControlAmbientalRequerido { get; set; } = null!;

    public bool Apilable { get; set; }
    public bool Fragil { get; set; }
    public bool RotacionLibre { get; set; }

    [MaxLength(500)]
    public string? ComentarioCondicion { get; set; }

    /// <summary>
    /// S5-HU-18.5/#277: los GET del Cubicador devuelven también las filas inactivas a
    /// propósito (su pantalla de administración las necesita) — la proyección de NEXO hace
    /// lo mismo: guarda la fila completa, el desplegable de Captura es quien filtra por
    /// Activo, no la proyección.
    /// </summary>
    public bool Activo { get; set; }

    /// <summary>Concurrencia optimista del lado del Cubicador — informativa acá, NEXO no escribe esta tabla.</summary>
    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
