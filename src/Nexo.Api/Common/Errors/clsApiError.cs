namespace Nexo.Api.Common.Errors;

/// <summary>Forma estándar de toda respuesta de error del API.</summary>
public record clsApiError(string Code, string Message, DateTimeOffset Timestamp, object? Details = null);
