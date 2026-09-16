# S2-HU-10 · Habilitador B8 — Set final consolidado ("la foto")

Une la BD propia (S2-HU-10.1, issue #35) con los extractos corporativos
(S2-HU-10.2, issue #36) en un solo entregable documentado, versionado, con
diccionario, linaje y control de calidad cruzado. Insumo directo del EDA de
S2-HU-11.

**Qué es y qué NO es:** extracción puntual, cruda salvo el filtrado explícito
del `DataSeeder` — no es un DW ni un cubo, y no impone grano ni variable
objetivo (eso lo decide el EDA).

**No aplana los datos.** Cada tabla/archivo conserva su propio grano; el
cruce entre el lado propio y el externo se hace por las llaves documentadas en
`diccionario-y-linaje.md`, no por un `JOIN` físico ya resuelto.

## Contenido

Esta carpeta es un **snapshot autocontenido** de esta versión — trae su propia
copia congelada de los datos, no solo la documentación. `../../00-fuentes/`
sigue existiendo aparte como el archivo crudo corriente (de ahí salió esta
copia, y de ahí saldrá la próxima versión), pero **esta carpeta no depende de
que `00-fuentes/` no cambie** — si mañana se agrega una extracción nueva allá,
esta versión sigue intacta con exactamente los mismos datos con los que se
entregó.

```
analitica/01-set-final/v1.0.0-piloto/
├── README.md                      (este archivo — acta de entrega)
├── diccionario-y-linaje.md        (qué es cada columna, de dónde viene, en qué unidad)
├── reporte-inconsistencias.md     (hallazgos de calidad, con causa)
└── datos/                         (copia congelada, idéntica a la de 00-fuentes en 2026-09-01)
    ├── bd-propia/                 (13 CSV, S2-HU-10.1/10.3 + DICCIONARIO_DATOS.md)
    └── fuentes-corporativas/      (4 CSV, S2-HU-10.2)
```

## Fecha de corte y volumen

| Fuente | Filtro aplicado | Filas |
|---|---|---|
| `solicitudes` | `CreatedAt >= 2026-08-22` (excluye el `DataSeeder`, 2026-08-21) | 13 |
| `lineas_solicitud` | misma solicitud filtrada | 43 |
| `cotizaciones` | `CreatedAt >= 2026-08-22` | 18 |
| `cotizacion_lineas` | misma cotización filtrada | 48 |
| `cotizacion_conexos` | misma cotización filtrada | 7 |
| `tarifas_historicas` | `Fecha >= 2026-08-22` | 15 |
| `audit_log` | `CreatedAt >= 2026-08-22` | 72 |
| `inconsistencias` | excluye las 15 filas del seeder | 1 |
| Catálogos (`clientes`/`rutas`/`configuraciones_vehiculo`/`servicios_conexos`/`usuarios`) | completos, sin filtro de fecha (son catálogos, no transacciones) | 21 / 31 / 16 / 11 / 11 |

Fecha de extracción: 2026-09-01. Lado externo: ver
`./datos/fuentes-corporativas/README.md` (misma fecha de corte).

## Versión

**v1.0.0** — primera entrega consolidada. Como no hay producción todavía, esta
versión queda marcada como **provisional/piloto**: cuando exista data de
producción real, se debe generar una v2.0.0 re-ejecutando la misma extracción.

## Control de calidad

Ver `reporte-inconsistencias.md` para el detalle completo. Resumen:
- 1 inconsistencia real de negocio (`Deadline` anterior a creación).
- 1 ruta malformada (dato de captura de prueba, no de mercado).
- 14 de 15 líneas con fuente externa (`Sicetac`/`Tarifario`) se verificaron
  exactas contra el DW real — el pipeline de costeo funciona correctamente.
- El bajo volumen de datos reales queda documentado como limitación
  estructural, no oculto.

## Acta de entrega

| Campo | Valor |
|---|---|
| Entregado a | Equipo de datos (autoentrega — mismo responsable de esta extracción) |
| Fecha de entrega | 2026-09-01 |
| Contenido entregado | Este directorio (`README.md`, `diccionario-y-linaje.md`, `reporte-inconsistencias.md`) + los datos crudos en `./datos/bd-propia/` (13 CSV + `DICCIONARIO_DATOS.md`, agregado el 2026-09-10 al detectarse que faltaba — ver nota abajo) y `./datos/fuentes-corporativas/` (4 archivos) |
| Versión | v1.0.0 (provisional — ver sección Versión) |
| Vía de entrega | Repositorio, rama de esta HU, PR a QA |

**Corrección post-entrega (2026-09-10):** `diccionario-y-linaje.md` referenciaba
`./datos/bd-propia/DICCIONARIO_DATOS.md` para el detalle de los 5 catálogos,
pero ese archivo nunca se copió a este snapshot congelado — solo existía en
`00-fuentes/bd-propia/`, rompiendo la promesa de que esta carpeta es
autocontenida. Detectado durante el EDA-2 (S2-HU-11 parte 2) al necesitar
tipar las columnas de los catálogos. Se copió el archivo tal cual estaba
(sin cambios desde su creación el 2026-09-01) — no se modificó ningún dato
ya entregado, solo se completó una referencia rota.

## Flujo de ramas

PR de esta rama a `QA` (CI verde + 1 revisor); Kevin (@kcano-ux) valida y hace
el PR `QA` → `main`.
