# Verificación QA — S2-HU-04.3 y S2-HU-04.4 (issues #15 y #16, PR #62)

Rama: `15-s2-hu-043-saga-auto-costeo` (se apila sobre HU-01.1/01.2/02.1/01.7/04.2).
Verificación hecha situándose en esa rama, antes de aprobar/mergear el PR #62.

## Qué trae el PR (cubre 2 issues, declarado por el propio autor)
- **Saga del auto-costeo** (máquina de estados MassTransit, pura): reparte el pliego en mensajes, cuenta desenlaces por línea (`Resueltas`, `PendientesTecnicas`, `RequierenManual`) y decide `Completado`/`Parcial` — nunca lee la base directamente.
- **Distinción entre 2 tipos de espera**: fallo técnico (se reintenta, tiene reloj) vs. requiere tarifa manual (no se reintenta, espera a una persona) — antes mezcladas en un solo mensaje, lo que quemaba reintentos y disparaba alertas falsas.
- **Consumidor de costeo por línea** con los 3 desenlaces (`LineaCosteada`, `LineaFalloTecnico`, `LineaRequiereTarifaManual`) + recosteo automático cuando aparecen tarifas nuevas.
- **Reglas del costeo extraídas a Domain** (antes atrapadas en un adaptador de 470 líneas con 4 conexiones externas, imposible de probar unitariamente).

## ⚠️ Defecto real corregido, con riesgo legal — verificado, no solo leído

**El hallazgo:** una ruta marcada "solo SICE-TAC" podía costearse con la tarifa de plaza normal en vez del piso obligatorio — falla silenciosa, sin error ni advertencia. La causa: 3 formas distintas de normalizar la misma ruta (captura, reglas, y el adaptador que las comparaba) con una igualdad exacta entre dos de ellas que nunca coincidía. La corrección usa `Domain.Common.Texto.ClaveRuta` como único criterio canónico en las 3 puntas.

**Cómo se verificó:**
- Corrí `ClaveDeRutaTests.cs` (14 casos, incluida la prueba que documenta el defecto exacto: compara las 2 formas viejas contra la canónica y confirma que ninguna de las 2 viejas coincidía): **en verde**, parte de los 61/61 de `Domain.Tests`.
- Confirmé que **no contradice mi PR #53**: son complementarios — mi fix arregla la detección de reglas *duplicadas* (escritura); este fix arregla la *aplicación* de la regla al costear (lectura). El propio commit de Duban cita el PR #53 como la corrección correcta del lado de escritura.
- `ReglaSoloSicetacTexto.cs` (el archivo del conflicto resuelto en la HU-01.7) no fue tocado por esta rama — sin contradicción ahí tampoco.

**Cambio de comportamiento, declarado por el autor:** rutas que hoy no aplicaban la regla van a empezar a aplicarla. No hay datos que migrar (la BD todavía no existe en ningún ambiente real).

## Conflicto real de merge — 7 archivos, todos resueltos como aditivos

A diferencia del conflicto de la HU-01.7 (una discrepancia real de negocio), este conflicto fue puramente **estructural**: esta rama se ramificó de la versión original (pre-revisión) del PR #61, así que trae su propia copia de los commits de mensajería con SHAs distintos, más las extensiones de la saga encima. Revisado archivo por archivo — los 7 son "esta rama agrega código nuevo que `QA` todavía no tiene", sin ninguna contradicción real:

| Archivo | Qué agrega |
|---|---|
| `MensajeriaDependencyInjection.cs` | Registro de consumidores, saga, y 8 validadores nuevos |
| `CargarLineasBulkCommand.cs` + su test | Registra el trabajo ANTES de publicar (cierra ventana de 404 en `/api/trabajos/{id}`) |
| `Program.cs` | DI de los módulos Costeo y Trabajos |
| `docker-compose.yml` | Imagen propia de RabbitMQ con el plugin de retardo (necesario para los timeouts de saga — sin él, el reloj se pierde si la API se reinicia a mitad de un pliego) |
| `CabinaFletesDbContext.cs` + snapshot de migraciones | Tablas nuevas `trabajos_auto_costeo` y `lineas_pendientes_de_costeo` |
| `OutboxConsistenciaTests.cs` | Timeout de comando ampliado (180s) para que la prueba no se vuelva intermitente con más migraciones |

**Verificación extra por ser código generado (el snapshot de migraciones):** no me limité a confiar en el merge de texto — apliqué las migraciones de verdad contra SQL Server real (`db.Database.MigrateAsync()` dentro de `OutboxConsistenciaTests`) y confirmé por consulta directa que `trabajos_auto_costeo` y `lineas_pendientes_de_costeo` existen con la estructura esperada.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| **Smoke** | ✅ `Build succeeded`, 0 errores, tras resolver los 7 conflictos |
| **Regresión** | `Domain.Tests` **61/61** ✅ (subió de 40). `Application.Tests` **70/70** ✅ (subió de 58) |
| **Integración** | Migraciones aplicadas contra SQL Server real + tablas confirmadas. `AutoCosteoSagaTests.cs` del autor (6 tests, orquestación de la saga): 6/6 ✅. `OutboxConsistenciaTests.cs`: 3/3 ✅ |
| **Carga y estrés** | Nueva: 20 trabajos concurrentes × 5 líneas c/u (140 mensajes entrelazados) — **0 contaminación cruzada entre sagas, todos `Completado` correctamente, ~733 ms** |

## Suite completa de `Api.Tests` (52 tests, con SQL Server real)

**46/52 en verde.** Las 6 fallas son las **mismas 6 ya documentadas en el PR #61** — ninguna nueva, ninguna causada por este PR:
1. `SolicitudesTests.Cargar_lineas_bulk_en_licitacion` — sigue sin actualizarse al contrato 202+id (ya señalado en #61, sigue pendiente).
2. `HealthTests.Health_responde_200_y_healthy` — preexistente, ajeno a este PR.
3-5. 3 fallas en `CotizacionesTests` — preexistentes, ajenas a este PR.
6. Condición de carrera de `ApiFixture` bajo paralelismo — preexistente, ajena a este PR.

Confirmado: el número de fallas y cuáles son exactamente las mismas que antes de este PR — **cero regresiones nuevas**.

## Conclusión
El defecto de negocio corregido es real, verificado y complementario (no contradictorio) con el fix ya mergeado. Los 7 conflictos de merge eran puramente aditivos, resueltos y verificados (incluida la aplicación real de las migraciones). La saga aguanta concurrencia sin contaminación cruzada. Sin objeciones para mergear.
