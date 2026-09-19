# Analítica — workstream de datos (Habilitador B8 y lo que siga)

Todo lo que produce y consume el equipo de datos vive aquí, separado de
`docs/` (documentación de la aplicación) — igual que `cubicador/` es su propio
proyecto Python al lado de `src/`, esto es su propio espacio para datos,
análisis y (más adelante) modelos.

Las carpetas están numeradas por **etapa del flujo**, no por HU — las HU van
a seguir apareciendo (10.4, 11, 12...) pero el orden fuentes → set final →
EDA → modelo es estable.

> **Nota (2026-09-10):** el plan original esperaba un solo `02-eda/` que
> analizara `01-set-final/`. En la práctica, S2-HU-11 resultó ser un "EDA
> doble": primero se analizó el histórico crudo de Viajes-Btura (que no se
> deriva de `01-set-final/`, es un insumo paralelo), y después sí el set
> consolidado de 4 fuentes. Por eso hay dos carpetas de EDA
> (`02-eda-viajes-btura/` y `03-eda-extraccion-4-fuentes/`) en vez de una, y
> `03-modelo/` se corrió a `04-modelo/` para dejarle el número 3 a la
> segunda.
>
> **Nota (2026-09-11):** S2-HU-11 cierra con un tercer entregable —
> comparativa de ambos EDA + propuesta de modelo(s) (issue #41), sin
> construir ningún modelo este sprint. Ese documento vive aparte de ambos EDA
> (`04-comparativa-y-propuesta-modelo/`), así que `04-modelo/` se corrió a
> `05-modelo/` para dejarle el número 4 a la comparativa.

## Mapa

```
analitica/
├── 00-fuentes/                     Extracción cruda, inmutable. Nunca se
│   │                               sobreescribe: cada corrida nueva se
│   │                               agrega con su propia fecha.
│   ├── bd-propia/                  BD transaccional CabinaFletes (S2-HU-10.1, issue #35)
│   └── fuentes-corporativas/       Los 4 DW externos de solo lectura (S2-HU-10.2, issue #36)
│
├── 01-set-final/                   Snapshot autocontenido que UNE ambas fuentes
│   └── v1.0.0-piloto/              (S2-HU-10.3): su propia copia de los datos +
│                                   diccionario + linaje + control de calidad.
│                                   Versionado por carpeta, cada una congelada.
│
├── 02-eda-viajes-btura/            EDA-1 (S2-HU-11 parte 1, issue #39): histórico
│                                   crudo de viajes Buenaventura, entregado
│                                   directamente por el negocio. ✅ Completo.
│
├── 03-eda-extraccion-4-fuentes/    EDA-2 (S2-HU-11 parte 2, issue #40): el set
│                                   consolidado de 01-set-final/ (BD propia +
│                                   SICE-TAC + SAP + clientes). ⏳ En curso.
│
├── 04-comparativa-y-propuesta-modelo/  Comparativa de ambos EDA + propuesta
│                                       de modelo(s) (S2-HU-11 parte 3, issue
│                                       #41). Ningún modelo se construye acá.
│
├── 05-modelo/                      Reservado para features/entrenamiento/
│                                   artefactos del modelo predictivo. Sin
│                                   contenido todavía.
│
└── pipeline/                       Reservado para si la extracción deja de ser
                                    "foto única" y pasa a un proceso recurrente.
                                    Sin contenido todavía — hoy todo es puntual.
```

## Cómo se conectan las etapas

1. **`00-fuentes/`** — dos extracciones independientes, cada una documentada
   con su propia fecha de corte y volumen. Ninguna sabe de la otra.
2. **`01-set-final/`** — no vuelve a extraer nada: toma una **copia congelada**
   de lo que existe en `00-fuentes/` en ese momento y arma el diccionario
   unificado + linaje + reporte de inconsistencias que permite cruzarlas por
   llave (ruta, vehículo, período, cliente). Sigue sin aplanar los datos en
   una sola tabla ancha — eso queda para quien haga el EDA. Al congelar su
   propia copia, una versión ya entregada no se ve afectada si `00-fuentes/`
   recibe una extracción nueva después.
3. **`02-eda-viajes-btura/`** (EDA-1, completo) — analiza el histórico crudo
   de Viajes-Btura entregado directamente por el negocio (no depende de
   `01-set-final/`); decide grano (carga consolidada por `FO`) y variable
   objetivo (`Flete Producción FO`) para el futuro modelo.
4. **`03-eda-extraccion-4-fuentes/`** (EDA-2, en curso) — parte de
   `01-set-final/` para explorar la señal de contexto de mercado (piso
   SICE-TAC, plaza SAP) que el histórico no tiene.
5. **`04-comparativa-y-propuesta-modelo/`** — contrasta los hallazgos de
   ambos EDA y propone modelo(s) con su objetivo, grano, features y
   supuestos. Sin construir ningún modelo.
6. **`05-modelo/`** (futuro) — parte de lo que salga de la comparativa.

## Convenciones

- **Nunca se sobreescribe `00-fuentes/`.** Una corrida nueva de extracción
  agrega archivos con fecha (`tarifa-plaza...-2022-09-01_a_hoy.csv`) o una
  carpeta nueva — nunca reemplaza lo que ya hay.
- **`01-set-final/` se versiona por carpeta** (`v1.0.0-piloto/`, luego
  `v2.0.0/` cuando exista data de producción real) — nunca se edita una
  versión ya entregada.
- **Ningún registro incompleto o contradictorio se descarta en silencio** —
  se documenta con su causa en el reporte de inconsistencias correspondiente.
  Un faltante también es hallazgo.
- **Artefactos de modelo (`.pkl`, `.joblib`, `.onnx`, etc.) nunca van directo
  a git** cuando lleguemos a `05-modelo/artefactos/` — usar Git LFS o un
  storage aparte (S3/blob) con solo un manifiesto en el repo. Anotado ahora
  para no tener que redescubrirlo después.

## Estado actual

| Carpeta | Estado |
|---|---|
| `00-fuentes/bd-propia/` | ✅ Poblada — 13 archivos, filtrados a datos reales (no seeder) |
| `00-fuentes/fuentes-corporativas/` | ✅ Poblada — 4 archivos |
| `01-set-final/v1.0.0-piloto/` | ✅ Entregado (ver su `README.md` para el acta) |
| `02-eda-viajes-btura/` | ✅ Completo — EDA-1 (S2-HU-11 parte 1) |
| `03-eda-extraccion-4-fuentes/` | ✅ Completo — EDA-2 (S2-HU-11 parte 2) |
| `04-comparativa-y-propuesta-modelo/` | ⏳ En curso — S2-HU-11 parte 3 |
| `05-modelo/` | ⏳ Reservada, sin contenido |
| `pipeline/` | ⏳ Reservada, sin contenido |
