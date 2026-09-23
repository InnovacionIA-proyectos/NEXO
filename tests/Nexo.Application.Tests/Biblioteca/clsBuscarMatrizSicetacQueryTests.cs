using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Moq;

namespace Nexo.Application.Tests.Biblioteca;

/// <summary>
/// Issue #156: la matriz SICE-TAC (~2,15 M de filas medidas en vivo) truncaba en
/// las primeras "top" filas sin ninguna forma de pedir la página siguiente,
/// aunque `total` ya viajaba en la respuesta. Este test protege el cableado
/// nuevo de "page" desde el query hasta el puerto — el Skip/Take real vive en
/// clsBibliotecaSicetacAdapter (Infrastructure, contra el Data Warehouse externo,
/// sin cobertura de test por no tener esa base disponible aquí).
/// </summary>
public class clsBuscarMatrizSicetacQueryTests
{
    private readonly Mock<IBibliotecaSicetacPort> _sicetac = new();

    private clsBuscarMatrizSicetacQueryHandler Handler() => new(_sicetac.Object);

    [Fact]
    public async Task Reenvia_page_al_puerto()
    {
        _sicetac.Setup(s => s.BuscarAsync(
                null, null, null, null, null, null, null, 50, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsMatrizSicetacResultDto(202601, 0, []));

        await Handler().Handle(new clsBuscarMatrizSicetacQuery(null, null, null, null, null, null, null, 50, 3));

        _sicetac.Verify(s => s.BuscarAsync(
            null, null, null, null, null, null, null, 50, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_page_explicito_pide_la_primera_pagina()
    {
        _sicetac.Setup(s => s.BuscarAsync(
                null, null, null, null, null, null, null, 50, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsMatrizSicetacResultDto(202601, 0, []));

        await Handler().Handle(new clsBuscarMatrizSicetacQuery(null, null, null, null, null, null, null, 50));

        _sicetac.Verify(s => s.BuscarAsync(
            null, null, null, null, null, null, null, 50, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
