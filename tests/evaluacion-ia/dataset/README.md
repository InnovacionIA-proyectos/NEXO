# Dataset dorado — mapeo de Excel de clientes (S6-HU-32.5, #353)

El examen con sus respuestas para medir qué tan bien propone Claude el mapeo de columnas (`POST /api/captura-solicitudes/sugerir-mapeo`).
Cada caso es una hoja de Excel de un cliente y la respuesta correcta escrita por una persona: a qué campo de NEXO corresponde cada columna
y con qué transformación. El arnés de evaluación ([`../arnes`](../arnes)) le pasa cada caso a Claude y compara su propuesta contra estas
respuestas.

## Qué hay

| Carpeta / archivo | Qué es |
|---|---|
| `casos/caso_NN_*.xlsx` | Réplica anonimizada de una hoja real, en una hoja llamada `Datos` |
| `respuestas/caso_NN_*.json` | La respuesta correcta del caso |
| `generar_dataset.py` | Genera las réplicas y las respuestas desde los Excels originales |
| `analizar_excel.py` | Reproduce cómo `analizar-excel` detecta la fila de encabezados, para que las respuestas usen los mismos índices |
| `fuentes.local.example.json` | Plantilla de `fuentes.local.json` (de qué archivo y hoja sale cada caso) |

`fuentes.local.json` no se versiona (está en el `.gitignore`): los nombres de archivo y de hoja identifican al cliente.

## Los casos

7 casos de 4 clientes (A, B, C y D). Solo hay Excels de esos 4 clientes; es una limitación para la lectura del informe.
No hay caso de distribución: NEXO todavía no la mapea (decisión de Sara del 2026-10-29).

| Caso | Formato | Columnas | Con campo | Filas |
|---|---|---|---|---|
| `caso_01_cliente_a_importacion` | vehículo en el encabezado | 25 | 3 | 21 |
| `caso_02_cliente_a_exportacion` | vehículo en el encabezado | 13 | 3 | 14 |
| `caso_03_cliente_a_nacional` | vehículo en el encabezado | 20 | 3 | 148 |
| `caso_04_cliente_b_escenarios` | vehículo en el encabezado | 6 | 2 | 43 |
| `caso_05_cliente_c_region_1` | matriz | 14 | 1 | 62 |
| `caso_06_cliente_c_region_2` | matriz | 16 | 1 | 37 |
| `caso_07_cliente_d_impo_expo` | una fila por línea | 14 | 3 | 66 |

`formato`:
- `fila_por_linea`: cada fila es un viaje y cada columna un dato. Es lo que `sugerir-mapeo` sabe representar.
- `vehiculo_en_encabezado`: cada fila es una ruta, pero el vehículo o la clase de viaje vienen en el nombre de la columna
  («CARGA SUELTA TURBO», «CONTENEDOR 20») y el valor es un volumen o una tarifa.
- `matriz`: formulario con vehículos en filas y orígenes en columnas; el destino ni siquiera está en una columna.

## Formato de la respuesta

```json
{
  "caso": "caso_07_cliente_d_impo_expo",
  "archivo": "casos/caso_07_cliente_d_impo_expo.xlsx",
  "hoja": "Datos",
  "cliente": "D",
  "formato": "fila_por_linea",
  "descripcion": "…",
  "filaEncabezado": 10,
  "filaSubEncabezado": null,
  "totalFilas": 66,
  "limitaciones": ["…"],
  "columnas": [
    {
      "indice": 1,
      "columna": "Secuencia de entrega",
      "campo": "origen",
      "transformacion": "dividir_origen_destino",
      "requiereDespivotar": false,
      "alternativasAceptadas": [{ "campo": "destino", "transformacion": "dividir_origen_destino" }],
      "nota": "…"
    }
  ]
}
```

- `indice` y `columna` son los que produce `analizar-excel` sobre la réplica (misma regla: primera fila con 3 o más valores distintos,
  celdas combinadas repetidas y subencabezado si hay rótulos repetidos). `generar_dataset.py` comprueba que la réplica produce los mismos
  encabezados que el original.
- `campo` y `transformacion` usan las listas cerradas de `sugerir-mapeo` (`CONTRATO.md`, sección de `sugerir-mapeo`).
- `alternativasAceptadas`: otras respuestas que también se dan por buenas (por ejemplo, la columna «ORIGEN - DESTINO» como `destino`, porque
  `dividir_origen_destino` llena los dos campos).
- `requiereDespivotar`: la columna trae en el encabezado un dato que debería ser un campo (vehículo, clase de viaje, origen). Con el contrato
  actual la única respuesta válida es `ignorar`; la marca sirve para contar cuánto de lo que llega no se puede representar todavía.

## Cómo califica el arnés

Una columna está bien si `campo` y `transformacion` de la propuesta son iguales a la respuesta o a una de sus `alternativasAceptadas`
(en `ignorar` la transformación no cuenta). El informe reporta por separado:
1. **Acierto en columnas con campo** (las que no son `ignorar`): lo que de verdad importa para importar el Excel.
2. **Falsos positivos**: columnas que la respuesta deja en `ignorar` y la propuesta asigna a un campo (por ejemplo, la capacidad del vehículo
   como `peso`).
3. **Acierto total** y **por formato**.
4. **Columnas con `requiereDespivotar`**: cuántas son y qué propone el modelo en ellas.

La comparación se hace sobre los encabezados que devuelve `analizar-excel` para la réplica: el arnés los lee con el lector de producción y
se detiene si no coinciden con la respuesta. Si algún día cambia esa regla, hay que volver a correr `generar_dataset.py` y revisar los índices.

## Cómo se anonimizó

Ningún Excel real entra al repositorio ni se le envía a Claude (decisión de seguridad de la IA, #349 §7). Las réplicas conservan lo que
importa para medir el mapeo (encabezados exactos, filas de título, celdas combinadas, columnas vacías y el tipo de dato de cada columna)
y cambian todo lo demás:

- **Ciudades**: por ciudades colombianas del catálogo (`docs/Database/seeder/municipios.csv`); la misma ciudad siempre por la misma dentro
  del caso, conservando sufijos («-DTAI») y prefijos («AEROPUERTO»). También dentro de otros textos de la hoja.
- **Departamento**: el de la ciudad nueva.
- **Rutas de varias paradas**: cada ciudad por su reemplazo, con el mismo separador.
- **Números** (volúmenes, kilómetros, precios, tiempos): inventados, con magnitud parecida y siempre distintos al original.
- **Tarifas ya diligenciadas**: si el original es un formulario ya respondido, esas columnas quedan vacías, como llega una licitación
  (casos 5 y 7).
- **Notas largas e instrucciones**: texto genérico. **Títulos y textos de arriba del encabezado**: texto genérico, conservando cuántos valores
  distintos tiene cada fila (si no, cambiaría la fila que `analizar-excel` toma como encabezado).
- **Se conserva**: el vocabulario que el modelo debe reconocer (tipos de vehículo y servicio, configuraciones SICE-TAC, contenedores por rango
  de peso, consecutivos como «Item-001» o «Ruta 1», zonas comerciales).
- **Se quita**: las demás hojas, comentarios, validaciones, nombres definidos, imágenes y los datos del autor del archivo.

## Cómo regenerar o agregar un caso

1. Poner el Excel original en `docs/docs_raw/` (ya ignorado por `.gitignore`).
2. Agregar el caso en `fuentes.local.json` (archivo y hoja) y en `CASOS` de `generar_dataset.py`: cómo tratar cada columna en la réplica
   (`mantener`, `ciudad`, `ruta`, `departamento:N`) y su respuesta. Para ver los índices, correr `analizar_excel.analizar` sobre la hoja.
3. `python generar_dataset.py`. El script falla si la réplica cambia los encabezados o si la respuesta no cubre exactamente todas las columnas.
4. Revisar la réplica antes de subirla: que no quede ningún nombre de cliente, proyecto, NIT, correo, teléfono ni valor comercial real.
