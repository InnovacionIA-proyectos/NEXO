using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Moq;

namespace Nexo.Application.Tests.BibliotecaSapHistorico;

/// <summary>
/// Issue #156: tarifas de plaza SAP (~22,8 K filas medidas en vivo) truncaban en
/// las primeras "top" filas sin ninguna forma de pedir la página siguiente,
/// aunque `total` ya viajaba en la respuesta. Este test protege el cableado
/// nuevo de "page" desde el query hasta el puerto — el Skip/Take real vive en
/// SapHistoricoAdapter (Infrastructure, contra el datacenter corporativo
/// externo, sin cobertura de test por no tener esa base disponible aquí).
///
/// A propósito NO se agrega "page" a BuscarSapHistoricoQuery (histórico): esa
/// tabla midió 0 filas en el ambiente medido y queda fuera de alcance de este
/// fix — ver la nota de decisión en el issue.
/// </summary>
public class BuscarSapPlazaQueryTests
{
    private readonly Mock<ISapHistoricoPort> _sap = new();

    private BuscarSapPlazaQueryHandler Handler() => new(_sap.Object);

    [Fact]
    public async Task Reenvia_page_al_puerto()
    {
        _sap.Setup(s => s.BuscarPlazaAsync(
                null, null, null, null, null, 50, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapHistoricoResultDto(0, []));

        await Handler().Handle(new BuscarSapPlazaQuery(null, null, null, null, null, 50, 3));

        _sap.Verify(s => s.BuscarPlazaAsync(
            null, null, null, null, null, 50, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_page_explicito_pide_la_primera_pagina()
    {
        _sap.Setup(s => s.BuscarPlazaAsync(
                null, null, null, null, null, 50, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapHistoricoResultDto(0, []));

        await Handler().Handle(new BuscarSapPlazaQuery(null, null, null, null, null, 50));

        _sap.Verify(s => s.BuscarPlazaAsync(
            null, null, null, null, null, 50, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
