# QA — PR #415: Identificar al cliente por NIT en la importación inteligente de Excel (#410)

## Resumen

Hasta ahora, para que la importación inteligente de Excel (sugerir mapeo,
aplicar mapeo, normalizar valores, guardar/listar plantillas) usara el
diccionario o la plantilla de un cliente, había que mandar su `clienteId`
(GUID interno de NEXO). Esta PR permite identificarlo en su lugar por
**NIT** — lo único que tienen en común el portal, Cotifly y los
prospectos, que es de donde vienen los Excel.

- **`clsNit.Normalizar`**: función pura que lleva `900.123.456-7`,
  `900 123 456` y `900123456` a la misma forma comparable (`900123456`);
  descarta el dígito de verificación (lo que sigue al primer guion) y
  cualquier carácter que no sea letra o dígito.
- **`clsClienteDeImportacion`**, con dos operaciones deliberadamente
  distintas:
  - `BuscarLocalAsync`: **solo lee**, nunca crea. Usada en los endpoints de
    solo lectura/sugerencia (`sugerir-mapeo`, `aplicar-mapeo`,
    `normalizar-valores`, `GET` de plantillas) — un NIT sin cliente local
    simplemente sigue el análisis sin cliente (diccionario general, sin
    plantilla), no es un error.
  - `ObtenerOCrearAsync`: usada **solo** al guardar una plantilla
    (`clsGuardarPlantillaMapeoCommandHandler`) — ahí sí tiene sentido crear
    el cliente local si no existe (primero busca en el catálogo del DW,
    luego en prospectos), porque guardar una plantilla es una acción
    deliberada y confirmada por el usuario, no una lectura de paso.
  - Si viene `clienteId` **y** `clienteNit`, `clienteId` manda (verificado
    en el código y en los tests).
- Cambios de API: los 4 endpoints de captura ganan un parámetro
  `clienteNit` opcional, alternativo a `clienteId`, documentados en
  `CONTRATO.md`.

## Qué se verificó

1. Diff aislado de la PR (1 commit, 10 archivos).
2. Revisado línea por línea que `BuscarLocalAsync` nunca escribe (ni
   siquiera en el DW) y que `ObtenerOCrearAsync` solo se invoca desde el
   flujo de guardar plantilla, no desde ningún endpoint de solo lectura.
3. Confirmado que `clienteId` manda sobre `clienteNit` cuando vienen los
   dos, en los 4 endpoints y en el command handler.
4. Revisado `clsNit.Normalizar`: maneja los 3 formatos de ejemplo
   correctamente y descarta el dígito de verificación.
5. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes,
   ninguno nuevo de esta PR).
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **1154/1154** (18 nuevos:
   `clsNitTests.cs` + `clsClienteDeImportacionTests.cs`).
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **1084/1106**, 22 omitidas (las
   mismas 13+9 ya documentadas en las PRs #405 y #414), incluido
   `clsClienteNitImportacionApiTests.cs` (259 líneas, integración completa
   end-to-end de los 4 endpoints con NIT).

## Datos de prueba

Ninguno manual: los tests de integración limpian lo que crean (clientes
nuevos a partir de un NIT de prueba, plantillas) en su propio `finally`/`Dispose`.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes.
