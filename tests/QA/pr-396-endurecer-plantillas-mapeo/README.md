# QA — PR #396: Endurecer las plantillas de mapeo por cliente (#365)

## Resumen

Endurece lo agregado en #394 en cuatro frentes:

1. **Firma de encabezados más robusta** (`clsFirmaDeEncabezados`): ahora quita
   caracteres invisibles (ancho cero, BOM) y normaliza cualquier tipo de
   espacio en blanco (tab, salto de línea, espacio de no separación) antes de
   calcular el SHA-256. Las columnas vacías **al final** del archivo ya no
   cuentan para la firma (Excel suele reportar columnas de más que nadie
   vio); una vacía **en el medio** sí sigue contando (ocupa su posición).
2. **Versionado del JSON de columnas** (`{ "version": 1, "columnas": [...] }`):
   una plantilla de una versión que el código no reconoce se trata como
   vieja/no usable.
3. **Plantilla obsoleta**: si lo guardado apunta a un campo o transformación
   que ya no existe en las listas vigentes, o ya no pasa las validaciones de
   `aplicar-mapeo`, no se usa — se sugiere como si no hubiera plantilla y se
   agrega el aviso `DudaPlantillaObsoleta` en `dudas` para que el usuario la
   reconfirme.
4. **Corrige la condición de carrera de doble confirmación** (el mismo tipo
   de hallazgo reportado como no bloqueante en PR #394 / issue #393):
   `clsGuardarPlantillaMapeoCommandHandler` ahora captura la violación del
   índice único `(ClienteId, FirmaEncabezados)` (SQL Server 2601/2627) vía
   `IPlantillasMapeoRepository.EsChoqueDeFirma`, descarta lo pendiente
   (`DescartarCambiosPendientes` → `ChangeTracker.Clear()`) y reintenta una
   vez como reemplazo — en vez de devolver `500`.

## Qué se verificó

1. Diff aislado (`git diff origin/QA...origin/365-endurecer-plantillas`):
   solo los 8 archivos relacionados a plantillas de mapeo, sin tocar nada
   fuera de ese alcance.
2. `clsFirmaDeEncabezados.Clave`/`Claves`/`Cantidad` revisado línea por
   línea: regex de invisibles (`\p{Cf}`) y de blancos (`\s+`) correctas;
   recorte de vacíos al final (`while (claves.Count > 0 && claves[^1].Length == 0)`)
   solo al final, no en el medio — confirmado contra `CONTRATO.md`.
3. `DesdePlantilla` revisado: valida cada columna guardada contra
   `clsTerminoCliente.Canonico` (campo/transformación vigentes) y contra
   `clsAplicarMapeoExcelQueryHandler.ValidarColumnas` (reglas completas del
   mapeo) antes de usarla; cualquier fallo marca `Obsoleta = true` y cae al
   flujo normal (diccionario/IA) con el aviso en `dudas`.
4. **Corrección de la condición de carrera** revisada en detalle:
   `EsChoqueDeFirma` distingue el error correcto (2601/2627, índices/
   restricciones únicas) de cualquier otro `DbUpdateException`; el reintento
   tiene guarda `!reintento` para no reintentar infinitamente ante un choque
   repetido (caso extremo, aceptable). Cubierto por los tests nuevos
   `Dos_confirmaciones_del_mismo_formato_a_la_vez_no_fallan_la_segunda_reemplaza`
   y `Un_error_que_no_es_el_choque_de_firma_no_se_reintenta`.
5. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes de
   `QA`, no de esta PR).
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **866/866** (23 tests
   nuevos sobre los 843 de #394: firma endurecida, versión, plantilla
   obsoleta, choque de firma).
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **858/868**, 10 omitidas esperadas
   (mismas de siempre: correo real, Cubicador E2E, paginación SAP).

## Resultado

Verificación técnica completa, sin hallazgos. Esta PR además **resuelve** el
hallazgo no bloqueante documentado en `tests/QA/pr-394-plantillas-mapeo-cliente/README.md`.
Pendiente de autorización de Kevin para comentar/aprobar/mergear en GitHub.
