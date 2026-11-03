# PR #291 (S5-HU-18) — NEXO consume el contrato nuevo del Cubicador (cierra #273-#278)

## Alcance

50 archivos. NEXO reemplaza el cliente del contrato viejo (`POST /api/cubicacion`,
retirado por la PR #280) por `POST`/`GET /recomendaciones`, agrega 6 controllers
de passthrough hacia los CRUD de catálogo del Cubicador, 2 consumers de eventos
para proyectar localmente tipo de mercancía/carrocería, y 4 columnas + 2 tablas
nuevas en `ESQUEMA_NEXO.sql`. Depende de la PR #280 (ya mergeada a `QA`).

## Qué se verificó

**Merge real contra `QA`** (con #280 ya adentro): sin conflictos, motor de
selección de flota preservado.

**Código revisado:** `clsCubicacionRapidaAdapter` (nueva ruta `recomendaciones`,
llave `RecomendacionesApiKey` distinta de `OutboundApiKey`, traducción de errores
reales del Cubicador en vez de códigos inventados, `SIN_SELECCION_AUTOMATICA`/
`condicionada` tratados como éxito); esquema de BD (4 columnas + 2 tablas, todo
aditivo).

**Suite completa (`dotnet test`), primera corrida — sin Cubicador configurado:**
228/234 en Api.Tests, **6 fallos** — todos en tests E2E marcados explícitamente
como "contra el Cubicador real" (`clsCubicacionControllerE2ETests`,
`clsCatalogoCubicadorPassthroughE2ETests`), con `ServiceUnavailable` — mi entorno
no tenía el Cubicador corriendo ni las llaves `Cubicador3D:*` configuradas. No es
un hallazgo: es exactamente el comportamiento "503 accionable" que documenta la
propia PR cuando faltan esas dos llaves.

**Se armó el Cubicador real localmente** (`docker compose up --build` en
`cubicador/`, imagen reconstruida desde el código de la PR #280 ya mergeada —
el contenedor viejo que había en la máquina era de 3 semanas atrás y respondía
404 en `/recomendaciones`) para no quedarme con la verificación a medias:
- `scripts/preparar_catalogo_local.py` sembró el catálogo de desarrollo (5
  vehículos, 4 carrocerías, 6 mercancías).
- `scripts/aplicar_sql.py docs/Database/ESQUEMA_CUBICADOR.sql` creó las 5
  tablas propias del motor 3D (`parametros_cubicacion`, etc.) — el `db-init`
  del compose solo crea la base/esquema, no las tablas; es diseño documentado,
  no un bug.
- Confirmé `POST /recomendaciones` end-to-end contra este Cubicador real:
  devuelve `ASIGNADO` con un vehículo real del catálogo sembrado, los dos
  porcentajes de llenado, y la advertencia de bajo llenado.

**Suite completa, segunda corrida, con `Cubicador3D:OutboundBaseUrl=http://localhost:8081`
y las llaves apuntando al Cubicador real recién armado:**
- `Nexo.Domain.Tests`: 147/147.
- `Nexo.Application.Tests`: 260/260.
- `Nexo.Api.Tests`: 232/234 (2 skips preexistentes de `clsMailSenderRealTests`,
  no relacionados). **Los 6 tests E2E que habían fallado ahora pasan**, incluido
  el ciclo completo de passthrough (crear → actualizar → eliminar → 403 con rol
  insuficiente) contra el Cubicador real.
- Coincide exacto con lo declarado en la PR (147/260/232-234).

**Sincronización de catálogo por eventos — verificado en vivo, no solo con
pytest/xUnit** (no hay test automatizado del lado NEXO para este flujo
específico, solo quedaba como verificación manual de la autora): levanté la
API de NEXO apuntando al Cubicador local y al RabbitMQ real compartido, creé un
tipo de carrocería vía `POST /api/tipos-carroceria` (passthrough), y confirmé
con una consulta directa a `db_NEXO.tbl_tipos_carroceria_catalogo` que la fila
apareció con el mismo Id — el evento viajó por RabbitMQ y el consumer de NEXO
la proyectó. Repetí el ciclo con `DELETE` (requiere `version` en el body, control
de concurrencia optimista) y confirmé que la proyección también se limpió.
Sin datos de prueba residuales: el registro se creó y se eliminó tanto en el
Cubicador como en la proyección de NEXO.

## Resultado

Sin hallazgos bloqueantes. Los 6 issues están completos y verificados, incluido
el flujo de sincronización por eventos que no tenía cobertura automatizada —
confirmado en vivo de punta a punta.
