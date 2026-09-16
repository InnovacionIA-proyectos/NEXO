# language: es
#
# Escenarios de las reglas de negocio del motor de costeo de CabinaFletes
# (`ReglaDeCosteo` + `PropuestaTarifaAdapter`). Documentación viva de
# criterios de aceptación, igual que `docs/contrato-integracion/escenarios.feature`:
# no hay runner de Gherkin instalado en este proyecto, así que estos escenarios
# no se ejecutan solos, se verifican por muestreo contra datos reales.
#
# Evidencia real de cada escenario (tabla caso/esperado/obtenido): ver
# tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/README.md
# (issue #43, S2-HU-13.4).

Característica: Reglas de negocio del costeo verificadas contra datos reales en producción

  Antecedentes:
    Dado que el motor de costeo vive en ReglaDeCosteo y PropuestaTarifaAdapter
    Y que esta verificación corre contra datos reales del ambiente ya desplegado,
      no contra datos sintéticos como en verificaciones anteriores

  # ------------------------------------------------------------ regla del mayor

  @hu-13.4 @regla-del-mayor
  Escenario: La propuesta de flete nunca es menor al mayor entre el piso SICE-TAC y la tarifa de plaza
    Dado una línea real con piso SICE-TAC vigente y tarifa de plaza vigente para su ruta
    Cuando el sistema calcula la propuesta de flete
    Entonces la propuesta es igual al mayor de los dos valores
    Y en caso de empate la propuesta toma la fuente "plaza"

  # ------------------------------------------------------------- piso inviolable

  @hu-13.4 @piso-inviolable
  Escenario: Ninguna propuesta real queda por debajo del piso SICE-TAC vigente
    Dado el universo de líneas reales costeadas accesible en el ambiente
    Cuando se compara cada propuesta contra el piso SICE-TAC vigente de su ruta y fecha
    Entonces cero líneas quedan con un valor propuesto menor a su piso

  # -------------------------------------------------------------- solo sicetac

  @hu-13.4 @solo-sicetac
  Escenario: Una ruta marcada "solo SICE-TAC" ignora la tarifa de plaza aunque exista
    Dado una ruta real con la regla "solo SICE-TAC" activa
    Y que existe una tarifa de plaza vigente para esa ruta, mayor al piso SICE-TAC
    Cuando el sistema calcula la propuesta de flete para una línea real de esa ruta
    Entonces la propuesta usa únicamente el piso SICE-TAC
    Y la tarifa de plaza no participa en el resultado

  # ------------------------------------------------------------- tarifas vencidas

  @hu-13.4 @tarifas-vencidas
  Escenario: Una tarifa de plaza vencida nunca se propone, aunque sea la más alta
    Dado una tarifa de plaza real cuya vigencia ya venció a la fecha de la línea
    Cuando el sistema calcula la propuesta de flete
    Entonces la tarifa vencida se marca como inactiva
    Y la propuesta gobierna por el piso SICE-TAC vigente, no por la tarifa vencida

  # --------------------------------------------------------- ganada archiva tarifa

  @hu-13.4 @ganada-archiva-tarifa
  Escenario: Marcar una cotización real como Ganada archiva su tarifa
    Dado una cotización real marcada como Ganada
    Cuando se revisa el archivo de tarifas históricas
    Entonces existe un registro de tarifa histórica por cada línea de esa cotización
    Y no hay registros duplicados para la misma línea

  # --------------------------------------------------------------- ajuste auditado

  @hu-13.4 @ajuste-auditado
  Escenario: Un ajuste manual de vigencia de plaza queda auditado con autoría real
    Dado un ajuste real de vigencia de tarifa de plaza en el ambiente
    Cuando se revisa el registro del ajuste
    Entonces el ajuste tiene el usuario real que lo hizo, no un valor genérico
    Y queda un registro de auditoría asociado

  # -------------------------------------------------------------- recálculo conexos

  @hu-13.4 @recalculo-conexos
  Escenario: Agregar un conexo a una cotización real recalcula su total
    Dado una cotización real con un Total ya calculado
    Cuando se agrega un servicio conexo a esa cotización
    Entonces el Total de la cotización se recalcula automáticamente
    Y el nuevo Total refleja el conexo agregado sin acción manual adicional
