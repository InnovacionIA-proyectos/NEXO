# Decisiones técnicas de NEXO

Registro de las decisiones de diseño ya tomadas en NEXO y en su integración con el Cubicador. Cada una explica qué
problema resolvía, qué se decidió, cómo se comprobó y cuándo conviene volver a revisarla.

Una decisión nueva se agrega al final con el siguiente número (`D-13`, `D-14`…) y con la misma estructura. Si una
decisión cambia, se actualiza en su lugar y se anota el cambio en su apartado «Historial»; no se crea un documento
aparte.

## Índice

| # | Decisión | Área | Estado | Fecha |
|---|---|---|---|---|
| [D-01](#d-01) | Cada vehículo se busca con su propio código en SICE-TAC y en plaza | Costeo | Vigente · falta confirmar con datos reales | 2026-10-27 |
| [D-02](#d-02) | Si el camino oficial de SICE-TAC no encuentra piso, se reintenta con el selector tolerante | Costeo | Vigente · falta remedir con datos reales | 2026-09-18 |
| [D-03](#d-03) | La plaza SAP sin coincidencia exacta se usa, pero con advertencia | Costeo | Vigente | 2026-09-18 |
| [D-04](#d-04) | El score de prioridad suma los cuatro componentes ponderados | Priorización | Vigente | 2026-09-18 |
| [D-05](#d-05) | Solo se paginan las bibliotecas de SICE-TAC y de plaza SAP | API | Vigente | 2026-09-18 |
| [D-06](#d-06) | La lentitud de la biblioteca SICE-TAC se mitiga desde NEXO y el índice se pide al DW | API | Vigente · índice pendiente del DW | 2026-09-18 |
| [D-07](#d-07) | NEXO genera el id del plan de cargue | Plan de cargue | Vigente | 2026-09-09 |
| [D-08](#d-08) | El ajuste manual de colocaciones es un comando asíncrono | Plan de cargue | Vigente | 2026-09-09 |
| [D-09](#d-09) | El evento `plan-de-cargue.generado` viaja completo, sin paginar ni comprimir | Plan de cargue | Vigente | 2026-09-09 |
| [D-10](#d-10) | Uso de Claude: llave, datos que salen, retención y modelo | IA | Propuesta · aprobaciones pendientes | 2026-10-27 |
| [D-11](#d-11) | La IA no decide la flota del Cubicador: solo modo sombra, interpretación y explicación | IA | Preliminar · falta cerrar el modo sombra | 2026-10-30 |
| [D-12](#d-12) | La identidad del portal viaja sin token | Identidad | Vigente | 2026-08-24 |

## Términos

| Término | Significado |
|---|---|
| Piso SICE-TAC | Valor mínimo legal del flete para una ruta y configuración vehicular, tomado de `DWSice.dbo.tblRutasSicetac`. |
| Plaza SAP | Tarifa de mercado registrada en SAP (`tblSAPTarifas` y su histórico `tblSAPTarifasLog`). |
| Plaza propia | Tarifa de plaza mantenida en NEXO (`tbl_tarifas_plaza` y su histórico). |
| Camino oficial | Búsqueda automática del piso SICE-TAC que hace el costeo cuando la línea tiene clase de viaje y hay conexión a SAP. |
| Camino «Solo SICE-TAC» | Búsqueda que se usa cuando el Administrador de Tarifas marca la ruta como «Solo SICE-TAC». Es más tolerante que el camino oficial. |
| Comando asíncrono | Petición que NEXO acepta con `202 Accepted` y que el Cubicador ejecuta después, a través de RabbitMQ. |

---

## Costeo

<a id="d-01"></a>
### D-01 · Cada vehículo se busca con su propio código en SICE-TAC y en plaza

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente · falta confirmar con datos reales | 2026-10-27 | `tbl_mapeo_vehiculo_tarifario` (carga inicial en `database/seeder/mapeo_vehiculos.csv`), `IMapeoVehiculoTarifarioPort` |

**Contexto.** SICE-TAC y plaza SAP nombran los vehículos con códigos distintos a los de NEXO. Al buscar con el código de
NEXO no aparecían filas, y el costeo tomaba sin avisar el vehículo más barato de la ruta (la camioneta `CA_`). El caso más
visible era el Sencillo: en NEXO es `C2` y en las dos fuentes es `2`, por lo que salía más barato de lo que era.

**Decisión.** Antes de consultar cada fuente, el vehículo de NEXO se traduce con la tabla de mapeo:

| Consulta | Código que se usa |
|---|---|
| Piso SICE-TAC (propuesta, aprovisionamiento, retorno de contenedor y camino oficial) | `CodigoSicetac`, comparado con `CONFIGURACION` de `tblRutasSicetac` |
| Plaza SAP (`tblSAPTarifas` y su histórico) | `CodigoPlazaSap`, convertido a id con el catálogo de vehículos de SAP |
| Plaza propia (`tbl_tarifas_plaza` y su histórico) | `CodigoPlazaSap` **o** el código del propio vehículo |

Reglas complementarias:

- **Vehículo sin fila de mapeo, o tabla no disponible:** se busca con su propio código. En el camino oficial de SICE-TAC se
  conserva la regla anterior de quitar la `C` inicial. Si la base no responde, el mapeo se da por vacío durante un minuto,
  se registra un aviso en el log y el costeo continúa sin fallar.
- **`CodigoPlazaSap` vacío significa que el vehículo no tiene tarifa de plaza.** Es el caso de `2_5`, `2_7`, `3S2`, `CA_`,
  `V2`, `V3` y `V4`, que solo existen en SICE-TAC. En una línea de cupo no se ofrece la tarifa de plaza de otro vehículo.
  En contenedor no cambia nada, porque ahí la plaza depende de la clase de contenedor y del peso, no del vehículo.
- **Turbo:** en plaza, `2L1`, `2L2` y `2L3` se tarifan como `2L2`. En SICE-TAC cada uno conserva su código.
- El mapeo se guarda en memoria cinco minutos. Es un servicio único con su propio alcance de base de datos, porque lo
  consultan varios costeos en paralelo.
- Ya no hace falta quitar la `C` inicial con `TrimStart('C')`, que además convertía `CA_` en `A_`.

**Plaza propia: se aceptan los dos códigos.** La carga inicial de `tbl_tarifas_plaza` registró algunos vehículos con
códigos de SICE-TAC (`3S2` en 21 filas y `2L3` en 1). Según el mapeo, `3S2` no tiene código de plaza y `2L3` se tarifa
como `2L2`. Mientras no se decida si se corrige esa tabla o se separan las columnas del mapeo, la búsqueda acepta el
código de plaza del mapeo y también el propio del vehículo. Es una ampliación: todo lo que antes coincidía sigue
coincidiendo.

**Contenedores.** En una línea de contenedor:

- **El vehículo** sale de `tbl_rangos_peso_contenedor` (carga inicial en `database/seeder/rangos_peso_contenedor.csv`).
  Entre las filas del tamaño del contenedor (20 o 40 pies, deducido de la clase: `CNT40.HC`, `CNT20.IT`…), se toma la de
  menor `PesoMaximo` que alcance para el peso. Ese vehículo se traduce con el mapeo antes de buscar en SICE-TAC. Por encima
  del tope de cada tamaño (34.000 kg en 40 pies y 30.000 kg en 20 pies) ningún vehículo aplica y no hay piso. Si la tabla
  no está disponible se usan los rangos de SAP (`tblRangoPeso` y `tblContenedorRangoPeso`). Esta regla aplica en el camino
  oficial y, cuando la línea no trae vehículo, también en la búsqueda tolerante.
- **La carrocería por defecto es `PORTACONTENEDORES`.** Antes quedaba en `ESTACAS` (parámetro SAP 458). Si SICE-TAC no
  tiene filas de portacontenedores para la ruta, se vuelve a la unidad de siempre y la unidad usada queda en el criterio
  del candidato. Una carrocería explícita en la línea, por ejemplo furgón, prevalece.

Frente a los rangos de SAP cambian algunos resultados, porque la tabla nueva sale del documento de negocio. En 40 pies,
25.000 kg pasa de 3S3 a 3S2, 22.500 kg de 3S3 a 2S3 y 5.000 kg de 2S2 a 2L3. En 20 pies, 8.200 kg pasa del camión de dos
ejes a 2S2. Las pruebas cubren los pesos de cada borde.

**Cómo se verificó.**

- Contra la tabla real: los 15 vehículos resuelven exactamente a los códigos del CSV.
- Contra el costeo real con un DWSice simulado (una base temporal con `tblRutasSicetac`): `C2` encuentra la fila `2` (antes
  caía en `CA_`), `C3` la `3` y cada Turbo su código. Quitar el mapeo hace fallar esas pruebas
  (`tests/Nexo.Api.Tests/Biblioteca/clsMapeoVehiculoSicetacTests.cs` y `clsMapeoVehiculoTarifarioTests.cs`).

**Pendiente.**

- Confirmar con el DWSice y SAP reales el Turbo y el Sencillo.
- Confirmar que las 11 líneas de contenedor del análisis Inbound coinciden con el Excel del cliente (por ejemplo,
  3S3 + PORTACONTENEDORES = 8.320.586).
- Decidir qué se hace con los códigos de SICE-TAC que quedaron en `tbl_tarifas_plaza`.

<a id="d-02"></a>
### D-02 · Si el camino oficial de SICE-TAC no encuentra piso, se reintenta con el selector tolerante

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente · falta remedir con datos reales | 2026-09-18 | `clsPropuestaTarifaAdapter.ProponerLineaAsync` |

**Contexto.** El costeo automático cubría solo 58 de 451 líneas (12,9 %), lejos de la meta del 80 %. Un caso
reproducible era Medellín–Guarne: no recibía flete propuesto, pero al marcarla «Solo SICE-TAC» el piso aparecía de
inmediato. La ruta siempre había estado en la tabla, con tarifa vigente.

La causa es que el costeo tiene dos caminos para encontrar el piso, y el automático es mucho más estricto:

| | Camino oficial (automático) | Camino «Solo SICE-TAC» (manual) |
|---|---|---|
| Método | `clsTarifarioSapAdapter.SicetacOficialAsync` | `clsPropuestaTarifaAdapter.CandidatoSicetacAsync` + `SeleccionarSice` |
| Configuración | Igualdad exacta, sin alternativa | Exacta; si no hay filas, se reintenta sin filtrar por configuración |
| Tipo de carga | Igualdad exacta con un parámetro global de SAP (457, normalmente `General`) | Se deduce de la tipología de la línea, con `General` como alternativa |
| Unidad de transporte | Igualdad exacta con otro parámetro global de SAP (458) | No filtra; se usa después para ordenar candidatos |
| Vía estándar | `= 'SI'` como filtro: descarta toda fila con `NO` | Solo ordena: prefiere `SI`, nunca descarta `NO` |
| Clase de viaje | Solo Contenedor y Cupo; Carga suelta y Extradimensionado quedan fuera | Cualquiera |

Cualquiera de esos cinco filtros puede descartar una fila que existe y tiene tarifa vigente. Medellín–Guarne es un
corredor corto, el perfil típico de una ruta sin fila `VIAESTANDAR = 'SI'`.

**Decisión.** Si el camino oficial no encuentra piso, se reintenta con el mismo selector tolerante del camino «Solo
SICE-TAC» antes de rendirse:

```csharp
var sicetac = oficial?.SicetacOficial;
if (sicetac is null && dw is not null)
    sicetac = await CandidatoSicetacAsync(dw, origen, destino, vehiculo, linea.Tipologia, linea.ConfiguracionVehiculo?.Carroceria, ct);
```

El camino oficial no se reemplaza: con un pliego real de 15 líneas, comparado una a una contra una cotización real, ya
acertaba en 8 de 12 líneas cotizadas. Su forma de resolver la configuración (por rango de peso en contenedor y por código
de vehículo en cupo) está probada y no se toca. El reintento solo corre cuando el camino oficial no encontró nada, así que
donde ya funcionaba el resultado no cambia. La regla de costeo (`clsReglaDeCosteo`) tampoco cambia: estaba bien, solo no
recibía piso en estos casos.

**Historial.** Después se cambió la forma de comparar la ruta: con los dos municipios de la línea resueltos, el camino
oficial compara por código DANE (`ORIGEN` y `DESTINO` de `tblRutasSicetac`, sin el cero inicial), igual que el camino
«Solo SICE-TAC». El patrón `LIKE '%origen%-%destino%'` queda solo como respaldo cuando falta alguno de los dos códigos.
Con texto, `%YUMBO%-%CALI%` también coincidía con `YUMBO - CALIMA`, y Cartagena–Ciénaga se resolvía con Ciénaga de Oro
(Córdoba) en vez de Ciénaga (Magdalena). El criterio del candidato lo muestra: termina en `ruta DANE <origen>-<destino>`.

**Cómo se verificó.** No hay proyecto de pruebas para `Nexo.Infrastructure`: los adaptadores consultan SQL Server real sin
dobles de prueba. Las consultas para verificarlo con datos reales están en `tests/QA/s4-hu-07-matching-sicetac-oficial/`:

1. `verify-medellin-guarne.sql`: cuál de los cinco filtros descartaba la ruta.
2. `verify-otros-2-casos-cargasuelta.sql`: líneas reales de Carga suelta y Extradimensionado afectadas.
3. Repetir el pliego de prueba después del despliegue y comparar con `GET /api/metricas/kpis-piloto`.

| | Antes | Después |
|---|---|---|
| Líneas costeadas automáticamente | 58 de 451 (12,9 %) | Por medir con el mismo pliego |

**Cuándo revisarla.** Si después de remedir la cobertura sigue lejos del 80 %, las causas que esta decisión no resuelve
son las líneas que no tienen ni plaza SAP vigente ni piso SICE-TAC para su ruta. Esas deben quedar para revisión manual.

<a id="d-03"></a>
### D-03 · La plaza SAP sin coincidencia exacta se usa, pero con advertencia

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-09-18 | `clsTarifarioSapAdapter.Seleccionar`, `CandidatoTarifaDto.Advertencia` |

**Contexto.** Se había pedido conectar `ISapTarifaFleteEnVivoPort` a la plaza SAP, suponiendo que el costeo nunca proponía
una tarifa de plaza real. Al revisarlo se encontró lo contrario: el costeo ya consulta la plaza real a través de
`ITarifarioSapPort` (`tblSAPTarifas` y `tblSAPTarifasLog`, alimentadas desde SAP). `ISapTarifaFleteEnVivoPort` es otro
contrato, reservado para una integración en tiempo real con SAP que todavía no existe. Conectarlo a la búsqueda de texto de
`/biblioteca-sap-plaza` habría reemplazado el candidato correcto por uno peor, así que esa vía se descartó.

El problema real era otro. `Seleccionar` intenta una coincidencia exacta por vehículo, peso y tipo de viaje (reglas A, B y
C). Si ninguna aplica, toma la tarifa vigente de mayor valor de la ruta, sin distinguirla de una coincidencia real. Se
comprobó con una base SQL Server local con el esquema de `tblSAPTarifas` y `tblSAPTiposVehiculoTarifario`, datos
sintéticos y el código real:

| Escenario | Antes |
|---|---|
| Vehículo y peso dentro del límite | Encuentra la tarifa correcta |
| El mismo caso en minúsculas | Igual |
| El peso supera el límite de todas las tarifas del vehículo | Devuelve la de mayor valor sin avisar |
| El vehículo no está en `tblSAPTiposVehiculoTarifario` | Devuelve la de mayor valor sin avisar |

**Decisión.** La línea se sigue cotizando con esa tarifa, pero con una advertencia explícita:

- `CandidatoTarifaDto` tiene el campo `Advertencia`, nulo cuando la coincidencia es exacta. Cuando no lo es, el mensaje
  dice cuál fue el peso que no encajó o que el vehículo no está en el catálogo. Nunca es un mensaje genérico.
- Si la propuesta final es esa plaza, la advertencia se agrega a la justificación de la línea, que es lo que ve el usuario.
- El contrato está en `CONTRATO.md` (`CandidatoTarifaDto.advertencia`).

**Cómo se verificó.** Los mismos cuatro escenarios después del cambio: sin advertencia en los dos primeros y con la
advertencia correcta en los otros dos. La prueba fue manual: no queda como prueba automática porque no existe proyecto de
pruebas para `Nexo.Infrastructure`.

**Alternativa descartada.** Dejar la línea sin propuesta cuando no hay coincidencia exacta. Se prefirió cotizarla y que el
Administrador decida con la advertencia a la vista.

## Priorización

<a id="d-04"></a>
### D-04 · El score de prioridad suma los cuatro componentes ponderados

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-09-18 | `clsScorePrioridadCalculator`, `clsParametrosScore` |

**Contexto.** El cálculo obtenía los cuatro componentes (urgencia, valor, probabilidad y tipo), pero el total solo tomaba
la urgencia. La documentación se contradecía: una parte describía cuatro factores con pesos configurables y otra afirmaba
que el total era solo la urgencia, y dos pruebas unitarias lo confirmaban así.

Lo que indicaba que la versión correcta era la de cuatro pesos:

1. `clsParametrosScore` ya tenía los cuatro pesos, editables por el Admin con `PUT /api/priorizacion/parametros` y con
   valores por defecto que suman 1,0 (0,40 urgencia, 0,30 valor, 0,20 probabilidad y 0,10 tipo). Ningún cálculo los usaba.
2. El cálculo ya obtenía cada componente por separado, lo que solo tiene sentido si se van a ponderar.
3. En 387 solicitudes de prueba, 337 empataban exactamente en 50 y 45 en 100: el «score» era en realidad un tramo de
   urgencia (lejano, medio, vencido). Oportunidades de $9 millones y de $3,9 millones tenían el mismo puntaje.

La versión de solo urgencia no tenía ninguna explicación escrita. Todo indica que quedó así en un cambio que no se terminó
de conectar con los parámetros.

**Decisión.**

```
total = PesoUrgencia × urgencia + PesoValor × valor + PesoProbabilidad × probabilidad + PesoTipo × tipo
```

Con los pesos por defecto, el total queda en la misma escala de 0 a 100. Las pruebas que afirmaban «solo urgencia» se
reemplazaron por pruebas de la suma ponderada.

**Cómo se verificó.** Con `tests/QA/s4-hu-06.3-score-4-pesos-387-solicitudes/verify-score-4-pesos.sql` (solo lectura),
sobre las 409 solicitudes que había en QA ese día:

- Las oportunidades de $9.000.000 y $3.900.000, que antes compartían 100, quedan en 86,08 y 80,72.
- Desaparecen los empates exactos en 50 y 100.
- Aparece otro empate: 293 solicitudes en 44,0 y 61 en 49,0. Son las que no tienen fecha límite, valor estimado ni
  probabilidad de cierre (el 87 % del conjunto), así que solo se diferencian por el tipo (Spot o Licitación).

| | Antes (solo urgencia) | Después (cuatro pesos) |
|---|---|---|
| Valor más repetido | 50,00 en 352 de 409 (86 %) | 44,0 en 293 de 409 (72 %) |
| Segundo más repetido | 100,00 en 47 de 409 (11 %) | 49,0 en 61 de 409 (15 %) |

**Cuándo revisarla.** El empate que queda no es un defecto del cálculo sino de la captura: mientras falten fecha límite,
valor y probabilidad, ninguna fórmula puede diferenciar esas solicitudes. Está pendiente decidir si esos campos se vuelven
obligatorios o si tienen otro valor por defecto.

## API y consultas

<a id="d-05"></a>
### D-05 · Solo se paginan las bibliotecas de SICE-TAC y de plaza SAP

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-09-18 | `GET /api/biblioteca-sicetac`, `GET /api/biblioteca-sap-plaza` (ver `CONTRATO.md`) |

**Contexto.** El front identificó ocho pantallas de listado que traían todo el resultado en una sola petición. Pidió
paginar solo las que lo justificaran, con el patrón de `GET /api/servicios-conexos` (`page` + `pageSize`, respuesta
`{ items, total }`).

Se contaron las filas reales en QA. En las tablas propias de NEXO el número no representa el crecimiento real, porque QA
se limpia periódicamente. En las bases corporativas externas (DW de SICE-TAC y datacenter de SAP) sí, porque son
compartidas y reales.

| Endpoint | Tabla | Filas | Visibles antes | Tipo |
|---|---|---:|---:|---|
| `GET /biblioteca-sicetac` | `DWSice.tblRutasSicetac` (externa) | 2.148.120 | 50 | Real, filtrada por periodo |
| `GET /biblioteca-sap-plaza` | `tblSAPTarifas` (externa) | 22.793 | 50 | Real |
| `GET /solicitudes` | `solicitudes` | 190 | todas | Transaccional, dato de QA |
| `GET /planes-cargue` | `planes_cargue` | 37 | todas | Transaccional, dato de QA |
| `GET /biblioteca-sap-historico` | `tblSAPTarifasLog` (externa) | 0 | 0 | Externa, vacía al medir |
| `GET /reglas-solo-sicetac` | `reglas_solo_sicetac` | 0 | 0 | Catálogo de alta manual |
| `GET /tarifas-historicas` | `tarifas_historicas` | 10 | todas | Transaccional, dato de QA |
| `GET /vehiculos` | `vehiculos_cubicacion` | 3 | todas | Catálogo de alta manual |

**Decisión.**

- **Se paginan SICE-TAC y plaza SAP.** El recorte ya estaba ocurriendo: en SICE-TAC más de 2,1 millones de filas no las
  podía ver nadie, sin aviso. Se agregó el parámetro `page` (empieza en 1, opcional, por defecto 1) junto al `top` que ya
  existía, con `Skip((page - 1) * top)` antes del `Take(top)`. Es un cambio compatible: sin `page`, la respuesta es la
  misma de antes.
- **No se paginan los otros seis por ahora.** Los catálogos de alta manual no crecen solos. Los transaccionales sí crecen,
  pero el único conteo disponible es de QA. El histórico de SAP estaba vacío al medir; si crece, agregar `page` es un
  cambio pequeño, porque comparte el método interno con plaza SAP (`clsSapHistoricoAdapter.BuscarCoreAsync`).

**Cuándo revisarla.**

1. `reglas-solo-sicetac` y `vehiculos`: si cambia la forma de cargarlos (por ejemplo, una importación masiva).
2. `solicitudes`, `planes-cargue` y `tarifas-historicas`: cuando se pueda contar en producción o si se reporta lentitud.
3. `biblioteca-sap-historico`: si una medición nueva muestra un volumen importante.

<a id="d-06"></a>
### D-06 · La lentitud de la biblioteca SICE-TAC se mitiga desde NEXO y el índice se pide al DW

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente · índice pendiente del equipo del DW | 2026-09-18 | `clsDwSiceDbContext`, `clsBibliotecaSicetacAdapter` |

**Contexto.** Sin filtros, `GET /api/biblioteca-sicetac` tardaba 33 segundos en traer 3 filas. Con filtros de origen,
destino o periodo terminaba en error por tiempo de espera. La tabla `DWSice.dbo.tblRutasSicetac` tiene unos 2,14 millones
de filas y solo un índice, sobre la llave primaria. Sin índice:

- El filtro por periodo recorre la tabla completa.
- El orden por ruta, configuración y unidad, que va antes de la paginación, obliga a ordenar todo lo filtrado antes de
  cortar la página.
- La búsqueda de texto libre usa `LIKE '%término%'`, que no aprovecha índices.
- La lista de periodos disponibles hace un `DISTINCT` sobre toda la tabla antes de cualquier filtro. Es la causa más
  probable de los 33 segundos sin filtros.

El arreglo de fondo no está en NEXO. El contexto del DW es de solo lectura (sin migraciones y con el guardado bloqueado), y
la tabla la mantiene el proceso de carga del Data Warehouse corporativo.

**Decisión.**

Desde NEXO se aplicaron dos mitigaciones:

1. Tiempo de espera de 90 segundos para las consultas al DW, en lugar de los 30 por defecto (`clsDependencyInjection`).
2. La lista de periodos se guarda en memoria seis horas en lugar de quince minutos, porque los periodos cambian una vez al
   mes.

Al equipo del Data Warehouse se le pidió este índice (ajustando los nombres de columna si difieren):

```sql
CREATE NONCLUSTERED INDEX IX_tblRutasSicetac_Periodo_Orden
    ON dbo.tblRutasSicetac (Periodo, NombreRuta, Configuracion, NombreUnidadTransporte)
    INCLUDE (Origen, Destino, NombreTipoCarga, CondicionCarga, ViaEstandar,
             Valor, ValorHora, ValorSiceConHora, HorasRecorrido, HorasPactadas,
             FechaCreacion, FechaModificacion);

CREATE NONCLUSTERED INDEX IX_tblRutasSicetac_Periodo
    ON dbo.tblRutasSicetac (Periodo);
```

**Pendiente.**

- Medir los tiempos de respuesta antes y después de que el DW aplique el índice, con y sin filtros.
- Confirmar la versión de SQL Server del DW. La paginación usa `OFFSET/FETCH`, que no existe antes de SQL Server 2012.

## Plan de cargue y Cubicador

<a id="d-07"></a>
### D-07 · NEXO genera el id del plan de cargue

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-09-09 | `clsCrearPlanCommand`, comando `crear_plan` del Cubicador |

**Contexto.** Antes, solo el Cubicador creaba planes de cargue con id nuevo. NEXO no podía conocer el id antes de que el
Cubicador terminara, lo que rompía el patrón de los trabajos pesados: responder `202` con el identificador en la misma
petición que acepta el trabajo (`docs/contrato-integracion/README.md`, §4).

**Decisión.** NEXO genera el `planId` (`Guid.NewGuid()`) al recibir `POST /api/planes-cargue` y lo envía en el comando
`crear_plan` (routing key `cubicacion.solicitada`):

```json
{ "tipo": "crear_plan", "planId": "<guid generado por NEXO>",
  "correlationId": "<igual a planId>",
  "solicitud": { "nombre": "...", "vehiculoId": "..." } }
```

- El worker del Cubicador crea el plan con ese id, nunca con uno propio. Cuando el id no viene (creación síncrona directa,
  `crear_plan_demo` o `crear_desde_linea`), lo sigue generando él. Lo cubre la prueba
  `cubicador/tests/worker/test_ejecutar_solicitud.py::test_crear_plan_usa_el_planid_que_manda_nexo_no_uno_propio`.
- En la misma petición, antes de publicar el comando, NEXO guarda una copia del plan en estado `borrador` con el mismo
  patrón de Outbox de la optimización. Así `GET /api/planes-cargue/{id}` responde algo coherente sin esperar al Cubicador.

**Efecto colateral resuelto.** En NEXO, `PlanCargue.VehiculoId` tiene una llave foránea a `VehiculosCubicacion`, pero el
vehículo que llega es del catálogo del Cubicador y nunca existió en NEXO. En lugar de quitar la llave foránea, antes de
crear el plan se asegura una fila mínima con solo `Id` y `Nombre` (`IPlanCargueRepository.PrepararCreacionAsync`). Nadie
más la lee. Está documentado también en `docs/mensajeria/CUBICACION.md`, sección 9.

**Cuándo revisarla.**

- Si NEXO necesita datos reales del vehículo y no solo el id: habría que replicar el catálogo o consultarlo al Cubicador.
- Si `crear_plan_demo` o `crear_desde_linea` llegan a necesitar un id conocido de antemano.

**Alternativas descartadas.**

- *Que el Cubicador genere el id y lo avise por un evento.* Obliga a crear un evento solo para eso, y el front no podría
  armar la URL de seguimiento hasta recibirlo.
- *Quitar la llave foránea de `PlanCargue.VehiculoId`.* Es más limpio a largo plazo, pero cambia el esquema para resolver
  algo que una fila sin uso resuelve.

<a id="d-08"></a>
### D-08 · El ajuste manual de colocaciones es un comando asíncrono

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-09-09 | `PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}`, `clsAjustarColocacionCommand`, comando `ajustar_colocacion` |

**Contexto.** El ajuste manual (mover, rotar, bloquear, retirar o reinsertar una unidad ya colocada) solo existía en el
Cubicador y era síncrono: el navegador le hablaba directo al motor 3D. Era la única escritura del plan de cargue que no
pasaba por la cola, con sus reintentos (1 s, 5 s, 15 s y 60 s), su cola de errores y su alerta.

**Decisión.** El ajuste pasa a ser `ajustar_colocacion`, uno de los ocho comandos asíncronos del plan de cargue:

- El endpoint está en NEXO, nunca en una URL del Cubicador, y responde `202` de inmediato con `ComandoAceptadoDto`.
- El worker del Cubicador aplica las mismas validaciones que ya tenía (límites, colisión, soporte mínimo del 60 % y
  apilamiento), sin cambios en las reglas.
- **Interfaz optimista:** el front muestra el ajuste en pantalla al recibir el `202`, sin esperar confirmación. Es una
  interacción de arrastrar y soltar en un visor 3D, y esperar la vuelta por la cola la haría inutilizable.

**Si el Cubicador rechaza el ajuste:**

1. El Cubicador publica `cubicacion.rechazada` con el código y el motivo. Es el mismo evento que usa la optimización cuando
   falla.
2. `clsCubicacionRechazadaConsumer` marca el plan en estado `error` en NEXO y avisa por `/hubs/plan-cargue` con el motivo
   en `AvisoPlanCargueDto.Motivo`.
3. El front revierte el movimiento o vuelve a consultar `GET /api/planes-cargue/{id}`. Es el mismo patrón del resto de los
   trabajos pesados: el aviso es una ayuda, la fuente de verdad es el GET.

No hizo falta ningún evento ni canal nuevo.

**Cuándo revisarla.**

- Si el tiempo entre el `202` y la confirmación es tan largo que aparecen ediciones simultáneas en conflicto sobre la misma
  colocación. Hoy no hay control de versión, solo reconciliación posterior.
- Si el front necesita distinguir «procesando» de «confirmado» por colocación. Hoy el aviso es por plan.

**Alternativas descartadas.**

- *Dejarlo síncrono y solo mover la URL a NEXO.* Seguiría siendo la única escritura sin reintentos ni cola de errores, y
  NEXO tendría que manejar una llamada HTTP saliente al Cubicador.
- *Control de versión por colocación (ETag).* Resolvería las ediciones simultáneas, pero es complejidad que hoy no se
  necesita. Es lo primero a considerar si se cumple la primera condición de revisión.

<a id="d-09"></a>
### D-09 · El evento `plan-de-cargue.generado` viaja completo, sin paginar ni comprimir

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-09-09 | Evento `plan-de-cargue.generado` (forma en `docs/mensajeria/CUBICACION.md`, sección 4) |

**Contexto.** El evento pasó de llevar solo el conteo de colocaciones a llevar el resultado completo (`items`,
`colocaciones`, `metricas`, `incidencias` y los totales de peso, volumen y uso). Antes de escribir el consumidor había que
saber cuánto pesaba.

Se midió con datos sintéticos, corriendo el motor real sin dobles de prueba para que la forma del resultado fuera idéntica
a la de un plan real. No se usaron datos de clientes.

| Escenario | Colocaciones | Evento con solo el conteo | Evento completo | Por colocación |
|---|---|---|---|---|
| Plan grande típico: 150 unidades, 10 tipos de ítem | 150 | 184 bytes | 165,78 KB | ~1,13 KB |
| Peor caso: 2.000 unidades (tope de una importación), 20 tipos | 2.000 | 185 bytes | 2.889,75 KB (≈ 2,9 MB) | ~1,48 KB |

Los dos escenarios usaron el vehículo más grande del catálogo (tractocamión furgón de 13,6 m). En el peor caso el motor
cargó 940 unidades y rechazó 1.060, y todas aparecen en `colocaciones`.

**Decisión.** El evento viaja completo, sin paginar ni comprimir:

1. RabbitMQ admite mensajes de hasta 128 MB por defecto, muy por encima de 2,9 MB.
2. El Outbox de MassTransit guarda el cuerpo en una columna `max`, así que unos pocos MB no son problema.
3. El evento se publica una vez por cada escritura del plan, no con alta frecuencia.
4. El peor caso es el tope de una sola importación. Un plan realista pesa unas 18 veces menos.

**Cuándo revisarla.**

- Si los planes reales se acercan habitualmente a las 2.000 unidades.
- Si se mide en producción un evento de más de 5 MB.
- Si el consumidor se vuelve lento. En ese caso, lo primero es el guardado de cientos o miles de filas en una sola
  transacción, no el tamaño del mensaje.

**Si hubiera que resolverlo.** Primero comprimir el cuerpo con gzip. El JSON repite las mismas claves en cada elemento y se
estima que el peor caso bajaría a 300–600 KB, sin cambiar los campos del contrato. Paginar sería el último recurso: exige
identificar las partes, rearmarlas en NEXO y manejar fallos parciales.

**Alternativas descartadas.**

- *Paginar desde ya.* Es lo más costoso y no hay evidencia de que haga falta.
- *Comprimir desde ya.* Es más barato, pero tampoco hace falta hoy y dificulta revisar a mano los mensajes de la cola de
  errores (`cubicacion.dlq`), que hoy se leen como texto.

## Inteligencia artificial

<a id="d-10"></a>
### D-10 · Uso de Claude: llave, datos que salen, retención y modelo

| Estado | Fecha | Dónde vive |
|---|---|---|
| Propuesta · aprobaciones pendientes (punto 9) | 2026-10-27 | NEXO (`clsAnthropicAdapter`, `clsOpcionesAnthropic`) y Cubicador |

**Contexto.** NEXO usa la API de Anthropic para la importación de Excel, el asistente de cotización y el apoyo al
Cubicador. Antes de enviar datos a un proveedor externo había que fijar dónde vive la llave, qué datos pueden salir, qué
retención se acepta, qué modelo se usa y cómo se prueba sin exponer datos reales. Ninguna función se activa en un ambiente
mientras falte alguna aprobación del punto 9.

**Decisión.**

**1. La llave vive solo en el servidor.**

- El front nunca llama a Anthropic. Una llave en el navegador la puede usar cualquiera, sin rol, sin auditoría y sin forma
  de apagarla.
- Quien necesita IA llama a un endpoint de NEXO, con la identidad del portal y su rol, y NEXO llama a Claude.
- Hay una llave por servicio (NEXO y Cubicador), cada una en su propia *workspace* con límite de gasto mensual. Se puede
  revocar una sin afectar la otra y el costo se ve por servicio.
- La llave está en las variables de entorno de Dokploy y en GitHub Secrets (`Anthropic__ApiKey`). Nunca va en el
  repositorio, en `appsettings`, en logs ni en mensajes de error.
- Sin llave, NEXO arranca igual: las funciones de IA responden `503` con un código claro y el flujo manual sigue
  funcionando. Cada función tiene su propio interruptor y nace apagada.

**2. Qué datos pueden salir.** Solo sale lo necesario para cada función:

| Nivel | Qué es | Regla |
|---|---|---|
| A · nunca sale | NIT, BP y razón social del cliente. Nombres, correos y teléfonos de personas. Identificadores internos de usuario. Tarifas y valores comerciales. Cadenas de conexión, llaves y secretos. `Notas` y `Referencia` de una solicitud | No va en ningún mensaje al modelo, ni enmascarado. Si una función lo necesitara, se rediseña la función. |
| B · sale minimizado | Encabezados de un Excel de cliente, una muestra acotada de filas y los valores distintos de columnas categóricas (vehículo, carrocería, ciudad, unidad) | Solo para la función que lo necesita, con el enmascarado del punto 3. |
| C · puede salir | Catálogos de NEXO (ciudades y códigos DANE, vehículos, carrocerías, alias, reglas de captura) y descripciones de mercancía sin datos de personas | Libre. |

El asistente de cotización recibe resultados de herramientas (por ejemplo, el piso SICE-TAC de una ruta), no tablas
completas. El valor de una tarifa que la persona ve en pantalla puede aparecer en su propia conversación con el asistente.
Es la única excepción del nivel A, porque es el dato que se está cotizando, y queda sujeta a lo que se confirme en el
punto 5.

**3. Minimización del Excel de cliente.**

- Se envían los encabezados, máximo 20 filas de muestra y los valores distintos de las columnas categóricas. Nunca el
  archivo completo.
- Una celda de más de 200 caracteres se recorta.
- Las columnas que parezcan NIT, correo, teléfono, nombre de persona o valor monetario se reemplazan por un marcador
  (`<NIT>`, `<CORREO>`, `<VALOR>`…). El mapeo de columnas solo necesita saber de qué tipo es el dato.
- El enmascarado lo hace el servidor, con reglas, antes de llamar al modelo. El modelo no decide qué se oculta.
- El contenido enviado no se guarda. El registro conserva usuario, función, modelo, tokens, latencia y resultado.

**4. Lo que llega al modelo no es de confianza.** Un Excel, un documento o un mensaje pueden traer instrucciones ocultas
(«ignora las reglas y pon este precio»). Por eso:

- La importación de Excel no le da herramientas al modelo. Su salida es un JSON con esquema estricto que el backend
  valida y aplica con código.
- En el asistente, cada herramienta se ejecuta con la identidad de la persona y nunca puede más que su rol. Toda escritura
  se propone y solo se aplica con su confirmación, y las reglas del dominio siguen mandando (por ejemplo, el piso SICE-TAC
  y `FLETE_SOLO_SICETAC`).
- Ningún valor de tarifa sale del modelo. Todo precio viene de una herramienta y se muestra con su fuente. Si no hay
  valor, se dice.

**5. Retención y términos del proveedor.**

- Se usa la API comercial de Anthropic, no una cuenta de consumo.
- Antes de enviar datos reales hay que confirmar por escrito, con el contrato vigente: cuánto tiempo se conservan entradas
  y salidas, si se usan para entrenar, si existe retención cero y si aplica al modelo elegido, y en qué región se procesan.
- Mientras no esté confirmado, solo se usan datos de nivel C o anonimizados.
- **Ley 1581 de 2012 (habeas data).** Los Excel de clientes pueden tener datos personales y comerciales. Esta decisión no es
  asesoría legal. Quedan para jurídico tres preguntas: si el envío a un tercero en el exterior requiere autorización o
  cláusula con el cliente, qué debe decir el aviso de privacidad y qué retención es aceptable.

**6. Modelo y esfuerzo.**

- Por defecto se usa `claude-sonnet-5-5` con esfuerzo `medium`. `medium` se eligió por costo: en la evaluación del mapeo
  de Excel acertó igual que `high` y gastó menos.
- El esfuerzo es uno solo para todas las funciones (mapeo y asistente). Si alguna necesita uno distinto, hay que separar la
  opción.
- El modelo y el esfuerzo son configuración (`Anthropic__Model` y `Anthropic__Effort`), no código. Cambiarlos es cambiar
  una variable de entorno.
- No se envían `temperature`, `top_p` ni `top_k`, porque la API los rechaza.
- Los identificadores de modelo se escriben como los publica el proveedor, sin sufijos de fecha.

**7. Pruebas sin datos reales.**

- El CI nunca llama a la API real. Se prueba con un adaptador falso y respuestas grabadas.
- La calidad de los prompts se mide aparte, bajo demanda, con un conjunto de evaluación. Esa corrida no hace parte del CI.
- Desarrollo y pruebas usan datos sintéticos o anonimizados. Un Excel real de cliente solo entra al conjunto de evaluación
  si el cliente lo autoriza y se anonimiza (sin datos de nivel A y con los valores comerciales reemplazados). Sin
  anonimizar, no se sube al repositorio.
- Las pruebas que crean datos en la base compartida usan una marca propia y los borran al terminar.

**8. Costo, uso e incidentes.**

- Cada llamada queda registrada en `tbl_ia_uso` (usuario, función, modelo, tokens, latencia y resultado), sin el contenido.
- Las conversaciones del asistente sí se guardan, en la base de NEXO y no en el proveedor. Cada conversación pertenece a
  quien la inicia e incluye sus mensajes y, por cada herramienta usada, la entrada, la respuesta, la fuente y el resultado.
  Es la prueba de dónde salió cada valor mostrado. Solo las ven su dueño y el Admin. Su retención la debe definir el
  responsable de datos; todavía no hay purga automática.
- Las preferencias propias de cada persona (`tbl_asistente_preferencias`) se guardan solo con su confirmación. Viajan al
  modelo después de las reglas del sistema, con la instrucción de que no las reemplazan. No pueden fijar valores de dinero
  ni apagar reglas.
- Antes de guardarse, la respuesta del asistente pasa por un filtro: si trae una cifra de dinero que no salió de una
  herramienta ni de lo que escribió la persona, se descarta y se guarda un aviso en su lugar.
- Hay tope de tokens por persona y día (500.000 por defecto), límite de gasto mensual por *workspace* e interruptor general
  de apagado.
- Si la llave se filtra o el gasto se dispara: revocar la llave en la consola del proveedor, apagar las funciones con el
  interruptor, rotar la llave en Dokploy y GitHub Secrets y revisar el registro de uso del periodo. El procedimiento
  completo está en `docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md`.

**9. Aprobaciones pendientes.** Mientras no se aprueben, no se envían datos reales al proveedor en ningún ambiente.

| Qué se aprueba | Quién | Estado |
|---|---|---|
| Clasificación de datos (punto 2) y enmascarado (punto 3) | Responsable de datos | Pendiente |
| Retención, entrenamiento y región del proveedor (punto 5) | Responsable de la cuenta de Anthropic | Pendiente |
| Habeas data: autorización o cláusula con clientes y aviso de privacidad (punto 5) | Jurídico | Pendiente |
| Salida a `api.anthropic.com` desde el ambiente desplegado | Infraestructura y seguridad | Pendiente |
| Modelo y esfuerzo por defecto (punto 6) | Producto | Pendiente |
| Uso de Excel reales de clientes en pruebas (punto 7) | Responsable de datos y el cliente | Pendiente |

**Consecuencias.**

- La IA se implementa como un puerto en `Application` y un adaptador en `Infrastructure`, igual que las demás
  integraciones, para que el proveedor se pueda reemplazar.
- Cada función nace con su interruptor, su tope de uso y su registro.
- Se agrega un gasto mensual en dólares que antes no existía. Se vigila con el registro de uso y los límites por
  *workspace*.

**Historial.** La propuesta original fijaba `claude-sonnet-4-6` como modelo por defecto. Después pasó a `claude-sonnet-5-5`,
y el esfuerzo `medium` se confirmó con la evaluación del mapeo de Excel.

<a id="d-11"></a>
### D-11 · La IA no decide la flota del Cubicador: solo modo sombra, interpretación y explicación

| Estado | Fecha | Dónde vive |
|---|---|---|
| Preliminar · falta cerrar el modo sombra y la aprobación | 2026-10-30 | `cubicador/application/ia_sombra/`, `evaluacion-ia/cubicador/` |

**Contexto.** El Cubicador recomienda qué vehículos y cuántos hacen falta para una carga con un motor determinista
(`seleccion_flota`). Había que decidir si la IA (Claude) amplía su papel más allá de comparar en segundo plano (modo sombra),
y con qué alcance. La decisión se apoya en dos mediciones que responden preguntas distintas:

| Medición | Pregunta | Estado |
|---|---|---|
| IA contra el conjunto de prueba validado | ¿Cuánto **acierta** la IA frente a respuestas correctas? | Hecha |
| Modo sombra en QA | ¿Cuánto **se parece** la IA al motor con tráfico real, cuánto falla y cuánto cuesta? | Pendiente: el modo sombra está apagado |

La metodología, las métricas y las consultas están en
[`evaluacion-ia/cubicador/ESTUDIO-CUBICADOR-IA.md`](../../evaluacion-ia/cubicador/ESTUDIO-CUBICADOR-IA.md).

**Decisión (preliminar).**

- **No** a que la IA decida la flota o reemplace al motor: no lo supera y falla de forma sistemática en lo geométrico, que
  es justo lo que el motor resuelve.
- **Sí** a encender el modo sombra en QA para recolectar datos reales. Es barato y no afecta la respuesta al usuario.
- **Sí** a usar la IA donde complementa al motor sin decidir: convertir descripciones libres de mercancía en posiciones
  (que el motor valida) y explicar la recomendación en lenguaje natural.

**Cómo se verificó.** Se midió la IA con el mismo prompt, el mismo esquema de salida y la misma validación del modo sombra,
sobre los 22 casos validados del conjunto de prueba y con las mismas métricas que la línea base del motor (los dos informes
tienen la misma huella de datos, `78884e7c8037`). Informes: `evaluacion-ia/cubicador/informe_ia_semilla.md` e
`informe_linea_base_semilla.md`.

| Métrica | Motor | IA (Sonnet 5.5, esfuerzo high) |
|---|---|---|
| Decisión (asignar o no asignar) | 100 % (22/22) | 100 % (22/22) |
| Motivo cuando no asigna | 100 % (2/2) | 100 % (2/2) |
| Acierto del vehículo (flota exacta) | 100 % (18/18) | 100 % (18/18) |
| Flota mínima (cantidad exacta) | 100 % (20/20) | **95 % (19/20)** |
| Respeta peso, volumen y unidades | 100 % (20/20) | 100 % (20/20) |
| Latencia p50 / p95 | 1,6 / 25,7 ms | **5,1 / 13,0 s** |
| Costo por cubicación | 0 | US$0,011 (estimado por lo alto) |

**El único error es grave.** Con 33 estibas de 120 × 100 × 150 cm, la IA propone una Minimula cuando hacen falta dos: el
volumen cabe (59 de 76 m³), pero en el piso, girando las estibas y apilando dos alturas, solo caben 32. Se repitió tres veces
y falló las tres. Recomendar menos vehículos de los necesarios produce una carga que no cabe: es el error más caro posible.
Las métricas de peso y volumen salen en 100 % porque comparan sumas, no el acomodo físico, que lo resuelve el motor.

Límites: son 22 casos sintéticos y sencillos, con una sola configuración (la del modo sombra). Alcanza para decir que la IA
hoy no supera al motor; no para decir cuánto acertaría con cargas reales.

En las dos funciones que sí se aprueban, la IA ya funciona junto al motor: la interpretación de descripciones libres acertó
23 de 23 casos y rechazó correctamente 7 de 7, y la explicación de la recomendación ya está disponible. En ninguna de las dos
decide: prepara datos o explica, y el motor sigue mandando.

**Criterios para reevaluar la IA en la selección de flota.** Los criterios iniciales eran «acierto igual o superior al
motor» y «p95 de latencia no peor». El segundo nunca se cumpliría (el motor responde en milisegundos y un modelo en
segundos) y no aplica en modo sombra, porque la IA corre aparte. Los criterios corregidos:

1. Acierto en flota mínima y en vehículo igual o superior al del motor, medido sobre **cargas reales validadas**.
2. **Cero propuestas con menos vehículos de los necesarios**, exigido aparte del promedio.
3. No retrasa la respuesta al usuario. Si la IA entrara en el camino de la respuesta, se fija un p95 máximo en segundos.
4. Costo por cubicación de US$0,02 o menos.
5. En modo sombra, menos del 5 % de intentos fallidos (tiempo agotado, límite del proveedor o respuesta inválida).

**Pendiente: modo sombra en QA.**

| | |
|---|---|
| Estado | Apagado. La tabla `iaSombra` ya existe en la base del Cubicador (vacía); falta la llave de Anthropic del Cubicador en QA. |
| Prueba local | Funcionó de punta a punta con el modelo real: la respuesta no cambió y quedó una fila en `iaSombra` con `Estado = OK`, la misma flota que el motor, 4,8 s y unos 2.400 tokens. |
| Tráfico esperado | Unas 15 recomendaciones por día hábil (134 entre el 14 y el 29 de octubre). |
| Ventana | 10 días hábiles desde el encendido, o 150 comparaciones con `Estado = OK`, lo que ocurra después. |
| Costo esperado | Unos US$0,17 por día y US$2 en toda la ventana. |

Para encenderlo: en Dokploy, app `cubicador-api`, configurar `IA_SOMBRA_HABILITADA=true`, `ANTHROPIC_API_KEY` y, si la llave
es de organización, `ANTHROPIC_WORKSPACE_ID`; redesplegar y comprobar que una recomendación pedida desde NEXO deja una fila
con `Estado = OK` (detalle en [`ROTACION-LLAVE-ANTHROPIC.md`](../despliegue/ROTACION-LLAVE-ANTHROPIC.md), sección 8). Si el
Cubicador pasa a su base propia antes de cerrar la ventana, las filas recolectadas se copian a la tabla `iaSombra` de esa base.

Al cerrar la ventana se completa esta tabla con las consultas del estudio (sección 7), de solo lectura:

| Métrica | Resultado |
|---|---|
| Ventana (desde / hasta) | |
| Intentos / comparaciones con `Estado = OK` | |
| Intentos fallidos, por `Estado` | |
| Acuerdo con el motor en asignar o no asignar | |
| Flota exacta igual a la del motor | |
| Misma configuración (sin mirar carrocería) | |
| Misma cantidad / la IA pide más / la IA pide menos | |
| Propuestas que exceden peso o volumen, o pierden unidades | |
| Latencia del modelo p50 / p95 | |
| Costo total / por cubicación | |

Cada comparación en la que la IA pide **menos** vehículos que el motor se revisa una por una, para saber si es un error como
el de las estibas o un error del motor.

**Cómo se confirma.** Si la revisión confirma algún caso en que la IA pidió menos vehículos de los necesarios, o el acuerdo
en flota exacta es menor al 95 %, la decisión queda confirmada. Si el acuerdo es alto y no hay casos de seguridad, se
mantiene igual (parecerse al motor no es superarlo) y la siguiente reevaluación se hace con cargas reales validadas, contra
los criterios de arriba.

**Siguiente paso.** Conectar la interpretación de descripciones libres al flujo real de captura: el texto libre de una
solicitud se convierte en posiciones, el motor las valida y recomienda, y lo que se rechaza se muestra con su motivo (nunca
se inventa un peso o una medida). Medirla con texto de capturas reales, que se escribe peor que el sintético (abreviaturas,
pulgadas, dos mercancías en una frase), y mostrar la explicación junto a la recomendación.

**Tareas para cerrar la decisión.**

| Tarea | Responsable |
|---|---|
| Definir la llave del Cubicador, cargarla en Dokploy con `IA_SOMBRA_HABILITADA=true` y redesplegar | Responsable del Cubicador, con quien administra la cuenta de Anthropic y el despliegue |
| Al cerrar la ventana, correr las consultas, revisar los casos en que la IA pide menos y completar los resultados | Responsable del Cubicador |
| Importar y validar cargas reales (`evaluacion-ia/cubicador/importar_cargas_reales.py`) y repetir la evaluación de la IA | Responsable del Cubicador |

**Aprobación.**

| Rol | Nombre | Decisión | Fecha |
|---|---|---|---|
| Responsable del Cubicador | | | |
| Líder técnico | | | |

**Cómo reproducir la medición.**

```bash
# gratis: arma los mensajes sin llamar al modelo
python evaluacion-ia/cubicador/evaluar_ia.py --simular

# con el modelo (necesita ANTHROPIC_API_KEY y, si la llave es de organización, ANTHROPIC_WORKSPACE_ID)
python evaluacion-ia/cubicador/evaluar_ia.py --salida-md evaluacion-ia/cubicador/informe_ia_semilla.md

# un caso, varias veces
python evaluacion-ia/cubicador/evaluar_ia.py --solo S-19 --repeticiones 3
```

La corrida completa (22 llamadas) costó unos US$0,24.

## Identidad

<a id="d-12"></a>
### D-12 · La identidad del portal viaja sin token

| Estado | Fecha | Dónde vive |
|---|---|---|
| Vigente | 2026-08-24 | `clsIdentidadPortalAuthenticationHandler`, `clsResolverIdentidadPortalAdapter` (ver `docs/contrato-integracion/README.md`, sección 1) |

**Contexto.** El portal de la Administración deja los datos de la persona en el navegador después del inicio de sesión:
identificador, nombre y lista de permisos. No hay token de sesión, firma ni fecha de vencimiento, nada que NEXO pueda
verificar contra el portal para confirmar que quien dice ser alguien realmente lo es. Conseguir esa pieza exige una
solicitud al equipo del portal, sin fecha comprometida.

**Decisión.** NEXO acepta el identificador que envía el navegador sin verificarlo, y resuelve los permisos en cada petición
contra la tabla de usuarios y roles del portal. La lista de permisos que trae el navegador no se usa.

**Qué protege y qué no.**

- **Sí cierra la falsificación de permisos.** Quien edite el almacenamiento del navegador para ponerse otro rol no logra
  nada: NEXO va a la tabla con el identificador y usa lo que la tabla diga. Un Comercial no puede alcanzar funciones de
  AdminTarifas manipulando el navegador.
- **No cierra la suplantación de identidad.** Cambiar el identificador por el de otra persona devuelve los permisos de esa
  persona. Por la misma razón, la auditoría puede registrar un autor que no es el real.

**Por qué se acepta.** El módulo corre en la red interna de la compañía y solo lo alcanza personal de la empresa.
Aprovechar la debilidad exige abrir las herramientas de desarrollo del navegador, entender el almacenamiento local y conocer
el identificador de otra persona. El riesgo residual se consideró bajo frente al costo de bloquear toda la integración
esperando una pieza que depende de otro equipo. La decisión es del líder técnico.

**Cuándo revisarla.** Cualquiera de estas situaciones invalida el razonamiento:

- El módulo deja de estar solo en la red interna.
- Aparecen usuarios externos, contratistas o proveedores en el portal.
- El portal empieza a emitir tokens de sesión.
- Una auditoría o una obligación de cumplimiento exige trazabilidad no repudiable.

**Qué costaría cerrarlo.** Poco, y por eso se diseñó así. El identificador viaja en un encabezado propio y nada más allá
del punto de entrada lo conoce; la resolución de permisos ya va contra la base. El día que exista un token, ese encabezado
se reemplaza por `Authorization` con su verificación, y el resto (los seis roles, la autorización y los controladores) no
cambia.

> El contrato nombra el encabezado `X-Portal-Usuario-Id`, pero el backend hoy lee `X-Portal-clsUsuario-Id`. Es una
> discrepancia abierta, documentada en `docs/contrato-integracion/README.md`.

**Alternativas descartadas.**

- *Login propio en NEXO.* Otra contraseña para el usuario y otro almacén de credenciales que proteger, y contradice la
  decisión de que la identidad la gobierna el portal.
- *Confiar en la lista de permisos del navegador.* Más barato de implementar, pero reabre la falsificación de roles, que es
  el riesgo grande y el que sí se podía cerrar sin costo.
- *Esperar el token antes de seguir.* Bloquea toda la integración contra un tercero sin fecha comprometida.
