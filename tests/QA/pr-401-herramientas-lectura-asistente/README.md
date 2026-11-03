# QA — PR #401: Herramientas de lectura del asistente de cotización (#371)

## Resumen

Primera pieza del asistente conversacional de cotización (cadena #371 → #372
→ #373 → #376): un catálogo de **herramientas de solo lectura** que el modelo
puede pedir por nombre, cada una envolviendo una consulta que NEXO ya expone,
ejecutada **con la identidad del usuario que conversa**.

- `IHerramientaAsistente` / `clsHerramientaBase`: contrato común. Cada
  herramienta declara su nombre, descripción, esquema de entrada (JSON
  Schema) y los roles que pueden usarla (vacío = cualquier autenticado).
  `clsEntradaHerramienta` valida tipos y límites (texto ≤ 200 caracteres,
  enteros con rango, GUID, enum) y lanza `HERRAMIENTA_ENTRADA_INVALIDA` ante
  cualquier desvío. Una fuente externa caída (`clsServiceUnavailableException`)
  se traduce en "sin datos", nunca en un 500.
- `clsHerramientasDelAsistente`: punto único de acceso. El catálogo que se le
  ofrece al modelo (`Disponibles()`) ya viene filtrado por el rol del usuario;
  si el modelo pide igual una que su rol no tiene, se rechaza con
  `HERRAMIENTA_NO_PERMITIDA` (403) **antes de ejecutar nada** — doble
  verificación, no solo en el listado.
- 8 herramientas, cada una reutilizando un caso de uso/puerto ya existente
  (sin SQL propio, sin bypass de las reglas de acceso de cada consulta):
  `consultar_piso_sicetac`, `consultar_tarifa_plaza_sap`,
  `consultar_tarifa_plaza_nexo` (restringida a `AdminTarifas`/`AdminBiblioteca`/
  `Admin`), `consultar_historico_sap`, `consultar_aprovisionamiento`
  (restringida a `Comercial`/`Admin`), `consultar_propuesta_linea`
  (restringida a los roles de la Mesa de Costeo, **con aislamiento por
  comercial**: un Comercial que pide la línea de una solicitud ajena recibe
  `SOLICITUD_AJENA`/403, igual que en el formulario), `consultar_servicios_conexos`,
  `buscar_ciudades_dane`.
- `clsAsistenteHerramientasController`: `GET /api/asistente/herramientas`
  (catálogo disponible) y `POST /api/asistente/herramientas/{nombre}`
  (ejecutar). Ningún endpoint escribe.

## Qué se verificó

1. Diff aislado de la PR (2 commits propios, uno de ellos solo quita una
   referencia a la issue de un comentario) más los merges de integración con
   `QA`, ya verificados en PRs anteriores.
2. Revisado que el filtrado por rol ocurre en dos capas: al listar
   (`Disponibles()`) y al ejecutar (`EjecutarAsync` revalida `Permitida`),
   para que pedir una herramienta no listada no la ejecute igual.
3. Revisado `clsConsultarPropuestaLineaHerramienta`: aislamiento por
   comercial (`SOLICITUD_AJENA`) idéntico al que ya protege el endpoint
   normal de propuesta de tarifa.
4. Confirmado que las 8 herramientas delegan en casos de uso/puertos
   existentes (no hay SQL nuevo ni acceso directo a datos en esta capa).
5. `tests/Nexo.Api.Tests/clsRolesPorEndpointTests.cs`: se agregaron 5 casos
   nuevos que verifican los roles exactos de cada endpoint del asistente
   (el endpoint HTTP deja pasar a todo autenticado; el rol limita cada
   herramienta).
6. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
7. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
8. `dotnet test tests/Nexo.Application.Tests` → **972/972** (971 de antes +
   el bloque nuevo de `clsHerramientasDelAsistenteTests.cs`, 584 líneas de
   pruebas sobre las 8 herramientas y sus reglas de rol/entrada).
9. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **938/951**, 13 omitidas: las
   esperadas de siempre (ya documentadas en PR #405), incluyendo
   `clsAsistenteHerramientasApiTests.cs` (223 líneas, integración completa
   del controller).

## Datos de prueba

Ninguno: la PR no agrega tablas ni escribe datos; todas las herramientas son
de solo lectura sobre datos/catálogos que ya existían.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes. Pendiente de
autorización de Kevin para comentar/aprobar/mergear en GitHub.
