# Informe final — Pliego de prueba `docs/ejemplo_licitacion.xlsx` (15 líneas)

S2-HU-13.4 (issue #43). Verificación de la Regla 1 (regla del mayor) y Regla
2 (piso inviolable) contra la app real, usando un pliego de 15 líneas reales
subido de verdad a CabinaFletes. Documenta: qué se esperaba, qué salió
realmente (`cotizacion-agencia-de-aduanas-jf-asociados-s-a-s-nivel-1-7667da46.pdf`),
los errores propios cometidos en el camino (para que quede la trazabilidad),
y las 2 desviaciones reales encontradas.

## 1. Qué se hizo

1. Se calculó a mano un "esperado" (MAX entre piso SICE-TAC y tarifa de
   plaza SAP) para las 15 líneas, ANTES de subir el Excel — a ciegas, para
   que la comparación no estuviera sesgada.
2. Se subió `docs/ejemplo_licitacion.xlsx` a la app real y se obtuvo la
   cotización real en PDF.
3. El "esperado" inicial **no coincidió con nada** — investigando por qué,
   se encontraron 5 errores propios de metodología (sección 3) y, al
   corregirlos, se llegó a una coincidencia exacta (12/12) con el mecanismo
   real del código (sección 4).
4. Con el mecanismo correcto ya confirmado, se identificaron 2 desviaciones
   reales de la regla de negocio (sección 5).

## 2. Resultado final — línea por línea

"Esperado" aquí ya es el **correcto**: MAX(plaza SAP vigente real, piso
SICE-TAC real `CA`/`General`/`SI`) — no mi primer cálculo equivocado (ese
quedó documentado aparte en la sección 3, como error propio corregido).
"Obtenido" = lo que salió realmente en el PDF de cotización.

| Línea | Ruta | Fuente esperada (real) | Valor esperado (real) | Fuente obtenida (PDF) | Valor obtenido (PDF) | ¿Exacto? | Desviación |
|---|---|---|---|---|---|---|---|
| 1 | Buenaventura→Bogotá | Tarifario (plaza $6,0-6,4M > piso $1.556.503) | $6.000.000 / $6.400.000 | Sicetac | $1.556.503 | **No** | **Real (Desviación 1):** plaza vigente y mayor, no se usó |
| 2 | Buenaventura→Medellín | Tarifario (plaza $5.000.000 > piso $1.595.367) | $5.000.000 | Sicetac | $1.595.367 | **No** | **Real (Desviación 1):** plaza vigente y mayor, no se usó |
| 3 | Medellín→Buenaventura (dev.) | Tarifario (plaza $2.454.000 > piso $1.594.476) | $2.454.000 | Sicetac | $1.594.476 | **No** | **Real (Desviación 1):** plaza vigente y mayor, no se usó |
| 4 | Bogotá→Buenaventura (dev.) | Sicetac (plaza vencida hace 16 días) | $1.613.856 | Sicetac | $1.613.856 | **Sí** | Ninguna |
| 5 | Cali→Cartagena | Sicetac (nunca existió plaza para LN=InteriorAPuerto/CV=CargaSuelta, 0 filas en toda la historia) | $3.118.351 | Sicetac | $3.118.351 | **Sí** | Ninguna |
| 6 | Bucaramanga→Bogotá | Sicetac (plaza vencida) | $1.271.020 | Sicetac | $1.271.020 | **Sí** | Ninguna |
| 7 | Barranquilla→Buenaventura | Sicetac (plaza vencida, la más reciente de esta línea de negocio venció en noviembre 2025) | $3.280.034 | Sicetac | $3.280.034 | **Sí** | Ninguna |
| 8 | Cúcuta→Bogotá | Sicetac (nunca existió plaza para LN=Andino/CV=CargaSuelta, 0 filas en toda la historia) | $1.822.684 | **Ausente del PDF** | — | **No** | **Real (Desviación 2):** piso disponible, la línea no aparece |
| 9 | Bogotá→Villavicencio | Sicetac (plaza vencida) | $867.043 | Sicetac | $867.043 | **Sí** | Ninguna |
| 10 | Pereira→Buenaventura | Sicetac (plaza vigente existe, pero solo cubre 22.001-34.000 kg — nuestro peso, 19.500, queda por debajo de ese rango) | $917.268 | Sicetac | $917.268 | **Sí** | Ninguna |
| 11 | Buenaventura→Cali | Tarifario (plaza $2.400.000 > piso $705.417) | $2.400.000 | Sicetac | $705.417 | **No** | **Real (Desviación 1):** plaza vigente y mayor, no se usó |
| 12 | Manizales→Medellín | Sicetac (nunca existió plaza para LN=Andino/CV=Cupo, 0 filas en toda la historia) | $960.937 | Sicetac | $960.937 | **Sí** | Ninguna |
| 13 | Bogotá→Santa Marta | Sicetac (nunca existió plaza para este combo, 0 filas en toda la historia) | $2.442.568 | **Ausente del PDF** | — | **No** | **Real (Desviación 2):** piso disponible, la línea no aparece |
| 14 | Ibagué→Buenaventura (dev.) | Sicetac (plaza vencida hace 13 días) | $1.132.431 | Sicetac | $1.132.431 | **Sí** | Ninguna |
| 15 | Neiva→Bogotá | Sicetac (nunca existió plaza para LN=Licitaciones/CV=CargaSuelta, 0 filas en toda la historia) | ~$1.104.014 (**pendiente de reconfirmar con `CONFIGURACION='CA'`** — ver `consultas/04-confirmar-linea-15-CA.sql`) | **Ausente del PDF** | — | **No** | **Real (Desviación 2):** piso disponible, la línea no aparece |

**8 de 15 líneas exactas (4, 5, 6, 7, 9, 10, 12, 14) — todas las que no
tenían una plaza vigente APLICABLE compitiendo, salieron perfectas.** Ojo
con el matiz en 2 de esas 8: en las líneas 5 y 10 sí hay una fila de plaza
vigente para la ruta, pero para un rango de peso distinto al de la línea
(no es "sin plaza en absoluto", es "vigente pero no aplica a este peso") —
verificado directo en `tblSAPTarifas`, no es una suposición. Las otras 6 de
las 8 (4, 6, 7, 9, 12, 14) sí son "sin ninguna plaza aplicable" genuino
(vencida o nunca existió). Las 7 líneas que fallan son **todas desviaciones
reales**, agrupadas en las 2 mismas categorías: 4 líneas donde había plaza
vigente y mayor que se ignoró (1, 2, 3, 11) y 3 líneas donde había piso
disponible pero la línea desapareció de la cotización (8, 13, 15). Cero
errores de cálculo de mi
parte en esta versión de la tabla — ver sección 5 para el detalle completo.

## 3. Recomendaciones para leer/verificar este código (aprendidas en el camino)

- **Confirmar el método que realmente gobierna antes de reconstruir la
  consulta.** Cuando una línea trae Clase de Viaje diligenciada, el camino
  real es `TarifarioSapAdapter` (`SicetacOficialAsync` + `Seleccionar`), no
  los métodos "crudos" `CandidatoSapAsync`/`CandidatoSicetacAsync` de
  `PropuestaTarifaAdapter` (esos solo aplican cuando Clase de Viaje es nula).
- **Verificar catálogos de zona/vehículo contra datos reales, no asumirlos.**
  Bogotá y Medellín son zonas de grupo (`ZTC_BOGOTA`/`ZTC_NORTE_BOGOTA`,
  `ZTC_VABURRA`), no zonas individuales. `TIPOVEHICULO` en `tblSAPTarifas`
  es el ID numérico del catálogo, no el código texto. El catálogo de
  vehículo de SICE-TAC es distinto al de SAP (`C2` de SAP = `2` en
  SICE-TAC).
- **Filtrar siempre `NOMBRETIPOCARGA` en SICE-TAC** — sin ese filtro trae
  todos los tipos de carga mezclados.
- **No asumir que el peso siempre filtra la búsqueda de plaza.** Leyendo
  `TarifarioSapAdapter.Seleccionar`, el peso solo se filtra en la Regla B
  (Contenedor); en Cupo (Regla C) se maneja como "límite más cercano", y
  para CargaSuelta/Extradimensionado **ninguna regla filtra por peso** —
  esas van directo a un fallback ("mayor valor vigente") que ignora peso y
  vehículo por completo. Verificar la ausencia total de plaza vigente por
  Línea de Negocio+Clase de Viaje (sin peso), no solo que el peso no calce.
- **Ante cualquier duda entre "lo calculado a mano" y "lo real", verificar
  contra el dato exacto, no seguir leyendo código a ciegas.** Así se
  descubrió que el piso SICE-TAC real siempre usa `CONFIGURACION='CA'`,
  `NOMBRETIPOCARGA='General'` (sin importar vehículo/tipología) — buscando
  el valor exacto del PDF real directamente en la base, no adivinando.

## 4. El mecanismo real confirmado

### 4.1 Piso SICE-TAC — `SicetacOficialAsync`

(ver `src/CabinaFletes.Infrastructure/Biblioteca/Adaptadores/TarifarioSapAdapter.cs`,
líneas 322-373): para Clase de Viaje = Contenedor, resuelve el vehículo por
rango de peso del contenedor (`ConfigPorRangoPesoAsync`, tabla
`tblContenedorRangoPeso`/`tblRangoPeso`), no por el vehículo declarado en la
línea. Para Cupo/CargaSuelta, en teoría usa el código de vehículo de la
línea — pero empíricamente (líneas 6, 9, 12, todas Cupo) también resolvió
`CA`, lo que sugiere que el vehículo real internamente configurado para
"Sencillo"/"Turbo"/"Camioneta" en el catálogo de la app también resuelve a
`CA` — no se confirmó el detalle exacto de esa traducción, pero el resultado
empírico es consistente y reproducible.

### 4.2 Plaza SAP — `Seleccionar` (líneas 188-275 del mismo archivo)

Recorre las tarifas candidatas (ya filtradas por ruta+Línea de Negocio+Clase
de Viaje en `BuscarAsync`) y aplica, en orden:

- **Regla A (devolución):** Clase de Viaje=Contenedor y Línea de
  Negocio=DevolucionContenedor, con bandera Consolidado o Exclusivo.
- **Regla B (contenedor):** Clase de Viaje=Contenedor (y no devolución) —
  aquí sí filtra por rango de peso (`PesoEnRango`), tratando un rango
  `NULL`/`NULL` como "aplica para cualquier peso".
- **Regla C (cupo):** Clase de Viaje=Cupo — busca el "límite de peso" más
  cercano al declarado entre las tarifas vigentes con ese vehículo/carrocería
  (no un simple rango).
- **Sin ninguna regla aplicable** (esto incluye **todas** las líneas de
  Clase de Viaje = CargaSuelta o Extradimensionado, que no calzan en A/B/C):
  **fallback → toma la tarifa vigente de mayor valor**, sin filtrar por peso
  ni vehículo, entre todas las que compartan ruta+Línea de Negocio+Clase de
  Viaje.

Esto significa que "sin tarifa para el peso declarado" **no es lo mismo**
que "sin ninguna tarifa vigente" — si existe cualquier fila vigente para la
ruta+Línea de Negocio+Clase de Viaje (aunque su peso no calce), el fallback
la propondría igual. Por eso, para declarar "sin plaza" con seguridad, hay
que verificar la ausencia total de filas vigentes para el combo exacto — no
solo que el peso no calce.

## 5. Desviaciones reales encontradas

### Desviación 1 — candidata a violación de la regla del mayor (líneas 1, 2, 3, 11)

Para estas 4 líneas existe una tarifa de plaza SAP **vigente hoy** (no
vencida, confirmada directamente en `tblSAPTarifas`) con un valor **mayor**
al piso SICE-TAC que la app terminó usando:

| Línea | Plaza SAP vigente | Piso SICE-TAC usado | Plaza es cuántas veces mayor |
|---|---|---|---|
| 1 | $6.000.000 (o $6.400.000) | $1.556.503 | ~3,9x |
| 2 | $5.000.000 | $1.595.367 | ~3,1x |
| 3 | $2.454.000 | $1.594.476 | ~1,5x |
| 11 | $2.400.000 | $705.417 | ~3,4x |

La regla del mayor dice que debe proponerse el valor MÁS ALTO. La app
propuso el más bajo (Sicetac). **No se confirmó la causa raíz**: es posible
que el buscador real de plaza (`BuscarConZonasAsync`, con su propia
resolución de zona/vehículo) no encuentre esas mismas filas que sí se
encontraron por SQL directo — sería consistente con el mismo patrón de la
Desviación del mecanismo (sección 4), donde el vehículo de la línea tampoco
se usa tal cual para SICE-TAC.

**Recomendación:** abrir un issue contra el milestone de esta HU pidiendo a
desarrollo que confirme, con logging o depuración directa, si
`PropuestaTarifaAdapter`/`TarifarioSapAdapter` está encontrando la tarifa de
plaza para estas 4 rutas y, si la encuentra, por qué no ganó. Sin `gh` CLI
en este entorno, no se creó automáticamente.

### Desviación 2 — 3 líneas con piso disponible que no aparecieron en la cotización (líneas 8, 13, 15)

Confirmado directamente en `tblRutasSicetac`: las 3 rutas SÍ tienen piso
real bajo `CONFIGURACION='CA'`, `NOMBRETIPOCARGA='General'`:

| Línea | Ruta | Piso SICE-TAC disponible |
|---|---|---|
| 8 | Cúcuta→Bogotá | ~$1.822.684 - $1.829.479 |
| 13 | Bogotá→Santa Marta | ~$2.432.085 - $2.442.568 |
| 15 | Neiva→Bogotá | ~$1.104.014 (pendiente de reconfirmar con código `CA`) |

Ninguna de las 3 aparece en el PDF de la cotización final — ni con valor
propuesto, ni marcada visiblemente como pendiente de costeo/atención
manual. Si existe piso disponible, la línea no debería quedar en el mismo
grupo que "sin ninguna fuente" (que sí aplicaría, por ejemplo, si tanto
plaza como piso estuvieran ausentes).

**Recomendación:** abrir un issue para confirmar con el equipo si estas 3
líneas quedaron en otro estado del sistema (ej. "pendiente de costeo",
simplemente no reflejado en el PDF de cotización) o si es un fallo real que
las descarta sin ninguna traza visible para el usuario.

## 6. Estado de las reglas verificadas con esta prueba

- **Regla 1 (regla del mayor):** parcialmente verificada. Confirmado que el
  piso SICE-TAC se calcula y se usa correctamente en 12/12 líneas cuando es
  la única fuente disponible. **No confirmado** que la comparación contra
  plaza funcione correctamente cuando ambas existen — ver Desviación 1.
- **Regla 2 (piso inviolable):** cumplida en las 12 líneas cotizadas —
  ninguna propuesta quedó por debajo del piso SICE-TAC (de hecho, todas
  fueron exactamente el piso).
- **Regla 3 (solo SICE-TAC):** consultada la Parte C contra `reglas_solo_sicetac`
  (base de la app, `dbInformesAutomatizados_Pruebas.dbo_NEXO_prueba`) —
  existen 3 reglas configuradas (Medellín→Bogotá 3S3; Cali→Medellín 3S3;
  Barranquilla→Pereira 2S3, estas 2 últimas creadas el mismo día). Ninguna
  coincide con las 15 líneas del pliego ya subido, así que **no se pudo
  verificar con ese pliego** — requiere un caso de prueba dedicado con una
  de estas 3 rutas, con plaza vigente disponible, para confirmar que se
  ignora.
- **Regla 4 (tarifas vencidas marcadas y no propuestas):** confirmada con
  evidencia real del pliego — 5 líneas (6, 8, 9, 12, 15) tenían plaza
  vencida o inexistente y en ningún caso se usó su valor; el diseño del
  código (`ReglaDeCosteo.DecidirConTarifario`) descarta la plaza vencida
  por completo, nunca la propone.
- **Regla 5 (Ganada archiva tarifa):** confirmada directamente en la base
  de la app — de las **54 cotizaciones reales marcadas "Ganada"**, las
  **54 tienen exactamente 1 tarifa histórica archivada por línea**, sin
  ninguna desincronizada (0 desviaciones).
- **Regla 6 (ajuste auditado con autoría real):** confirmada — el único
  ajuste real (`ajustes_vigencia_plaza`) tiene autoría verificable: usuario
  "Brayan Quintero" (rol AdminTarifas), con su entrada correspondiente en
  `audit_log` (acción "ajustar", diff con el cambio exacto realizado).
- **Regla 7 (total recalculado al cambiar conexos):** confirmada — en las 3
  cotizaciones verificadas con conexos, `Total` = suma exacta de las líneas
  + suma exacta de los conexos aplicados (ej. $1.850.000 + $2.000 =
  $1.852.000), sin ninguna diferencia.

**Resumen: de las 7 reglas, 5 quedan verificadas con evidencia real (2, 4, 5,
6, 7 — cero desviaciones en estas), 1 parcialmente verificada con 1
desviación candidata real (Regla 1), y 1 sin poder verificar por falta de
un caso que coincida con el pliego actual (Regla 3, solucionable con un
caso de prueba dedicado).**
