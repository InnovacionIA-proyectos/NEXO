namespace Nexo.Application.CasosDeUso.RangosPeso.DTOs;

/// <summary>Banda de peso (kg) del catálogo propio de Nexo. <c>PesoMaximo</c> null = banda abierta.</summary>
public record clsRangoPesoDto(int Id, int PesoMinimo, int? PesoMaximo);
