using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.ReglasSoloSicetac.Dtos;
using CabinaFletes.Application.ReglasSoloSicetac.Ports;

namespace CabinaFletes.Application.ReglasSoloSicetac.Commands;

/// <summary>Carga masiva de reglas "solo SICE-TAC" desde un Excel (.xlsx) ya validado por Api.</summary>
public sealed record ImportarReglasSoloSicetacExcelCommand(Stream Archivo) : ICommand<ImportarReglasResultDto>;

public sealed class ImportarReglasSoloSicetacExcelCommandHandler(
    IExcelReglasSoloSicetacPort excel, IReglasSoloSicetacRepository reglas, IUnitOfWork uow)
    : ICommandHandler<ImportarReglasSoloSicetacExcelCommand, ImportarReglasResultDto>
{
    public async Task<ImportarReglasResultDto> Handle(ImportarReglasSoloSicetacExcelCommand command, CancellationToken ct = default)
    {
        var filas = excel.Leer(command.Archivo);

        // Claves ya existentes en BD; el HashSet detecta también duplicados DENTRO del archivo.
        var claves = await reglas.ClavesOrigenDestinoTipoVehiculoAsync(ct);
        var yaVistas = new HashSet<string>(claves);

        int leidas = 0, creadas = 0, duplicadas = 0, invalidas = 0;
        var nuevas = new List<Domain.Entities.ReglaSoloSicetac>();

        foreach (var fila in filas)
        {
            var o = ReglaSoloSicetacTexto.Normalizar(fila.Origen);
            var d = ReglaSoloSicetacTexto.Normalizar(fila.Destino);
            var v = ReglaSoloSicetacTexto.Normalizar(fila.TipoVehiculo);
            if (o.Length == 0 && d.Length == 0 && v.Length == 0) continue; // fila vacía: se ignora
            leidas++;
            if (o.Length == 0 || d.Length == 0 || v.Length == 0) { invalidas++; continue; }
            if (!yaVistas.Add($"{o}|{d}|{v}")) { duplicadas++; continue; }
            nuevas.Add(new Domain.Entities.ReglaSoloSicetac { Origen = o, Destino = d, TipoVehiculo = v });
            creadas++;
        }

        if (nuevas.Count > 0)
        {
            reglas.AgregarRango(nuevas);
            await uow.SaveChangesAsync(ct);
        }
        return new ImportarReglasResultDto(leidas, creadas, duplicadas, invalidas);
    }
}
