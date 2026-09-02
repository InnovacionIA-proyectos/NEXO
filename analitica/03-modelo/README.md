# Modelo predictivo — reservado, sin HU asignada todavía

Sin contenido todavía. Parte de las conclusiones de [`../02-eda/`](../02-eda/)
(grano y variable objetivo se deciden ahí, no aquí).

Estructura esperada (a confirmar cuando exista la HU):

```
03-modelo/
├── features/         Ingeniería de variables derivadas del EDA
├── entrenamiento/     Código de entrenamiento
└── artefactos/        Modelos entrenados
```

## ⚠️ Antes de poner nada en `artefactos/`

Los archivos de modelo entrenado (`.pkl`, `.joblib`, `.onnx`, etc.) **no
deben ir directo a git** — crecen rápido y no se pueden diffear. Usar Git LFS
o un storage aparte (S3/blob), dejando solo un manifiesto (ruta + hash +
métricas) versionado en el repo.
