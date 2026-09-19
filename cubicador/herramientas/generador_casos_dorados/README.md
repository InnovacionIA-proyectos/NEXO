# Generador de casos dorados (S2-HU-07.3)

Herramienta de un solo propósito: corre el motor de cubicación **C# real**
de `cabinaFletes` (no una reescritura, no una descripción — las clases
compiladas tal cual) para cada uno de los 26 "casos dorados" de
`cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs`, y graba su
salida exacta en JSON. Esos 26 archivos (`../../tests/paridad/fixtures_dorados/`)
son el "esperado" contra el que `tests/paridad/test_paridad_casos_dorados.py`
compara el motor Python — la evidencia de paridad que pide el criterio de
cierre de la HU.

**No es parte del servicio**: no se ejecuta en producción, no se ejecuta en
CI (los fixtures ya generados sí se usan en CI, pero no requieren volver a
correr esto). Solo hace falta volver a correrlo si `MotorCubicacion3DTests.cs`
cambia (no debería — es código congelado que este servicio reemplaza) o si
se agregan casos dorados nuevos.

**No modifica `cabinaFletes` para nada** — solo lo referencia (`ProjectReference`)
para compilar contra sus clases reales y ejecutarlas en memoria. Ni un
archivo se crea, edita ni borra ahí.

## Requisitos

- SDK de .NET 8 (`dotnet --version` debe reportar `8.x`).
- El repo `cabinaFletes` clonado **como carpeta hermana de `NEXO`** — es
  decir, si este repo está en `.../Documents/NEXO`, `cabinaFletes` debe
  estar en `.../Documents/cabinaFletes`. Esa es la convención asumida por
  defecto en `GeneradorCasosDorados.csproj`.

Si tu clon de `cabinaFletes` vive en otro lado, no hace falta moverlo:
sobreescribe la ruta al compilar/correr:

```bash
dotnet run -p:NexoApiProj="C:\ruta\completa\a\cabinaFletes\src\CabinaFletes.Api\CabinaFletes.Api.csproj"
```

## Cómo correrlo

Desde esta carpeta:

```bash
dotnet run
```

Esto:
1. Compila referenciando `Nexo.Api.csproj` de verdad (primera vez
   tarda más — trae su cadena de dependencias de ASP.NET Core/EF Core solo
   para compilar, no las usa en tiempo de ejecución).
2. Ejecuta cada uno de los 26 casos con el motor C# real
   (`MotorCubicacion3D`, `Validadores`, `Orientaciones` de
   `cabinaFletes/src/CabinaFletes.Api/Services/Cubicacion3D/*`).
3. Escribe `salida/caso_NN_nombre.json` (uno por caso; ignorado por git,
   ver `.gitignore` de esta carpeta).

Para elegir otro directorio de salida: `dotnet run -- C:\otra\ruta`.

## Cómo actualizar los fixtures que usa el CI

Los JSON de `salida/` son solo un borrador local — el CI usa los que están
commiteados en `../../tests/paridad/fixtures_dorados/`. Si regeneraste
porque algo cambió de verdad (raro, ver arriba), copia el resultado ahí y
commitea:

```bash
dotnet run
cp salida/*.json ../../tests/paridad/fixtures_dorados/
cd ../..
pytest tests/paridad -v   # confirmar que los 26 siguen pasando
git add tests/paridad/fixtures_dorados/
git commit -m "chore: regenerar fixtures dorados"
```

## Qué NO hace

- No reimplementa ni reinterpreta el motor C# — llama a sus clases públicas
  tal cual (`new MotorCubicacion3D().Optimizar(...)`, etc.), exactamente
  como lo hace `MotorCubicacion3DTests.cs`. Si algún caso parece "raro" en
  el JSON generado, el lugar para investigar es el motor C# original, no
  este harness.
- No corre contra una base de datos, HTTP, ni ningún servicio — el motor es
  puro/sin estado (ver `docs/DECISIONES_TECNICAS.md` #3 del repo Cubicador),
  así que esto es una ejecución en memoria, instantánea.
