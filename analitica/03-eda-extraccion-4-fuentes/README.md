# EDA-2 — extracción que cruza las 4 fuentes

Plan listo en [`PLAN_EDA_Extraccion_4_Fuentes.md`](PLAN_EDA_Extraccion_4_Fuentes.md);
notebooks sin empezar todavía. Segundo de los dos análisis exploratorios de
este proyecto (ver [`../02-eda-viajes-btura/`](../02-eda-viajes-btura/) para
el primero, ya completo). Este va sobre el **set consolidado**
(`../01-set-final/v1.0.0-piloto/`) — BD propia + piso SICE-TAC histórico +
plaza/histórico SAP + clientes — no sobre el histórico crudo de Viajes-Btura.

**Dependencia:** requiere la entrega del set final (ya completada, ver
`01-set-final/v1.0.0-piloto/`).

## Objetivo

Análisis exploratorio del dataset consolidado, con el mismo rigor del EDA-1,
evaluando qué **señal** aparece al cruzar el contexto de mercado que el set
de históricos no tenía (ej. relación cotización vs. piso/plaza del período).

## Criterio de cierre

- [ ] Notebook reproducible y versionado en el repo
- [ ] Hallazgos con contexto de mercado documentados
- [ ] Evidencia comparable con el EDA-1 (mismas métricas de calidad y
      cobertura — completitud, unicidad, validez, consistencia, exactitud,
      sesgos)

## Estructura esperada (mismo patrón que `02-eda-viajes-btura/`, por simetría)

```
03-eda-extraccion-4-fuentes/
├── README.md
├── PLAN_EDA_Extraccion_4_Fuentes.md
├── requirements.txt
├── datos/
│   └── interim/
├── notebooks/
└── reportes/
    ├── figures/
    ├── hallazgos.md
    └── limitaciones.md
```

## Nota de alcance

Este EDA-2, junto con el EDA-1, alimentan la **comparativa con propuesta de
modelo(s)** que cierra esta etapa de análisis — ningún modelo se construye
en esta etapa.
