# PR #299 (S5-HU-17.7/17.8) — codigoMercancia en la cubicación rápida e indicePosicion por camión

## Qué cambia

- Contrato NEXO→Cubicador (`/recomendaciones`): se envía `codigoMercancia` (string
  estable) en vez de `idTipoMercancia` (id tipo GUID), igual que ya viajan
  `codigoVehiculo`/`codigoCarroceria`. Afecta `clsMercanciaSolicitadaInput`,
  `clsMercanciaDeVehiculoDto`, `clsPiezaFueraDeMedidaDto` (NEXO) y sus espejos en
  Cubicador (`MercanciaSolicitadaDto`, `MercanciaDeVehiculoDto`,
  `PiezaFueraDeMedidaDto`, lookup por `mercancia_por_codigo()` en vez de
  `mercancia_por_id()`).
- `indicePosicion` (int?, base 0) nuevo en el detalle de mercancía por camión
  (`clsMercanciaDeVehiculoDto`/`clsPiezaFueraDeMedidaDto` y sus espejos Python):
  dos posiciones con el mismo código de mercancía pueden tener medidas distintas,
  así que el detalle ahora viaja por posición, no solo por código. `NULL` en
  corridas anteriores al cambio (agrupaban por código).
- Cambio de esquema en Cubicador: columna `IndicePosicion int NULL` en
  `dbo_cubicador.recomendacionVehiculoMercancia`, ya incluida en el script base
  `ESQUEMA_CUBICADOR.sql` (no requiere paso aparte para verificación local) más un
  script incremental aparte (`S5-HU-17.8_posicion_de_mercancia.sql`) para aplicar
  sobre una tabla productiva ya existente sin recrearla.

## Qué se verificó

**Rama ya al día con `QA`**: merge limpio (`git merge origin/QA --no-commit --no-ff`,
sin conflictos), commit `de24f32`.

**Diff real contra el punto de divergencia** (`git diff --stat
$(git merge-base origin/QA origin/S5-HU-17.7-codigo-mercancia)
origin/S5-HU-17.7-codigo-mercancia`): 33 archivos, 1373 inserciones, 133
eliminaciones, en ambos lados (NEXO .cs y Cubicador .py) más documentación nueva
de cara a Cotifly.

**Código revisado línea por línea:** el rename `IdTipoMercancia`→`CodigoMercancia`
en los 3 DTOs afectados a ambos lados del contrato, y la nueva lógica de
`indice_posicion`/`IndicePosicion` en los casos de uso de recomendaciones
(Cubicador) — confirmando que el valor viaja sin transformación desde la solicitud
hasta el detalle por camión, y que es `null` cuando no se envía.

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta, 0
errores (1 warning preexistente ajeno a esta PR).

**Diagnóstico de los 3 fallos reales en CI** (`clsCubicacionControllerE2ETests`,
`ServiceUnavailable`/`Assert.Equal`): la propia PR advierte una ventana de
despliegue ("Desplegar el Cubicador... hay una ventana corta" entre desplegar
Cubicador primero y NEXO después, porque el Cubicador real en CI aún no tenía el
contrato nuevo `codigoMercancia` cuando corrió el job). Para confirmarlo de forma
independiente, en vez de confiar en la explicación de la PR, armé mi propio
Cubicador local **con el código de esta PR en ambos lados a la vez** (NEXO +
Cubicador sincronizados, nunca uno viejo contra el otro nuevo):

- `docker compose up -d --build` en `cubicador/` (SQL Server local propio, no el
  servidor compartido de red).
- Catálogo local sembrado (`scripts/preparar_catalogo_local.py`): 5 vehículos, 4
  carrocerías, 6 mercancías, 1 regla, 5 parámetros.
- Esquema completo aplicado (`scripts/aplicar_sql.py docs/Database/ESQUEMA_CUBICADOR.sql`,
  vía `pymssql` en venv temporal) — confirmado que crea `IndicePosicion int NULL`
  en `recomendacionVehiculoMercancia`.

**Suite completa contra la base real de pruebas, con este Cubicador local
arriba** (`TEST_DB_CONNECTION` desde `.env.pruebas-reales.local`,
`Cubicador3D__OutboundBaseUrl=http://localhost:8081` apuntando al Cubicador recién
levantado con el código de esta misma PR):
- `Nexo.Domain.Tests`: **173/173**.
- `Nexo.Application.Tests`: **390/390**.
- `Nexo.Api.Tests`: **370/372** (solo los 2 skips preexistentes de
  `clsMailSenderRealTests`) — **0 errores**, las 6 pruebas E2E contra Cubicador
  corrieron de verdad (no se saltaron) y pasaron.

Con NEXO y Cubicador sincronizados en la misma versión del contrato, la suite
queda completamente limpia. Esto confirma el diagnóstico: los 3 fallos en CI son
un artefacto de la ventana de despliegue (CI hablando con un Cubicador real ya
desplegado pero aún no actualizado al contrato nuevo), **no un defecto de código**
de esta PR.

## Limpieza

- `docker compose down -v` (contenedores y volumen del Cubicador local
  eliminados).
- `cubicador/.env` temporal borrado.
- Sin escritura en ninguna base de datos compartida de red (el Cubicador de
  verificación fue 100% local, propio y descartable).

## Resultado

Sin hallazgos bloqueantes. Los 3 fallos reportados por CI no son un defecto de
esta PR, sino la ventana de despliegue que la propia PR ya documenta — se
resuelven desplegando el Cubicador antes que NEXO, como indica la PR.
