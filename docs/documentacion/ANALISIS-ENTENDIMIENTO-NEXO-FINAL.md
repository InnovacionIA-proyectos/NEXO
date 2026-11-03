# Análisis del Entendimiento de Nexo contra el código

Documento de trabajo: aquí se van anotando los puntos del Entendimiento y las reglas de negocio que se revisan contra el código de Nexo.

- **Código revisado:** rama `QA` de NEXO, commit `9945554` (14-oct-2026).
- **Leyenda:** ✔ se cumple · ◐ se cumple en parte · ✘ no se cumple · ⏳ planificado.
- **Cómo se agregan reglas:** una sección por regla, con su estado, lo verificado, la diferencia y lo que hay que decidir o hacer.

## Resumen

| Regla | Estado | Qué falta |
|---|---|---|
| RN-01 Fuente de cálculo según la regla de la ruta | ◐ | Decidir si manda el flujo actual o la regla tal como está escrita; falta la regla «ruta configurada para plaza». |
| RN-02 Ningún flete bajo el piso SICE-TAC | ✔ (lo principal) | Confirmar que el SICE-TAC corresponde a la ruta aplicable; resultado de la investigación de diferencias. |
| RN-03 Horas 4+4 y retorno vacío 1+1 | ✘ (probable) | Separar las horas del retorno de las del viaje cargado. |
| La plaza solo se usa si es mayor que SICE-TAC | ✔ | Nada. |
| Nunca guardar un flete bajo el piso SICE-TAC | ◐ | Cerrar dos huecos: sin piso encontrado y falla de SICE-TAC. |
| 1+1 horas en contenedor vacío (devolución y levante) | ✘ | Aplicarlo en devolución de contenedor y en levante en otra ciudad. |

---

## RN-01 · Fuente de cálculo según la regla de la ruta

**Texto de la regla:** las rutas con regla configurada para SICE-TAC se calculan con SICE-TAC; las configuradas para plaza se calculan con plaza, respetando siempre el piso SICE-TAC; para las demás se elige el mayor valor entre plaza y SICE-TAC. Ningún resultado puede quedar por debajo del piso aplicable.

**Estado: ◐ en parte.**

| Parte | Estado | Detalle |
|---|---|---|
| Ruta con regla SICE-TAC → se calcula con SICE-TAC | ✔ | Con regla y con piso, el piso gobierna solo (modo `SoloSicetac`). |
| Ruta «configurada para plaza» → se calcula con plaza | ✘ | No existe esa configuración; solo hay reglas Solo-SICE. |
| Demás rutas → el mayor entre plaza y SICE-TAC | ◐ | Ver diferencias abajo. |

**Diferencias frente a la regla** (ejemplo con piso SICE-TAC de 2,5 M):
- Plaza vigente 3,0 M: la regla y Nexo coinciden (plaza, 3,0 M, se guarda sola). ✔
- Plaza vigente 2,0 M (menor que el piso): la regla elegiría el piso (2,5 M) automáticamente; Nexo manda la línea a **consulta completa** y AdminTarifas decide.
- Con un histórico de 3,0 M: la regla solo compara plaza y SICE-TAC; Nexo también mira el histórico y lo recomienda.

**Origen de la diferencia:** el motor implementa el flujo del PDF confirmado por negocio (modos `SoloSicetac`, `PlazaVigenteMayor` y `ConsultaCompleta`), que es distinto de esta redacción.

**Por decidir:** (1) cuál manda, la regla escrita o el flujo actual; (2) si se necesita una regla «ruta configurada para plaza».

---

## RN-02 · Ningún flete bajo el piso SICE-TAC

**Texto de la regla:** ningún flete puede quedar por debajo del piso SICE-TAC aplicable, incluso cuando la ruta se calcule con tarifa de plaza. Validar que el valor SICE-TAC corresponda a la ruta o regla aplicable y que la tarifa esté vigente. Las diferencias observadas en pruebas deben investigarse.

**Estado: ✔ en lo principal.**

- ✔ El servidor rechaza agregar o editar un flete por debajo del piso (`FLETE_BAJO_PISO_SICETAC`), con cualquier fuente: manual, plaza o histórico.
- ✔ El autocosteo tampoco propone valores bajo el piso.
- ◐ **Que el SICE-TAC corresponda a la ruta aplicable:** la búsqueda de la ruta es aproximada y, con una línea sin vehículo, toma el menor valor entre todos los vehículos. Lo cubre la issue NEXO#297 («línea de Cupo sin vehículo no se autocostea»).
- ◐ **Investigar diferencias observadas:** el PR #292 indica que la investigación (#284) quedó solo como documentación, sin cambio de código. No se verificó si se encontró la causa.

**Por hacer:** confirmar con QA el resultado de la investigación y seguir la issue #297.

---

## RN-03 · Horas 4+4 en el viaje cargado y 1+1 en el retorno vacío

**Texto de la regla:** para viaje cargado se aplican 4 horas de cargue + 4 de descarga. Para retorno de contenedor vacío se aplican 1+1 horas logísticas. El retorno vacío debe quedar discriminado del viaje cargado; no se deben sustituir ni borrar los tiempos del tramo cargado al activar el retorno.

**Estado: ✘ probablemente no se cumple.**

- ✔ El viaje cargado usa 4+4 por defecto.
- ✔ El valor del retorno se cobra aparte, con la tarifa de devolución (línea de negocio 6, exclusiva).
- ✘ **Cuando una línea marca «retorna contenedor», el código reemplaza las horas de toda la línea por 1+1.** El piso del viaje cargado se calcula entonces con 2 horas, que es lo que la regla prohíbe.
- ✘ El retorno no tiene su propio cálculo con horas: solo se le suma la tarifa de devolución. El 1+1 queda aplicado al tramo equivocado.

**Riesgo:** el piso del viaje cargado puede quedar más bajo de lo debido y arrastrar el flete.

**Por hacer:** issue de backend para que el viaje cargado conserve 4+4 y el retorno se calcule aparte con 1+1.
**Por decidir:** cómo se calcula el piso del retorno (¿SICE-TAC en vacío, o solo la tarifa de devolución?).

---

## La plaza solo se usa si es mayor que SICE-TAC

**Pregunta:** cuando hay una tarifa plaza, ¿se compara con SICE-TAC antes de usarla?

**Estado: ✔ se cumple.**

- **Ruta con regla SICE-TAC:** se usa SICE-TAC y no se mira la plaza.
- **Sin regla y con plaza vigente:** la plaza se usa y se guarda sola **solo si es mayor** que SICE-TAC.
- **Si la plaza es igual o menor:** no se usa sola. Va a **consulta completa**, y el valor que se sugiere nunca baja del piso. AdminTarifas decide.
- **Si hay plaza pero no se encuentra SICE-TAC:** no hay con qué comparar y tampoco se guarda sola; queda en consulta completa.
- **Al crear o editar una tarifa plaza** (pantalla o Excel) no se compara con SICE-TAC; la comparación ocurre al costear una línea.

---

## Nunca guardar un flete por debajo del piso SICE-TAC

**Pregunta:** ¿puede quedar guardada alguna tarifa menor que SICE-TAC, en cualquier camino?

**Estado: ◐ casi nunca, pero hay dos huecos.**

**Protegido ✔**
- Autocosteo al capturar y al cargar un pliego: solo guarda SICE-TAC, o una plaza mayor.
- «Aplicar todas las propuestas»: el valor sugerido nunca baja del piso.
- Agregar o editar un flete a mano: el servidor lo rechaza si es menor que SICE-TAC, con cualquier fuente (manual, plaza o histórico).

**Huecos ⚠**
1. **No se encuentra valor de SICE-TAC para la línea:** no hay con qué comparar y se puede guardar cualquier valor. Es lo que dice el Entendimiento («no inventar un piso que no existe»), pero puede dejar pasar un valor bajo un piso que sí existía y no se halló.
2. **El servicio de SICE-TAC falla o no responde:** la validación **se salta en silencio** y el valor se guarda sin revisar. Es el más delicado, porque nadie se entera.

**Relacionado:** una línea sin vehículo busca el piso de forma imprecisa y puede tomar uno bajo (issue NEXO#297).

**Por hacer:**
- Si no se puede consultar SICE-TAC, **no dejar guardar** el valor manual: «no se pudo verificar el piso, intenta de nuevo».
- Si no hay piso para la línea, mostrar un aviso visible para que AdminTarifas sepa que no se pudo comparar.

---

## Horas del contenedor vacío: 1+1 por defecto

**Pregunta:** por defecto se calculan 4+4 horas; si es contenedor vacío (devolución, o levante en otra ciudad) debería usarse 1+1. ¿Se está haciendo?

**Estado: ✘ en parte.**

| Caso | ¿Usa 1+1? |
|---|---|
| Viaje normal | 4+4. ✔ Correcto. |
| Línea con «retorna contenedor» marcado | Sí, 1+1 ✔. Pero se aplica a **toda** la línea (ver RN-03). |
| Línea de negocio «Devolución de contenedor» | **No, usa 4+4** ✘. La pantalla no manda horas en esa línea y el servidor pone las de siempre; el 1+1 solo se activa con «retorna contenedor», que en la devolución ni aparece. |
| Levante de contenedor en ciudad diferente | **No usa 1+1** ✘. El costo de ese traslado en vacío se toma directo del valor «vacío» de SICE-TAC, sin recalcularlo con 1+1. |

**Nota:** lo del frontend se leyó en la copia local del repositorio, que puede no ser la última versión.

**Por hacer:** que la devolución de contenedor y el levante en otra ciudad usen 1+1 por defecto, junto con la corrección de la RN-03 (separar las horas del retorno de las del viaje cargado). Issue de backend y de frontend.
