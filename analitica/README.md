# Analítica — workstream de datos (Habilitador B8 y lo que siga)

Todo lo que produce y consume el equipo de datos vive aquí, separado de
`docs/` (documentación de la aplicación) — igual que `cubicador/` es su propio
proyecto Python al lado de `src/`, esto es su propio espacio para datos,
análisis y (más adelante) modelos.

Las carpetas están numeradas por **etapa del flujo**, no por HU — las HU van
a seguir apareciendo (10.4, 11, 12...) pero el orden fuentes → set final →
EDA → modelo es estable.

## Mapa

```
analitica/
├── 00-fuentes/            Extracción cruda, inmutable. Nunca se sobreescribe:
│   │                      cada corrida nueva se agrega con su propia fecha.
│   ├── bd-propia/         BD transaccional CabinaFletes (S2-HU-10.1, issue #35)
│   └── fuentes-corporativas/   Los 4 DW externos de solo lectura (S2-HU-10.2, issue #36)
│
├── 01-set-final/          Snapshot autocontenido que UNE ambas fuentes
│   └── v1.0.0-piloto/     (S2-HU-10.3): su propia copia de los datos +
│                          diccionario + linaje + control de calidad.
│                          Versionado por carpeta, cada una congelada.
│
├── 02-eda/                Reservado para S2-HU-11. Sin contenido todavía.
│
├── 03-modelo/             Reservado para features/entrenamiento/artefactos
│                          del modelo predictivo. Sin contenido todavía.
│
└── pipeline/              Reservado para si la extracción deja de ser "foto
                           única" y pasa a un proceso recurrente. Sin
                           contenido todavía — hoy todo es puntual.
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
3. **`02-eda/`** (futuro) — parte de `01-set-final/` para explorar, y ahí sí
   decide grano y variable objetivo (esta carpeta y las anteriores
   deliberadamente no lo hacen).
4. **`03-modelo/`** (futuro) — parte de lo que salga del EDA.

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
  a git** cuando lleguemos a `03-modelo/artefactos/` — usar Git LFS o un
  storage aparte (S3/blob) con solo un manifiesto en el repo. Anotado ahora
  para no tener que redescubrirlo después.

## Estado actual

| Carpeta | Estado |
|---|---|
| `00-fuentes/bd-propia/` | ✅ Poblada — 13 archivos, filtrados a datos reales (no seeder) |
| `00-fuentes/fuentes-corporativas/` | ✅ Poblada — 4 archivos |
| `01-set-final/v1.0.0-piloto/` | ✅ Entregado (ver su `README.md` para el acta) |
| `02-eda/` | ⏳ Reservada, sin contenido |
| `03-modelo/` | ⏳ Reservada, sin contenido |
| `pipeline/` | ⏳ Reservada, sin contenido |
