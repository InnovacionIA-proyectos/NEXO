namespace Nexo.Application.Common.Dtos;

/// <summary>
/// Envoltorio de paginación compartido por los endpoints de listado nuevos
/// (S4-HU-02, issue #166) — mismo shape que ya usa `/api/servicios-conexos`
/// (`clsPaginaServiciosDto`): `{ items, total }`, sin `page`/`pageSize` en la
/// respuesta (el cliente ya los conoce, los mandó él). Genérico para no
/// duplicar el mismo record por cada endpoint que se pagine; `servicios-conexos`
/// no se migra a este tipo para no tocar un contrato ya en producción sin
/// necesidad.
/// </summary>
public record clsPaginaDto<T>(IReadOnlyList<T> Items, int Total);
