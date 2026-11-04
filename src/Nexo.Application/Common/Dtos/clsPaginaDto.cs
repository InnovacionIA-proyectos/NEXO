namespace Nexo.Application.Common.Dtos;

/// <summary>Paginación `{ items, total }` compartida por los listados; sin page/pageSize porque el cliente ya los conoce.</summary>
public record clsPaginaDto<T>(IReadOnlyList<T> Items, int Total);
