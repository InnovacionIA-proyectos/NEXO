using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Carga masiva de reglas "solo SICE-TAC" desde un Excel (.xlsx) ya validado por Api.</summary>
public sealed record clsImportarReglasSoloSicetacExcelCommand(Stream Archivo) : ICommand<clsImportarReglasResultDto>;

public sealed class clsImportarReglasSoloSicetacExcelCommandHandler(
    IExcelReglasSoloSicetacPort excel, IReglasSoloSicetacRepository reglas, IUnitOfWork uow)
    : ICommandHandler<clsImportarReglasSoloSicetacExcelCommand, clsImportarReglasResultDto>
{
    public async Task<clsImportarReglasResultDto> Handle(clsImportarReglasSoloSicetacExcelCommand command, CancellationToken ct = default)
    {
        var filas = excel.Leer(command.Archivo);

        // Claves ya existentes en BD; el HashSet detecta también duplicados DENTRO del archivo.
        var claves = await reglas.ClavesOrigenDestinoTipoVehiculoAsync(ct);
        var yaVistas = new HashSet<string>(claves);

        int leidas = 0, creadas = 0, duplicadas = 0, invalidas = 0;
        var nuevas = new List<Domain.Costeo.Entidades.clsReglaSoloSicetac>();

        foreach (var fila in filas)
        {
            var o = clsReglaSoloSicetacTexto.Normalizar(fila.Origen);
            var d = clsReglaSoloSicetacTexto.Normalizar(fila.Destino);
            var v = clsReglaSoloSicetacTexto.Normalizar(fila.TipoVehiculo);
            if (o.Length == 0 && d.Length == 0 && v.Length == 0) continue; // fila vacía: se ignora
            leidas++;
            if (o.Length == 0 || d.Length == 0 || v.Length == 0) { invalidas++; continue; }
            if (!yaVistas.Add($"{o}|{d}|{v}")) { duplicadas++; continue; }
            nuevas.Add(new Domain.Costeo.Entidades.clsReglaSoloSicetac { Origen = o, Destino = d, TipoVehiculo = v });
            creadas++;
        }

        if (nuevas.Count > 0)
        {
            reglas.AgregarRango(nuevas);
            await uow.SaveChangesAsync(ct);
        }
        return new clsImportarReglasResultDto(leidas, creadas, duplicadas, invalidas);
    }
}
