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

## Topes al probar contra el modelo real

Medir la interpretación (#368) sí llama al modelo, a diferencia de `evaluar.py`,
que solo corre el motor y nunca sale a la red. Mientras se desarrolla conviene
ponerse topes, y **vale la pena entender qué limita cada cosa, porque no se
solapan**:

| Dónde | Qué frena | Estado |
|---|---|---|
| Consola de Anthropic | el gasto de verdad | falta la *workspace* `cubicador-api` |
| `IA_SOMBRA_TOPE_TOKENS_DIA` | **solo** el modo sombra | existe |
| Tope del corredor | las corridas de evaluación | se agrega con el corredor |

El tope del modo sombra lo aplica `_tope_agotado` contando los tokens que se
registraron en la tabla `iaSombra`. **Un corredor de evaluación no pasa por
ahí**, así que ese tope no lo detiene: hace falta uno propio.

Y el de la consola, que es el único que corta de verdad, hoy no se puede poner
por servicio: con una llave de organización, un límite ahí también frenaría a
NEXO. Por eso el ADR pide una *workspace* por servicio
(`docs/decisiones/ia-claude-seguridad-y-datos.md`, §8).

### Por qué ponerlos aunque el costo sea mínimo

Una corrida de los 30 casos cuesta unos **US$0,27** con `claude-sonnet-5-5`
(US$2 el millón de entrada, US$10 el de salida; ~3.000 tokens de entrada y ~300
de salida por caso). No es el gasto de una corrida lo que preocupa.

Lo que preocupa es el accidente: un bucle que reintenta, una lista que se
recorre dos veces, un prompt que se infló sin que nadie mirara. Un tope convierte
ese error en un corte limpio con mensaje, en vez de una factura. Y durante el
desarrollo del prompt, que es cuando se corre la evaluación muchas veces
seguidas, ese error es más probable que nunca.

Por eso los topes van **por llamadas y por tokens**, no por uno solo: el de
llamadas atrapa el bucle, el de tokens atrapa el prompt que creció.

### Valores de prueba

Mientras se desarrolla, en `cubicador/.env` (que es local y no se versiona):

| Variable | Normal | De prueba | Por qué |
|---|---|---|---|
| `IA_SOMBRA_TOPE_TOKENS_DIA` | 1000000 | **100000** | un millón diario son hasta ~US$10 si alguien lo enciende |
| `IA_SOMBRA_MAX_TOKENS_SALIDA` | 4096 | **1024** | una respuesta de interpretación no necesita más |
| `IA_SOMBRA_MAX_PENDIENTES` | 20 | **5** | menos comparaciones a la vez mientras se depura |

**Son temporales.** Al terminar la evaluación se devuelven a los valores de
`cubicador/.env.example`, que son los que rigen en QA y producción. El
interruptor `IA_SOMBRA_HABILITADA` sigue en `false`: para medir la
interpretación no hace falta encender el modo sombra.
