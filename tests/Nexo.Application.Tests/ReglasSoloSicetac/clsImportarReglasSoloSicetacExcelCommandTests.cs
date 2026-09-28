using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Costeo.Entidades;
using Moq;

namespace Nexo.Application.Tests.ReglasSoloSicetac;

public class clsImportarReglasSoloSicetacExcelCommandTests
{
    private readonly Mock<IExcelReglasSoloSicetacPort> _excel = new();
    private readonly Mock<IReglasSoloSicetacRepository> _reglas = new();
    private readonly Mock<ITiposVehiculoPort> _tiposVehiculo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsImportarReglasSoloSicetacExcelCommandHandler Handler() =>
        new(_excel.Object, _reglas.Object, _tiposVehiculo.Object, _uow.Object);

    private void SinReglasExistentes() =>
        _reglas.Setup(r => r.ClavesOrigenDestinoTipoVehiculoAsync(default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());

    private void ConCatalogo(params string[] codigos) =>
        _tiposVehiculo.Setup(t => t.ListarAsync(default))
            .ReturnsAsync(codigos.Select(c => new clsTipoVehiculoDto(c, c, true)).ToList());

    [Fact]
    public async Task Fila_valida_con_tipo_de_vehiculo_del_catalogo_se_crea()
    {
        SinReglasExistentes();
        ConCatalogo("3S3");
        _excel.Setup(e => e.Leer(It.IsAny<Stream>()))
            .Returns([new clsFilaReglaSoloSicetacDto("Medellín", "Bogotá", "3S3")]);
        List<clsReglaSoloSicetac>? agregadas = null;
        _reglas.Setup(r => r.AgregarRango(It.IsAny<IEnumerable<clsReglaSoloSicetac>>()))
            .Callback<IEnumerable<clsReglaSoloSicetac>>(rs => agregadas = rs.ToList());

        var result = await Handler().Handle(new clsImportarReglasSoloSicetacExcelCommand(Stream.Null));

        Assert.Equal(1, result.Creadas);
        Assert.Equal(0, result.Invalidas);
        Assert.Empty(result.Errores);
        Assert.NotNull(agregadas);
        Assert.Single(agregadas!);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Tipo_de_vehiculo_fuera_del_catalogo_se_rechaza_con_motivo_por_fila()
    {
        SinReglasExistentes();
        ConCatalogo("3S3");
        _excel.Setup(e => e.Leer(It.IsAny<Stream>()))
            .Returns([new clsFilaReglaSoloSicetacDto("Medellín", "Bogotá", "NO-EXISTE")]);

        var result = await Handler().Handle(new clsImportarReglasSoloSicetacExcelCommand(Stream.Null));

        Assert.Equal(1, result.Invalidas);
        Assert.Equal(0, result.Creadas);
        Assert.Contains(result.Errores, e => e.Contains("Fila 2") && e.Contains("NO-EXISTE"));
        _reglas.Verify(r => r.AgregarRango(It.IsAny<IEnumerable<clsReglaSoloSicetac>>()), Times.Never);
    }

    [Fact]
    public async Task Fila_con_campo_vacio_se_rechaza_con_motivo_por_fila()
    {
        SinReglasExistentes();
        ConCatalogo("3S3");
        _excel.Setup(e => e.Leer(It.IsAny<Stream>()))
            .Returns([new clsFilaReglaSoloSicetacDto("Medellín", "", "3S3")]);

        var result = await Handler().Handle(new clsImportarReglasSoloSicetacExcelCommand(Stream.Null));

        Assert.Equal(1, result.Invalidas);
        Assert.Contains(result.Errores, e => e.Contains("Fila 2") && e.Contains("obligatorios"));
    }

    [Fact]
    public async Task Fila_duplicada_contra_una_regla_ya_existente_se_cuenta_y_reporta_sin_crear()
    {
        _reglas.Setup(r => r.ClavesOrigenDestinoTipoVehiculoAsync(default))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "MEDELLIN|BOGOTA|3S3" });
        ConCatalogo("3S3");
        _excel.Setup(e => e.Leer(It.IsAny<Stream>()))
            .Returns([new clsFilaReglaSoloSicetacDto("Medellín", "Bogotá", "3S3")]);

        var result = await Handler().Handle(new clsImportarReglasSoloSicetacExcelCommand(Stream.Null));

        Assert.Equal(1, result.Duplicadas);
        Assert.Equal(0, result.Creadas);
        Assert.Contains(result.Errores, e => e.Contains("Fila 2") && e.Contains("ya existe"));
    }

    [Fact]
    public async Task Fila_completamente_vacia_se_ignora_sin_contar_como_leida()
    {
        SinReglasExistentes();
        ConCatalogo("3S3");
        _excel.Setup(e => e.Leer(It.IsAny<Stream>()))
            .Returns([new clsFilaReglaSoloSicetacDto("", "", "")]);

        var result = await Handler().Handle(new clsImportarReglasSoloSicetacExcelCommand(Stream.Null));

        Assert.Equal(0, result.Filas);
        Assert.Empty(result.Errores);
    }
}
