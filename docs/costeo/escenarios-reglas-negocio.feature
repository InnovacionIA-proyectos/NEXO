# language: es
#
# Escenarios de las reglas de negocio del motor de costeo de Nexo
# (`ReglaDeCosteo` + `PropuestaTarifaAdapter`). Documentación viva de
# criterios de aceptación, igual que `docs/contrato-integracion/escenarios.feature`:
# no hay runner de Gherkin instalado en este proyecto, así que estos escenarios
# no se ejecutan solos, se verifican por muestreo contra datos reales.
#
# Evidencia real de cada escenario (tabla caso/esperado/obtenido): ver
# tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/README.md.

Característica: Reglas de negocio del costeo verificadas contra datos reales en producción

  Antecedentes:
    Dado que el motor de costeo vive en ReglaDeCosteo y PropuestaTarifaAdapter
    Y que esta verificación corre contra datos reales del ambiente ya desplegado,
      no contra datos sintéticos como en verificaciones anteriores

  # ------------------------------------------------------------ regla del mayor
  # Evidencia real: tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/esperado-final-pliego.md
  # NO se cumplió en 4 de 12 líneas reales cotizadas (1, 2, 3, 11): había
  # plaza SAP vigente y mayor, y la app propuso el piso SICE-TAC (menor) en
  # su lugar — ver "Desviación 1" en ese documento, pendiente de revisión.

  @regla-del-mayor
  Escenario: La propuesta de flete nunca es menor al mayor entre el piso SICE-TAC y la tarifa de plaza
    Dado una línea real con piso SICE-TAC vigente y tarifa de plaza vigente para su ruta
    Cuando el sistema calcula la propuesta de flete
    Entonces la propuesta es igual al mayor de los dos valores
    Y en caso de empate la propuesta toma la fuente "plaza"

  # ------------------------------------------------------------- piso inviolable
  # Evidencia real: cumplido en las 12 líneas reales cotizadas del pliego de
  # prueba — ver esperado-final-pliego.md, sección 2. No se verificó el
  # lado "plaza nunca por debajo del piso" porque ninguna línea real salió
  # con Fuente=Tarifario (ver Desviación 1, arriba).

  @piso-inviolable
  Escenario: Ninguna propuesta real queda por debajo del piso SICE-TAC vigente
    Dado el universo de líneas reales costeadas accesible en el ambiente
    Cuando se compara cada propuesta contra el piso SICE-TAC vigente de su ruta y fecha
    Entonces cero líneas quedan con un valor propuesto menor a su piso

  # -------------------------------------------------------------- solo sicetac
  # Evidencia real: NO verificado todavía — las 3 reglas "solo SICE-TAC"
  # configuradas en `reglas_solo_sicetac` (Medellín→Bogotá 3S3, Cali→Medellín
  # 3S3, Barranquilla→Pereira 2S3) no coinciden con ninguna línea del pliego
  # de prueba usado. Ver README.md, sección "Estado de las reglas".

  @solo-sicetac
  Escenario: Una ruta marcada "solo SICE-TAC" ignora la tarifa de plaza aunque exista
    Dado una ruta real con la regla "solo SICE-TAC" activa
    Y que existe una tarifa de plaza vigente para esa ruta, mayor al piso SICE-TAC
    Cuando el sistema calcula la propuesta de flete para una línea real de esa ruta
    Entonces la propuesta usa únicamente el piso SICE-TAC
    Y la tarifa de plaza no participa en el resultado

  # ------------------------------------------------------------- tarifas vencidas
  # Evidencia real: confirmado en 5 líneas del pliego (6, 8, 9, 12, 15) —
  # todas con plaza vencida o inexistente, ninguna se propuso. Ver
  # esperado-final-pliego.md, sección 6 (Regla 4).

  @tarifas-vencidas
  Escenario: Una tarifa de plaza vencida nunca se propone, aunque sea la más alta
    Dado una tarifa de plaza real cuya vigencia ya venció a la fecha de la línea
    Cuando el sistema calcula la propuesta de flete
    Entonces la tarifa vencida se marca como inactiva
    Y la propuesta gobierna por el piso SICE-TAC vigente, no por la tarifa vencida

  # --------------------------------------------------------- ganada archiva tarifa
  # Evidencia real: confirmado en las 54 cotizaciones reales marcadas
  # "Ganada" en `dbInformesAutomatizados_Pruebas.db_NEXO.cotizaciones`
  # — las 54 con exactamente 1 tarifa histórica por línea, 0 desincronizadas.

  @ganada-archiva-tarifa
  Escenario: Marcar una cotización real como Ganada archiva su tarifa
    Dado una cotización real marcada como Ganada
    Cuando se revisa el archivo de tarifas históricas
    Entonces existe un registro de tarifa histórica por cada línea de esa cotización
    Y no hay registros duplicados para la misma línea

  # --------------------------------------------------------------- ajuste auditado
  # Evidencia real: confirmado con el único ajuste real que existe
  # (`ajustes_vigencia_plaza`) — autoría de Brayan Quintero (rol
  # AdminTarifas), con entrada correspondiente en `audit_log`.

  @ajuste-auditado
  Escenario: Un ajuste manual de vigencia de plaza queda auditado con autoría real
    Dado un ajuste real de vigencia de tarifa de plaza en el ambiente
    Cuando se revisa el registro del ajuste
    Entonces el ajuste tiene el usuario real que lo hizo, no un valor genérico
    Y queda un registro de auditoría asociado

  # -------------------------------------------------------------- recálculo conexos
  # Evidencia real: confirmado en 3 cotizaciones reales — Total = suma de
  # líneas + suma de conexos, exacto en las 3 (ej. $1.850.000 + $2.000 =
  # $1.852.000). Ver esperado-final-pliego.md, sección 6 (Regla 7).

  @recalculo-conexos
  Escenario: Agregar un conexo a una cotización real recalcula su total
    Dado una cotización real con un Total ya calculado
    Cuando se agrega un servicio conexo a esa cotización
    Entonces el Total de la cotización se recalcula automáticamente
    Y el nuevo Total refleja el conexo agregado sin acción manual adicional
