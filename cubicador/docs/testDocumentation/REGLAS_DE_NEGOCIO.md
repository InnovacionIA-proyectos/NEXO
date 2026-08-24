# Reglas de negocio del motor (portadas como pytest — ✅ verificadas)

Estas 31 reglas estaban codificadas como tests xUnit en
`cabinaFletes/src/CabinaFletes.Tests/{MotorCubicacion3DTests.cs,ApilamientoMultiSoporteTests.cs}`.
No son "casos dorados de paridad entre dos motores" (eso se descartó, ver
DECISIONES_TECNICAS.md #3) — son las reglas geométricas/de negocio que
codifican CÓMO debe comportarse el motor. Como se trajo el cubicador completo
de cabinaFletes, estas reglas viajaron con él: cada una es hoy un test en
`tests/domain/` contra la implementación portada de `domain/motor/*`.

**Estado: las 33 pruebas pasan** (`pytest tests/domain -v` → 33 passed; una
regla tiene 3 casos parametrizados, de ahí 33 en vez de 31) — el port del
motor es fiel a estas reglas sin haber necesitado ningún ajuste de
comportamiento.

Cada fila: nombre de la regla, qué verifica, el test Python que la cubre, y
el origen (archivo:línea real en cabinaFletes) por si hace falta consultar la
implementación original ante cualquier duda futura.

## Colisiones (`validadores.colisiona`)

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 1 | Unidades separadas no colisionan | Dos cajas 100³ separadas en X (0 vs 200) → `False` | `test_colision_unidades_separadas_no_colisionan` | `MotorCubicacion3DTests.cs:30` |
| 2 | Contacto exacto no colisiona | Dos cajas 100³ adyacentes (0 vs 100 en X, cara con cara) → `False` (el contacto exacto es apoyo, no colisión) | `test_colision_unidades_adyacentes_contacto_exacto_no_colisionan` | `:34` |
| 3 | Parcialmente superpuestas colisionan | Dos cajas 100³, la segunda desplazada 50 en X e Y → `True` | `test_colision_parcialmente_superpuestas_colisionan` | `:38` |
| 4 | Una contenida en otra colisiona | Caja 500³ vs caja 50³ dentro de ella → `True` | `test_colision_una_contenida_en_otra_colisionan` | `:42` |
| 5 | Tolerancia configurable | A 10mm de separación con tolerancia exigida de 20mm → colisiona; a 30mm de separación con la misma tolerancia → no colisiona | `test_colision_con_tolerancia_configurable_separacion_insuficiente_colisiona` | `:46` |

## Orientaciones (`dominio.orientaciones_de`)

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 6 | Prisma general → 6 orientaciones | Item 100×200×300, rotable, sin restricciones → 6 orientaciones distintas | `test_orientaciones_prisma_general_son_seis` | `:56` |
| 7 | Cubo → 1 orientación (sin duplicadas) | Item 100×100×100 → solo 1 (las 6 son iguales) | `test_orientaciones_cubo_sin_duplicadas_es_una` | `:60` |
| 8 | `MantenerVertical` → solo rotación horizontal | Item 100×200×300 con `vertical=true` → 2 orientaciones, todas con alto=300 | `test_orientaciones_mantener_vertical_solo_rotacion_horizontal` | `:64` |
| 9 | `Rotable=false` → solo la original | Item 100×200×300 no rotable → 1 orientación, exactamente (100,200,300) | `test_orientaciones_no_rotable_solo_la_original` | `:72` |

## Encaje y límites

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 10 | Unidad que cabe exacto se carga | Item = dimensiones exactas del contenedor (puerta = sección completa) → 1 colocación, 0 rechazos | `test_unidad_que_cabe_exactamente_se_carga` | `:82` |
| 11 | Excede una dimensión sin rotación → rechazo | 3 casos (excede largo / ancho / alto), no rotable → 0 colocaciones, 1 rechazo | `test_unidad_que_excede_una_dimension_sin_rotacion_se_rechaza` (parametrizado, 3 casos) | `:91` |
| 12 | Solo cabe rotada → se carga rotada | Item 1000×2600×1000 (2600 no cabe de ancho=2450, pero si rota sí) → se coloca con `AnchoMm <= 2450` | `test_unidad_que_solo_cabe_rotada_se_carga_rotandola` | `:103` |
| 13 | Solo cabe rotada pero `Rotable=false` → rechazo | Mismo item, no rotable → 0 colocaciones | `test_unidad_que_solo_cabe_rotada_pero_no_rotable_se_rechaza` | `:112` |
| 14 | Dos unidades caben lado a lado sin colisión | Dos items 1200×1000×1000 → 2 colocaciones, sin incidencia `COLISION` | `test_dos_unidades_caben_lado_a_lado_sin_colision` | `:119` |

## Apilamiento y soporte

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 15 | Apilamiento válido: segunda encima de la primera | Item "arriba" sobre "base" → Z=1000 (altura de base), `SoporteId="base"`, `PorcentajeSoporte=1.0` | `test_apilamiento_valido_segunda_encima_de_la_primera` | `:131` |
| 16 | Sobre no-apilable → rechazo | Base con `Apilable=false` → la unidad de encima se rechaza | `test_apilamiento_sobre_no_apilable_se_rechaza` | `:145` |
| 17 | Excede peso soportado → rechazo | Base con `MaxPesoEncimaKg=50`, encima pesa 100 → rechazo | `test_apilamiento_excede_peso_soportado_se_rechaza` | `:156` |
| 18 | Soporte insuficiente nunca dice "flotando" | Unidad grande solo apoyaría 55% de su base (mínimo 60%) → se rechaza como `SIN_ESPACIO`, nunca se coloca flotando | `test_soporte_insuficiente_no_deja_unidades_flotando` | `:167` |

## Peso total y balance de ejes

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 19 | Excede peso total → rechazo con razón `PESO_TOTAL` | Capacidad 500kg, dos items de 400+300 → 1 colocación, 1 rechazo con código `PESO_TOTAL` | `test_exceso_de_peso_total_se_rechaza_con_razon` | `:185` |
| 20 | Balance de ejes: carga centrada reparte y conserva el total | Item de 10000kg centrado → suma de `estimar_peso_por_eje` = 10000 (redondeado) | `test_balance_de_ejes_carga_centrada_estimacion_repartida` | `:196` |
| 21 | Balance de ejes: reparto inverso a la distancia | CM exactamente entre dos ejes → reparto 50/50 | `test_balance_de_ejes_carga_entre_ejes_reparto_inverso_a_la_distancia` | `:205` |
| 22 | Balance de ejes: efecto palanca en voladizo | CM por delante del primer eje → el eje delantero recibe MÁS que el peso total, el trasero recibe una fracción NEGATIVA, la suma se conserva | `test_balance_de_ejes_voladizo_efecto_palanca` | `:216` |

## Puerta

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 23 | No pasa por la puerta → rechazo `PUERTA` | Item cabe en el contenedor pero no por la puerta, no rotable → rechazo con código `PUERTA` | `test_unidad_que_no_pasa_por_la_puerta_se_rechaza` | `:237` |

## Secuencia de entrega / accesibilidad

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 24 | Entrega temprana al fondo genera violación | Plan manual: entrega-1 al fondo, entrega-2 la tapa → incidencia `SECUENCIA_BLOQUEADA`, `Accesibilidad="bloqueada"` | `test_secuencia_entrega_temprana_al_fondo_genera_violacion` | `:249` |
| 25 | Estrategia de secuencia acerca la entrega temprana a la puerta | Con `PriorizarSecuenciaEntrega`: secuencia=1 queda más cerca de la puerta que secuencia=3, sin `SECUENCIA_BLOQUEADA` | `test_secuencia_con_estrategia_de_secuencia_entrega_temprana_queda_cerca_de_la_puerta` | `:271` |

## Métricas

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 26 | Utilización volumétrica = volumen cargado / volumen útil | Contenedor de 2m³, item de 1m³ → `PorcentajeVolumen=50`, `UnidadesCargadas=1`, `PesoCargadoKg=100` | `test_metricas_utilizacion_volumetrica_es_volumen_cargado_sobre_util` | `:290` |

## Apilamiento multi-soporte (CUB-01/CUB-02 — `ApilamientoMultiSoporteTests.cs`)

Una unidad puede apoyarse sobre VARIAS unidades a la vez (puentea dos cajas
adyacentes) — estas reglas validan que TODOS los soportes se consideren, no
solo el principal. Test file: `tests/domain/test_apilamiento_multi_soporte.py`.

| # | Regla | Verifica | Test Python | Origen |
|---|---|---|---|---|
| 27 | Unidad que puentea devuelve dos soportes con fracción proporcional | Dos bases adyacentes, unidad superior en el medio → soporte 100% total, repartido 50%/50% entre A y B | `test_calcular_soporte_unidad_que_puentea_devuelve_dos_soportes` | `ApilamientoMultiSoporteTests.cs:38` |
| 28 | Soporte SECUNDARIO no apilable → rechazado | A apilable, B (secundario, mismo % de apoyo) NO apilable → se detecta y rechaza aunque el principal (A) sí sea válido | `test_validar_apilamiento_soporte_secundario_no_apilable_es_rechazado` | `:49` |
| 29 | Soporte secundario frágil + unidad más pesada → rechazado | B es frágil y pesa 50kg, la unidad de encima pesa 200kg (>B) → rechazado | `test_validar_apilamiento_soporte_secundario_fragil_mas_liviano_es_rechazado` | `:62` |
| 30 | Peso sobre soporte secundario se mide proporcional al área | Unidad de 200kg, 50% de apoyo sobre B → B recibe 100kg proporcional; con límite 60kg → rechaza; con límite 120kg → válido | `test_validar_apilamiento_peso_sobre_secundario_se_mide_proporcional` | `:73` |
| 31 | Todos apilables sin límites → válido | Caso base sin restricciones → válido | `test_validar_apilamiento_todos_apilables_sin_limites_es_valido` | `:87` |

## Cómo correr esta suite

```bash
pytest tests/domain -v
```

Si alguna vez alguna de estas 33 pruebas falla tras un cambio: es un bug del
port — no una "rareza a documentar" (ver DECISIONES_TECNICAS.md #2). Estas 31
reglas son parte del contrato de comportamiento, no candidatas a
reinterpretación.
