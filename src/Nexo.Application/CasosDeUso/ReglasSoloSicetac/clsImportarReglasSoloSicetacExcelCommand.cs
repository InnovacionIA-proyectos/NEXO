using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Carga masiva de reglas "solo SICE-TAC" desde un Excel (.xlsx) ya validado por Api.</summary>
public sealed record clsImportarReglasSoloSicetacExcelCommand(Stream Archivo) : ICommand<clsImportarReglasResultDto>;

public sealed class clsImportarReglasSoloSicetacExcelCommandHandler(
    IExcelReglasSoloSicetacPort excel, IReglasSoloSicetacRepository reglas,
    ITiposVehiculoPort tiposVehiculo, IUnitOfWork uow)
    : ICommandHandler<clsImportarReglasSoloSicetacExcelCommand, clsImportarReglasResultDto>
{
    private const int TopeErrores = 20;

    public async Task<clsImportarReglasResultDto> Handle(clsImportarReglasSoloSicetacExcelCommand command, CancellationToken ct = default)
    {
        var filas = excel.Leer(command.Archivo);

        // Claves ya existentes en BD; el HashSet detecta también duplicados DENTRO del archivo.
        var claves = await reglas.ClavesOrigenDestinoTipoVehiculoAsync(ct);
        var yaVistas = new HashSet<string>(claves);
        var codigosVehiculo = new HashSet<string>(
            (await tiposVehiculo.ListarAsync(ct)).Select(v => clsReglaSoloSicetacTexto.Normalizar(v.Codigo)));

        int leidas = 0, creadas = 0, duplicadas = 0, invalidas = 0, fila = 1;
        var nuevas = new List<clsReglaSoloSicetac>();
        var errores = new List<string>();

        foreach (var f in filas)
        {
            fila++;
            var o = clsReglaSoloSicetacTexto.Normalizar(f.Origen);
            var d = clsReglaSoloSicetacTexto.Normalizar(f.Destino);
            var v = clsReglaSoloSicetacTexto.Normalizar(f.TipoVehiculo);
            if (o.Length == 0 && d.Length == 0 && v.Length == 0) continue; // fila vacía: se ignora, no cuenta como leída

            leidas++;
            if (o.Length == 0 || d.Length == 0 || v.Length == 0)
            {
                invalidas++;
                if (errores.Count < TopeErrores) errores.Add($"Fila {fila}: origen, destino y tipo de vehículo son obligatorios.");
                continue;
            }
            if (!codigosVehiculo.Contains(v))
            {
                invalidas++;
                if (errores.Count < TopeErrores) errores.Add($"Fila {fila}: '{f.TipoVehiculo}' no es un tipo de vehículo del catálogo.");
                continue;
            }
            if (!yaVistas.Add($"{o}|{d}|{v}"))
            {
                duplicadas++;
                if (errores.Count < TopeErrores) errores.Add($"Fila {fila}: ya existe una regla para {o} → {d} ({v}).");
                continue;
            }
            nuevas.Add(new clsReglaSoloSicetac { Origen = o, Destino = d, TipoVehiculo = v });
            creadas++;
        }

        if (nuevas.Count > 0)
        {
            reglas.AgregarRango(nuevas);
            await uow.SaveChangesAsync(ct);
        }
        return new clsImportarReglasResultDto(leidas, creadas, duplicadas, invalidas, errores);
    }
}
