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
/// Una línea concreta que espera a que el Administrador de Tarifas actúe.
///
/// Esto es lo que hace útil el aviso. Un contador dice "3 pendientes" y nadie sabe qué
/// hacer; esto dice qué línea y qué le falta, y con eso el comercial sabe a quién ir a
/// buscar y el administrador sabe qué cargar.
/// </summary>
public record EsperaDeTarifaDto(Guid LineaId, string Motivo, DateTimeOffset EsperandoDesde);

/// <summary>
/// Estado consultable de un trabajo. Es la consulta de respaldo del contrato: quien se
/// reconecta reconstruye todo con esta sola llamada, sin haber recibido ningún aviso.
/// </summary>
public record EstadoTrabajoDto(
    Guid TrabajoId,
    string Estado,
    AvanceTrabajoDto Avance,
    DateTimeOffset SolicitadoEn,
    DateTimeOffset? TerminadoEn,
    IReadOnlyList<EsperaDeTarifaDto> EsperandoTarifa);
