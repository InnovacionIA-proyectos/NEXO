# PR #329 (#304, lado NEXO) — correcciones rápidas del Cubicador

## Qué cambia

- `POST /api/planes-cargue/desde-linea` valida el peso **antes** de encolar:
  `400 PESO_NO_DECLARADO` si alguna posición no declara `PesoKg > 0` (el cero
  cuenta como no declarado), sin publicar nada. Antes el rechazo nacía en el
  worker del Cubicador y volvía por cola a un consumer que solo lo registraba
  en el log — el front quedaba sondeando para siempre.
- Nuevo `GET /api/planes-cargue/por-linea/{lineaId}/estado`: `procesando` /
  `listo` / `rechazado` (con código y mensaje), contra una proyección local
  (`tbl_rechazos_cubicacion_linea`, tabla nueva y aditiva).
- Captura: `PesoKg` por posición pasa a obligatorio y es **por unidad, no
  total** (el motor hace `peso_total = pesoKg × cantidad`); `Cantidad` baja de
  tope 100.000 a 10.000 por posición, y se agrega el límite de 10.000 en
  total por línea (suma de posiciones) — `400 CUBICACION_DEMASIADO_GRANDE`.
- Cubicador: `k_max` (tope de vehículos combinados antes de rendirse) sube de
  10 a 25, justificado con mediciones (`k⁴` combinaciones: 25 → 0,01s, 50 →
  0,75s, 100 → 9,8s) en `cubicador/docs/DECISIONES_TECNICAS.md`.

## Qué se verificó (ver `verificar.sh`, script real ejecutado)

**Rama actualizada con `QA`** (incluye las PRs #324, #325, #328): merge local
sin conflictos reales — git auto-resolvió `ESQUEMA_NEXO.sql` porque los
cambios de #328 (columnas de rechazo) y #329 (tabla nueva) tocan partes
distintas del archivo; confirmado manualmente que ambos sobrevivieron.

**Esquema de BD — no se confió en la afirmación de la PR**: consulté
`sys.columns` directamente contra `dbInformesAutomatizados_Pruebas` y
confirmé que `tbl_rechazos_cubicacion_linea` ya existe con el tipo y
nulabilidad exactos declarados en el script (`Codigo` nvarchar(60)=120 bytes,
`Mensaje` nvarchar(1000)=2000 bytes, `RechazadoAt` NOT NULL). Tabla vacía
(0 filas), sin contaminación de datos de prueba.

**`k_max` vive en los 4 sitios que la PR dice que debe vivir** (verificado
directamente, no solo leído en la documentación): la fila seed
(`_generar_seeders.py`, `parametrosMotor.csv`), el respaldo en código
(`catalogo.py: return self.entero("k_max", 25)`), y el script de catálogo
local (`preparar_catalogo_local.py`) — los 4 en 25. Los usos de `"10"` que
quedan en archivos de test son fixtures explícitos de esos tests (no el
valor por defecto), no un descuido.

**Verificación del valor límite EXACTO (no solo "el endpoint responde
bien")**: leí `clsCapturarSolicitudValidadorUnidadesTests` y confirmé que la
aserción cubre el límite preciso, no una aproximación: 6.000+4.000=**10.000**
unidades se ACEPTA, 6.000+4.001=**10.001** se RECHAZA con
`CUBICACION_DEMASIADO_GRANDE` y el mensaje cita "10.000"; una posición sola
de 100.000 (lo que antes pasaba el `[Range]` viejo) también cae. Esto confirma
que el tope es exactamente 10.000, sin error de +/-1 en el límite.

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta,
0 errores (1 warning preexistente ajeno a esta PR).

**Suite completa contra la base real de pruebas**:
- `Nexo.Domain.Tests`: **183/183**.
- `Nexo.Application.Tests`: **406/406**.
- `Nexo.Api.Tests`: primera corrida sin Cubicador local → **393/401** (8
  omitidas: 2 MailSender + 6 E2E de Cubicador) — **0 errores**.

**Cubicador real armado localmente con el código de esta misma PR** (ambos
lados sincronizados): primer intento de las 6 E2E falló con `503
ServiceUnavailable` — diagnostiqué la causa (el contenedor `api` no podía
resolver el host `rabbit` de la red compartida `nexo_internal` porque el
RabbitMQ de NEXO no estaba levantado), levanté solo el servicio `rabbit` del
`docker-compose.yml` raíz de NEXO, reinicié `api`/`worker` del Cubicador, y
repetí → **399/401, 0 errores, solo los 2 skips de MailSender** — las 6 E2E
corrieron de verdad con el código real de esta PR y pasaron.

## Limpieza

- `docker compose down -v` en `cubicador/` (contenedores y volumen
  eliminados).
- `cubicador/.env` temporal borrado.
- `docker compose stop/rm rabbit` en la raíz de NEXO (contenedor que levanté
  solo para esta verificación, no estaba corriendo antes).

## Resultado

Sin hallazgos. Verificación directa contra la base real y el Cubicador local
con el código exacto de esta PR, incluida la confirmación del valor límite
preciso (10.000, no una aproximación) del tope de unidades.
