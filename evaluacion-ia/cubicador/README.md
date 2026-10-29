# Evaluación del Cubicador: motor actual contra IA

Mide el motor de selección de flota (`cubicador/domain/motor/seleccion_flota.py`) contra un dataset dorado, para tener la línea base con la que se compara la IA. El diseño del estudio, las métricas y los resultados están en [`docs/ia/ESTUDIO-CUBICADOR-IA.md`](../../docs/ia/ESTUDIO-CUBICADOR-IA.md).

**No es parte del servicio ni del CI**: está fuera de `cubicador/` a propósito (un cambio aquí no redespliega el Cubicador) y no toca base de datos ni red. Corre el motor real, sin reescribirlo.

| Archivo | Para qué |
|---|---|
| `evaluar.py` | Corre el motor sobre los casos y escribe el informe |
| `metricas.py` | Las cinco métricas, como funciones puras |
| `catalogo_qa.py` | Catálogo de QA calcado, para correr sin base de datos |
| `casos/semilla_sintetica.json` | 22 casos escritos a mano, con su respuesta correcta |
| `extraer_cargas_reales.sql` | SELECT de solo lectura que saca las cargas reales |
| `importar_cargas_reales.py` | Convierte ese resultado en casos por validar |
| `informe_linea_base_semilla.md` | Línea base del motor sobre la semilla |
| `tests/` | Pruebas de las cuentas, del dataset y del importador |

## Uso

Desde la raíz del repositorio, con Python 3.14 o superior (el dominio del motor no necesita instalar nada):

```
python evaluacion-ia/cubicador/evaluar.py
python evaluacion-ia/cubicador/evaluar.py --repeticiones 50 --salida-md informe.md --salida-json informe.json
python -m pytest evaluacion-ia/cubicador --noconftest
```

El evaluador lee todos los `*.json` de `casos/` y **solo cuenta los casos con `etiquetaValidada: true`** (`--incluir-sin-validar` los suma, no usarlo para decidir).

## Dataset real

```
-- 1. Correr extraer_cargas_reales.sql (solo lectura) y exportar el resultado a CSV
python evaluacion-ia/cubicador/importar_cargas_reales.py cargas.csv evaluacion-ia/cubicador/casos/reales_por_validar.json
-- 2. Un experto revisa cada caso y pone etiquetaValidada: true
python evaluacion-ia/cubicador/evaluar.py
```

Los archivos `casos/reales*.json` y los `*.csv` están en `.gitignore`: tienen datos de operación y no se versionan; se guardan en la carpeta compartida del equipo.

## Formato de un caso

```json
{
  "id": "S-01",
  "origen": "sintetico",
  "etiquetaValidada": true,
  "etiquetaMetodo": "cómo se obtuvo la respuesta correcta",
  "mercancias": [{"codigo": "BEBIDAS", "cantidad": 50, "largoCm": 50, "anchoCm": 40, "altoCm": 40, "pesoKg": 20}],
  "claseViaje": null,
  "decisionCorrecta": {
    "resultado": "ASIGNADO",
    "cantidadMinima": 1,
    "flotas": [[{"vehiculo": "2L2", "carroceria": "FURGON"}]]
  }
}
```

- `flotas` lista las flotas que un humano daría por buenas; todas con `cantidadMinima` vehículos. Puede ir vacía cuando hay demasiadas combinaciones: el caso solo cuenta para la flota mínima.
- Para una carga que no se debe asignar: `"resultado": "SIN_SELECCION_AUTOMATICA"` y `"motivo": "EXTRADIMENSIONADO"` (u otro motivo del contrato).
