using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;

/// <summary>
/// Avance de un trabajo. Los pendientes vienen desglosados porque son dos cosas
/// distintas y la pantalla dice cosas distintas con cada una.
///
/// <c>Pendientes</c> es la suma, para quien solo quiera un número. Pero quien pinta la
/// pantalla necesita el desglose: "reintentando" y "esperando al Administrador de
/// Tarifas" no se le explican igual a un comercial, y en el segundo caso hay alguien
/// concreto a quien ir a buscar.
/// </summary>
public record AvanceTrabajoDto(
    int? Total,
    int Resueltas,
    int Pendientes,
    int PendientesTecnicas,
    int RequierenTarifaManual);

/// <summary>
/// Una línea concreta que quedó sin costear, con su tipo y su porqué.
///
/// Esto es lo que hace útil el aviso. Un contador dice "3 pendientes" y nadie sabe qué
/// hacer; esto dice qué línea, qué pasó y si se va a resolver sola o si hace falta que
/// alguien actúe.
/// </summary>
public record PendienteDeCosteoDto(
    Guid LineaId, TipoPendienteDeCosteo Tipo, string Motivo, DateTimeOffset PendienteDesde);

/// <summary>
/// Estado consultable de un trabajo. Es la consulta de respaldo del contrato: quien se
/// reconecta reconstruye todo con esta sola llamada, sin haber recibido ningún aviso.
///
/// Trae <c>Secuencia</c> además del estado, y es lo que hace que reconectar funcione:
/// el cliente consulta, se queda con ese número y a partir de ahí ignora cualquier
/// aviso con un número menor o igual. Sin él, un aviso rezagado que llegue justo
/// después de la consulta pisaría datos más nuevos con datos más viejos.
/// </summary>
public record EstadoTrabajoDto(
    Guid TrabajoId,
    string Estado,
    AvanceTrabajoDto Avance,
    DateTimeOffset SolicitadoEn,
    DateTimeOffset? TerminadoEn,
    IReadOnlyList<PendienteDeCosteoDto> Pendientes,
    int Secuencia);

/// <summary>
/// Lo que viaja por el canal de avisos cuando un trabajo cambia.
///
/// Es el estado SIN la lista de pendientes, y se construye desde <see cref="EstadoTrabajoDto"/>
/// para que los campos compartidos no puedan divergir. La equivalencia que pide el
/// contrato no se consigue escribiendo dos cosas parecidas: se consigue derivando una
/// de la otra.
///
/// POR QUÉ NO LLEVA LOS PENDIENTES. Un pliego de 300 líneas con 50 pendientes pesa
/// entre 12 y 25 KB —cada motivo admite 500 caracteres— y se avisa 300 veces: son
/// varios megabytes por pliego y por cliente conectado. Solo con los contadores el
/// aviso pesa unos 200 bytes.
///
/// El reparto es ese: el aviso mueve la barra de progreso, la consulta trae el detalle
/// cuando alguien lo necesita — al terminar, o al abrir el panel de pendientes.
///
/// POR QUÉ LLEVA SECUENCIA. Que el mensaje interno solo lleve el identificador evita
/// que el aviso salga con datos viejos, pero no ordena las ENTREGAS: dos avisos que se
/// procesan a la vez pueden llegar al navegador al revés, y el más viejo dejaría la
/// barra de progreso retrocedida hasta el siguiente aviso. <c>Secuencia</c> solo crece;
/// el cliente descarta lo que llegue con un número menor o igual al último que pintó.
/// </summary>
public record AvisoDeTrabajo(
    Guid TrabajoId,
    string Estado,
    AvanceTrabajoDto Avance,
    DateTimeOffset? TerminadoEn,
    int Secuencia)
{
    public static AvisoDeTrabajo Desde(EstadoTrabajoDto estado) =>
        new(estado.TrabajoId, estado.Estado, estado.Avance, estado.TerminadoEn, estado.Secuencia);
}
