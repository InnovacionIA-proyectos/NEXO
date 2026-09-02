# language: es
#
# Escenarios del contrato de integración con el portal de la Administración.
#
# No son documentación: son los criterios de aceptación que implementan las issues
# #9 (validar la sesión en cada petición), #10 (los 6 roles y la auditoría) y #11
# (retiro del esquema X-Rol). Cada escenario debería terminar como una prueba.
#
# Las etiquetas dicen a qué issue pertenece cada uno.

Característica: Identidad e integración con el portal de la Administración

  Antecedentes:
    Dado que Nexo no tiene inicio de sesión propio
    Y que la identidad la aporta la sesión del portal
    Y que los permisos se leen de la tabla de la administración, nunca del navegador

  # ---------------------------------------------------------------- identidad

  @issue-9 @identidad
  Escenario: Una persona con sesión válida entra al módulo
    Dado un usuario con identificador 10123 en la tabla de la administración
    Y que tiene permiso sobre Cabina de Fletes
    Cuando consulta la sesión actual enviando su identificador
    Entonces recibe 200
    Y el rol viene resuelto desde la tabla
    Y la respuesta incluye las capacidades de ese rol

  @issue-9 @identidad
  Escenario: Un identificador que no está en la tabla no entra
    Dado un identificador 99999 que no existe en la tabla de la administración
    Cuando consulta la sesión actual
    Entonces recibe 401
    Y el cuerpo trae la forma estándar de error con su código
    Y no se le asigna ningún rol por defecto

  @issue-9 @identidad
  Escenario: Una persona real sin permiso sobre Cabina de Fletes no entra
    Dado un usuario que sí existe en la tabla
    Pero que no tiene permiso sobre Cabina de Fletes
    Cuando consulta la sesión actual
    Entonces recibe 403
    Y el mensaje explica que le falta el permiso, no que no exista

  @issue-9 @identidad
  Escenario: Una petición sin identidad no pasa
    Dado que la petición llega sin el encabezado de identidad del portal
    Cuando alcanza cualquier endpoint del API
    Entonces recibe 401
    Y no se ejecuta nada de la operación pedida

  @issue-9 @identidad
  Escenario: La sesión se valida en cada petición, no una vez
    Dado un usuario que entró correctamente al módulo
    Cuando se le retira el permiso en la tabla de la administración
    Y hace la siguiente petición
    Entonces recibe 403
    Y no hay sesión guardada en Nexo que lo mantenga dentro

  # ------------------------------------------------- lo que la auditoría exige

  @issue-11 @seguridad @auditoria-qa
  Escenario: Falsificar el rol desde el navegador no sirve de nada
    Dado un usuario cuyo rol en la tabla es Comercial
    Cuando manipula el navegador para afirmar que su rol es AdminTarifas
    Y llama a una operación reservada a AdminTarifas
    Entonces recibe 403
    Y el rol usado para decidir es el que dice la tabla, no el que dice el navegador

  @issue-11 @seguridad
  Escenario: El encabezado de rol del prototipo ya no existe
    Dado que el esquema de desarrollo con encabezado de rol fue retirado
    Cuando una petición intenta declarar su propio rol por encabezado
    Entonces el encabezado se ignora por completo
    Y el rol se resuelve únicamente contra la tabla

  @issue-11 @seguridad @fail-closed
  Escenario: Sin fuente de identidad configurada, la API no arranca en Producción
    Dado un despliegue en Producción sin la fuente de identidad configurada
    Cuando la API intenta arrancar
    Entonces el arranque falla con un mensaje accionable
    Y no queda expuesto ningún endpoint

  @riesgo-aceptado @adr-001
  Escenario: Suplantar a otra persona hoy funciona, y está declarado
    Dado que la identidad viaja sin firma ni vencimiento
    Cuando alguien cambia el identificador por el de otra persona
    Entonces obtiene los permisos de esa otra persona
    Y la auditoría registra a esa otra persona como autora
    # Este escenario documenta un riesgo aceptado a conciencia, no un defecto por
    # corregir en este sprint. Ver ADR-001. Se cierra cuando el portal emita un
    # token verificable, y entonces este escenario se invierte.

  # ----------------------------------------------------------------- roles

  @issue-10 @roles
  Escenario: Un rol del portal se traduce a su equivalente en Nexo
    Dado un usuario cuyo permiso en la administración corresponde a tarifas comerciales
    Cuando consulta la sesión actual
    Entonces su rol en Nexo es Comercial
    Y la traducción ocurre en el servidor

  @issue-10 @roles @pendiente-P3
  Escenario: Con varios roles a la vez gana el de mayor precedencia
    Dado un usuario con permisos de tarifas comerciales y de administración de tarifas
    Cuando consulta la sesión actual
    Entonces recibe un único rol efectivo
    Y es el de mayor precedencia según el orden acordado
    # Depende del pendiente P2/P3: la equivalencia y el orden los confirma negocio.

  @issue-10 @roles
  Escenario: Un permiso sin equivalencia en Nexo no abre la puerta
    Dado un usuario cuyos permisos no corresponden a ninguno de los 6 roles de Nexo
    Cuando consulta la sesión actual
    Entonces recibe 403
    Y no se le asigna el rol menos privilegiado como consuelo

  @issue-10 @auditoria
  Escenario: Todo ajuste queda con autor real
    Dado un usuario autenticado que ajusta una tarifa
    Cuando la operación se completa
    Entonces la auditoría guarda su identificador y su nombre reales
    Y no un usuario de sistema genérico

  # -------------------------------------------------------- autorización

  @issue-10 @autorizacion
  Esquema del escenario: Cada operación exige los roles que declara el contrato
    Dado un usuario con rol <rol>
    Cuando intenta <operacion>
    Entonces el resultado es <resultado>

    Ejemplos:
      | rol                 | operacion                        | resultado |
      | Comercial           | capturar una solicitud           | permitido |
      | AdminTarifas        | capturar una solicitud           | 403       |
      | CoordinadorFletes   | cargar líneas de un pliego       | permitido |
      | Comercial           | cargar líneas de un pliego       | 403       |
      | ServiciosLogisticos | crear un servicio conexo         | permitido |
      | Comercial           | crear un servicio conexo         | 403       |
      | AdminTarifas        | editar reglas solo SICE-TAC      | permitido |
      | LiderProceso        | editar reglas solo SICE-TAC      | 403       |
      | LiderProceso        | ver métricas de cumplimiento     | permitido |
      | Comercial           | ver métricas de cumplimiento     | 403       |

  # ---------------------------------------------------- trabajos pesados

  # Insumo real (S2-HU-04.7): tests/CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.json
  # (320 líneas) — ver tests/QA/hu-04.7-pliego-dataset-validacion-muestral/README.md
  @hu-04 @asincrono
  Escenario: Cargar un pliego responde de inmediato
    Dado un pliego de 300 líneas
    Cuando se carga
    Entonces la respuesta es 202 antes de costear nada
    Y trae un identificador de trabajo
    Y trae la ruta donde consultar su estado

  @hu-04 @asincrono
  Escenario: El estado se puede reconstruir sin haber recibido ningún aviso
    Dado un trabajo en curso
    Y un cliente que nunca se conectó al canal de avisos
    Cuando consulta el trabajo por su identificador
    Entonces obtiene el estado completo y el avance
    Y no necesitó ningún aviso para saberlo

  # Insumo real (S2-HU-04.7): las 3 líneas fuenteCae=true (ids 318, 319, 320) del
  # mismo pliego-300-lineas.json — ver tests/QA/hu-04.7-pliego-dataset-validacion-muestral/README.md
  @hu-04 @asincrono
  Escenario: Un pliego con líneas sin resolver termina en estado parcial
    Dado un pliego donde una fuente externa falla para 3 líneas
    Cuando el trabajo termina
    Entonces el estado es Parcial y no Fallido
    Y las 297 líneas restantes quedan costeadas
    Y cada línea pendiente conserva su motivo

  @hu-045 @avisos
  Escenario: Un aviso rezagado no hace retroceder la barra de progreso
    Dado un cliente suscrito a un trabajo que ya pintó la secuencia 195
    Cuando le llega un aviso con secuencia 191, entregado tarde
    Entonces lo descarta y la pantalla no cambia
    Y el siguiente aviso con secuencia 196 sí se pinta

  @hu-045 @avisos
  Escenario: Al reconectar, la consulta deja al cliente sincronizado
    Dado un cliente que estuvo desconectado y se perdió varios avisos
    Cuando consulta el trabajo por su identificador
    Entonces obtiene el estado actual y su secuencia
    Y desde ahí descarta los avisos con secuencia menor o igual

  # ------------------------------------------------- fuentes de tarifa provisionales

  # Insumo real (S2-HU-09.2, issue #32):
  # docs/tarifas-plaza-provisionales/dataset-tarifas-plaza-provisionales.{json,xlsx}
  # (210 filas: 150 de núcleo + 60 de casos borde, extraídas de dbSolucionesTI —
  # ver el README de esa carpeta para el origen documentado y el acta de
  # aprobación). El mecanismo que hace que ReglaDeCosteo/PropuestaTarifaAdapter
  # lean de esta fuente todavía no existe — depende de la issue #33.
  @hu-09 @issue-32 @pendiente-issue-33
  Escenario: Propuesta calculada con datos de plaza provisionales realistas
    Dado un dataset de tarifas de plaza provisionales validado por negocio,
      con rutas frecuentes, valores reales y casos borde documentados
      (sin plaza, vigencia vencida, valores extremos)
    Cuando el motor de costeo propone una línea usando ese dataset como fuente
    Entonces la propuesta sigue la regla del mayor y nunca queda por debajo del piso
    Y la fuente de la propuesta queda trazada como "provisional"
    Y una línea sobre una ruta marcada "sin plaza" en el dataset no propone nada solo
      con esa fuente, igual que si no hubiera plaza real

  # ------------------------------------------------- contexto de mercado (DW externos)

  # Insumo real (S2-HU-10, Habilitador B8 parte 1):
  # analitica/00-fuentes/fuentes-corporativas/{README.md,*.csv}
  # (4 extractos puntuales desde los 4 data warehouses de solo lectura: piso
  # SICE-TAC, tarifa de plaza SAP, histórico SAP y catálogo de clientes — ver el
  # README de esa carpeta para fecha de corte, volumen, llaves de cruce y
  # hallazgos). Es el insumo de contexto de mercado para el EDA de S2-HU-11,
  # que corre sobre este set y sobre el de S2-HU-10.1 (issue #35); no se consume
  # desde ningún flujo del producto real.
  @hu-10 @habilitador-b8
  Escenario: Contexto de mercado entregado y documentado para el EDA
    Dado un extracto puntual de piso SICE-TAC, tarifa de plaza SAP, histórico SAP
      y catálogo de clientes, sacado de los 4 data warehouses de solo lectura
    Cuando se publica junto con su documentación
    Entonces cada archivo tiene su fecha de corte y su volumen de filas documentados
    Y las llaves de cruce contra el histórico propio quedan identificadas para ruta,
      vehículo, período y cliente
    Y no se reporta impacto sobre los data warehouses compartidos durante la extracción

  # Insumo real (S2-HU-10, Habilitador B8 parte 3):
  # analitica/01-set-final/v1.0.0-piloto/{README.md,diccionario-y-linaje.md,
  # reporte-inconsistencias.md} + los crudos en analitica/00-fuentes/ — une la
  # BD propia (S2-HU-10.1, issue #35) con los extractos corporativos
  # (S2-HU-10.2, issue #36) en un solo entregable versionado. No es un DW ni
  # un cubo, y no impone grano ni variable objetivo — eso lo decide el EDA de
  # S2-HU-11.
  @hu-10 @habilitador-b8
  Escenario: El set final queda consolidado, versionado y con diccionario y linaje
    Dado el lado propio (BD CabinaFletes) y el lado externo (4 fuentes corporativas)
      ya extraídos y documentados por separado
    Cuando se publican juntos como un set final con número de versión
    Entonces existe un diccionario único que explica cada columna, su significado,
      su unidad y de qué tabla de origen viene
    Y cada tabla conserva su propio grano — ninguna columna del set final resulta
      de aplanar varias fuentes en una sola tabla ancha

  @hu-10 @habilitador-b8
  Escenario: Los registros incompletos o contradictorios se documentan, no se descartan
    Dado un registro con datos incompletos o contradictorios en cualquiera de las
      dos fuentes del set final
    Cuando se genera el reporte de inconsistencias
    Entonces ese registro aparece en el reporte con el tipo de caso y su causa
    Y el registro sigue presente en su archivo original, sin ser eliminado en silencio

  @hu-10 @habilitador-b8
  Escenario: La entrega al equipo de datos queda registrada
    Dado el set final ya consolidado, versionado y con su control de calidad documentado
    Cuando se entrega al equipo de datos
    Entonces queda registrada la fecha de entrega y el contenido exacto entregado
    Y la versión entregada queda identificada para poder rastrearla después

  @hu-051 @priorizacion
  Escenario: La bandeja se reordena al recalcular el puntaje por paso del tiempo
    Dado un trabajo de recálculo programado en el broker
    Cuando vence el plazo
    Entonces se pide refrescar el puntaje de las solicitudes activas
    Y el plazo siguiente queda programado

  @hu-051 @priorizacion
  Escenario: La bandeja se reordena al recalcular el puntaje por cambio de parámetros
    Dado un Admin que cambia el horizonte de urgencia de 240 a 4000 horas
    Cuando guarda el cambio
    Entonces la respuesta es inmediata y no espera el recálculo
    Y se pide refrescar el puntaje sin esperar el plazo
    Y una solicitud a 30 días de su deadline pasa de 0 a 82

  @hu-051 @priorizacion
  Escenario: Cambiar un peso no mueve ningún puntaje
    Dado que el total del puntaje es solo la urgencia por deadline
    Cuando el Admin cambia el peso de urgencia de 0.40 a 0.60
    Entonces se revisan todas las solicitudes activas
    Y no se actualiza ninguna

  @hu-051 @priorizacion
  Escenario: Pesos que no suman uno se rechazan
    Dado un Admin que envía pesos que suman 1.3
    Cuando guarda el cambio
    Entonces la respuesta es 400 PARAMETROS_SCORE_INVALIDOS
    Y los parámetros guardados no cambiaron

  @hu-051 @priorizacion
  Escenario: El recálculo falla sin afectar mi trabajo
    Dado un recálculo del puntaje que falla
    Cuando se agotan sus reintentos
    Entonces el mensaje queda en la cola de error para alertar al equipo
    Y el API sigue atendiendo con normalidad
    Y el plazo siguiente sigue programado

  @hu-052 @bandeja
  Escenario: Una solicitud nueva entra priorizada a la bandeja
    Dado un cliente con la bandeja abierta y suscrito al canal
    Cuando se da de alta una solicitud
    Entonces recibe un aviso con motivo solicitud-nueva
    Y al reconsultar con sus filtros la ve en la posición que le toca por puntaje

  @hu-052 @bandeja
  Escenario: Un pliego grande produce un solo aviso de bandeja
    Dado un pliego de 300 líneas costeándose
    Cuando termina el trabajo
    Entonces se recibe UN aviso de bandeja, no uno por línea
    Y el canal de avance sí recibió uno por línea

  @hu-052 @bandeja
  Escenario: La bandeja se refresca aunque no cambie ningún puntaje guardado
    Dado que la urgencia depende del momento y el orden se mueve solo
    Cuando corre el recálculo periódico sin actualizar ninguna fila
    Entonces igual se avisa con motivo refresco-periodico

  @hu-052 @bandeja
  Escenario: La consulta de la bandeja sigue siendo el respaldo
    Dado un cliente que nunca se conectó al canal de la bandeja
    Cuando consulta la bandeja
    Entonces obtiene el orden correcto y completo
    Y no necesitó ningún aviso para saberlo

  @hu-061 @notificacion
  Escenario: El correo falla y la entrega no se afecta
    Dado un relay de correo caído
    Cuando se entrega una cotización
    Entonces la respuesta es inmediata y la cotización queda Entregada
    Y la notificación queda encolada para reintentarse

  @hu-061 @notificacion
  Escenario: Un rechazo sin arreglo no gasta reintentos
    Dado un adjunto que pesa más de lo que el relay admite
    Cuando el consumidor intenta enviar el correo
    Entonces no se reintenta
    Y el fallo queda en la cola de error al primer intento

  @hu-061 @notificacion
  Escenario: Un fallo pasajero se reintenta antes de alertar
    Dado un relay de correo que no responde
    Cuando el consumidor intenta enviar el correo
    Entonces se reintenta de forma escalonada
    Y solo al agotar los intentos el fallo queda en la cola de error

  @hu-061 @notificacion
  Escenario: El correo nunca se manda dentro de la petición de entrega
    Dado el caso de uso que entrega la cotización
    Cuando se revisan sus dependencias
    Entonces no tiene ninguna salida de correo entre ellas
