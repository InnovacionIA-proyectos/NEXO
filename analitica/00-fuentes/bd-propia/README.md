# BD propia — extracción puntual

Las vistas de solo lectura (`analitica.*`) y su definición están documentadas
en [`DICCIONARIO_DATOS.md`](DICCIONARIO_DATOS.md). Ese
documento describe el **esquema** (qué columnas trae cada vista). Este README
documenta la **extracción real** hecha a partir de esas vistas, que el
trabajo original de definición de vistas nunca llegó a ejecutar.

## Por qué esta extracción, y no la de producción

La aplicación está en construcción — no existe una instancia de producción
todavía. Esta extracción usa la única base disponible (el entorno de
desarrollo local, `localhost,1434`), **filtrada para excluir el `DataSeeder`**
y quedarse solo con los datos generados probando la aplicación de verdad desde
el frontend (pasaron por el motor de costeo real, contra los DW reales — a
diferencia del seeder, que son valores fijos inventados).

**Esto no es producción a escala.** Es un piloto pequeño y real. Cuando exista
producción, hay que repetir esta misma extracción (ver sección "Cómo
reproducir" abajo) y generar una versión nueva en `../../01-set-final/`.

## Fecha de corte y volumen

Fecha de extracción: **2026-09-01**. Filtro aplicado: **`CreatedAt >= '2026-08-22'`**
(la tanda del `DataSeeder`, concentrada el 2026-08-21, queda excluida).

| Archivo | Filtro exacto | Filas |
|---|---|---|
| `solicitudes.csv` | `CreatedAt >= 2026-08-22` | 13 |
| `lineas_solicitud.csv` | de solicitudes filtradas | 43 |
| `cotizaciones.csv` | `CreatedAt >= 2026-08-22` | 18 |
| `cotizacion_lineas.csv` | de cotizaciones filtradas | 48 |
| `cotizacion_conexos.csv` | de cotizaciones filtradas | 7 |
| `tarifas_historicas.csv` | `Fecha >= 2026-08-22` | 15 |
| `audit_log.csv` | `CreatedAt >= 2026-08-22` | 72 |
| `inconsistencias.csv` | excluye las 15 filas del `DataSeeder` | 1 |
| `catalogo_clientes.csv` | completo (catálogo, no transacción) | 21 |
| `catalogo_rutas.csv` | completo | 31 |
| `catalogo_configuraciones_vehiculo.csv` | completo | 16 |
| `catalogo_servicios_conexos.csv` | completo | 11 |
| `catalogo_usuarios.csv` | completo | 11 |

Formato: CSV, separador `,`, codificación UTF-8 con BOM (`sqlcmd -f o:65001`).

## Columnas y llaves de cruce

Ver el diccionario unificado en
[`../../01-set-final/v1.0.0-piloto/diccionario-y-linaje.md`](../../01-set-final/v1.0.0-piloto/diccionario-y-linaje.md)
— cubre estas 13 tablas columna por columna, con su significado, unidad y
cómo cruzan contra `../fuentes-corporativas/`.

## Cómo reproducir esta extracción

```sql
-- Ejemplo para solicitudes; mismo patrón (filtro CreatedAt/Fecha) para el resto
SELECT * FROM analitica.solicitudes WHERE CreatedAt >= '<fecha de corte deseada>';
```

Requiere que la migración `20260831194721_VistasAnaliticaSoloLectura` esté
aplicada (`dotnet ef database update --context NexoDbContext`) —
en este entorno no lo estaba hasta que se corrió como parte de esta extracción.
