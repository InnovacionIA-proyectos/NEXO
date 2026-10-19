# Milestones e issues para NEXO — Sprint 5 (borrador para GitHub, version ampliada)

_Última actualización: 2026-10-14_

30 issues en 6 milestones. Cada issue se puede ejecutar sin leer nada mas: trae **Contexto**, **Hoy -> Debe quedar**, **Checklist** y **Criterios de aceptacion**. El detalle largo y el codigo de referencia estan en `docs/2026-10-09_HU-BACKEND.md` y `docs/2026-10-09_HU-FRONTEND.md` (cada issue dice su "Origen"). El flujo completo esta en `docs/documentacion/FLUJO-DECISION-TARIFA.md` (adjuntarlo a las issues del milestone S5-HU-19).

**Reglas aplicadas**
- Titulo: `[S5-HU-NN.M] Titulo corto`, sin tildes. Milestone: `S5-HU-NN — Descripcion larga`.
- Cuerpo: `Parte de S5-HU-NN (milestone).` (+ ` Depende de S5-HU-NN.M.`), luego el contenido. Casillas `- [ ]`; `[x]` si ya esta hecho.
- Labels: `nexo`, `sprint-5`, `tarea-tecnica` + rol (`backend`, `frontend`, `qa`); bugs llevan `bug`. Tipo: `Task` o `Bug`.
- **Una issue = un repo:** backend en `NEXO`, frontend en `administracion_embarqa_nexo`.

**Confirmar antes de crear**
1. **Numeracion y choque:** el borrador del cubicador (de Cotifly) usa S5-HU-17, 18 y 19, y este documento usa S5-HU-19 a S5-HU-24. Hay que decidir quien renumera antes de crear nada en GitHub. Tampoco pude consultar GitHub para confirmar cual es el ultimo numero usado.
2. El label `frontend` no estaba en la lista de labels del equipo.
3. Los milestones del frontend se crean en el repo `administracion_embarqa_nexo` con el mismo nombre.
4. Usuarios de GitHub: `@ivasco-dev` y `@jpvelez-bts` confirmados (asignados en las issues ya creadas, NEXO#297 y administracion#310); `@kcano` por confirmar.
5. Las issues 20.5 y 20.6 (contrato con Cotifly) YA ESTAN CREADAS: NEXO#297 [S5-HU-20.5] (milestone S5-HU-20) y administracion#310 titulada [S5-HU-26.6], porque el repo del frontend (ahora llamado `administracion`) lleva su propia numeracion: su S5-HU-26 equivale a S5-HU-20 de NEXO. En NEXO ya existe la issue #286 [S5-HU-20.1], cerrada: la numeracion de este documento (S5-HU-19 a 24) choca con milestones reales ya existentes y hay que reconciliarla antes de crear el resto.

| Issue | Repo |
|---|---|
| 19.1 a 19.5 | NEXO |
| 20.1, 20.5 | NEXO |
| 20.2, 20.3, 20.4, 20.6, 20.7 | administracion_embarqa_nexo |
| 21.1 | NEXO |
| 21.2, 21.3 | administracion_embarqa_nexo |
| 22.1 | NEXO |
| 22.2, 22.3 | administracion_embarqa_nexo |
| 23.1, 23.2 | NEXO |
| 23.3 | administracion_embarqa_nexo |
| 24.1 a 24.9 | administracion_embarqa_nexo |

**Asignacion (fija para todas las issues)**
- Backend: `@ivasco-dev` (19.1 a 19.4, 20.1, 20.5, 21.1, 22.1, 23.1) — 9 issues
- Frontend: `@jpvelez-bts` (20.2, 20.3, 20.6, 20.7, 21.2, 22.2, 23.3, 24.1 a 24.8) — 15 issues — **confirmar el usuario exacto de GitHub** (me dijeron "jpvelez o algo asi")
- QA: `@kcano` (19.5, 20.4, 21.3, 22.3, 23.2, 24.9) — 6 issues
- Confirmar tambien `@ivasco-dev` y `@kcano` antes de crear. Cada issue lleva la linea "Asignado a:" despues de los labels.

**Vocabulario**
- **Piso SICE-TAC:** valor minimo legal de la ruta (`tblRutasSicetac`, con tiempos logisticos). Cotizar por debajo es sancion (RN-02).
- **Plaza:** tarifa comercial por ruta. Hay 2 tablas: la externa de SAP (`tblSAPTarifas`) y la propia de NEXO (`tbl_tarifas_plaza`, la que se gestiona en Biblioteca -> Tarifas de plaza).
- **Historico:** valores anteriores. Tambien 2 tablas: log de SAP (`tblSAPTarifasLog`) y propia de NEXO (`tbl_tarifas_plaza_historico`, llenada por trigger).
- **Consulta completa:** se muestran los 3 valores (SICE-TAC, plaza, historico) y decide una persona.
- **Auto-costeo:** el sistema calcula el precio solo, al capturar una solicitud o al cargar un pliego.

---

## Milestone S5-HU-19 — Motor de decision de tarifa: Solo-SICETAC, plaza vigente mayor y consulta completa

El costeo decide hoy distinto al flujo que definio negocio (`NEXO FLUJO.pdf`). Esta historia lo alinea: busca plaza e historico en SAP y luego en las tablas propias de NEXO, decide en 3 modos, el auto-costeo no guarda precio en consulta completa y nunca se guarda un flete por debajo del piso SICE-TAC.

**Flujo objetivo (confirmado por negocio, 2026-10-12)**
1. Regla Solo-SICETAC (clave ruta + tipo de vehiculo): con regla y con valor SICE-TAC -> se usa solo el piso (modo `SoloSicetac`). Con regla pero sin valor -> consulta completa. Sin regla -> paso 2.
2. Plaza: se busca en SAP; si ahi no hay una vigente, en `tbl_tarifas_plaza`. Si no aparece ninguna vigente -> consulta completa.
3. Plaza vigente **estrictamente mayor** que el piso -> se usa sola (modo `PlazaVigenteMayor`), sin consultar el historico. Menor o igual, o sin piso -> consulta completa.
4. Consulta completa (modo `ConsultaCompleta`): SICE-TAC + plaza (vigente, o la vencida solo como referencia) + historico (log de SAP y, si no hay, historico de NEXO). Recomendado = el mas alto, nunca por debajo del piso, la plaza vencida no se recomienda, y sin piso el historico es solo referencia.

**Tabla de 10 casos (piso SICE-TAC = 2,5 M cuando existe)**

| # | Regla Solo-SICETAC | Piso | Plaza | Historico | Modo | Auto-costeo | Recomendado |
|---|---|---|---|---|---|---|---|
| 1 | Si | 2,5 M | (no se mira) | (no se mira) | SoloSicetac | Guarda 2,5 M | — |
| 2 | Si | — | 3,0 M vigente | 2,8 M | ConsultaCompleta | Pendiente | 3,0 M plaza |
| 3 | No | 2,5 M | 3,0 M vigente | 3,5 M | PlazaVigenteMayor | Guarda 3,0 M | — (el historico no se mira) |
| 4 | No | 2,5 M | 2,0 M vigente | 2,2 M | ConsultaCompleta | Pendiente | 2,5 M SICE-TAC (plaza e historico estan bajo el piso) |
| 5 | No | 2,5 M | 3,0 M vencida | 2,8 M | ConsultaCompleta | Pendiente | 2,8 M historico |
| 6 | No | 2,5 M | — | 2,8 M | ConsultaCompleta | Pendiente | 2,8 M historico |
| 7 | No | 2,5 M | — | — | ConsultaCompleta | Pendiente | 2,5 M SICE-TAC |
| 8 | No | — | 3,0 M vigente | — | ConsultaCompleta | Pendiente | 3,0 M plaza |
| 9 | No | — | — | 2,8 M | ConsultaCompleta | Pendiente | Sin recomendado |
| 10 | No | — | — | — | ConsultaCompleta | Pendiente | Sin recomendado |

### [S5-HU-19.1] Plaza e historico en dos fuentes (SAP y tablas propias de NEXO) + traductor de zonas
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend` · Tipo: Task
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-19 (milestone).

CONTEXTO
- Hoy el motor (clsPropuestaTarifaAdapter.ProponerLineaAsync, lineas 209-331) busca la plaza y el historico SOLO en SAP (dbSolucionesTI). La tabla propia tbl_tarifas_plaza (issue #210, S4-HU-10.1; la que se gestiona en Biblioteca -> Tarifas de plaza) nunca se consulta: lo que se carga alli no tiene efecto al costear. Verificado con git: clsTarifaPlaza, ITarifasPlazaRepository y clsEfTarifasPlazaRepository tienen un solo commit (su creacion) y ninguno de los 10 commits posteriores del motor las conecta.
- tbl_tarifas_plaza_historico (la llena el trigger TRG_tarifas_plaza_historico al editar Valor, Vigencia o DetallesTarifa; ESQUEMA_NEXO.sql lineas 441-458 y 782-802) no tiene mapeo de EF (clsNexoDbContext.cs:403-405): hoy no se puede ni consultar. Nace vacia: solo guarda una fila cuando alguien edita una tarifa interna (tambien al reimportar por Excel una fila existente).
- SAP guarda las ciudades por codigo de zona (ZC_MEDELLIN, ZTC_VABURRA). Algunos codigos no traen el nombre de la ciudad (VABURRA = Valle de Aburra = Medellin) o traen abreviaturas (PTO = Puerto). La busqueda actual compara "el codigo contiene el nombre" y falla en esos casos -> responde "Sin coincidencias" aunque la tarifa exista. Ya existe clsResolucionZonaSap (src/Nexo.Infrastructure/Solicitudes/Adaptadores/clsResolucionZonaSap.cs), probado contra 577 zonas reales (resuelve 564; 13 en AliasZonasAgregadas), pero solo se usa en el desplegable de ciudades de Captura (clsCiudadesDwAdapter.cs:42,49,65-66).

HOY -> DEBE QUEDAR
- Plaza: solo SAP -> SAP oficial, luego SAP tolerante, luego tbl_tarifas_plaza. Gana la primera VIGENTE (externa primero). Si SAP solo trae una vencida, se sigue buscando una vigente (hoy no se hace: clsPropuestaTarifaAdapter.cs:261-264 no intenta el selector tolerante si "oficial" ya trajo una tarifa vencida). La vencida queda como referencia solo si no aparece ninguna vigente (SAP primero, luego interna).
- Historico: solo log de SAP -> log de SAP y, si es null, tbl_tarifas_plaza_historico.
- Ciudades: Contains directo -> traductor de zonas.

CHECKLIST
Historico propio
- [ ] Entidad clsTarifaPlazaHistorico en src/Nexo.Domain/Costeo/Entidades/ con las columnas: Id, TarifaPlazaId, LineaNegocio, ClaseViaje, OrigenMunicipioId, DestinoMunicipioId, ClaseContenedor, PesoId, TipoVehiculo, Valor, Vigencia, DetallesTarifa, CreadoPor, CreatedAt, ReemplazadoAt
- [ ] DbSet<clsTarifaPlazaHistorico> y ToTable("tbl_tarifas_plaza_historico") en clsNexoDbContext. SIN migracion de EF (la tabla ya existe; mismo criterio que tbl_tarifas_plaza). Navegacion opcional a clsMunicipio y clsRangoPeso
- [ ] La aplicacion NUNCA escribe en esta tabla (solo el trigger): no agregar alta ni edicion
Plaza
- [ ] clsConsultaTarifaPlazaInterna (Origen, Destino, LineaNegocio?, ClaseViaje?, ClaseContenedor?, TipoVehiculo?, Peso?) y clsResultadoPlazaInterna (Vigente, Vencida); puerto ITarifaPlazaInternaPort.BuscarAsync
- [ ] Adaptador sobre clsNexoDbContext.TarifasPlaza con Include(Origen/Destino/Peso). Origen y destino obligatorios: municipios de tbl_municipios cuyo nombre normalizado este contenido en el texto de la ruta o al reves (misma regla de CodigosDaneAsync, clsPropuestaTarifaAdapter.cs:588-604; Normalizar, :621-631). La ruta guarda texto libre tipo "Medellin (ANT)"
- [ ] Nivel exacto: ademas coinciden linea de negocio (enum -> codigo SAP CodigoLinea, clsTarifarioSapAdapter.cs:45-52 -> Descripcion del catalogo ILineasNegocioPort, comparada normalizada con tbl_tarifas_plaza.LineaNegocio, que guarda el NOMBRE no el Id), clase de viaje (CodigoClaseViaje :56-60 + IClasesViajePort), clase de contenedor, tipo de vehiculo y banda de peso (PesoMinimo <= peso <= PesoMaximo; banda abierta si PesoMaximo es null)
- [ ] Nivel tolerante solo si el exacto deja 0 filas: origen y destino siguen obligatorios; los demas filtros se aplican solo si dejan al menos una fila (mismo patron que CandidatoSapAsync :521-533 y :566-576)
- [ ] Separar vigentes (Vigencia >= hoy) y vencidas; de cada grupo, la de mayor Valor
- [ ] Candidato: Fuente "plaza" (NO inventar otra: el mapeo a FuenteFlete en clsCapturarSolicitudCommand.cs:206-212 y clsAplicarPropuestasCommand convertiria cualquier otro texto en Manual sin avisar), Detalle "{Origen} -> {Destino} · vigencia {fecha} · {DetallesTarifa}", Criterio con los filtros aplicados, Coincidencias, y campo nuevo TablaOrigen ("SAP" | "NEXO") en clsCandidatoTarifaDto; los candidatos de SAP llevan TablaOrigen "SAP"
- [ ] Integracion en ProponerLineaAsync (:243-264): oficial vigente -> selector tolerante de SAP -> interna; plazaVencida = la de SAP si hay, si no la interna; si hay vigente, plazaVencida = null. La rama _sapEnVivo (:291-304) no cambia
- [ ] Los ajustes de vigencia (IAjustesVigenciaPlazaRepository) aplican solo a filas de SAP (su llave es el Id entero de SAP); a la interna no: ahi la vigencia se corrige editando la tarifa
Historico
- [ ] Puerto IHistoricoPlazaInternoPort con la misma consulta (exacto -> tolerante). Ventana: ReemplazadoAt >= hoy - 3 meses. Ultimo registro por TarifaPlazaId. No se separa vigente/vencida (el de SAP tampoco). Descartar Valor <= 0; tomar el mayor. Candidato Fuente "historico_sap" (nombre conservado por el mapeo a FuenteFlete.Historica), TablaOrigen "NEXO"
- [ ] Integracion (:281): historico = log de SAP; historico ??= historico interno
Traductor de zonas
- [ ] clsTarifarioSapAdapter.BuscarConZonasAsync (lineas 114-164): en el paso por zonas (:129-131, hoy z.Transporte.Contains(origen)) comparar cada codigo de zona pasado por SinPrefijoZona + ExpandirAbreviaturas, en las 2 direcciones de "contiene" (igual que Resuelve)
- [ ] Antes de rendirse, buscar la ciudad en AliasZonasAgregadas (ej. Medellin -> VABURRA) y agregar esos codigos a las zonas de origen o destino
- [ ] Aplicar lo mismo en CandidatoSapAsync (:499-501) para la plaza tolerante y el historico de SAP
- [ ] Mover clsResolucionZonaSap a una carpeta comun (hoy vive en Solicitudes/Adaptadores)
Pruebas
- [ ] SAP con vigente: gana la de SAP y la interna ni se consulta
- [ ] SAP solo vencida + interna vigente: gana la interna vigente
- [ ] Nada vigente en ninguna: la referencia vencida es la de SAP si existe, si no la interna
- [ ] Plaza que solo existe en tbl_tarifas_plaza: aparece con TablaOrigen "NEXO"; Fuente sigue siendo "plaza"
- [ ] Historico de SAP presente: no se consulta el interno. Solo interno: aparece. Valor reemplazado hace mas de 3 meses: no aparece. Tarifa editada varias veces: cuenta solo el valor anterior mas reciente
- [ ] Ruta con zona agregada (Medellin como ZTC_VABURRA) y ruta con abreviatura (PTO) encuentran tarifa; las rutas que ya encontraban siguen encontrando la misma (el cambio solo agrega coincidencias)

CRITERIOS DE ACEPTACION
- Una ruta/vehiculo cargado solo en Biblioteca -> Tarifas de plaza se propone en Mesa de Costeo como "Tarifario (NEXO)"
- Rutas que antes daban "Sin coincidencias" por zona agregada o abreviatura ahora encuentran tarifa en SAP
- Ningun codigo de aplicacion escribe en tbl_tarifas_plaza_historico
```
Origen: HU-B14, HU-B15, HU-B22. Hallazgo de partida: issue #210 (S4-HU-10.1).

### [S5-HU-19.2] Nuevo orden de decision (ModoDecision) y auto-costeo sin precio en consulta completa
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend` · Tipo: Task
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-19 (milestone). Depende de S5-HU-19.1.

CONTEXTO
- Hoy el motor tiene 2 caminos, no 3 pasos: (a) Solo-SICETAC, que sin valor SICE-TAC corta con "requiere atencion manual" y ni consulta plaza/historico (clsPropuestaTarifaAdapter.cs:232-241, deja Plaza e Historico en null a proposito); (b) una rama unica que SIEMPRE trae los 3 candidatos y decide por "regla del mayor" (clsReglaDeCosteo.Decidir, :53-99): plaza vs historico y el ganador contra el piso. No existe "plaza vigente mayor gana sola": si el historico es mayor que la plaza, gana el historico.
- El auto-costeo aplica el mayor y deja la linea AutoResuelta (clsCapturarSolicitudCommand.cs:184-246; en pliegos clsCostearLineaCommand -> MarcarAutoResueltaAsync). Decision de negocio: en consulta completa NO debe guardar precio; decide el Administrador de Tarifas.
- Confusion aclarada: "Iniciar costeo" no aplica nada (iniciarCosteo(), mesa-costeo-page.component.ts:209-232; clsCrearCotizacionCommand solo crea o recupera el borrador). Lo que parece "aplicar todo" es que el auto-costeo ya guardo los fletes al capturar y la pantalla no los mostraba hasta el clic (se corrige en S5-HU-20.1 / 20.3).

HOY -> DEBE QUEDAR
- Solo-SICETAC sin piso: "atencion manual" -> consulta completa.
- Plaza vigente mayor que el piso: compite con el historico -> gobierna sola, sin consultar el historico.
- Plaza vigente menor o igual al piso, vencida, o sin piso: regla del mayor -> consulta completa (mismo valor recomendado que hoy; cambia el modo).
- Auto-costeo en consulta completa: guarda el mayor -> queda PorCostear sin flete. Solo se auto-costean SoloSicetac y PlazaVigenteMayor.
- Comercial al abrir Mesa de Costeo: aplica TODAS en silencio (intentarAutoAplicar) -> solo las automaticas.

CHECKLIST
Dominio (src/Nexo.Domain/Costeo/Reglas/clsReglaDeCosteo.cs). Decidir y DecidirConTarifario NO se tocan: siguen siendo la regla del Paso 4
- [ ] enum ModoDecision { SoloSicetac, PlazaVigenteMayor, ConsultaCompleta } y record clsDecisionDeFlujo(Resultado, Modo)
- [ ] DecidirFlujo(hayReglaSoloSicetac, sicetac, plazaVigente, plazaVencida, historico): (1) regla + sicetac -> SoloSicetac con justificacion "Ruta marcada como SOLO SICE-TAC...". (2) sin regla + plazaVigente + sicetac + plazaVigente.Valor > sicetac.Valor (estricto) -> PlazaVigenteMayor. (3) resto -> ConsultaCompleta: con plazaVigente, Decidir(sicetac, plazaVigente, historico); sin ella, DecidirConTarifario(sicetac, plazaVencida, historico, tarifarioVencido: plazaVencida != null). Caso regla SIN piso: va directo al Paso 4, no evalua el 3
Adaptador (clsPropuestaTarifaAdapter.ProponerLineaAsync)
- [ ] El bloque Solo-SICETAC (:232-241) solo hace return cuando HAY piso; sin piso sigue a plaza -> historico -> consulta completa
- [ ] En modo PlazaVigenteMayor se puede omitir la consulta del historico (no se muestra)
- [ ] Reemplazar DecidirConTarifario (:306-307) por DecidirFlujo
- [ ] Vencida (:330) = hay plaza de referencia vencida (de SAP o NEXO); PublicarVencidaSiAplicaAsync (:118-128) sigue igual
DTOs (src/Nexo.Application/Common/Dtos/PropuestaTarifaDtos.cs)
- [ ] clsCandidatoTarifaDto: campo TablaOrigen (si no lo agrego 19.1)
- [ ] clsPropuestaLineaDto: ModoDecision como TEXTO ("SoloSicetac"|"PlazaVigenteMayor"|"ConsultaCompleta", default "ConsultaCompleta"), no numero de enum
- [ ] ProponerParaLineaAsync (:79-107) devuelve tambien el modo (p. ej. clsDecisionDeFlujo en vez de clsResultadoDeCosteo, ajustando IPropuestaDeLineaPort)
Auto-costeo
- [ ] Captura (AutoCostearAsync, clsCapturarSolicitudCommand.cs:194-222): guardar el flete SOLO si el modo es SoloSicetac o PlazaVigenteMayor. En ConsultaCompleta: EstadoLinea = PorCostear y MotivoPendiente = "Consulta completa: el Administrador de Tarifas debe elegir la tarifa (recomendado: {valor} - {fuente})." (sin la parte del recomendado si no lo hay)
- [ ] Pliego (clsCostearLineaCommand.cs:39-62): en ConsultaCompleta usar la misma rama que hoy usa RequiereTarifaManual (pendientes.RegistrarAsync Manual + lineas.MarcarPendienteAsync + publicar clsLineaRequiereTarifaManual) en vez de MarcarAutoResueltaAsync. La saga lo cuenta como "requiere persona" y cierra en Parcial (correcto). Ese evento solo lo consume la saga, no manda correos
- [ ] Recomendado: en modos automaticos el pliego tambien debe guardar el flete en la cotizacion, igual que la captura (hoy clsEfLineasCosteadasRepository.cs:10-24 solo marca AutoResuelta y el precio llega despues). Si no se hace, esas lineas quedan sin precio hasta "Aplicar todas las propuestas"
- [ ] POST /api/cotizaciones/{id}/aplicar-propuestas con parametro opcional [FromQuery] bool soloAutomaticas = false: con true, clsAplicarPropuestasCommand salta las lineas cuyo ModoDecision sea ConsultaCompleta. Sin el parametro aplica todas (es el boton de Fredy)
- [ ] El mapeo a FuenteFlete no cambia (los valores de Fuente no cambian)
Pruebas
- [ ] Una prueba de DecidirFlujo por cada una de las 10 filas de la tabla del milestone
- [ ] Las pruebas actuales de Decidir/DecidirConTarifario siguen valiendo
- [ ] Actualizar lo que asumia el resultado anterior del caso 3: tests/Nexo.Domain.Tests/clsPliegoDatasetValidacionTests.cs y docs/costeo/escenarios-reglas-negocio.feature (@regla-del-mayor)

EFECTOS A AVISAR
- El % de lineas auto-costeadas del Tablero baja (ya no cuenta consulta completa)
- Llegan mas lineas pendientes a Mesa de Costeo (caso 7: solo piso, sin plaza, tambien queda pendiente aunque haya un solo valor)
- Una solicitud con lineas de consulta completa ya no queda lista sola: alguien decide antes de poder entregarla

CRITERIOS DE ACEPTACION
- Cada fila de la tabla de 10 casos produce el modo y el valor indicados
- Caso 3: la plaza vigente mayor que el piso gana aunque el historico sea mas alto
- Regla Solo-SICETAC sin piso ya no queda en "atencion manual": muestra plaza/historico y decide por consulta completa
- Al capturar o cargar un pliego, las lineas de consulta completa quedan PorCostear sin flete y con el motivo "Consulta completa..."; las de SoloSicetac y PlazaVigenteMayor quedan con su flete
- aplicar-propuestas?soloAutomaticas=true no toca las de consulta completa; sin el parametro las aplica todas
- El JSON de la propuesta trae modoDecision y, por candidato, tablaOrigen
```
Origen: HU-B16.

### [S5-HU-19.3] Validaciones de flete en el servidor: bajo el piso, historico de la propuesta y entrega con lineas pendientes
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend`, `bug` · Tipo: Bug
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-19 (milestone). Depende de S5-HU-19.2.

CONTEXTO
- Bajo el piso: la regla MAYOR(plaza, piso) solo protege el camino automatico. Al agregar o editar un flete a mano (clsAgregarLineaCotizacionCommand.cs:32-76, clsEditarLineaCotizacionCommand) nada compara el valor contra el piso: una persona puede guardar cualquier numero. Decision de negocio (2026-10-12): NUNCA se guarda un flete por debajo del piso, sea tarifario, historico o manual — se bloquea con aviso, no se pide confirmacion. Reemplaza el "confirmar de todas formas" que se habia prototipado (HU-12 de frontend). Debe estar tambien en el servidor: si solo se bloquea en pantalla, se salta llamando la API.
- Historico: al guardar con fuente Historica, el servidor exige una "historica sugerida" vigente de la ultima semana (ITarifasHistoricasPort.HayVigenteAsync sobre tbl_tarifas_historicas = fletes que NEXO gano, RN-04) y si no, rechaza con HISTORICA_VENCIDA (clsAgregarLineaCotizacionCommand.cs:50-60). Esa regla se penso para el boton "Usar" de "Historicas sugeridas". Pero la fuente Historica tambien se usa cuando el valor viene del candidato Historico de la PROPUESTA (otra tabla: log de SAP o, con 19.1, tbl_tarifas_plaza_historico), que ya trae su ventana de 3 meses. Resultado: guardar ese valor por el formulario puede fallar aunque sea valido. Ademas "Aplicar propuestas" (clsAplicarPropuestasCommand) agrega Historica sin esa validacion: el mismo valor se guarda en lote pero no linea por linea.
- Entrega: clsEntregarCotizacionCommandHandler solo exige al menos 1 clsCotizacionLinea (lineas 47-49, clsCotizacionSinFleteException); no compara contra las lineas de la solicitud. La cotizacion se arma linea por linea (clsAgregarLineaCotizacionCommand.cs:45 marca la linea Revisada), asi que se puede entregar con lineas todavia PorCostear. Ni validadores, ni clsMaquinaEstados, ni pruebas lo cubren.

HOY -> DEBE QUEDAR
- Flete bajo el piso: se guarda -> falla con FLETE_BAJO_PISO_SICETAC (cualquier fuente).
- Historico de la propuesta: puede fallar con HISTORICA_VENCIDA -> se acepta si coincide con el historico de la propuesta.
- Entrega con lineas pendientes: se permite -> falla con COTIZACION_CON_LINEAS_PENDIENTES.

CHECKLIST
Bajo el piso
- [ ] En clsAgregarLineaCotizacionCommand y clsEditarLineaCotizacionCommand, antes de guardar: obtener el piso de la linea con el MISMO calculo de la propuesta (IPropuestaTarifaPort.ProponerAsync(c.SolicitudId), ya tiene cache) -> la linea -> Sicetac?.Valor (incluye tiempos logisticos con las horas de la linea)
- [ ] Si hay piso y ValorFlete < piso: throw new clsValidationFailedException("FLETE_BAJO_PISO_SICETAC", $"El flete ({valor:C0}) esta por debajo del piso SICE-TAC ({piso:C0}). No se puede guardar un valor por debajo del piso.")
- [ ] Sin piso para la linea: se guarda normal (no hay contra que comparar)
- [ ] El auto-costeo y "Aplicar propuestas" no necesitan esta validacion (clsReglaDeCosteo regla 1 ya nunca propone bajo el piso)
Historico de la propuesta (opcion A, decidida)
- [ ] Para fuente Historica aceptar si se cumple CUALQUIERA: (1) hay historica sugerida vigente (regla de hoy), o (2) el valor coincide con el candidato Historico de la propuesta de esa linea, recalculada en el servidor (IPropuestaTarifaPort.ProponerAsync). El servidor no depende de lo que diga el navegador
- [ ] Una historica que no viene de ninguna de las dos sigue fallando con HISTORICA_VENCIDA
Entrega
- [ ] En clsEntregarCotizacionCommandHandler validar que TODAS las lineas de clsSolicitud.Lineas esten AutoResuelta o Revisada; si alguna sigue PorCostear, rechazar con COTIZACION_CON_LINEAS_PENDIENTES. Mantener COTIZACION_SIN_FLETE para el caso de cero lineas
Pruebas
- [ ] Bajo el piso falla con tarifario, historica y manual; igual o mayor se guarda; linea sin piso se guarda
- [ ] Historico de la propuesta se guarda aunque no haya "Historicas sugeridas" vigentes; una historica ajena a ambas falla
- [ ] Entrega con una linea PorCostear falla; con todas costeadas funciona

CRITERIOS DE ACEPTACION
- La API rechaza agregar o editar un flete por debajo del piso aunque se llame directo
- Guardar el historico que muestra la propuesta funciona
- No se puede entregar una cotizacion con lineas del pliego sin costear
```
Origen: HU-B18, HU-B17, HU-B8.

### [S5-HU-19.4] Investigar por que no se propone una plaza vigente y mayor (Desviacion 1 de QA)
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend`, `bug` · Tipo: Bug
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-19 (milestone).

CONTEXTO
- QA (tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/esperado-final-pliego.md:134-159) verifico un pliego real de 12 lineas: en 4 (lineas 1, 2, 3 y 11) existia una tarifa de plaza SAP vigente hasta 3,9 veces mayor que el piso SICE-TAC y el sistema propuso el piso (el valor menor). Cobra de menos. Las consultas SQL que uso QA estan en .../consultas/01-pliego-licitacion-esperado.sql:52-154 (ej. ORIGEN='ZC_BUENAVENTURA', DESTINO IN ('ZTC_BOGOTA','ZTC_NORTE_BOGOTA')).
- Causa raiz sin confirmar. Leyendo codigo, el match directo por LIKE deberia funcionar para esas rutas ('ZTC_BOGOTA' contiene 'BOGOTA'), asi que no se aisla una causa determinista sin datos en vivo.
- Relacionado: en el ambiente de pruebas la plaza de SAP tiene 107 filas, todas ClaseViaje = Contenedor (tests/QA/pr-253-fix-seeders-reglas-sicetac-tarifas-plaza/README.md:23): para Cupo, Carga Suelta y Extradimensionado nunca habra plaza en SAP (eso no es bug, es dato). Con 19.1 la tabla propia si podria cubrirlos.

CHECKLIST (esta issue solo investiga; no corrige nada)
- [ ] Agregar logs temporales en clsTarifarioSapAdapter.ConsultarAsync y BuscarConZonasAsync con: ciudad de origen/destino que entra, codigos DANE resueltos, zonasOrigen/zonasDestino, claseContId y vehiculoId resueltos, y cuantas filas quedan despues de cada filtro
- [ ] Correr la propuesta para las 4 rutas (lineas 1, 2, 3 y 11 del pliego de prueba) contra la base real y ver en que filtro se pierde la fila que SQL si encuentra
- [ ] Confirmar o descartar hipotesis (ninguna confirmada): (a) el filtro exacto de clase de contenedor (t.ClaseContenedor == cc, clsTarifarioSapAdapter.cs:332) descarta la fila porque la clase capturada resuelve a otro Id; (b) falta la relacion zona granular -> zona agregada que necesita el segundo nivel de BuscarConZonasAsync (lineas 129-164)
- [ ] Documentar la causa raiz de las 4 lineas y crear la issue de correccion
- [ ] Quitar los logs temporales

CRITERIOS DE ACEPTACION
- Causa raiz identificada y documentada para las 4 lineas, con la issue de correccion creada
- Conviene correrla despues de 19.1 para medir cuanto mejora el traductor de zonas
```
Origen: HU-B19.

### [S5-HU-19.5] QA: verificacion end-to-end del flujo de decision de tarifa
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `qa` · Tipo: Task
Asignado a: @kcano (QA)
```
Parte de S5-HU-19 (milestone). Depende de S5-HU-19.1 a S5-HU-19.4.

CONTEXTO
- Valida que el motor cumple el flujo de FLUJO-DECISION-TARIFA.md y que las reglas nuevas no se pueden saltar por la API.

CHECKLIST
- [ ] Probar con datos reales (o sembrados) cada una de las 10 filas de la tabla del milestone y confirmar modo y valor
- [ ] Correr la prueba de 20 lineas: cargar primero PRUEBA-SUPREMA-REGLAS-SOLO-SICETAC.xlsx en Reglas Solo-SICETAC y luego subir la hoja "Licitacion" de PRUEBA-SUPREMA-LICITACION-20-LINEAS.xlsx (docs/documentacion/); comparar cada linea con la hoja "Resultados esperados". Ojo: los pisos son aproximados y las plazas vigentes de esa copia vencen el 12-13 de octubre de 2026; si se prueba otro dia, los casos de plaza vigente pasan a plaza vencida
- [ ] Una plaza que solo existe en tbl_tarifas_plaza aparece como "Tarifario (NEXO)" y se guarda con fuente tarifario
- [ ] Un flete bajo el piso falla en la API (FLETE_BAJO_PISO_SICETAC) llamando directo, sin pasar por la pantalla
- [ ] Una linea de consulta completa llega a Mesa de Costeo sin precio, con el motivo "Consulta completa..." y el Comercial no la llena en silencio
- [ ] Guardar el historico de la propuesta funciona sin "Historicas sugeridas" vigentes
- [ ] Entregar con lineas PorCostear falla
- [ ] Comparar el % de lineas auto-costeadas del Tablero antes y despues (debe bajar)
- [ ] Verificar el caso 5 (plaza vencida) y la alerta de tarifa vencida (PublicarVencidaSiAplicaAsync) siguen funcionando

CRITERIOS DE ACEPTACION
- Aprobacion explicita de QA registrada en la issue
```

---

## Milestone S5-HU-20 — Mesa de Costeo: propuesta por modo, edicion de flete y aplicar todas las propuestas

Ajustes de API y de pantalla de Mesa de Costeo para el nuevo flujo: la propuesta muestra el modo y la tabla de origen, el Administrador de Tarifas elige un candidato con un clic en consulta completa, edita un flete manual, aplica todas las propuestas con confirmacion y no puede guardar valores bajo el piso. Roles que ven la propuesta (`verPropuestaCosteo`): AdminTarifas, CoordinadorFletes y Admin (`nexo-access.models.ts:43-88`). El Comercial no la ve.

### [S5-HU-20.1] API de Mesa de Costeo: valor de mercancia, desglose piso + tiempos, cotizacion existente y servicios conexos
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend` · Tipo: Task
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-20 (milestone).

CONTEXTO
- Valor de mercancia: el dato YA se captura (CapturaRequests.cs:102) y se guarda (clsLineaSolicitud.ValorMercancia, clsLineaSolicitud.cs:93), pero clsLineaCosteoDto (SolicitudDtos.cs:69-79) no lo envia a Mesa de Costeo. Mismo patron que ya se hizo con LineaNegocio/ClaseViaje/ClaseContenedor.
- Desglose piso + tiempos: CandidatoSicetacAsync (clsPropuestaTarifaAdapter.cs:362-376) calcula el valor con clsPisoConHoras.Calcular(fila.Valor, fila.ValorHora, fila.HorasPactadas, fila.ValorSiceConHora, horasSolicitud) (:371), pero solo devuelve el total combinado en clsCandidatoTarifaDto (PropuestaTarifaDtos.cs:21-22). El piso base (fila.Valor) existe ahi mismo y se descarta. El frontend no puede reconstruirlo: la tabla "Tarifas SICE-TAC" de Biblioteca es catalogo crudo (valorHora, horasPactadas por defecto) y no usa las horas reales de la linea; reconstruirlo en pantalla revive el bug de horas que HU-1 de Mesa de Costeo ya corrigio (floorTotal() usaba las horas de la solicitud, no las de la linea).
- Cotizacion existente: hoy el unico camino para traer la cotizacion es POST /api/cotizaciones (crear o recuperar, con efecto secundario). Mesa de Costeo solo la llama si el usuario hace clic o tiene el permiso autoAplicarPropuestas; sin eso la pantalla no sabe que ya existe una cotizacion (ej. auto-costeada al capturar). Es la causa de que "Iniciar costeo" parezca aplicar todo.
- Editar flete manual: clsAgregarLineaCotizacionCommand SIEMPRE agrega una fila nueva (c.Lineas.Add); editar desde el front sin comando nuevo duplicaria la linea e inflaria el total (clsCotizacionCalculos.Recalcular suma ambas). Ya implementado: clsEditarLineaCotizacionCommand.cs.

CHECKLIST
- [ ] clsLineaCosteoDto: agregar al final "decimal? ValorMercancia = null" (opcional, no rompe nada); en clsObtenerCosteoQuery.cs:50-54 pasar l.ValorMercancia
- [ ] clsCandidatoTarifaDto: agregar "decimal? ValorPisoBase = null, decimal? ValorTiemposLogisticos = null" (despues de Advertencia). En CandidatoSicetacAsync poblar ValorPisoBase: fila.Valor y ValorTiemposLogisticos: valor - fila.Valor (diferencia, para que sirva tambien en el fallback valorSiceConHora). Plaza e historico quedan null. Invariante: ValorPisoBase + ValorTiemposLogisticos == Valor
- [ ] Query clsObtenerCotizacionPorSolicitudQuery (IQuery<clsCotizacionDto?>) usando ICotizacionRepository.ObtenerConLineasYConexosAsync(x => x.SolicitudId == id) (overload ya existe: ICotizacionRepository.cs:20, clsEfCotizacionRepository.cs:17); devuelve null en vez de lanzar. Endpoint GET /api/cotizaciones/por-solicitud/{solicitudId:guid}: 200 con la cotizacion o 204 No Content, NUNCA crea nada. Inyectar el handler en el constructor de clsCotizacionesController
- [x] PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId} (clsEditarLineaCotizacionCommand): solo cotizacion Borrador (409 COTIZACION_NO_EDITABLE), linea existente (404 LINEA_NO_ENCONTRADA), solo fuente Manual (409 FLETE_NO_EDITABLE), no recibe Fuente, auditado con valor anterior y nuevo; roles AdminTarifas,CoordinadorFletes,Admin. Compilado, 0 errores
- [ ] Desplegar el backend (el frontend apunta al backend desplegado, no al codigo local; hasta entonces "Editar" falla en pantalla con 404) y probar de extremo a extremo

CRITERIOS DE ACEPTACION
- El GET del costeo trae valorMercancia por linea (o null si no se capturo)
- La propuesta trae, para el candidato SICE-TAC, valorPisoBase y valorTiemposLogisticos que suman el valor; plaza e historico sin esos campos
- GET por-solicitud devuelve 204 si no hay cotizacion y no crea una
- Editar un flete manual actualiza la MISMA linea, no duplica ni infla el total

---- PARTE 2: SERVICIOS CONEXOS (HU-B23 y HU-B24) ----

CONTEXTO (CONEXOS)
- POST /api/cotizaciones/{id}/conexos (clsCotizacionesController.cs:90) autoriza solo Comercial,CoordinadorFletes,Admin: falta AdminTarifas (en el resto del controlador si esta, lineas 39, 60, 68, 78, 104). Recordar que [Authorize(Roles)] de metodo se combina con AND con el de clase.
- clsAgregarConexosCotizacionCommand.cs lanza 409 COTIZACION_NO_EDITABLE si Estado != Borrador, o sea bloquea toda cotizacion Entregada.
- No existe endpoint para quitar un conexo.
- Referencia completa: HU-B23 y HU-B24 en docs/2026-10-09_HU-BACKEND.md (seccion "Servicios conexos de la cotizacion").
- El aviso por correo reutiliza el patron de la entrega (clsNotificarEntregaCommand.cs): destinatario clsSolicitud.Creador (CreadoPor), evento publicado, correo fuera de la peticion en un consumidor.

HOY -> DEBE QUEDAR (CONEXOS)
- Roles de agregar: Comercial,CoordinadorFletes,Admin -> Comercial,AdminTarifas,CoordinadorFletes,Admin
- Regla de estado: solo Borrador -> mientras Resultado == SinDefinir (Borrador o Entregada); Ganada/Perdida -> 409 COTIZACION_CERRADA ("La cotizacion ya esta marcada como Ganada/Perdida: no admite cambios de servicios.")
- Quitar un conexo: no existe -> DELETE /api/cotizaciones/{id}/conexos/{conexoId}, mismos roles y misma regla

CHECKLIST (CONEXOS)
- [ ] Agregar AdminTarifas a los roles del POST .../conexos
- [ ] clsAgregarConexosCotizacionCommand: cambiar la condicion Estado != Borrador por Resultado != SinDefinir y lanzar clsConflictException("COTIZACION_CERRADA", ...)
- [ ] Nuevo clsQuitarConexoCotizacionCommand + handler (patron del de agregar): cargar con ObtenerConLineasYConexosAsync (404 COTIZACION_NO_ENCONTRADA), regla de estado (409 COTIZACION_CERRADA), buscar el conexo en c.Conexos (404 CONEXO_NO_ENCONTRADO), quitarlo, clsCotizacionCalculos.Recalcular(c), guardar, devolver clsCotizacionDto
- [ ] Endpoint DELETE {id}/conexos/{conexoId} en clsCotizacionesController, roles Comercial,AdminTarifas,CoordinadorFletes,Admin; registrar el handler en clsDependencyInjection.cs de Cotizaciones
- [ ] Auditoria: agregar_conexos y quitar_conexo con servicio, cantidad y valor (valor anterior al quitar) y, si aplica, que fue despues de la entrega
- [ ] Columna nueva ModificadaTrasEntregaAt (DateTimeOffset?) en clsCotizacion + ESQUEMA_NEXO.sql + migracion; se llena al agregar/quitar con Estado == Entregada; NO cambia Estado ni EntregadaAt (SLA y metricas siguen midiendo la entrega original); exponerla en clsCotizacionDto
- [ ] Aviso al comercial: si Estado == Entregada y usuarioActual.Id != solicitud.CreadoPor, publicar evento y consumidor que envia "Cotizacion actualizada: se agrego/quito [servicio] x [cantidad]; nuevo total $X" a clsSolicitud.Creador. Si el actor es el mismo creador no se envia nada; sobre una cotizacion Borrador no se envia nada. Un fallo de correo sube al bus (no se traga), igual que en la entrega
- [ ] Pruebas unitarias: roles, Borrador/Entregada permitidos, Ganada/Perdida 409, quitar existente/inexistente/de otra cotizacion, recalculo del Total, marca de modificacion, aviso solo cuando el actor != creador

CRITERIOS DE ACEPTACION (CONEXOS)
- AdminTarifas, Comercial, CoordinadorFletes y Admin agregan y quitan conexos con la cotizacion Borrador y con la Entregada
- Con Ganada o Perdida, agregar y quitar devuelven 409 COTIZACION_CERRADA y no cambian nada
- Quitar devuelve 200 con la cotizacion sin ese conexo y el Total recalculado; un conexoId ajeno o inexistente da 404; sin rol da 403
- Cambiar una cotizacion Entregada llena ModificadaTrasEntregaAt y deja Estado y EntregadaAt iguales
- El creador recibe el correo solo cuando el cambio lo hace otro usuario sobre una cotizacion Entregada
- Por decidir (no bloquea): agrupar varios cambios seguidos en un solo correo (hoy uno por peticion; "agregar a todas las lineas" ya es una sola peticion)
```
Origen: HU-B20, HU-B9, HU-B7, HU-B10. Conexos: HU-B23, HU-B24.

### [S5-HU-20.2] Propuesta por modo, "Usar este", bloqueo bajo el piso y "Aplicar todas las propuestas"
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-20 (milestone). Depende de S5-HU-19.2, S5-HU-19.3 y S5-HU-24.1 (las 3 cajas consolidadas y candidateSourceLabel() salen de ahi).

CONTEXTO
- Hoy la Propuesta de la Biblioteca muestra 3 cajas fijas (SICE-TAC, Tarifario, Historico SAP) y un unico boton "Usar propuesta" que aplica solo el candidato que el motor decidio como ganador. Para elegir otro candidato Fredy tiene que reescribir el valor a mano. Decision de negocio: cuando decide el motor solo (Solo-SICETAC, o auto-costeo) esta bien; cuando un humano revisa en consulta completa el sistema SUGIERE y el Administrador de Tarifas tiene la ultima palabra con un clic. En Solo-SICETAC y PlazaVigenteMayor no hay nada que elegir.
- Piso: nunca se puede guardar un valor por debajo del piso (de ninguna fuente). El prototipo anterior (popup con "Confirmar de todas formas", solo para fuente manual, agregarFlete() ts:264) queda reemplazado por un bloqueo. El backend lo valida en 19.3; aqui va el aviso.
- "Aplicar propuestas": decision final de negocio: el boton NO se quita; se renombra "Aplicar todas las propuestas" y pide confirmacion antes de aplicar. Sirve para llenar de una vez un pliego (las lineas autocosteadas de un pliego pueden quedar sin flete guardado).
- Comercial: hoy intentarAutoAplicar() (ts:183-207) llama aplicarPropuestas y llena TODAS las lineas sin flete en silencio; ahora solo debe llenar las automaticas (si no, decidiria las lineas que son de Fredy).

HOY -> DEBE QUEDAR
- Cajas: sin tabla de origen -> con "Tarifario (NEXO)" / "Historico (NEXO)" cuando aplica, aviso del modo y marca "Recomendado".
- Boton por candidato: no existe -> "Usar este" solo en ConsultaCompleta.
- Bajo el piso: popup que deja confirmar (solo manual) -> aviso que bloquea, para cualquier fuente.
- "Aplicar propuestas (N)" aplica de una -> "Aplicar todas las propuestas (N)" con confirmacion.

CHECKLIST
Modelos (cabina-api.models.ts:203-223)
- [ ] CandidatoTarifaDto: advertencia?: string|null (el backend ya lo manda), tablaOrigen?: 'SAP'|'NEXO'|null
- [ ] type ModoDecision = 'SoloSicetac'|'PlazaVigenteMayor'|'ConsultaCompleta'; PropuestaLineaDto: vencida?: boolean (ya lo manda el backend) y modoDecision?: ModoDecision
Propuesta de la Biblioteca (mesa-costeo-page.component.html:219-236, *ngFor de candidatos en :224; ts: candidateSourceLabel() ~602, candidateText() ~606, usarPropuesta() ~409, fuenteDesdePropuesta() ~1060)
- [ ] candidateSourceLabel(): "Tarifario (NEXO)" / "Historico (NEXO)" cuando tablaOrigen = NEXO; SICE-TAC sin cambio
- [ ] Aviso encima de las 3 cajas segun modo: SoloSicetac "Ruta marcada Solo SICE-TAC: se usa el piso SICE-TAC, sin comparar."; PlazaVigenteMayor "La tarifa de plaza vigente supera el piso SICE-TAC: se usa la plaza."; ConsultaCompleta "Consulta completa: elige el candidato que vas a usar (el recomendado esta marcado)."
- [ ] Marca "Recomendado" en la caja cuyo candidato coincide con fuentePropuesta
- [ ] Plaza con vencida = true: marca "Vencida - solo referencia" y SIN boton
- [ ] Boton "Usar este" en cada .mesa-candidate, visible SOLO si TODAS: modoDecision === 'ConsultaCompleta'; el candidato existe y no es la plaza vencida; no esta por debajo del piso (!(entry.propuesta?.sicetac && candidate.valor < entry.propuesta.sicetac.valor)); la linea no tiene flete (!fleteDeLinea(...)); no se esta editando (!editandoFlete[...]); cotizacion no entregada y access.puede('agregarFlete'). Un candidato bajo el piso muestra "Por debajo del piso SICE-TAC - no se puede usar" en vez del boton
- [ ] usarCandidato(entry, candidate, fuente): patchValue({valorFlete: candidate.valor, fuente}) + markAsDirty en valorFlete y fuente + markForCheck; SOLO rellena el formulario, no guarda. Mapeo de fuente: SICE-TAC 'sicetac', Plaza 'tarifario', Historico 'historica' (los mismos de fuenteDesdePropuesta())
- [ ] <option value="historica"> (html:334): [disabled]="!tieneHistoricaVigente(entry) && !entry.propuesta?.historicoSap". NO cambiar tieneHistoricaVigente() (ts:517-519): tambien alimenta el aviso "Todas las tarifas historicas de esta ruta estan vencidas" (html:283)
- [ ] intentarAutoAplicar (ts:183-207): llamar cotizacionesApi.aplicarPropuestas(cotizacion.id, true); en cotizaciones-api.service.ts:43-48 agregar el parametro soloAutomaticas = false que manda params {soloAutomaticas: 'true'} (HttpParams ya esta importado), conservando el cuerpo {} y .pipe(timeout(300000))
Bloqueo bajo el piso (agregarFlete() ts:264, editarFlete() ts:363, modal despues de html:465)
- [ ] En AMBOS metodos: const piso = entry.propuesta?.sicetac?.valor; if (piso != null && Number(value.valorFlete) < piso) { mensaje "El flete (X) esta por debajo del piso SICE-TAC (Y). No se puede guardar un valor por debajo del piso: corrige el valor."; abrir modal; return } — sin mirar la fuente y SIN guardar
- [ ] Modal: titulo "No se puede guardar: flete por debajo del piso SICE-TAC"; un solo boton "Entendido" (cerrarFleteBajoPiso()); el formulario queda intacto para corregir
- [ ] Borrar lo que queda sin uso: confirmarFleteBajoPiso(), fleteBajoPisoPendiente, fleteBajoPisoEditando y el boton "Confirmar de todas formas"
- [ ] Si el backend rechaza igual con FLETE_BAJO_PISO_SICETAC (el piso cambio), mostrarlo como cualquier error de guardado (mutationError)
Aplicar todas (html:79-87, ts ~652)
- [ ] Boton "Aplicar todas las propuestas (N)" (N = lineasSinFlete) y texto de ayuda: "Aplica de una vez el valor recomendado a cada linea que todavia no tiene flete."
- [ ] El boton abre un modal con el mismo patron que "Cancelar pliego" (.mesa-modal-overlay/.mesa-modal, role="dialog", cdkTrapFocus, html ~448-465): titulo "Aplicar todas las propuestas"; texto "Se aplicara el valor recomendado a {N} linea(s) sin flete. Las lineas que ya tienen flete no cambian. ¿Confirmas?"; botones "Cancelar" y "Confirmar" (llama al aplicarPropuestas() actual, SIN el parametro soloAutomaticas)
- [ ] No se toca: el endpoint, el auto-aplicado del Comercial (salvo el parametro), "Usar propuesta" y las lineas que ya tienen flete (el backend las salta)

CRITERIOS DE ACEPTACION
- Cada caja dice de que tabla sale; se ve el aviso del modo y la marca "Recomendado"
- En SoloSicetac y PlazaVigenteMayor no aparece ningun "Usar este"
- En ConsultaCompleta, en una linea sin flete, aparece "Usar este" en cada candidato con valor, salvo plaza vencida y candidatos bajo el piso
- En una linea que ya tiene flete no aparece ningun "Usar este"; "Usar este" solo rellena el formulario
- Elegir el historico con "Usar este" deja la opcion "Historica" seleccionada y NO en gris
- Un valor bajo el piso (de cualquier fuente, agregando o editando) muestra el aviso y no guarda; no existe forma de confirmar y guardar igual
- "Aplicar todas las propuestas" no aplica nada hasta "Confirmar"; "Cancelar" cierra sin aplicar
- Cuando el Comercial abre Mesa de Costeo solo se llenan solas las lineas SoloSicetac y PlazaVigenteMayor
```
Origen: HU-16, HU-17, HU-19 (frontend). Lineas verificadas contra el codigo el 2026-10-12.

### [S5-HU-20.3] Mesa de Costeo: ajustes menores y correcciones de pantalla
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend`, `bug` · Tipo: Bug
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-20 (milestone). Depende de S5-HU-20.1, S5-HU-19.3 y S5-HU-24.1 (lineasCosteadas() y el icono de aviso se crean ahi).

CONTEXTO
Varias correcciones pequenas de la misma pantalla, agrupadas. Cada una depende de un cambio de backend ya listado en 20.1 o 19.3; sin ese backend desplegado no se pueden probar.

CHECKLIST
1) Chip "Valor mercancia" (depende de 20.1)
- [ ] LineaCosteoDto (cabina-api.models.ts:119): valorMercancia?: number | null
- [ ] Despues del chip "Clase de contenedor" (html:152): <span *ngIf="entry.linea.valorMercancia !== null && entry.linea.valorMercancia !== undefined"><b>Valor mercancia</b>{{ formatMoney(entry.linea.valorMercancia) }}</span>. Si no tiene valor, no aparece
2) Desglose "Piso + Tiempos" (depende de 20.1)
- [ ] CandidatoTarifaDto: valorPisoBase?, valorTiemposLogisticos?. Nuevo candidateTextoConDesglose(candidate): si ambos != null -> "$Piso + $Tiempos = $Total" con formatMoney; si no, formatMoney(candidate.valor) como hoy. Usarlo SOLO en la primera caja (i === 0, html:226); plaza e historico siguen con candidateText()
3) Mostrar la cotizacion existente al abrir (depende de 20.1: GET por-solicitud)
- [ ] Consultarla siempre al abrir la pantalla (quitar el if (!this.access.puede('autoAplicarPropuestas')) que hoy lo bloquea); guardar el resultado en this.cotizacion si existe. El auto-aplicado silencioso solo del Comercial no cambia. Un usuario sin autoAplicarPropuestas que abre una solicitud ya auto-costeada ve sus fletes de una vez, sin "Iniciar costeo"; y no se le crea una cotizacion en borrador solo por mirar
4) Editar flete manual (backend ya implementado; ver 20.1)
- [ ] cabina-api.models.ts: EditarLineaCotizacionRequest { valorFlete: number; valorRetornoContenedor?: number }; cotizaciones-api.service.ts: editarLinea(id, lineaSolicitudId, request) -> PUT /cotizaciones/{id}/lineas/{lineaSolicitudId}
- [ ] ts: editandoFlete: Record<string, boolean>; iniciarEdicionFlete(entry, flete) (patchValue con el valor actual y fuente 'manual'); cancelarEdicionFlete(entry, flete); editarFlete(entry) y guardarEdicionFlete(entry) (mismo bloqueo bajo el piso de 20.2)
- [ ] html: *ngIf de la tarjeta (:286) pasa a "!editandoFlete[entry.linea.lineaId] && fleteDeLinea(...) as flete; else editorFlete"; boton "Editar" en mesa-flete-success y fleteActualCard SOLO si flete.fuente === 'manual' && cotizacion?.estado !== 'Entregada' && access.puede('agregarFlete'); (ngSubmit) bifurca editarFlete/agregarFlete; <select fuente> deshabilitado al editar (el backend no cambia la fuente); boton del form dice "Guardar cambios"; boton "Cancelar" solo en edicion
- [ ] CSS: .mesa-flete-actual .cabina-button { display:block; margin:12px auto 0 }
5) Entregar solo con todas las lineas costeadas (depende de 19.3)
- [ ] puedeEntregar() (ts:700-702, hoy solo cotizacion.lineas.length > 0 && estado !== 'Entregada') agrega lineasCosteadas(vm.lineas) === vm.lineas.length (la cuenta ya existe y alimenta el icono de advertencia, html:43). Con pliego incompleto el boton queda deshabilitado
6) Ganada/Perdida solo si Entregada o auto-costeo
- [ ] Los botones de Mesa de Costeo (html:35-38) aplican el mismo criterio de puedeRegistrarResultado() de cotizacion-resumen-page.component.ts:67-74: habilitados solo si la cotizacion esta Entregada, o Borrador con esAutoCosteo = true. Hoy solo revisan el permiso marcarGanadaPerdida
7) Permiso de cubicar
- [ ] Agregar 'cubicarLinea' a Comercial, CoordinadorFletes, LiderProceso y ServiciosLogisticos en NEXO_MATRIZ_ACCESO (nexo-access.models.ts:35-89; hoy solo AdminTarifas y Admin). Actualizar nexo-access.service.spec.ts:40 (Comercial hoy espera false -> true); el de AdminTarifas (:56) ya espera true. Nota: solo CoordinadorFletes llega a ver el boton (los otros 3 no entran a Mesa de Costeo)
8) Aviso "Pendiente por enviar al comercial" se ve pequeno
- [ ] Causa: el estilo .mat-tooltip.cabina-tooltip (letra 14 px, padding, ancho max) esta SOLO en bandeja-page.component.css:69-74 con ::ng-deep, asi que fuera de Bandeja el texto sale diminuto; afecta las 7 pantallas con matTooltipClass="cabina-tooltip". Mover la regla a src/styles.css (sin ::ng-deep) y borrarla de la Bandeja
- [ ] Junto al icono (mesa-header-actions__aviso, css:320, 28 px) mostrar el texto "Pendiente por enviar" siempre visible en la misma etiqueta amarilla con letra roja (#fef3e2 / #c62828), conservando el tooltip con la frase completa; desaparece cuando la cotizacion queda Entregada

CRITERIOS DE ACEPTACION
- Chip "Valor mercancia" aparece solo con valor; la primera caja muestra "$Piso + $Tiempos = $Total" o un solo numero si el backend no trae el desglose
- Abrir una solicitud ya auto-costeada muestra sus fletes sin "Iniciar costeo"
- "Editar" aparece solo en fletes de fuente Manual y actualiza la misma linea
- No se puede entregar con lineas sin costear; Ganada/Perdida respeta el criterio
- Entrando directo a Mesa de Costeo el texto de los iconos de informacion se ve a 14 px
```
Origen: HU-18, HU-14, HU-9, HU-15, HU-11, HU-10, HU-13, HU-20 (frontend).

### [S5-HU-20.4] QA: verificacion end-to-end de Mesa de Costeo
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `qa` · Tipo: Task
Asignado a: @kcano (QA)
```
Parte de S5-HU-20 (milestone). Depende de S5-HU-20.1 a S5-HU-20.3, S5-HU-20.5, S5-HU-20.6 y S5-HU-24.6 y 24.7 (parte de conexos).

CHECKLIST
- [ ] Con un pliego real: lineas en los 3 modos, "Usar este", aplicar todas con confirmacion, editar un flete manual, bloqueo bajo el piso (agregando y editando)
- [ ] Roles: Comercial (no ve la propuesta; solo se le llenan solas las automaticas), AdminTarifas y CoordinadorFletes (ven la propuesta y los botones); y el permiso de cubicar de HU-13
- [ ] Entrega con lineas pendientes (boton deshabilitado y error del servidor) y botones Ganada/Perdida
- [ ] Elegir el candidato historico con "Usar este" y guardarlo (desplegable no queda gris)
- [ ] Abrir una solicitud auto-costeada sin "Iniciar costeo": los fletes se ven
- [ ] Entrando directo a Mesa de Costeo el texto de los iconos de informacion se ve bien
- [ ] Probar despues del despliegue del backend (hasta entonces "Editar" falla con 404)

CONEXOS (quitar, entregada, Ganada/Perdida y aviso)
- [ ] AdminTarifas agrega un conexo con la cotizacion pendiente (Borrador) y con la entregada; antes daba 403 / sin seccion
- [ ] Comercial, CoordinadorFletes y Admin agregan y quitan en ambos estados
- [ ] Quitar un conexo: desaparece de la linea, del consolidado y baja el Total; la auditoria registra servicio, cantidad y valor
- [ ] Marcar la cotizacion Ganada o Perdida: agregar y quitar quedan bloqueados (front con mensaje, API 409 COTIZACION_CERRADA)
- [ ] Cambiar conexos en una cotizacion Entregada: aparece el chip "Modificada despues de entregar"; Estado y EntregadaAt no cambian (el SLA del Tablero no se mueve)
- [ ] Aviso por correo: cambio hecho por AdminTarifas sobre una cotizacion Entregada llega al creador de la solicitud; el mismo comercial cambiando sus conexos no genera correo; un cambio sobre Borrador no genera correo
- [ ] Un conexoId de otra cotizacion devuelve 404; un usuario sin rol 403
- [ ] El PDF y la exportacion de la cotizacion reflejan los conexos tras agregar o quitar

CRITERIOS DE ACEPTACION
- Aprobacion explicita de QA registrada en la issue
```

### [S5-HU-20.5] Contrato de captura con Cotifly (backend): grupoRutaId + orden, idRecomendacionVehiculo, lineas sin vehiculo y normalizacion de vehiculos
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend` · Tipo: Task · Prioridad: por definir · Esfuerzo: por definir
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-20 (milestone; sugerido, por definir). Relacion con el borrador del cubicador: grupoRutaId e idRecomendacionVehiculo tambien aparecen alli (18.3 y 18.2). ESTA issue cubre el lado del contrato de captura con Cotifly en NEXO; las HU 18.x del cubicador cubren el lado del cubicador. Para no duplicar, el trabajo de NEXO vive aqui. La parte 3 se relaciona con S5-HU-19.2 (modos de decision de tarifa); se incluye aqui igualmente. La parte de pantalla (Mesa de Costeo) esta en S5-HU-20.6.

HISTORIA
Como Cotifly, que manda a POST /api/captura-solicitudes una linea por camion, quiero que NEXO sepa que lineas son de la misma ruta, en que orden van y de que recomendacion del cubicador salio cada camion, y que no se autocosteen lineas sin vehiculo con un precio bajo, para poder asignar cada precio a su camion al leer el resultado.

CONTEXTO (verificado en el codigo de NEXO)
- clsCapturaLineaInput no tiene campos de ruta/orden/recomendacion; tbl_lineas_solicitud no tiene columnas para ellos; el numeroLinea del handler de captura es solo un contador local que no se persiste; y GET /api/solicitudes/{id} recorre las lineas sin ordenarlas (Id GUID aleatorio), asi que el orden puede cambiar entre consultas.
- La captura ya acepta una linea sin vehiculo (el validador no lo exige y ResolverVehiculo devuelve null sin error), pero el motor busca el piso SICE-TAC sin filtro de vehiculo y toma la configuracion de menor valor (tiende a subestimar el piso); en plaza de Cupo sin vehiculo cae al "mayor valor vigente" solo con una advertencia. Una linea asi puede autocostearse con un precio bajo.
- Los codigos canonicos del cubicador (2S2, FURGON...) llegan a NEXO en la captura, mandados por quien crea la solicitud, en los campos de texto TipoVehiculo y Carroceria; el cubicador no escribe en NEXO. De las 20 parejas posibles, segun el equipo del cubicador, 6 ya existen y 14 nacerian en cuarentena (cifra no verificada por NEXO).
- clsResolucionDeCatalogos.Normalizar solo limpia espacios y pasa a mayusculas: no quita tildes ni unifica separadores. Por eso FURGON_REFRIGERADO no empata con "Furgon Refrigerado" y se crearia un duplicado en el catalogo. Lo mismo pasa con las rutas ("Bogota" y "Bogota" con tilde crean dos rutas). En el mismo archivo ya existe SinTildes, sin usar en estas claves.
- CapturaLineaRequest tiene additionalProperties: false en Swagger.

CRITERIO TRANSVERSAL (partes 1 y 2)
- Todos los campos nuevos son OPCIONALES: una captura sin ellos funciona exactamente como hoy. Cotifly los activa con su bandera Nexo:EnviarReferenciaRuta cuando NEXO los tenga. Swagger/OpenAPI y CONTRATO.md actualizados.

CHECKLIST

PARTE 1 - grupoRutaId y orden (guardados, devueltos y ordenados)
- [ ] clsCapturaLineaInput (clsCapturarSolicitudCommand.cs) y el request de la API (CapturaLineaRequest): agregar GrupoRutaId (Guid?) y Orden (int?), JSON camelCase grupoRutaId (GUID como string) y orden
- [ ] Validacion: grupoRutaId que no es GUID -> 400 con codigo propio (p. ej. GRUPO_RUTA_INVALIDO); orden >= 1 si viene; no se exige unicidad de orden ni que venga junto a grupoRutaId
- [ ] clsLineaSolicitud: propiedades GrupoRutaId y Orden; el handler de captura las persiste tal cual vienen (si no vienen, quedan null)
- [ ] docs/Database/ESQUEMA_NEXO.sql + migracion: columnas nullable GrupoRutaId (uniqueidentifier) y Orden (int) en tbl_lineas_solicitud, sin default ni backfill
- [ ] GET /api/solicitudes/{id}: clsLineaCosteoDto devuelve GrupoRutaId y Orden por linea; clsObtenerCosteoQuery ordena por Orden ascendente (nulos al final), luego por fecha de creacion y luego por Id (orden estable)

PARTE 2 - idRecomendacionVehiculo por linea
- [ ] clsCapturaLineaInput y CapturaLineaRequest: IdRecomendacionVehiculo (Guid?), JSON idRecomendacionVehiculo (GUID como string)
- [ ] Validacion: GUID invalido -> 400 con codigo propio (p. ej. RECOMENDACION_VEHICULO_INVALIDA)
- [ ] clsLineaSolicitud y tbl_lineas_solicitud: columna nullable IdRecomendacionVehiculo (uniqueidentifier), en la misma migracion de la parte 1 (sin backfill)
- [ ] GET /api/solicitudes/{id}: clsLineaCosteoDto devuelve IdRecomendacionVehiculo por linea

PARTE 3 - linea sin vehiculo no se autocostea y va a la Mesa
- [ ] En el motor de propuesta (clsPropuestaTarifaAdapter.ProponerLineaAsync / decision por modo): si la linea exige vehiculo y llega sin vehiculo, NO se autocostea (ni Solo-SICETAC ni plaza vigente mayor): no hay valor automatico y queda para consulta completa
- [ ] Alcance de "exige vehiculo": solo clase de viaje Cupo (y lineas sin clase de viaje y sin vehiculo). NO aplica a Contenedor (alli el vehiculo se deduce por peso y clase de contenedor; no es "linea sin vehiculo"). Decidido por Sara: Carga suelta y Extradimensionado no lo exigen
- [ ] La linea queda EstadoLinea PorCostear con MotivoPendiente "Linea sin vehiculo: requiere consulta completa"
- [ ] El motor sigue devolviendo los candidatos (piso SICE-TAC sin filtro, plaza, historico) como referencia para la Mesa; el Administrador de Tarifas elige o ingresa el valor (nunca bajo el piso)
- [ ] Una linea sin vehiculo que recibe vehiculo en la Mesa se puede costear normalmente

PARTE 4 - normalizar vehiculos (y rutas) y cuarentena de codigos desconocidos
- [ ] clsResolucionDeCatalogos.Normalizar / ClaveVehiculo (y de paso ClaveRuta): quitar tildes (reutilizar SinTildes) y unificar separadores (guion bajo, guion, espacios) antes de comparar, para que FURGON_REFRIGERADO y "Furgon Refrigerado", o "Bogota" y "Bogota" con tilde, no creen duplicados
- [ ] Alinear con el diccionario de alias del borrador del cubicador (18.4): o el diccionario cubre las variantes o la normalizacion las cubre; no duplicar logica
- [ ] Cuarentena (18.4 del borrador del cubicador): un codigo de vehiculo desconocido no debe entrar al catalogo como configuracion oficial; se detalla en la HU de cuarentena del cubicador, esta issue solo deja la normalizacion lista y no crea duplicados
- [ ] Pruebas con variantes de tildes, mayusculas y separadores (vehiculos y rutas), y regresion: los vehiculos y rutas existentes siguen resolviendose a la misma fila

CONTRATO Y PRUEBAS
- [ ] Swagger/OpenAPI y CONTRATO.md (seccion 3.3, tabla "Cada linea" y respuesta del GET) actualizados con grupoRutaId, orden e idRecomendacionVehiculo
- [ ] Pruebas: captura con y sin los campos (sin ellos, identica a hoy); GET devuelve las lineas en el orden enviado aunque se capturen desordenadas; GUID invalido -> 400; ida y vuelta de idRecomendacionVehiculo; Cupo sin vehiculo -> PorCostear sin precio; Cupo con vehiculo y Contenedor sin vehiculo -> sin cambios; pliego mixto: solo las lineas sin vehiculo quedan pendientes; un pliego por Excel sigue funcionando sin los campos
- [ ] QA: prueba real contra la API de una linea sin vehiculo y de varios camiones de la misma ruta enviados desordenados

CRITERIOS DE ACEPTACION
- Una captura sin grupoRutaId, orden ni idRecomendacionVehiculo funciona exactamente como hoy
- Con los campos, el GET los devuelve por linea con los mismos nombres y las lineas salen ordenadas por orden
- Un GUID invalido en grupoRutaId o idRecomendacionVehiculo se rechaza con un error claro
- Ninguna linea de Cupo sin vehiculo se autocostea con un precio: queda pendiente con motivo; las lineas de contenedor y las que traen vehiculo no cambian
- Un vehiculo o una ruta con tildes o separadores distintos no crea duplicados
- Swagger y CONTRATO.md reflejan los campos nuevos

RESPUESTAS DE NEXO A COTIFLY (decididas por Sara)
- Codigo de mercancia: no se manda en la captura. NEXO no la usa para costear y el cubicador se comunica directo con Cotifly. Si mas adelante se necesitara, iria por posicion.
- Endpoint de cubicacion de NEXO: no se retira (la pantalla de cubicar de NEXO lo usa). Basta con que Cotifly deje de llamarlo. Retirar el POST del cubicador es otra decision: exige migrar antes el adaptador de NEXO a /recomendaciones.
- Alcance de "linea sin vehiculo": solo Cupo.
- posiciones[]: Cotifly puede dejar de mandarlas; NEXO las sigue aceptando como opcionales (para costear basta el peso del camion; se pierde el detalle de dimensiones guardado en NEXO).
- Codigos del cubicador (cuarentena de vehiculos): llegan en los campos TipoVehiculo y Carroceria actuales; ver parte 4.
```
Origen: acuerdo de contrato de captura con Cotifly (HU-021 de Cotifly).

### [S5-HU-20.6] Mesa de Costeo: lineas agrupadas por ruta y aviso de linea sin vehiculo
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task · Prioridad: por definir · Esfuerzo: por definir
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-20 (milestone; sugerido, por definir). Depende de S5-HU-20.5 (grupoRutaId y orden en el GET, y las lineas sin vehiculo pendientes con motivo). Se puede hacer con datos simulados mientras el backend no este.

CONTEXTO
- Con una linea por camion, una ruta puede tener varias lineas. Hoy la Mesa las muestra todas planas, sin ninguna relacion entre ellas.
- Las lineas sin vehiculo ya no se autocostean (20.5): quedan pendientes con el motivo "Linea sin vehiculo: requiere consulta completa" y hay que explicarlo en pantalla.

CHECKLIST
- [ ] Modelo: LineaCosteoDto (cabina-api.models.ts) agrega grupoRutaId?: string | null y orden?: number | null
- [ ] mesa-costeo-page: agrupar las lineas por grupoRutaId; cada grupo muestra la ruta (origen -> destino) y cuantas lineas/camiones tiene; dentro del grupo, las lineas van en el orden recibido del servidor (ya viene ordenado por orden)
- [ ] Las lineas sin grupoRutaId se muestran como hoy (sin grupo); una solicitud sin ningun grupoRutaId se ve exactamente igual que hoy
- [ ] La numeracion "Linea N", el indicador de avance, el resumen completo y las demas funciones de la Mesa no cambian
- [ ] Aviso en las lineas sin vehiculo: mostrar el motivo "Linea sin vehiculo: requiere consulta completa" para que se entienda por que quedaron pendientes; sin bloquear que el Administrador de Tarifas elija vehiculo, elija un candidato o ingrese el valor

CRITERIOS DE ACEPTACION
- Una solicitud con grupoRutaId en sus lineas se ve agrupada por ruta y ordenada
- Una solicitud sin esos campos se ve igual que hoy
- Una linea sin vehiculo muestra su motivo y se puede costear desde la Mesa
- No cambia ninguna otra funcion de la Mesa de Costeo
```
Origen: acuerdo de contrato de captura con Cotifly (HU-021 de Cotifly).

### [S5-HU-20.7] Mesa de Costeo: ofrecer guardar el valor manual como tarifa plaza (solo frontend)
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task · Prioridad: por definir · Esfuerzo: por definir
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-20 (milestone). Sin cambios de backend: usa los endpoints de Tarifas Plaza que ya existen (GET listar, POST crear, PUT editar; roles AdminTarifas y Admin). Referencia: RN-04 a RN-07 y flujo 5.2 del Entendimiento, con una diferencia decidida por Sara: la pregunta aparece SOLO con valor manual.

CONTEXTO
- Hoy las tarifas plaza de NEXO solo se mantienen en la pantalla Tarifas Plaza (a mano o por Excel). Cuando el Administrador de Tarifas escribe un valor manual en la Mesa de Costeo, ese valor se aplica a la linea y se pierde como referencia.
- El disparador de la base de datos guarda el valor anterior en tbl_tarifas_plaza_historico al editar Valor, Vigencia o Detalles, asi que actualizar una tarifa existente por el PUT ya conserva el historico.
- Prototipado y validado en el sandbox el 2026-10-14 (compila; escritura de la plaza simulada).

FLUJO
1. En consulta completa, AdminTarifas escribe un valor MANUAL (fuente "manual") al agregar o editar el flete de una linea.
2. Primero corre la validacion de piso que ya existe: si el valor es menor que el piso SICE-TAC se bloquea y no se sigue.
3. Si el rol tiene access.puede('verTarifasPlaza') (AdminTarifas y Admin; CoordinadorFletes NO), se abre un modal: "¿Quieres que este valor sea el nuevo precio de la tarifa de plaza de esta ruta?" con "No, solo aplicar a la linea" y "Si, guardar como plaza".
4. "No": se guarda el flete como hoy y no se toca ninguna plaza.
5. "Si": se muestra la fila de tarifa prellenada y editable: linea de negocio, clase de viaje, origen, destino, clase de contenedor, rango de peso, tipo de vehiculo, valor (el manual), vigencia (fecha, OBLIGATORIA, vacia por defecto, no se ingresan dias) y detalles (opcional).
6. Antes de la fila, se busca si ya existe la tarifa (misma clave que la carga masiva: linea de negocio, clase de viaje, origen, destino, clase de contenedor, rango de peso, tipo de vehiculo) y el modal avisa, mostrando valor y vigencia anteriores:
   - Vigente: "Ya existe una tarifa de plaza vigente para esta ruta y configuración. ¿Deseas actualizarla?"
   - Vencida: se ofrece actualizarla con el nuevo valor.
   - No existe: "No hay tarifa de plaza para esta ruta. Se creará una nueva."
7. Al confirmar: se guarda el flete (endpoint de siempre) y despues la plaza (PUT sobre la existente o POST si no existe). Si la plaza falla, el flete ya quedo guardado: mensaje claro sin deshacerlo. Si falla el flete, el modal queda abierto para reintentar.

CHECKLIST
- [ ] Punto unico de entrada continuarGuardado() que llaman agregarFlete(), editarFlete() y confirmarFleteBajoPiso(); abre el modal solo si fuente === 'manual' && access.puede('verTarifasPlaza')
- [ ] guardarFlete()/guardarEdicionFlete() exponen el observable crudo (agregarLinea$/editarLinea$) para encadenar la plaza DESPUES del flete sin duplicar la construccion del request
- [ ] Modal de 2 pasos (pregunta y formulario) con el patron .mesa-modal-overlay/.mesa-modal/cdkTrapFocus; variante ancha para el formulario
- [ ] Prellenado desde la linea (LineaCosteoDto ya trae lineaNegocio, claseViaje, claseContenedor, tipoVehiculo, peso y ruta): origen y destino con municipioCoincidente() (desempate Colombia) sobre el texto de la ruta separado por la flecha; si no se resuelve o es ambiguo, el campo queda vacio y se elige con el autocompletado de ciudades; rango de peso = el RangoPesoDto cuyo [pesoMinimo, pesoMaximo] cubre el peso de la linea
- [ ] Reutilizar (exportar) de tarifas-plaza-excel.util.ts municipioCoincidente, claveDeTarifaExistente y claveDeValoresPlaza, sin duplicar la logica
- [ ] Busqueda de la tarifa existente con tarifasPlazaApi.listar() (cacheado por CabinaCacheService mientras dure la pantalla)
- [ ] Vigencia obligatoria; detalles opcionales (diferente a Tarifas Plaza manual, donde si se exige: decision de Sara)
- [ ] Escritura REAL de la plaza (el prototipo la simulaba con un console.log en guardarTarifaPlazaSimulada$): reemplazar por tarifasPlazaApi.crear / editar y mostrar el mensaje de exito
- [ ] NO aparece al elegir un candidato (SICE-TAC, plaza, historico), ni con "Aplicar todas las propuestas", ni con roles sin verTarifasPlaza

PRUEBAS
- [ ] AdminTarifas con valor manual que pasa el piso: sale el modal; "No" guarda solo el flete; "Si" abre la fila prellenada
- [ ] Con tarifa vigente existente, vencida existente y sin tarifa: se ve el aviso correcto y, cuando existe, el valor y la vigencia anteriores
- [ ] Al confirmar, el flete se guarda y la tarifa plaza se crea o se actualiza; al actualizar, el valor anterior queda en tbl_tarifas_plaza_historico
- [ ] Si falla la escritura de la plaza, el flete queda guardado y el mensaje lo dice; si falla el flete, el modal queda abierto
- [ ] Valor manual bajo el piso: se bloquea antes de preguntar
- [ ] CoordinadorFletes con valor manual: no sale la pregunta y guarda como siempre
- [ ] Ciudad ambigua o no resuelta: el campo queda vacio y se puede elegir a mano
- [ ] Aplicar todas las propuestas y elegir un candidato: nunca abren el modal
- [ ] QA: probar de extremo a extremo contra el backend real y revisar la tarifa y su historico en Tarifas Plaza

CRITERIOS DE ACEPTACION
- La pregunta aparece solo con valor manual, solo para AdminTarifas y Admin, y despues de validar el piso
- Con "Si", la fila sale prellenada y editable, con la vigencia obligatoria; con "No", la plaza no cambia
- El aviso distingue tarifa vigente, vencida y no existente, y muestra la tarifa anterior cuando existe
- El flete se guarda primero y la plaza despues; un fallo de la plaza no deshace el flete
- Actualizar una tarifa existente conserva el valor anterior en el historico
```
Origen: HU-25 de Mesa de Costeo (2026-10-09_HU-FRONTEND.md), RN-04 a RN-07 del Entendimiento. Numeracion en el repositorio `administracion`: su S5-HU-26 equivale a S5-HU-20 de NEXO (esta issue seria 26.x alli).

---

## Milestone S5-HU-21 — Tablero: subvistas, orden de indicadores, filtro de fecha y tiempo de costeo

El Tablero se divide en Productividad y SICE-TAC, reordena y colorea los indicadores de la cola, agrega un filtro de fecha compartido y mide el tiempo de costeo de principio a fin, con corte por tamano de pliego. **Es de cara a AdminTarifas y Admin** (clsMetricasController: `[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]`; de los 3 roles que se entregan al cliente, el Comercial no lo ve).

### [S5-HU-21.1] API del Tablero: filtro de fecha en la cola y tiempo de costeo hibrido por tamano
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend` · Tipo: Task
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-21 (milestone). Depende de S5-HU-19.3 (para que EntregadaAt signifique pliego completo).

CONTEXTO
- Filtro: CumplimientoAsync (clsMetricasAdapter.cs:13-42) agrupa Solicitudes por Estado sin filtro: Entregadas, Descartadas y Total son acumulados desde el inicio del sistema y no sirven para ver "que paso esta semana". Decision de negocio: UN solo filtro Desde/Hasta compartido con "KPIs del Piloto", y Cumplimiento SLA tambien entra al filtro (se filtra por EntregadaAt; las otras 5 tarjetas por Solicitud.CreatedAt). Para Descartadas no existe fecha de descarte: se aproxima por la de creacion. Cumplimiento SLA = % de cotizaciones entregadas con Deadline informado que se entregaron a tiempo (EntregadaAt <= Deadline); su formula no cambia (el hueco de no contar las vencidas sin entregar fue aceptado por negocio).
- Tiempo de costeo: hoy KpisPilotoAsync mide TerminadoEn - SolicitadoEn de la saga clsEstadoAutoCosteo. TerminadoEn se marca cuando todas las lineas tienen "desenlace" (clsEstadoAutoCosteo.cs:59-66), incluso las que quedan esperando a una persona: mide cuanto tardo el robot en revisar todo, no cuando la cotizacion esta lista. Las lineas se procesan en paralelo (un mensaje por linea, clsRepartidorDeLineasConsumer), por eso el tiempo NO es proporcional a las lineas y dividir por linea no sirve; se segmenta por tamano. No existe un umbral de 300 lineas en el codigo (era un ejemplo del documento original); el unico umbral real es UmbralCosteoDiferido = 10 (sincrono vs. segundo plano, clsCapturarSolicitudCommand.cs:62).
- Redefinicion decidida: pliego 100% automatico (saga Completado) = TerminadoEn - SolicitadoEn; pliego con intervencion manual (saga Parcial) = desde que se origina la solicitud hasta Cotizacion.EntregadaAt. Limitacion conocida: hasta cerrar 19.3 una entrega parcial puede subestimar el tiempo.

CHECKLIST
Filtro de fecha
- [ ] IMetricasPort.CumplimientoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, ct); clsObtenerCumplimientoQuery(Desde, Hasta); en el adaptador, db.Solicitudes filtrado por CreatedAt para Pendientes/En costeo/Entregadas/Descartadas/Total
- [ ] Cumplimiento SLA: filtrar las Cotizaciones consideradas (EntregadaAt y Deadline informados) por EntregadaAt en el rango. Licitaciones sin filtro (ni siquiera se renderizan)
- [ ] clsCumplimientoDto agrega Desde y Hasta como primeros 2 campos (igual que clsKpisPilotoDto); GET /api/metricas/cumplimiento acepta [FromQuery] desde y hasta opcionales. Sin ellos, comportamiento identico al de hoy
Tiempo de costeo
- [ ] En KpisPilotoAsync: para cada saga con TerminadoEn: si CurrentState = Completado, minutos = TerminadoEn - SolicitadoEn; si Parcial, minutos = Cotizacion.EntregadaAt (de esa SolicitudId) - Solicitud.CreatedAt; si todavia no se entrego, ese pliego no entra (mismo criterio que Cumplimiento SLA)
- [ ] Con esa lista combinada, calcular TiempoPromedioResolucionMinutos, TiempoMedianoResolucionMinutos y PorcentajePliegosBajo10Minutos (<= 10 min) igual que hoy
- [ ] Parametro de tamano (por TotalLineas de la saga): 1-10, 11-100, +100; sin parametro = todos
Pruebas
- [ ] Con y sin desde/hasta; tamano por tamano; un pliego automatico y uno con intervencion manual

CRITERIOS DE ACEPTACION
- GET cumplimiento sin parametros se comporta igual que hoy
- Con desde/hasta, las 5 tarjetas cuentan solicitudes creadas en el rango y Cumplimiento SLA solo cotizaciones entregadas en el rango
- El tiempo de costeo refleja el ciclo completo en pliegos con intervencion manual y se puede cortar por tamano
```
Origen: HU-B12, HU-B13.

### [S5-HU-21.2] Tablero: pestanas, orden y color de la cola, filtro de fecha compartido y selector de tamano
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-21 (milestone). Depende de S5-HU-21.1.

CONTEXTO
La pantalla tenia 3 bloques seguidos (cola, KPIs del Piloto con su filtro, iframe SICE-TAC). Se separan en pestanas; la cola se reordena con colores que reflejan urgencia; un solo filtro de fecha alimenta cola y Piloto. Los graficos de antiguedad y evolucion semanal se evaluaron y se DESCARTARON (no implementar).

CHECKLIST
- [x] Pestanas Productividad (cola + KPIs del Piloto) y SICE-TAC (iframe, sin llamada a API) con el patron de Biblioteca: tabs/tab, cambiarTab(), onTabKeydown(), roles ARIA tablist/tab/tabpanel (sandbox, tablero-page.component.*)
- [x] construirKpis() en este orden: Total solicitudes (neutral/negro), Pendientes (tono bad/rojo), En costeo (tono nuevo info/azul #1565c0; agregar 'info' al tipo Tono y .tablero-kpi__value--info), Entregadas (ok/verde), Descartadas (neutral/negro), Cumplimiento SLA (tono dinamico y su hint "X de Y a tiempo" sin cambios); quitar los hints "por costear" y "en proceso" (sandbox)
- [ ] Un solo par desdeInput/hastaInput que al cambiar dispara cargar() Y cargarPiloto() con el mismo rango; modelos CumplimientoDto con desde/hasta; metricas-api.service.ts obtenerCumplimiento(desde?, hasta?) con los mismos params que obtenerKpisPiloto; sin fechas, historico completo; un solo control en la pantalla (no dos), ubicado para que se vea que aplica a los dos bloques
- [ ] Selector Todos / 1-10 / 11-100 / +100 para "Tiempo de costeo del pliego" y "Pliegos bajo 10 minutos": recalcula sin recargar; "Todos" sigue como numero principal

CRITERIOS DE ACEPTACION
- Al cambiar el rango se actualizan ambos bloques; Cumplimiento SLA cambia con el filtro
- Sin fechas todo muestra el historico como hoy
- Los 3 hints/colores quedan como se describe; el selector de tamano cambia los 2 numeros de inmediato
```
Origen: HU-2, HU-3, HU-4 del Tablero; HU-5 descartada.

### [S5-HU-21.3] QA: verificacion de los calculos del Tablero
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `qa` · Tipo: Task
Asignado a: @kcano (QA)
```
Parte de S5-HU-21 (milestone). Depende de S5-HU-21.1 y S5-HU-21.2.

CHECKLIST
- [ ] Validar cada tarjeta de la cola contra un conteo manual en la base, con y sin filtro de fecha
- [ ] Validar el tiempo de costeo con un pliego automatico y uno con intervencion manual (ambos contra el calculo a mano)
- [ ] Validar el corte por tamano (1-10 / 11-100 / +100)
- [ ] Solo AdminTarifas y Admin ven el Tablero
- [ ] Confirmar que "% lineas auto-costeadas" baja respecto de antes de S5-HU-19.2 (esperado)

CRITERIOS DE ACEPTACION
- Aprobacion explicita de QA registrada en la issue
```

---

## Milestone S5-HU-22 — Tarifas Plaza: carga masiva de Excel y calidad de datos

Seguimiento de las tarifas de plaza propias de NEXO (S4-HU-10 ya cerro): leer bien las fechas del Excel, quitar los casos especiales del catalogo de vehiculos, evitar que una ciudad con nombre repetido caiga en el departamento equivocado y dejar listo el export de Reglas Solo-SICETAC y un permiso de catalogos.

### [S5-HU-22.1] Carga masiva y catalogos: fecha de Vigencia, casos especiales de vehiculos, departamento, ciudades y export
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend`, `bug` · Tipo: Bug
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-22 (milestone).

CONTEXTO
1) Fecha de Vigencia. clsEfTarifasPlazaRepository.cs (lineas 93, 126, 198) lee VIGENCIA con Str() = GetString() y DateOnly.TryParse sin cultura: una fecha guardada como valor nativo de Excel (serial) falla o se transforma segun la cultura del servidor. Caso real (2026-10-05): celda en formato General con serial 46315 (2026-10-20), rechazada con "no es una fecha de vigencia valida". El arreglo ya existe y esta probado en clsEfServiciosConexosRepository.cs:226-235 (LeerFecha). Importadores: Tarifas Plaza roto; Servicios Conexos ya correcto; Reglas Solo-SICETAC sin fecha; captura masiva de lineas (clsExcelSolicitudesAdapter) no confirmado.
2) Casos especiales. clsTiposVehiculoAdapter.cs sumaba 5 valores escritos a mano (CNT20/Consolidado, CNT20/Exclusivo, CNT40/Consolidado, CNT40/Exclusivo, PBV 7500-8000) al catalogo real tblSAPTiposVehiculoTarifario. Negocio confirmo que ya no son necesarios: se ELIMINAN, no se mueven a una tabla. Arregla el desplegable en todas las pantallas que usan GET /api/catalogos/tipos-vehiculo (Reglas Solo-SICETAC, formulario manual de Tarifas Plaza y vista previa de carga masiva). Los codigos 3S2 y 2L3 faltan en el catalogo de SAP: decision de negocio, se deja asi por ahora (no se hardcodean).
3) Ciudades repetidas. El importador agrupa los municipios por nombre y toma el primero (Colombia primero; clsEfTarifasPlazaRepository.cs:64-66: GroupBy(Normalizar(m.Municipio)) ... OrderBy(Pais=="CO"?0:1).First()). Entre 2 municipios colombianos con el mismo nombre (ej. "ARMENIA" en Antioquia y Quindio) gana el que este primero en la base, sin avisar. El formulario manual no tiene el problema (elige por Id).
4) Ciudades para CoordinadorFletes. GET /api/captura-solicitudes/ciudades (y /departamentos, /municipios) hereda [Authorize(Roles = "Comercial,Admin")] de la clase (clsCapturaSolicitudesController.cs:20) y devuelve 403 al rol CoordinadorFletes, que si tiene gestionarServicios en Biblioteca; el frontend traga el error (catchError(() => of([]))) y el campo se queda sin sugerencias. Un [Authorize(Roles)] en el metodo NO reemplaza el de la clase: se combinan con AND; hay que sacar los roles del nivel de clase.
5) Reglas Solo-SICETAC solo tiene POST importar-excel y GET plantilla-excel (en blanco); falta descargar las reglas ya cargadas.

CHECKLIST
Fecha
- [ ] Copiar LeerFecha(IXLCell) a clsEfTarifasPlazaRepository (celda via row.Cell(cols[col]); tipo nativo DateTime primero, luego formatos dd/MM/yyyy, d/M/yyyy, yyyy-MM-dd, MM/dd/yyyy en InvariantCulture, al final es-CO a proposito) y usarla en vez de Str(row, cols, "VIGENCIA") + DateOnly.TryParse
- [ ] Idealmente mover LeerFecha a un helper compartido; regla para todo importador nuevo con fecha: tipo nativo + formatos explicitos + es-CO
- [ ] El error "no es una fecha de vigencia valida" solo aparece cuando de verdad no es una fecha reconocible
Vehiculos
- [x] Quitar CasosEspeciales y la concatenacion de ListarAsync (devuelve solo el catalogo real, o [] si dbSolucionesTI no esta disponible): compilado, 0 errores
- [ ] Desplegar y verificar en las 3 pantallas; las reglas ya guardadas con alguno de los 5 codigos no se tocan
Departamento
- [ ] El importador acepta columnas OPCIONALES DEPARTAMENTO ORIGEN y DEPARTAMENTO DESTINO: con valor, busca el municipio por nombre + departamento (ambos normalizados); vacias o inexistentes, igual que hoy; si el par no existe: error de fila "'Armenia' no existe en el departamento 'X'". El frontend las llena (S5-HU-22.2)
Ciudades
- [ ] clsCapturaSolicitudesController: clase con [Authorize] simple (sin roles; exige estar autenticado con un rol valido via la politica de respaldo, igual que clsSolicitudesController.cs:15-16) y [Authorize(Roles = "Comercial,Admin")] en los metodos AnalizarExcel (POST analizar-excel) y Capturar (POST). Ciudades, Departamentos y Municipios sin [Authorize] propio. Revisar caso por caso Aprovisionamiento y Clientes (no asumir)
Reglas Solo-SICETAC
- [ ] GET exportar-excel en clsReglasSoloSicetacController.cs, mismo patron que Tarifas Plaza (clsTarifasPlazaController.cs:84); mismo formato de columnas que acepta importar-excel

CRITERIOS DE ACEPTACION
- Una fecha nativa de Excel y una en texto (dd/MM/yyyy, d/M/yyyy, yyyy-MM-dd, MM/dd/yyyy) se leen bien sin importar la cultura del servidor
- El desplegable de tipos de vehiculo ya no muestra los 5 casos especiales
- Con las columnas de departamento, la tarifa queda en el municipio de ese departamento; un departamento que no corresponde da error en esa fila; sin las columnas, igual que hoy
- CoordinadorFletes ve sugerencias de ciudades en Biblioteca -> Servicios -> Nuevo -> Almacenamiento; otros roles validos tambien; Comercial/Admin sin regresion; Capturar y AnalizarExcel siguen solo para Comercial/Admin
- El exportado de Reglas Solo-SICETAC se edita y reimporta sin errores
```
Origen: HU-B4, HU-B5, HU-B21, HU-B3, HU-B6.

### [S5-HU-22.2] Carga masiva y reglas: elegir departamento de ciudades repetidas y exportar Excel
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-22 (milestone). Depende de S5-HU-22.1 y S5-HU-24.3 (la vista previa editable donde va este recuadro).

CONTEXTO
- La vista previa de la carga masiva de Tarifas Plaza (todo en el frontend, sin endpoints nuevos: parsea el Excel en el navegador, lo reconstruye y lo manda al mismo importar-excel) resuelve la ciudad con municipioCoincidente() (tarifas-plaza-excel.util.ts:85-89), que toma el primero (Colombia primero) sin avisar. Decision de negocio: en la vista previa, antes de confirmar, mostrar abajo cual municipio y de que departamento se usara, para que el usuario confirme o lo cambie. MunicipioDto ya trae departamento y pais (tarifas-plaza.models.ts:64-72).
- Reglas Solo-SICETAC necesita el mismo boton "Exportar Excel" que ya tiene Tarifas Plaza, para editar y reimportar.

CHECKLIST
Ciudades repetidas
- [ ] municipioCoincidente() devuelve TODOS los candidatos con ese nombre normalizado (el desempate por defecto, Colombia primero, queda como valor preseleccionado)
- [ ] validarValoresFilaPlaza() (:179): cuando el nombre de origen o destino coincide con 2 o mas municipios, guardar en la fila el municipio elegido con su departamento y la lista de departamentos posibles
- [ ] Recuadro debajo de la grilla, antes de confirmar, "Ciudades con el mismo nombre en varios departamentos": una linea por caso, ej. "Fila 12 · Origen · ARMENIA -> se usara Armenia (Quindio) [desplegable] (tambien existe en: Antioquia)"; el desplegable lista los departamentos posibles, preseleccionado con el elegido; sin casos, el recuadro no aparece
- [ ] crearArchivoCargaPlaza() (:284) agrega las columnas DEPARTAMENTO ORIGEN y DEPARTAMENTO DESTINO con el departamento elegido (las que entiende el backend, 22.1)
Reglas Solo-SICETAC
- [ ] Boton "Exportar Excel" que descarga el GET exportar-excel de 22.1, igual que el de Tarifas Plaza

CRITERIOS DE ACEPTACION
- Una ciudad repetida muestra cual se usara, de que departamento y en cuales otros existe; el usuario puede cambiarlo antes de confirmar; la tarifa queda en el municipio elegido
- Sin ciudades repetidas la vista previa se ve igual que hoy
- El boton exporta las reglas cargadas
- No se valida tipo de vehiculo contra rango de peso (decidido: no se hace)
```
Origen: HU-9 (Carga masiva), HU-1 (Reglas Solo-SICETAC).

### [S5-HU-22.3] QA: verificacion de Tarifas Plaza y carga masiva
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `qa` · Tipo: Task
Asignado a: @kcano (QA)
```
Parte de S5-HU-22 (milestone). Depende de S5-HU-22.1 y S5-HU-22.2.

CHECKLIST
- [ ] Importar un Excel con fechas nativas, con fechas en texto y con una fecha en serial general (46315): se leen bien
- [ ] Importar una fila con "Armenia" eligiendo cada departamento y confirmar el municipio guardado en tbl_tarifas_plaza
- [ ] Importar sin las columnas de departamento: comportamiento igual al de hoy
- [ ] El catalogo de vehiculos ya no muestra los 5 casos especiales en Reglas Solo-SICETAC, formulario de Tarifas Plaza y vista previa
- [ ] Con rol CoordinadorFletes, buscar ciudades en Biblioteca -> Servicios -> Almacenamiento; con Comercial y Admin sin regresion; Capturar/AnalizarExcel con otro rol da 403
- [ ] Exportar Reglas Solo-SICETAC, editar una fila y reimportar

CRITERIOS DE ACEPTACION
- Aprobacion explicita de QA registrada en la issue
```

---

## Milestone S5-HU-23 — Bandeja: vigencia por Fecha Limite, pendientes por entregar y resultado de la cotizacion

La Bandeja tiene 2 errores de logica en el backend (filtro "vigente" y filtro "Pendientes"). Es UNA sola consulta (GET de Bandeja, clsObtenerBandejaQuery) para todos los roles, asi que la correccion tambien aplica a la bandeja del Comercial. El aislamiento por comercial (el Comercial solo ve lo que el mismo creo) YA existe: issue #199 / S4-HU-19.1, en esa misma consulta.

### [S5-HU-23.1] Bandeja backend: vigencia por Fecha Limite, pendientes por entregar y resultado de la cotizacion
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `backend` · Tipo: Task
Asignado a: @ivasco-dev (Backend)
```
Parte de S5-HU-23 (milestone).

CONTEXTO
- Vigencia: clsObtenerBandejaQuery.cs (lineas 50-53 y 67) calcula "vigente" como "capturada hace menos de 7 dias" (CreatedAt >= ahora-7d). Nunca mira Deadline (Fecha Limite). Regla confirmada: una solicitud sin Deadline cuenta como vigente.
- Pendientes: clsEfSolicitudesCatalogoRepository.cs (lineas 50-51 y 57-69). Una solicitud con todas las lineas revisadas por el Administrador de Fletes pero sin clic en "Entregar" no aparece en "Pendientes" (solo mira "hay linea sin flete"). Decidido: NO crear categoria nueva; sumarla a "Pendientes" y que siga apareciendo en "Atendidas por el Administrador de Fletes". No aplica a Automatico (auto-costeo tiene su propio atajo S4-HU-15.6).
- Resultado (HU-B25): al marcar Ganada o Perdida la solicitud pasa a Entregada (clsMarcarResultadoCommand) y sale de la vista por defecto "Pendientes"; la Bandeja no muestra el resultado. Decidido por Sara (opcion A): columna Resultado + filtro nuevo "Esperando resultado"; NO se mezclan en "Pendientes" (es la cola de trabajo del Administrador de Tarifas). Detalle en HU-B25.
- Aplica a TODOS los roles, incluido Comercial: es la misma consulta (clsObtenerBandejaQuery) y el mismo repositorio. Para el Comercial la consulta ya se limita a lo que el mismo creo (creadoPorFiltro, issue #199 / S4-HU-19.1), asi que sus filtros "vigente/vencida" y "Pendientes" cambian igual, pero solo sobre sus solicitudes.
- Aislamiento por comercial (HU-008.1 de Cotifly): YA esta implementado en clsObtenerBandejaQueryHandler (creadoPorFiltro = usuarioActual.Id cuando el rol es Comercial; los demas roles ven todo). No hay que codificarlo; solo verificar con QA (ver 23.2) y confirmar que cubre tambien GET por id y Mesa de Costeo.

HOY -> DEBE QUEDAR
- Vigencia: la comparacion usa f.Deadline contra "ahora" en las 3 referencias (filtro vigente, filtro vencida, calculo del campo vigencia). Sin Deadline = vigente.
- Pendientes: la rama "pendientes" se combina (OR) con la condicion de la rama "administrador": todas las lineas con flete Y alguna Revisada:
  "pendientes" => q.Where(s =>
      s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))
      || (s.Lineas.Any() && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)) && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada))),

CHECKLIST
- [ ] clsObtenerBandejaQuery: cambiar las 3 referencias de CreatedAt/limiteVigencia por Deadline vs ahora; Deadline nulo = vigente
- [ ] clsEfSolicitudesCatalogoRepository: rama "pendientes" con el OR descrito (el filtro externo Estado Pendiente/EnCosteo ya excluye Entregada y Descartada)
- [ ] HU-008.1: NO implementar de nuevo. Verificar que el aislamiento existente (creadoPorFiltro) tambien se aplica a GET de una solicitud por id, a Mesa de Costeo y a la cotizacion; si falta en alguno, abrir una issue aparte con ese alcance concreto
- [ ] Verificar que "Pendientes" con el OR no mezcla solicitudes de otro comercial para el rol Comercial (el filtro por creador se aplica antes)
- [ ] Pruebas unitarias: vigente/vencida/sin Deadline; pendientes con todas las lineas Revisada; automatica sin cambios; con rol Comercial los filtros nuevos operan solo sobre sus solicitudes
- [ ] El frontend de vigencia no requiere cambio (el badge consume el campo ya corregido); el de resultado va en 23.3
- [ ] Resultado (HU-B25): clsBandejaItemDto agrega Resultado ("SinDefinir"|"Ganada"|"Perdida", null si no hay cotizacion; con varias cotizaciones: la que tenga resultado distinto de SinDefinir, si no la mas reciente); proyectarlo en BandejaFilasAsync / clsBandejaFilaRaw con una subconsulta
- [ ] Filtro nuevo resultado en GET /api/solicitudes y en clsBandejaFiltro: esperando (cotizacion Entregada, o Borrador con EsAutoCosteo, con Resultado SinDefinir y solicitud no Descartada), ganada, perdida; documentarlo en el comentario del controlador
- [ ] Con resultado informado y sin atencion, no aplicar la restriccion por defecto Estado Pendiente/EnCosteo (tratar atencion vacia como todas)
- [ ] Visibilidad (decidido por Sara): Resultado solo para Comercial y Admin; con rol AdminTarifas la Bandeja devuelve Resultado = null y el parametro resultado no devuelve informacion de resultados (ignorar o 403)
- [ ] Pruebas unitarias del filtro resultado: esperando excluye Ganada, Perdida y descartadas; ganada/perdida; con rol Comercial solo lo suyo; con AdminTarifas no se filtra ni se ve el resultado

CRITERIOS DE ACEPTACION
- "Vigente" solo muestra solicitudes con Deadline no vencida o sin Deadline; "vencida" solo las vencidas
- Una solicitud con todas las lineas revisadas y sin Entregada aparece en "Pendientes" y tambien en "Atendidas por el Administrador de Fletes"; al pasar a Entregada deja de salir en Pendientes
- Una solicitud auto-costeada no cambia de comportamiento
- Con rol Comercial los filtros corregidos funcionan igual, solo sobre sus propias solicitudes; los demas roles ven todas (el aislamiento ya existente no se rompe)
- Cada fila trae Resultado; resultado=esperando lista solo cotizaciones entregadas o auto-costeadas sin resultado (no Ganadas, Perdidas ni descartadas) aunque la solicitud este en estado Entregada; resultado=ganada y resultado=perdida listan solo esas
```
Origen: HU-B1, HU-B2, HU-B25 (2026-10-09_HU-BACKEND.md). HU-008.1 (backlog Cotifly) ya esta cubierta por #199 / S4-HU-19.1.

### [S5-HU-23.2] QA: verificacion de la Bandeja
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `qa` · Tipo: Task
Asignado a: @kcano (QA)
```
Parte de S5-HU-23 (milestone). Depende de S5-HU-23.1.

CHECKLIST
- [ ] Solicitud con Deadline en el pasado: aparece en "vencida", no en "vigente"; capturada hace 10 dias con Deadline futura: aparece "vigente"
- [ ] Solicitud sin Deadline: aparece "vigente"
- [ ] Todas las lineas revisadas por Admin de Fletes sin entregar: sale en "Pendientes" y en "Atendidas por el Administrador de Fletes"; tras Entregar sale de "Pendientes"
- [ ] Solicitud auto-costeada (Atencion Automatico): sin cambios
- [ ] Resultado: marcar una cotizacion Ganada y otra Perdida; la columna Resultado las distingue; el filtro "Esperando resultado" lista las entregadas sin marcar (y las auto-costeadas) y no las cerradas ni las descartadas; con Atencion por defecto no se oculta nada que el filtro deba mostrar; el Comercial solo ve lo suyo
- [ ] Con 2 usuarios Comercial distintos: cada uno ve solo las suyas (regresion del aislamiento existente) y los filtros "vigente/vencida" y "Pendientes" corregidos tambien funcionan en la bandeja del Comercial; con AdminTarifas/Admin ven todas

CRITERIOS DE ACEPTACION
- Aprobacion explicita de QA registrada en la issue
```

### [S5-HU-23.3] Bandeja frontend: columna Resultado y filtro Esperando resultado
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-23 (milestone). Depende de S5-HU-23.1 (campo resultado en la fila y parametro resultado del GET).

CONTEXTO
- Al marcar una cotizacion Ganada o Perdida la solicitud pasa a Entregada y sale de la vista por defecto "Pendientes"; la Bandeja no muestra el resultado. Decidido por Sara: columna Resultado y filtro "Esperando resultado"; NO mezclar en "Pendientes" (cola de trabajo del Administrador de Tarifas).
- Archivos: src/app/cabina-fletes/pages/bandeja/bandeja-page.component.ts / .html / .css (formulario de filtros: selects atencion, estado, vigencia). Referencia: HU-1 de la seccion Bandeja en docs/2026-10-09_HU-FRONTEND.md.

CHECKLIST
- [ ] NO copiar al repo real los datos de resultado simulados ni el aviso "datos simulados" del sandbox: el resultado real viene de S5-HU-23.1
- [ ] Mostrar el filtro y la columna Resultado solo con access.puede('marcarGanadaPerdida') (Comercial y Admin); AdminTarifas no ve cuales estan ganadas o perdidas (decidido por Sara)
- [ ] Select nuevo "Resultado" en el formulario de filtros, IGUAL a los de Atencion, Estado y Vigencia (mismo markup, estilo, ancho y posicion; formControlName="resultado"): Todos (defecto), Esperando resultado, Ganada, Perdida; se envia como parametro resultado al GET (vacio si es Todos)
- [ ] Al elegir un resultado distinto de Todos, el control de Atencion pasa a "Todas" automaticamente (si no, el defecto "Pendientes" oculta lo ya entregado)
- [ ] Columna nueva "Resultado" con .cabina-badge: "Sin definir" (neutro/ambar) si resultado es SinDefinir y ya hay cotizacion, "Ganada" (verde), "Perdida" (rojo), "—" si resultado es null
- (Descartado por Sara: chip o boton de acceso rapido; el filtro es solo el select)
- [ ] Modelos: item de bandeja con resultado?: 'SinDefinir' | 'Ganada' | 'Perdida' | null; filtros con resultado
- [ ] Actualizar el texto del tooltip del titulo (lo menciona el filtro de resultado y aclara que "Pendientes" no incluye lo ya entregado)
- [ ] Sin cambios de orden ni paginacion

CRITERIOS DE ACEPTACION
- El filtro Resultado existe con las 4 opciones y filtra la tabla
- Elegir un resultado distinto de Todos pone Atencion en Todas
- La columna Resultado muestra el badge correcto en cada fila
- El filtro Resultado se ve y se comporta igual que los otros filtros seleccionables
- El tooltip del titulo menciona el filtro de resultado
```
Origen: HU-1 de Bandeja (2026-10-09_HU-FRONTEND.md) y HU-B25.


---

## Milestone S5-HU-24 — Frontend: especificaciones validadas en el sandbox (Mesa de Costeo, Biblioteca, Tarifas Plaza, Carga masiva, iconos)

Todo lo de este milestone esta en `administracion_embarqa_nexo` y ya fue prototipado y validado en el sandbox (no esta hecho en el repo real). Cada issue es la lista para implementarlo de verdad. El codigo de referencia esta en `docs/2026-10-09_HU-FRONTEND.md` (cada issue dice su "Origen"); el sandbox solo sirve de modelo visual.

### [S5-HU-24.1] Mesa de Costeo: comparacion de 3 fuentes, chips de cada linea, avance, aviso y resumen completo
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). Depende de S5-HU-20.1 solo para los 3 chips de clasificacion (punto 4). HACER ANTES de S5-HU-20.2 y S5-HU-20.3: esas issues usan lineasCosteadas(), el icono de aviso y las 3 cajas que se crean aqui.

CONTEXTO
- Archivos: src/app/cabina-fletes/pages/mesa-costeo/mesa-costeo-page.component.ts / .html / .css.
- 8 mejoras de la Mesa de Costeo validadas en el sandbox (no estan en el repo real). Codigo de referencia completo en 2026-10-09_HU-FRONTEND.md, Mesa de Costeo HU-1 a HU-8.

CHECKLIST
1. Una sola comparacion de 3 fuentes (HU-1)
- [ ] Causa: existia una caja aparte .mesa-rate-box "Piso SICE-TAC + tiempos logisticos" calculada en el front con floorTotal(piso, tiempos), que usaba SIEMPRE costeo.tiempos (horas de la solicitud) y nunca las horas de la linea. El backend si prioriza las de la linea (HorasEfectivasDeLinea en clsPropuestaTarifaAdapter.cs), asi que la caja podia mostrar otro numero, y como floorTotal() prellenaba valorFlete, el flete guardado podia quedar mal
- [ ] Eliminar la caja aparte, floorTotal(), horasTotalesDeLinea() y el import PisoSicetacDto (quedan sin uso)
- [ ] candidateSourceLabel(value): 'sicetac' -> "Piso SICE-TAC + tiempos logisticos", 'plaza' -> "Tarifario", otro -> "Historico SAP" (20.2 le agrega luego "(NEXO)" segun la tabla de origen)
- [ ] El formulario prellena desde el candidato ya calculado por el backend: valorFlete: [lineaPropuesta?.sicetac?.valor ?? null, [Validators.required, Validators.min(0.01)]], fuente: [lineaPropuesta?.sicetac ? 'sicetac' : 'manual', Validators.required]
- [ ] Caja "Fuente propuesta" debajo de las 3 cajas con precio, fuente y boton "Usar propuesta" juntos en 3 columnas (.mesa-rate-box__content / __precio / __fuente); el boton solo si la linea aun no tiene flete
- [ ] Clase mesa-rate-box--success (verde) si hay propuesta o flete guardado; mesa-rate-box--empty (rojo claro) si no hay ninguno
- [ ] Si ya hay flete guardado (de cualquier fuente: manual, SICE-TAC, tarifario, historica), la caja muestra ESE valor y fuente, no la propuesta original
2. Ocultar "Valor estimado" (HU-2)
- [ ] Quitar <div><dt>Valor estimado</dt>...</div> del resumen de la solicitud (html:65). No tocar backend (clsScorePrioridadCalculator.cs sigue usando ValorEstimado; tema aparte)
3. Tiempos logisticos por linea (HU-3)
- [ ] Chip en .mesa-data-chips (junto a Tipologia del vehiculo/Carroceria/Peso/Tipologia de carga): "Tiempos logisticos X h cargue + Y h descargue"
- [ ] Metodo nuevo tiemposDeLinea(linea, tiemposSolicitud): usa linea.horasCargue/horasDescargue SOLO si las dos no son null; si falta cualquiera, devuelve completos los de costeo.tiempos (nunca mezcla una de cada lado). Mismo criterio que HorasEfectivasDeLinea del backend
- [ ] Cuando hereda de la solicitud agrega "(de la solicitud)". Solo lectura
4. Clasificacion de la linea (HU-4) — necesita los 3 campos del DTO de costeo (ya en NEXO, falta desplegar; ver 20.1)
- [ ] 3 chips en el mismo bloque: "Linea de negocio" (entry.linea.lineaNegocio), "Clase de viaje" (claseViaje), "Clase de contenedor" (claseContenedor), cada uno con *ngIf: si es nulo no aparece (contenedor solo trae valor cuando la clase de viaje es Contenedor)
5. Numerar lineas (HU-5)
- [ ] <p class="cabina-eyebrow">Linea {{ i + 1 }}</p> en el encabezado de cada linea, con el indice i del *ngFor (el mismo del resumen completo)
6. Indicador de avance (HU-6)
- [ ] Seccion .mesa-avance debajo del mensaje de exito, con [ngClass] mesa-avance--completo / mesa-avance--pendiente
- [ ] Pendiente: "X de Y linea(s) costeadas" en rojo claro. Completo: "Costeo terminado" en verde, sin badge ni icono adentro
- [ ] Metodo nuevo lineasCosteadas(lineas): cuenta lineas con flete real guardado. NO reusar lineasSinFlete() (tiene otro proposito)
- [ ] Solo aparece si ya existe cotizacion y la solicitud tiene al menos 1 linea. Convive con el boton "Aplicar todas las propuestas" (20.2)
7. Aviso "pendiente por enviar al comercial" (HU-7; 20.3 le agrega el texto visible y arregla el tamano del tooltip)
- [ ] Envolver "Entregar a Comercial" en <span class="mesa-header-actions__entregar"> y agregar <mat-icon class="mesa-header-actions__aviso">warning_amber</mat-icon>
- [ ] Visible solo si: cotizacion existe && estado !== 'Entregada' && vm.lineas.length > 0 && lineasCosteadas(vm.lineas) === vm.lineas.length
- [ ] matTooltip="Pendiente por enviar al comercial. Envia para que le aparezca en su bandeja." matTooltipClass="cabina-tooltip" matTooltipPosition="right" (mismo patron que Bandeja, no un tooltip propio)
- [ ] CSS: .mesa-header-actions__aviso { display:inline-flex; align-items:center; justify-content:center; width:28px; height:28px; border-radius:50%; background:#fef3e2; color:#c62828; font-size:16px; cursor:help; }
8. Ver resumen completo (HU-8)
- [ ] Card propia <section class="cabina-card mesa-resumen-completo-card">, hermana de "Consolidado de tarifa" (NO anidada adentro)
- [ ] Boton toggleResumenCompleto(): "Ver resumen completo" / "Ocultar resumen completo"; mostrarResumenCompleto arranca en false
- [ ] Lista .mesa-resumen-completo con TODAS las lineas: "Linea N: origen -> destino, flete, fuente" o "Linea N: origen -> destino - sin flete todavia"; N con el mismo indice de la HU-5

CRITERIOS DE ACEPTACION
- "Propuesta de la Biblioteca" muestra exactamente 3 cajas (Piso SICE-TAC + tiempos logisticos, Tarifario, Historico SAP); nunca una 4a caja de piso suelto
- El valor del piso coincide siempre con el del backend (horas de la linea o, si falta alguna, las de la solicitud); el front no calcula nada aparte
- "Fuente propuesta" verde/roja segun corresponda y con el flete real cuando ya hay uno
- No aparece "Valor estimado"
- Cada linea muestra "Linea N", sus tiempos logisticos (con "(de la solicitud)" cuando hereda) y los chips de clasificacion que tengan valor
- Avance en rojo claro mientras falten lineas y "Costeo terminado" en verde al completar
- El icono de aviso aparece solo con todo costeado y sin entregar, y desaparece al entregar
- El resumen completo arranca colapsado, cubre todas las lineas y usa la misma numeracion
```
Origen: HU-1 a HU-8 de Mesa de Costeo (2026-10-09_HU-FRONTEND.md). HU-9 a HU-20 estan en las issues 20.2 y 20.3.

### [S5-HU-24.2] Biblioteca: semaforo, orden y columna de vigencia, paginacion de Historico SAP, Almacenamiento e ITR
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend`, `bug` · Tipo: Bug
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone).

CONTEXTO
- Archivos: src/app/cabina-fletes/pages/biblioteca/biblioteca-page.component.ts / .html / .css. 8 ajustes validados en el sandbox; codigo de referencia completo en 2026-10-09_HU-FRONTEND.md, Biblioteca HU-1 a HU-9. El icono de informacion (HU-6) va en 24.5.
- Ninguno necesita backend. Ninguno toca el modulo Tarifas Plaza.

CHECKLIST
1. Semaforo de vigencia de "Tarifas de plaza" siempre dice "Vigente" (HU-1, bug)
- [ ] Causa: semaforo() se copio de Tarifas Plaza y concatena `T00:00:00` a la fecha. En Biblioteca la fecha llega con hora (clsSapTarifaHistoricoItemDto, DateTime: "2026-10-08T00:00:00"), queda "...T00:00:00T00:00:00", new Date() da invalido y la funcion cae en su fallback return 'vigente'
- [ ] Arreglo: const fecha = new Date(vigencia.includes('T') ? vigencia : `${vigencia}T00:00:00`);
- [ ] NO tocar el semaforo() de Tarifas Plaza (su DTO es DateOnly, sin hora; ahi la concatenacion es correcta y evita el corrimiento de un dia por UTC-5)
2. Ordenar "Tarifas de plaza" por vigencia (HU-2)
- [ ] const ORDEN_SEVERIDAD = { vencida: 0, por_vencer: 1, vigente: 2 } (mismo de tarifas-plaza-page.component.ts:22)
- [ ] plazaItemsOrdenados(): copia de plazaResultado.items ordenada por ORDEN_SEVERIDAD[semaforo(t.vigenciaAjustada || t.vigencia)]; el *ngFor de la tabla itera plazaItemsOrdenados() en vez de plazaResultado?.items
- [ ] Ordena SOLO la pagina ya cargada (Biblioteca pagina en el servidor). NO pedir ORDER BY al backend (decision de Sara)
3. Separar la columna "Vigencia" de la fecha (HU-3)
- [ ] Encabezado: <th>Vigencia</th><th></th><th>Origen</th>... (columna nueva sin titulo)
- [ ] Primer <td>: solo el badge de estado. Segundo <td>: fecha + chip "Ajustada" + botones "Ajustar"/"Quitar" (mismos *ngIf y permisos de hoy, access.puede('ajustarVigencia'))
- [ ] No hay colspan que ajustar (el mensaje vacio biblioteca-vacio es un <p> fuera de la tabla)
4. Paginacion en "Historico SAP" (HU-4), espejo exacto de "Tarifas de plaza"
- [ ] Estado: sapPagina = 1; sapTamanoPagina = TAM_PAGINA_PLAZA_DEFAULT
- [ ] get totalPaginasSap(): Math.max(1, Math.ceil((this.sapResultado?.total ?? 0) / this.sapTamanoPagina))
- [ ] cambiarTamanoPaginaSap(event) e irAPaginaSap(pagina), espejo de los de plaza
- [ ] buscarSap() pone sapPagina = 1 y delega en un nuevo private cargarSap(), que hace la llamada real agregando top: this.sapTamanoPagina, page: this.sapPagina a los filtros (FiltrosSapHistorico ya acepta top/page)
- [ ] reintentarSap() llama cargarSap() directo (no reinicia la pagina)
- [ ] HTML: mismo bloque de paginacion de plaza renombrado a sap* (Anterior/Siguiente, selector 25/50/100/200, "X tarifa(s) · pagina N de M"), antes de la tabla y despues del loading/error
5. Quitar "(mostrando las primeras X)" en las 3 pestañas (HU-5)
- [ ] Quitar el <ng-container> del parentesis en "Tarifas de plaza", "Historico SAP" y "SICE-TAC". Quedan: "{{ total }} tarifas · Fuente: dbsolucionesTI · tblSAPTarifas (solo lectura) · la vigencia es ajustable en Nexo (override local)"; "{{ total }} tarifas · Fuente: dbsolucionesTI · tblSAPTarifasLog (solo lectura)"; "Periodo <strong>...</strong> · {{ total }} tarifas encontradas · Fuente: DWSice · tblRutasSicetac (solo lectura)"
6. Unidad editable en "Almacenamiento" (HU-7)
- [ ] Para tipoMiniVista === 'almacenamiento': <select> con exactamente "Por estiba" y "Por unidad" ((change) asigna servicioForm.unidad; [selected] segun el valor)
- [ ] El input deshabilitado de Unidad pasa a mostrarse con !== 'valor_unico' && !== 'almacenamiento' (ITR, por ruta, por origen/destino siguen bloqueados igual)
- [ ] UNIDAD_FIJA.almacenamiento = 'Por estiba' se mantiene como valor por defecto
7. Codigo de "Almacenamiento" en mayuscula inicial (HU-8)
- [ ] CODIGO_SERVICIO_FIJO.almacenamiento: 'ALMACENAMIENTO' -> 'Almacenamiento' (ts:74-77). itr: 'ITR' no cambia
8. ITR manual: "Maquina" y "Movimiento" como desplegables (HU-9)
- [ ] No hay columna nueva: TipoMaquina (clsServicioConexo.cs) sigue siendo UN texto combinado, como en los datos reales ("Maquina 3.5 - 1 Movimiento", "Maquina 7.5 - 2 Movimientos", "Manual - 1 Movimiento")
- [ ] Input de texto de maquina (html:499) -> <select> con "— seleccionar —" (vacio), "Manual", "Maquina 3.5", "Maquina 7.5"
- [ ] servicioForm.movimientosMaquina: '1' | '2' (default '1') y <select> "Movimiento" con 1 y 2; servicioForm.tipoMaquina guarda solo la maquina
- [ ] guardarServicio(): combinarTipoMaquina(maquina, n) -> "{maquina} - 1 Movimiento" (singular) o "{maquina} - 2 Movimientos" (plural); separador siempre " - "
- [ ] abrirEditarServicio(): partirTipoMaquina() con /^(.*) - (\d+) Movimientos?$/; si un registro viejo no calza, pone el texto completo en maquina y "1" en movimiento (no rompe)
- [ ] La carga masiva de Excel de Servicios NO cambia (sigue esperando el texto combinado en una columna)

CRITERIOS DE ACEPTACION
- Una tarifa con vigencia anterior a hoy muestra "Vencida" en rojo en Biblioteca; Tarifas Plaza sigue igual
- La pagina cargada se ve ordenada vencida -> por vencer -> vigente, usando vigenciaAjustada si existe
- Badge y fecha en columnas separadas y alineadas en todas las filas
- Historico SAP pagina con los mismos controles que Tarifas de plaza; cambiar un filtro vuelve a la pagina 1
- Ninguna de las 3 pestañas dice "(mostrando las primeras X)"
- Almacenamiento permite elegir Por estiba / Por unidad (por defecto Por estiba); los demas tipos no cambian
- Un servicio Almacenamiento nuevo muestra el codigo "Almacenamiento"
- ITR sin texto libre: se guarda p. ej. "Maquina 3.5 - 2 Movimientos" y al editar cada desplegable vuelve con su valor
```
Origen: HU-1 a HU-5, HU-7, HU-8, HU-9 de Biblioteca (2026-10-09_HU-FRONTEND.md).

### [S5-HU-24.3] Carga masiva de Excel: vista previa editable en Servicios de Biblioteca y Tarifas Plaza
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). Hacer ANTES de S5-HU-22.2 (que agrega a esta misma vista previa el departamento de ciudades repetidas).

CONTEXTO
- Decidido (2026-10-09): 100% frontend, sin endpoints nuevos. El Excel se parsea en el navegador (libreria xlsx), se muestra en una grilla editable y al confirmar se reconstruye el Excel con lo editado y se manda al mismo importar-excel de siempre.
- Hoy: Servicios valida en el navegador (servicios-excel.util.ts) pero bloquea TODO el archivo si una fila falla, o importa directo sin pausa si no hay errores. Tarifas Plaza manda el archivo crudo (tarifas-plaza-page.component.ts:183-190) y toda la validacion vive en clsEfTarifasPlazaRepository.cs.
- Estado final confirmado en pantalla por Sara el 2026-10-12: ver "Estado final" en 2026-10-09_HU-FRONTEND.md (seccion Carga masiva). Codigo de referencia en HU-1 a HU-8 de esa seccion.

CHECKLIST
A) Compartido
- [ ] Archivo nuevo src/app/cabina-fletes/util/excel-lectura.util.ts con los helpers genericos (cargarXlsx, normalizar, iguales, parsearImporte, parsearFecha, leerCelda —tipo nativo primero, texto de respaldo—, celdasConDatos, escribirLibro); servicios-excel.util.ts los importa de ahi (no duplicar)
- [ ] Al subir: nunca se guarda; siempre abre un modal con grilla de TODAS las filas (patron .biblioteca-modal-overlay/.biblioteca-modal con variante ancha), un control por celda, fila en rojo con sus mensajes en la ultima columna
- [ ] Revalida en vivo cada edicion; "Confirmar carga" deshabilitado mientras quede alguna fila en rojo
- [ ] Al confirmar: reconstruye el Excel con los valores editados y llama al importarExcel() de siempre
- [ ] trackBy: trackByIndice en el *ngFor de filas de AMBAS grillas (sin trackBy cada tecla recrea el <tr>, se pierde el foco y solo entra una letra)
- [ ] Ocultar filas existentes sin error (HU-4): campo esNueva en cada fila (calculado en revalidarLectura()/revalidarLecturaPlaza() con la busqueda "¿ya existe esta clave?"); <ng-container *ngFor> + <tr *ngIf="fila.esNueva || fila.errores.length > 0"> para conservar el indice real; arriba de la tabla "{{ ocultas }} fila(s) sin cambios, ya aplicada(s) sin necesidad de revision."; las ocultas SI se guardan (el guardado filtra solo por errores.length === 0)
- [ ] Los <select> de las grillas con el mismo estilo de la pagina (agregar select a la regla de .biblioteca-preview-tabla-wrap en los dos componentes)
B) Servicios (Biblioteca)
- [ ] leerServiciosDesdeExcel() deja de descartar las filas con error: todas quedan en lectura.filas con su errores: string[]
- [ ] Auto-correccion de columnas fijas (HU-2): Codigo, Tipo de servicio, Unidad y Categoria se resuelven como config.tipoServicioFijo/unidadFija ?? textoDelExcel desde el inicio de validarFila(); quitar los 4 errores "debe ser «X»"; "Falta X" solo si no hay valor fijo y vino vacio; Categoria siempre forzada
- [ ] validarValoresFila(tipo, categoriaEsperada, incluirActivo, valoresOriginales) devuelve { valores, errores } (fuerza los campos, no solo valida); revalidarLectura() usa los valores corregidos y suma duplicado-en-lote + choque con catalogo + esNueva; eliminar detectarChoquesConCatalogo()
- [ ] Bug a no repetir: la clave de duplicados y el chequeo de longitud maxima (textosConLimite) usan el codigo YA corregido, no el crudo
- [ ] Controles (HU-3): Codigo/Tipo de servicio/Categoria <input disabled readonly>; Unidad <select> (Por estiba/Por unidad); Activo <select> SI/NO (la grilla guarda 'SI'/'NO'); Vigencia <input type="date">; Origen/Destino con el buscador de ciudades del formulario manual (buscarCiudadPreview()/seleccionarCiudadPreview(), reusando origenQuery$/destinoQuery$); Costo unitario con separador de miles (formatMilesPreview()/onCostoPreviewInput(), toLocaleString('es-CO'), guarda el numero limpio)
C) Tarifas Plaza
- [ ] Archivo nuevo tarifas-plaza-excel.util.ts, espejo de clsEfTarifasPlazaRepository.ImportarExcelAsync: obligatorias LINEA DE NEGOCIO, CLASE DE VIAJE, ORIGEN, DESTINO, VALOR, VIGENCIA, DETALLES; opcionales CLASE CONTENEDOR, RANGO PESO, TIPO VEHICULO; encabezado por texto EXACTO (no "contiene"); regla "al menos rango de peso o tipo de vehiculo"
- [ ] Duplicados dentro del archivo NO son error (el backend los fusiona, gana la ultima fila)
- [ ] Catalogos: cargarCatalogos() devuelve un Observable y importarExcel() lo encadena ANTES de armar la vista previa (si no, entrando directo a "Importar Excel" los desplegables salen vacios). Fuentes: /api/catalogos/municipios, /api/rangos-peso, /api/catalogos/lineas-negocio, /api/catalogos/clases-viaje, clases de contenedor y tipos de vehiculo (ya cargados por el formulario manual)
- [ ] validarValoresFilaPlaza() devuelve { valores, errores } y CANONIZA: con match normalizado (iguales(), sin tildes ni mayusculas) reemplaza el valor por la forma exacta del catalogo, igual que hace el backend al guardar. Aplica a Linea de negocio, Clase de viaje, Origen, Destino, Rango de peso, Clase de contenedor y Tipo de vehiculo
- [ ] Rango de peso: formato "min-max" / "min+"; ignora espacios ("3 - 15000" = "3-15000") pero los numeros deben coincidir exacto con el catalogo
- [ ] Origen/Destino: guardar SOLO el nombre del municipio (no "{municipio} · {departamento}" del formulario manual; si no, todas las filas darian "no es un municipio del catalogo"); desempate Colombia primero si el nombre choca entre paises; metodos propios buscarMunicipioPreviewPlaza()/seleccionarMunicipioPreviewPlaza() y celdaCiudadActivaPreviewPlaza
- [ ] Controles: Linea de negocio, Clase de viaje, Rango de peso (opciones formateadas, ej. "3 - 15000 kg"), Clase de contenedor ("— ninguno —" + catalogo) y Tipo de vehiculo ("— ninguno —" + catalogo) como <select> cerrados; Vigencia <input type="date">; Valor y Detalles texto
- [ ] Tipo de vehiculo: <option [value]="tv.codigo">{{ tv.codigo }} · {{ tv.descripcion }}</option> (ej. "3S3 · Tractomula", como en Reglas Solo-SICETAC); el valor guardado sigue siendo el codigo
- [ ] Que marca error: Linea de negocio, Clase de viaje, Origen, Destino y Clase de contenedor si no coinciden con el catalogo (Clase de contenedor es mas estricta que el backend a proposito, decision de Sara). Tipo de vehiculo NUNCA marca error (el catalogo de SAP esta incompleto: faltan 3S2, 2L3...; decidido no hardcodear)
- [ ] Bug de preseleccion de los <select>: dibujar la fila con los 5 select en blanco y, con un setTimeout, poner un instante despues los valores ya canonizados (Angular no reaplica [value] si el dato no cambio y la primera vez las <option> asincronas aun no estaban). Parpadeo breve aceptado por Sara
- [ ] Filtro de filas ocultas contarFilasOcultasPreviewPlaza y estilos del modal copiados en tarifas-plaza-page.component.css (no se comparten entre componentes)
- [ ] El formulario manual "Agregar tarifa" NO cambia (sigue con datalist en Clase de contenedor y Tipo de vehiculo)

CRITERIOS DE ACEPTACION
- Subir un Excel nunca guarda de inmediato; solo al confirmar, y con los valores editados
- Se ven todas las filas nuevas y las existentes con error; las existentes sin error se ocultan con su conteo y se guardan igual
- Las filas con error se ven en rojo con el motivo y no se puede confirmar mientras haya alguna
- Se puede escribir normal en cualquier celda (no se pierde el foco)
- En Servicios, Codigo/Tipo/Unidad/Categoria fijos se corrigen solos y no marcan error; dos filas con el mismo codigo corregido y mismas dimensiones se marcan duplicadas
- En Tarifas Plaza los desplegables salen preseleccionados con el valor del Excel (canonizado) y se ve lo que el backend guardaria
- Tipo de vehiculo muestra "codigo · descripcion" y no marca error; Clase de contenedor si
- Mismo endpoint importar-excel de siempre; ningun endpoint nuevo
- Riesgo aceptado: el algoritmo de validacion queda duplicado entre front y back; si se cambia en clsEfTarifasPlazaRepository.cs hay que replicarlo aqui
- Pendiente externo (no bloquea): codigos faltantes en el catalogo de SAP (3S2, 2L3...) dependen de actualizar dbSolucionesTI; mientras tanto no se pueden elegir en el desplegable
```
Origen: HU-1 a HU-8 de Carga masiva de Excel (2026-10-09_HU-FRONTEND.md). HU-9 (departamento) esta en S5-HU-22.2.

### [S5-HU-24.4] Tarifas Plaza: mostrar el detalle de errores de la importacion y editar la linea rechazada
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). Depende de S5-HU-24.3 (la vista previa editable reduce los errores que llegan hasta aqui).

CONTEXTO
- tarifas-plaza-page.component.ts (lineas 216-217): el backend ya devuelve resultado.errores (IReadOnlyList<string> en clsImportarTarifasPlazaResultDto, detalle por fila, ej. "fila 109: '46315' no es una fecha de vigencia valida") pero la pantalla solo pinta "N rechazada(s)".
- Con la vista previa editable (la vista previa de la parte 1) casi todos los errores se corrigen antes de importar; esta issue cubre lo que el backend aun rechace al importar.

CHECKLIST
- [ ] Cuando rechazadas > 0, mostrar el detalle de resultado.errores (en la misma notificacion o en un detalle expandible), conservando el conteo "N rechazada(s)"
- [ ] Permitir editar una linea rechazada directo en pantalla y reintentar solo esa linea, sin reimportar el Excel completo (depende del punto anterior: hay que saber que campo fallo)
- [ ] Al guardar la correccion, validar con el mismo criterio del importador (catalogo de municipios, rangos de peso, linea de negocio, clase de viaje); se puede reusar la validacion de tarifas-plaza-excel.util.ts de S5-HU-24.3

CRITERIOS DE ACEPTACION
- Con rechazos, el usuario ve que fila fallo, que dato y por que, ademas del conteo
- Una linea rechazada se corrige en pantalla sin volver al Excel
```
Origen: HU-1 y HU-2 de Tarifas Plaza (2026-10-09_HU-FRONTEND.md). La 2 depende de la 1.

### [S5-HU-24.5] Iconos de informacion en Biblioteca, Tarifas Plaza, Reglas Solo-SICETAC, Cubicador y Tablero
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). El tamano de letra del tooltip fuera de Bandeja se arregla en S5-HU-20.3 (punto 8: mover .cabina-tooltip a src/styles.css); hacer esa parte primero o junto con esta.

CONTEXTO
- La Bandeja muestra la explicacion de la vista en un icono de informacion con tooltip (bandeja-page.component.html:4-16). Estas 5 vistas aun tienen un <p> descriptivo fijo debajo del titulo. Decidido: mismo patron en todas, con el MISMO texto exacto (no se resume ni se corrige).

PATRON (igual en las 5)
<h1>
  {Titulo}
  <button type="button" class="cabina-info-btn" matTooltip="{texto}" matTooltipClass="cabina-tooltip" matTooltipPosition="right" aria-label="Más información sobre esta vista">
    <mat-icon inline="true">info_outline</mat-icon>
  </button>
</h1>

CHECKLIST
- [ ] Biblioteca (biblioteca-page.component.html:1-16): titulo "Biblioteca"; texto "Matriz oficial SICE-TAC, tarifas de plaza e histórico SAP TM (en vivo, solo lectura) y el catálogo de servicios logísticos y adicionales."
- [ ] Tarifas Plaza (tarifas-plaza-page.component.html:2-7): titulo "Tarifas de plaza"; texto "Tarifas de plaza gobernadas por Nexo, con carga y descarga masiva por Excel."; quitar el <div> envolvente (no tiene otro uso)
- [ ] Reglas Solo-SICETAC (reglas-solo-sicetac-page.component.html:2-5): titulo "Rutas solo SICE"; texto "Demarca combinaciones de origen, destino y tipo de vehículo que se cotizan EXCLUSIVAMENTE con el piso SICE-TAC (sin comparar con plaza ni tarifario). Las solicitudes que hagan match se auto-costean y quedan atendidas para el comercial."
- [ ] Cubicador (cubicador-page.component.html:2-5): titulo "Cubicador 3D"; texto "Plan de cargue: registra el vehículo y la carga, optimiza la distribución y revisa la gráfica 3D interactiva."
- [ ] Tablero (tablero-page.component.html:2-5): titulo "Tablero de Estado"; texto "Cumplimiento de SLA y estado de la cola." (se mueve tal cual; actualizarlo para mencionar los otros bloques seria un pedido aparte)
- [ ] En cada componente: quitar el <p> fijo; copiar el bloque CSS .cabina-info-btn de Bandeja (los estilos son por componente en Angular): .cabina-info-btn {...}, .cabina-info-btn mat-icon { font-size:20px; width:20px; height:20px; }, .cabina-page-header h1 .cabina-info-btn { margin-top:4px; }, hover/focus-visible (background #cfe3f5; color #00478f; outline 2px solid #0055b8)
- [ ] Agregar display:flex; align-items:center; gap:8px a .cabina-page-header h1 y borrar .cabina-page-header p si queda huerfana
- [ ] Verificar que MatTooltipModule y MatIconModule esten importados en cada componente/modulo

CRITERIOS DE ACEPTACION
- Ninguna de las 5 vistas tiene el parrafo descriptivo fijo bajo el titulo
- Junto a cada titulo hay un icono info_outline que al pasar el mouse muestra el mismo texto exacto que se quito, con el mismo estilo y posicion que Bandeja
- El icono queda alineado con el titulo
```
Origen: HU-6 de Biblioteca, HU-3 de Tarifas Plaza, HU-2 de Reglas Solo-SICETAC, HU-1 de Cubicador, HU-1 de Tablero.

### [S5-HU-24.6] Mesa de Costeo: agregar servicios conexos paso a paso
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). Sin backend. Ya prototipado y validado (compilacion + simulacion contra el catalogo real) en el sandbox; falta la prueba visual de Sara. Las 3 decisiones de negocio ya estan tomadas (ver "Decidido por Sara").

CONTEXTO
- Hoy el selector de conexos (components/servicio-conexo-selector/, usado 2 veces en mesa-costeo-page.component.html: por linea y general) es un buscador de texto libre sobre 1.336 servicios activos.
- Pedido de Sara: elegir por pasos. Primero el tipo; luego los desplegables propios de cada tipo, uno por uno; al final se ven precio, unidad y subtotal.
- Contrato del componente SIN cambios: @Input catalogo, agregando, bloqueado, motivoBloqueo, resultado; @Output agregar, reintentarCatalogo. mesa-costeo-page no se toca.

TABLA DE GRUPOS Y PASOS (etiqueta -> columna del catalogo)
- Almacenamiento (TipoServicio "Almacenamiento"): 1 Ciudad -> origen
- ITR (TipoServicio "ITR"): 1 Ciudad -> origen; 2 Puerto -> puerto; 3 Tipo de maquina -> tipoMaquina
- Sellos (Precintos) (TipoServicio que contenga "precinto"; hoy "Precinto (Sello) Barra Satelital"): 1 Tipo de precinto -> tipoServicio; 2 Ruta -> ruta
- Acompanamiento (TipoServicio que contenga "acompanante"; hoy 5 valores: total en auto, total en moto, total en moto armada, parcial en moto, parcial en moto armada): 1 Tipo de acompanante -> tipoServicio; 2 Origen -> origen; 3 Destino -> destino
- Los 8 TipoServicio reales encajan (1+1+1+5). No existe "parcial en auto".

CHECKLIST
- [ ] Archivo nuevo servicio-conexo-pasos.util.ts: GRUPOS_CONEXO (4 grupos, cada uno con etiqueta, coincide(tipoServicio) con normalizarBusqueda y lista de pasos {campo, etiqueta}), ORDEN_GRUPOS_CONEXO, grupoDeTipoServicio(), gruposDisponibles(activos) (solo grupos con algun servicio activo), opcionesDelPaso(activos, grupo, valores, indice), servicioDelGrupo() (devuelve el unico servicio activo cuando estan todos los pasos, o null si faltan pasos o no hay exactamente 1)
- [ ] opcionesDelPaso: valores distintos de la columna del paso entre los activos del grupo cuyos pasos anteriores sean IGUALES (===) a lo elegido; orden Intl.Collator('es', { sensitivity: 'base', numeric: true }) (Maquina 3.5 antes que Maquina 7.5)
- [ ] Componente .ts: estado modoBusqueda, grupos, grupoElegido, valoresPasos; metodos elegirGrupo(), elegirPaso(), opcionesPaso(), alternarModoBusqueda(); confirmar(), resumen, mensajes y puedeAgregar NO cambian (solo cambia como se llena seleccionado)
- [ ] .html: primer <select> "Tipo de servicio" y un <select> por paso, todos con <label [for]> (idBase + '-tipo' / idBase + '-paso-' + i) y primera opcion value="" con texto "— elegir —"
- [ ] Cambiar el tipo limpia valoresPasos y seleccionado; cambiar un paso limpia solo los siguientes; la Cantidad se conserva en ambos
- [ ] Un paso con una sola opcion se autoselecciona (decidido por Sara); el paso siguiente se habilita de inmediato
- [ ] Con todos los pasos elegidos y 0 o 2+ coincidencias: aviso rojo "Esta combinacion no tiene un servicio activo. Revisa los pasos elegidos." y sin resumen/boton
- [ ] Tras agregar con exito: reiniciar() limpia grupo, pasos, seleccionado y deja Cantidad en 1
- [ ] Enlace "Buscar por texto" (en modo pasos) / "Volver a elegir por pasos" (en modo texto) que conserva el buscador de texto de siempre; alternar CONSERVA el servicio elegido (decidido por Sara): de pasos a texto el servicio queda seleccionado en el buscador; de texto a pasos se reconstruyen grupo y pasos a partir del servicio
- [ ] CSS local al componente (nada global): .selector__select (como .selector__input), .selector__pasos (flex-wrap), .selector__paso (flex: 1 1 160px), .selector__enlace (link azul #0055b8), .selector__aviso--error
- [ ] Accesibilidad: <select> nativos con label, orden de tab Tipo -> pasos -> enlace -> Cantidad -> Agregar; evaluar agregar aria-live="polite" cuando cambian las opciones de un paso siguiente (hoy no esta)
- [ ] Verificar en Mesa de Costeo los 2 usos (selector por linea y general) y que el PDF y los totales no cambian

DECIDIDO POR SARA (2026-10-12)
- Un paso con una sola opcion se autoselecciona
- Tras agregar se reinicia todo (no se conserva el grupo)
- Alternar entre por pasos y por texto conserva la seleccion

CRITERIOS DE ACEPTACION
- El primer control es "Tipo de servicio", no un buscador libre; ofrece solo los grupos con servicios activos
- Cada tipo muestra solo sus desplegables, en el orden de la tabla, filtrados por lo elegido antes y ordenados alfabeticamente
- Recorridos de referencia (catalogo actual): Almacenamiento -> Barranquilla = $29.852,60 por estiba; ITR -> Barranquilla -> Todos -> "Manual - 2 Movimientos" = $1.760.869,50 por estiba; Sellos -> Precinto Barra Satelital -> "Colombia a Ecuador" = $401.824,92 por unidad; Acompanamiento -> parcial en moto -> Alto De La Tribuna -> BOGOTA, D.C. = $180.000,00 por unidad
- Un paso con una sola opcion queda autoseleccionado; alternar entre por pasos y por texto no pierde el servicio elegido
- Se ven precio, unidad y subtotal antes de agregar; cada recorrido resuelve a exactamente 1 servicio
- El servicio agregado, el resumen por linea, los totales y el PDF son los mismos de hoy
- Limitacion conocida: un TipoServicio nuevo que no encaje en ningun grupo queda invisible por pasos (sigue en "Buscar por texto"); para agregar un 5o grupo basta una entrada mas en GRUPOS_CONEXO
```
Origen: HU-21 de Mesa de Costeo (2026-10-09_HU-FRONTEND.md).

### [S5-HU-24.7] Mesa de Costeo: quitar conexos y permitir agregar/quitar con cotizacion entregada
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). Depende de S5-HU-20.1 (parte de conexos: endpoint DELETE, regla Ganada/Perdida y campos nuevos del DTO) y de S5-HU-24.6 (mismo selector y misma pantalla). NO copiar al repo real la botonera temporal "Simular" ni la simulacion local de quitar del sandbox.

CONTEXTO
- nexo-access.models.ts: gestionarServicios esta solo en Comercial, CoordinadorFletes y Admin; falta AdminTarifas (por eso no ve la seccion de servicios).
- mesa-costeo-page.component.ts: motivoBloqueoLinea() y motivoBloqueoGeneral() bloquean con MENSAJE_COTIZACION_ENTREGADA ("La cotizacion ya fue entregada: no admite mas servicios") cuando estado === 'Entregada'; el html tambien oculta ayudas con cotizacion.estado !== 'Entregada'.
- No hay forma de quitar un conexo agregado.
- Referencia completa: HU-22 y HU-23 en docs/2026-10-09_HU-FRONTEND.md.

CHECKLIST
Permiso y reglas (HU-23)
- [ ] Agregar 'gestionarServicios' a AdminTarifas en NEXO_MATRIZ_ACCESO y actualizar nexo-access.service.spec.ts
- [ ] CotizacionDto (cabina-api.models.ts): resultado y modificadaTrasEntregaAt (si aun no estan)
- [ ] motivoBloqueoLinea() y motivoBloqueoGeneral(): dejan de mirar Entregada; si resultado es Ganada o Perdida devuelven "La cotizacion ya esta marcada como Ganada/Perdida: no admite cambios de servicios." (constante nueva); borrar MENSAJE_COTIZACION_ENTREGADA
- [ ] Quitar las condiciones estado !== 'Entregada' de la ayuda y el aviso de "Servicios para todas las lineas"
- [ ] Con la cotizacion Entregada, nota informativa sobre los selectores: "Esta cotizacion ya fue entregada. Los cambios en servicios quedan registrados y, si los hace alguien distinto al comercial, se le avisa por correo." (no bloquea)
- [ ] Si modificadaTrasEntregaAt viene lleno, chip "Modificada despues de entregar" con la fecha
Quitar conexos (HU-22)
- [ ] cotizaciones-api.service.ts: quitarConexo(cotizacionId, conexoId) -> DELETE /cotizaciones/{id}/conexos/{conexoId}, devuelve CotizacionDto
- [ ] Boton "Quitar" al final de cada <li> de .mesa-linea-servicios__lista, con aria-label="Quitar {servicio} de la linea N"; visible solo con access.puede('gestionarServicios') y cotizacion no Ganada ni Perdida
- [ ] Confirmacion con el patron .mesa-modal-overlay/.mesa-modal (como "Cancelar pliego"): "¿Quitar «{servicio}» de la linea N? El total se recalculara." Botones "Cancelar" / "Quitar"
- [ ] Al confirmar: reemplazar this.cotizacion con la respuesta; el resumen por linea, el consolidado y el total se recalculan; reutilizar conexoPendiente para bloquear selectores mientras corre
- [ ] Mostrar errores con el patron del selector (404 CONEXO_NO_ENCONTRADO, 409 COTIZACION_CERRADA, otros)

CRITERIOS DE ACEPTACION
- AdminTarifas ve y usa la seccion de servicios (agregar y quitar) con la cotizacion pendiente y con la entregada
- Con la cotizacion Entregada los selectores y Quitar funcionan y se ve la nota informativa
- Con la cotizacion Ganada o Perdida todo queda bloqueado con el mensaje nuevo
- Cada conexo tiene Quitar con confirmacion; al confirmar desaparece de la lista y del Consolidado de tarifa (baja el conteo y monto de Servicios Logisticos o Adicionales segun su categoria y baja el Total); el resumen completo y el PDF dejan de incluirlo
- No queda ningun texto "no admite mas servicios" por estar Entregada
```
Origen: HU-22 y HU-23 (2026-10-09_HU-FRONTEND.md).

### [S5-HU-24.8] Captura: no autoseleccionar al escribir y campos obligatorios
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `frontend` · Tipo: Task
Asignado a: @jpvelez-bts (Frontend)
```
Parte de S5-HU-24 (milestone). Solo frontend.

CONTEXTO
- En Captura, al escribir en Origen, Destino o Cliente, si la busqueda devuelve exactamente 1 resultado el campo se autoselecciona solo (ej. escribir "MEDELLIN" deja la ciudad elegida sin mostrar la opcion). Sara quiere que al escribir a mano nunca se autoseleccione: la unica opcion se muestra en la lista y el usuario la elige. La autoseleccion solo debe ocurrir al cargar un Excel.
- Codigo en captura-page.component.ts: cliente en ngOnInit (~404), origen y destino en createLinea (~1111 y ~1118). Referencia: HU-1 de la seccion Captura en docs/2026-10-09_HU-FRONTEND.md.

CHECKLIST
- [ ] Cliente: quitar la autoseleccion (el Excel nunca llena el cliente); siempre this.clientes = clientes para que la unica opcion se vea en la lista
- [ ] Origen y Destino: la autoseleccion con 1 resultado solo si el valor lo puso aplicarExcelALineas() (marca por linea y campo, por ejemplo un Set de grupo:origen / grupo:destino, llenada antes del patchValue y consumida y limpiada por el suscriptor aunque haya 0 o 2+ resultados; un booleano temporal no sirve por el debounce de 400 ms y la llamada HTTP)
- [ ] Cubrir tambien el modo matriz de Excel (autoMapearMatrizExcel / recalcularLineasMatrizExcel) si crea lineas por otro camino
- [ ] Revisar ciudad de devolucion del contenedor y ciudad de levante: mismo criterio si tienen el mismo patron
- [ ] Verificar en el html que la lista de sugerencias se pinte tambien con 1 solo elemento
Vista previa de Excel (HU-3 de Captura)
- [ ] La tabla de vista previa (html ~146) muestra TODAS las columnas mapeadas: ademas de las 10 actuales, Exclusivo, Consolidado, Retorna contenedor, Ciudad devolucion y Levante diferente (booleanos como Si/No, con el mismo parseo de aplicarExcelALineas)
- [ ] Scroll horizontal si no cabe; mostrar todas las filas (scroll vertical con altura maxima) o una nota "Mostrando 8 de 20" si se mantiene el tope de slice(0, 8)
Campos obligatorios (HU-2 de Captura)
- [ ] Fecha limite (deadline, html ~36): obligatoria siempre (Validators.required, asterisco, mensaje como los demas)
- [ ] Configuracion vehiculo y Carroceria: obligatorias cuando claseViaje es Cupo (la carroceria se suma a la regla actual del vehiculo en campoInvalido ~441: asterisco, aria-invalid y mensaje "Linea N: selecciona la carroceria."); no aplica a Contenedor ni Devolucion de contenedor
- [ ] Clase de contenedor: obligatoria siempre que el campo se muestre (mostrarClaseContenedor: contenedor o extradimensionado nacional), no solo en contenedor (campoInvalido ~441 y mensaje ~1083)
- [ ] Sin errores antes de intentar enviar (mismo mecanismo submitted/touched); las lineas incompletas que vienen de un Excel se marcan en la revision sin romper la carga
- [ ] Por decidir con Sara (por defecto indicado): Carga suelta y Extradimensionado tambien exigen vehiculo y carroceria (no, solo Cupo); el backend rechaza una solicitud sin fecha limite (no, solo frontend)

CRITERIOS DE ACEPTACION
- Escribir "medellin" a mano con un solo resultado muestra la sugerencia y no selecciona la ciudad
- Escribir un cliente con un solo resultado lo muestra en la lista y no lo selecciona solo
- Cargar un Excel sigue resolviendo las ciudades de las lineas sin intervencion
- Tras cargar un Excel, escribir a mano en esa linea ya no autoselecciona
- La vista previa del Excel muestra todas las columnas mapeadas y todas las filas (o avisa cuantas no muestra)
- Sin fecha limite no se puede enviar; una linea de Cupo sin vehiculo o sin carroceria no se puede enviar; una linea con clase de contenedor visible y vacia no se puede enviar; cada caso muestra su mensaje
```
Origen: HU-1, HU-2 y HU-3 de Captura (2026-10-09_HU-FRONTEND.md).

### [S5-HU-24.9] QA: verificacion de las pantallas del frontend (Mesa de Costeo, Biblioteca, Tarifas Plaza, Carga masiva, iconos, conexos, Captura)
Labels: `nexo`, `sprint-5`, `tarea-tecnica`, `qa` · Tipo: Task
Asignado a: @kcano (QA)
```
Parte de S5-HU-24 (milestone). Depende de S5-HU-24.1 a 24.8.

CHECKLIST
- [ ] Mesa de Costeo: sin "Valor estimado", sin caja duplicada de piso, chips de tiempos y clasificacion, "Linea N", avance rojo/verde, aviso de pendiente y resumen completo con la misma numeracion
- [ ] Biblioteca: semaforo, orden, columna Vigencia, paginacion de Historico SAP, sin "mostrando las primeras X", Unidad en Almacenamiento, ITR con Maquina/Movimiento
- [ ] Tarifas Plaza: detalle de errores al importar y edicion de linea rechazada
- [ ] Carga masiva Servicios: subir Excel con errores, corregir en la grilla, confirmar; filas existentes sin error ocultas
- [ ] Carga masiva Tarifas Plaza: columnas opcionales, tipo de vehiculo sin marcar error, duplicados fusionados, valores preseleccionados
- [ ] Iconos de informacion en las 5 vistas
- [ ] Captura (24.8): escribir una ciudad o un cliente a mano con un solo resultado muestra la sugerencia sin seleccionarla; cargar un Excel sigue resolviendo las ciudades solas
- [ ] Conexos por pasos (24.6) y quitar/permitir (24.7), en los 2 selectores (por linea y general): un recorrido por grupo (Almacenamiento, ITR, Sellos, Acompanamiento) con el precio, unidad y subtotal de los criterios de 24.6; cambiar un paso limpia solo los siguientes; cambiar el tipo limpia todo; tras agregar queda vacio con Cantidad 1; "Buscar por texto" funciona como antes; el total, el consolidado y el PDF no cambian

CRITERIOS DE ACEPTACION
- Aprobacion explicita de QA registrada en la issue
```


---

## Resumen

| Milestone | Issues | Repos |
|---|---|---|
| S5-HU-19 — Motor de decision de tarifa | 19.1 a 19.5 | NEXO |
| S5-HU-20 — Mesa de Costeo | 20.1 y 20.5 (NEXO), 20.2 a 20.4, 20.6 y 20.7 (admin) | NEXO + admin |
| S5-HU-21 — Tablero | 21.1 (NEXO), 21.2 y 21.3 (admin) | NEXO + admin |
| S5-HU-22 — Tarifas Plaza y carga masiva | 22.1 (NEXO), 22.2 y 22.3 (admin) | NEXO + admin |
| S5-HU-23 — Bandeja | 23.1 y 23.2 (NEXO), 23.3 (admin) | NEXO + admin |
| S5-HU-24 — Frontend validado en sandbox | 24.1 a 24.9 | admin |

**Orden:** 19.1 -> 19.2 -> 19.3; 19.4 en paralelo; 20.1 -> 20.2 y 20.3; 20.5 -> 20.6 (independientes del resto); 24.1 antes de 20.2 y 20.3; 24.6 -> 24.7 (ambas despues de 24.1; la 24.7 tambien depende de la parte de conexos de 20.1); 24.3 antes de 22.2 y de 24.4; 21.1 -> 21.2; 22.1 -> 22.2; 23.1 -> 23.3 -> 23.2; 24.2, 24.5 y 24.8 en paralelo; la QA de cada milestone al final. Dentro de 19.1, el historico propio (mapeo EF) va primero porque 19.2 y 19.5 lo usan.

**Sin issue (decisiones cerradas o ya hechas):** graficos de antiguedad y evolucion semanal del Tablero (descartados); validar tipo de vehiculo contra rango de peso en la carga masiva (no se hace); filtro por comercial en la Bandeja (HU-008.1, ya existe: #199 / S4-HU-19.1); buscador de clientes (HU-005.10, ya existe: clsBuscarClientesQuery busca primero en DimBPSolicitante y solo si no encuentra cae a tblProspectos, devuelve NIT y origen cliente/prospecto, sin combinar los dos catalogos a proposito). Si Cotifly exige resultados combinados (cliente y prospecto con el mismo nombre en la misma lista), abrir una issue aparte con ese alcance.

**Pendiente de decision de negocio (no bloquea):** si en el caso 7 (solo piso SICE-TAC, sin plaza) la linea deberia aplicarse sola en vez de quedar pendiente; hoy queda pendiente, como se pidio.
