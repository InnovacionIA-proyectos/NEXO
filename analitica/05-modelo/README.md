# Modelo predictivo — reservado, trabajo futuro

Sin contenido todavía. Parte de las conclusiones de los dos EDA y
de la comparativa que los contrasta:
[`../02-eda-viajes-btura/`](../02-eda-viajes-btura/) (grano y variable
objetivo — predecir `Flete Producción FO` — se deciden ahí),
[`../03-eda-extraccion-4-fuentes/`](../03-eda-extraccion-4-fuentes/) (señal
de contexto de mercado: piso SICE-TAC, plaza SAP) y
[`../04-comparativa-y-propuesta-modelo/`](../04-comparativa-y-propuesta-modelo/)
(la propuesta de modelo(s) con su objetivo, grano, features y supuestos).

Estructura esperada (a confirmar cuando arranque este trabajo):

```
05-modelo/
├── features/         Ingeniería de variables derivadas del EDA
├── entrenamiento/     Código de entrenamiento
└── artefactos/        Modelos entrenados
```

## ⚠️ Antes de poner nada en `artefactos/`

Los archivos de modelo entrenado (`.pkl`, `.joblib`, `.onnx`, etc.) **no
deben ir directo a git** — crecen rápido y no se pueden diffear. Usar Git LFS
o un storage aparte (S3/blob), dejando solo un manifiesto (ruta + hash +
métricas) versionado en el repo.
