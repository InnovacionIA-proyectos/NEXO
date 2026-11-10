# language: es
#
# Escenarios del contrato de integración con el portal de la Administración.
#
# No son documentación: son los criterios de aceptación de la integración con el
# portal. Cubren identidad (validar la sesión en cada petición), roles (los 6 roles,
# su equivalencia y la auditoría), seguridad (retiro del esquema X-Rol),
# autorización por operación, trabajos pesados, avisos en vivo, bandeja,
# priorización y entrega. Cada escenario debería terminar como una prueba.
#
# Las etiquetas agrupan los escenarios por área.

Característica: Integración de NEXO con el portal de la Administración

  Antecedentes:
    Dado que NEXO no tiene inicio de sesión propio
    Y que la identidad la aporta la sesión del portal
    Y que los permisos se leen de la tabla de la administración, nunca del navegador

  # ---------------------------------------------------------------- identidad

  @identidad
  Escenario: Una persona con sesión válida entra al módulo
    Dado un usuario con identificador 10123 en la tabla de la administración
    Y que uno de sus roles del portal se traduce a un rol de NEXO
    Cuando consulta la sesión actual enviando su identificador
    Entonces recibe 200
    Y el rol viene resuelto desde la tabla
    Y la respuesta incluye las capacidades de ese rol

  @identidad
  Escenario: Un identificador que no está en la tabla no entra
    Dado un identificador 99999 que no existe en la tabla de la administración
    Cuando consulta la sesión actual
    Entonces recibe 401 SESION_INVALIDA
    Y el cuerpo trae la forma estándar de error con su código
    Y el mensaje es el mismo que para cualquier otra identidad inválida
    Y no se le asigna ningún rol por defecto

  @identidad
  Escenario: Una persona real sin rol de NEXO no entra
    Dado un usuario que sí existe en la tabla
    Pero ninguno de sus roles del portal se traduce a un rol de NEXO
    Cuando consulta la sesión actual
    Entonces recibe 403 SIN_PERMISO_CABINA_FLETES
    Y el mensaje explica que le falta el permiso, no que no exista

  @identidad
  Escenario: Una petición sin identidad no pasa
    Dado que la petición llega sin el encabezado de identidad del portal
    Cuando alcanza cualquier endpoint del API destinado al portal
    Entonces recibe 401
    Y no se ejecuta nada de la operación pedida

  @identidad
  Escenario: Un identificador que no es un número no pasa
    Dado que el encabezado de identidad trae un valor que no es un número entero
    Cuando alcanza cualquier endpoint del API destinado al portal
    Entonces recibe 401

  @identidad @fail-closed
  Escenario: Si la base del portal no responde, la petición se rechaza
    Dado que la tabla de usuarios del portal no está disponible
    Cuando llega una petición con un identificador válido
    Entonces recibe 401
    Y no un 500 que exponga la base externa caída

  @identidad
  Escenario: La sesión se valida en cada petición, no una vez
    Dado un usuario que entró correctamente al módulo
    Cuando se le retira en el portal el rol que lo traducía a NEXO
    Y hace la siguiente petición
    Entonces recibe 403
    Y no hay sesión guardada en NEXO que lo mantenga dentro

  # ------------------------------------------------- lo que la auditoría exige

  @seguridad @auditoria-qa
  Escenario: Falsificar el rol desde el navegador no sirve de nada
    Dado un usuario cuyo rol en la tabla es Comercial
    Cuando manipula el navegador para afirmar que su rol es AdminTarifas
    Y llama a una operación reservada a AdminTarifas
    Entonces recibe 403
    Y el rol usado para decidir es el que dice la tabla, no el que dice el navegador

  @seguridad
  Escenario: El encabezado de rol del prototipo ya no existe
    Dado que el esquema de desarrollo con encabezado de rol fue retirado
    Cuando una petición intenta declarar su propio rol por encabezado
    Entonces el encabezado se ignora por completo
    Y el rol se resuelve únicamente contra la tabla

  @seguridad @fail-closed
  Escenario: Sin fuente de identidad configurada, la API no arranca en Producción
    Dado un despliegue en Producción sin la fuente de identidad configurada
    Cuando la API intenta arrancar
    Entonces el arranque falla con un mensaje accionable
    Y no queda expuesto ningún endpoint

  @seguridad
  Escenario: En Desarrollo y QA el rol sale de un usuario real de la base local
    Dado un entorno de Desarrollo o QA sin la fuente de identidad del portal
    Cuando llega una petición con un identificador
    Entonces el rol se resuelve contra el usuario local con ese identificador del portal
    Y si no existe ese usuario la respuesta es 401
    Y no hay encabezado que permita declarar otro rol

  @riesgo-aceptado @adr-001
  Escenario: Suplantar a otra persona hoy funciona, y está declarado
    Dado que la identidad viaja sin firma ni vencimiento
    Cuando alguien cambia el identificador por el de otra persona
    Entonces obtiene los permisos de esa otra persona
    Y la auditoría registra a esa otra persona como autora
    # Este escenario documenta un riesgo aceptado a conciencia, no un defecto por
    # corregir de inmediato. Ver la decisión D-12 (docs/decisiones/DECISIONES-NEXO.md). Se cierra cuando el portal emita un
    # token verificable, y entonces este escenario se invierte.

  # ----------------------------------------------------------------- roles

  @roles
  Escenario: Un rol del portal se traduce a su equivalente en NEXO
    Dado un usuario con el rol TarifasComercial en la administración
    Cuando consulta la sesión actual
    Entonces su rol en NEXO es Comercial
    Y la traducción ocurre en el servidor

  @roles
  Escenario: El rol del portal se reconoce primero por su id y después por su nombre
    Dado un usuario con el rol 49 del portal, llamado CoordinadorFletesNEXO
    Cuando consulta la sesión actual
    Entonces su rol en NEXO es AdminBiblioteca
    Y un rol del portal sin id en la equivalencia se reconoce por su nombre

  @roles @pendiente-P3
  Escenario: Con varios roles a la vez gana el de mayor precedencia
    Dado un usuario con los roles TarifasComercial y TarifasAdministrador en la administración
    Cuando consulta la sesión actual
    Entonces recibe un único rol efectivo
    Y ese rol es AdminTarifas
    # El orden en uso es Admin > AdminTarifas > AdminBiblioteca > Comercial >
    # LiderProceso > ServiciosLogisticos. Falta que negocio lo confirme.

  @roles
  Escenario: Un permiso sin equivalencia en NEXO no abre la puerta
    Dado un usuario cuyos roles no corresponden a ninguno de los 6 roles de NEXO
    Cuando consulta la sesión actual
    Entonces recibe 403
    Y no se le asigna el rol menos privilegiado como consuelo

  @auditoria
  Escenario: Todo ajuste queda con autor real
    Dado un usuario autenticado que ajusta una tarifa
    Cuando la operación se completa
    Entonces la auditoría guarda su usuario de NEXO, sincronizado con su identidad del portal
    Y no un usuario de sistema genérico

  # -------------------------------------------------------- autorización

  @autorizacion
  Esquema del escenario: Cada operación exige los roles que declara el contrato
    Dado un usuario con rol <rol>
    Cuando intenta <operacion>
    Entonces el resultado es <resultado>

    Ejemplos:
      | rol                 | operacion                                | resultado |
      | Comercial           | capturar una solicitud                   | permitido |
      | AdminTarifas        | capturar una solicitud                   | 403       |
      | Admin               | cargar líneas de un pliego               | permitido |
      | AdminBiblioteca     | cargar líneas de un pliego               | 403       |
      | Comercial           | cargar líneas de un pliego               | 403       |
      | AdminBiblioteca     | ver la bandeja                           | 403       |
      | AdminBiblioteca     | suscribirse al canal de la bandeja       | 403       |
      | AdminBiblioteca     | ver una cotización                       | 403       |
      | AdminTarifas        | entregar una cotización                  | permitido |
      | Comercial           | entregar una cotización                  | 403       |
      | ServiciosLogisticos | crear un servicio conexo                 | permitido |
      | Comercial           | crear un servicio conexo                 | 403       |
      | AdminTarifas        | editar reglas solo SICE-TAC              | permitido |
      | AdminBiblioteca     | editar reglas solo SICE-TAC              | permitido |
      | LiderProceso        | editar reglas solo SICE-TAC              | 403       |
      | LiderProceso        | ver métricas de cumplimiento             | permitido |
      | Comercial           | ver métricas de cumplimiento             | 403       |
      | AdminTarifas        | conversar con el asistente               | permitido |
      | Comercial           | conversar con el asistente               | 403       |
      | Admin               | ver el gasto de la IA                    | permitido |
      | AdminTarifas        | ver el gasto de la IA                    | 403       |
      | Admin               | recalibrar la priorización               | permitido |
      | AdminTarifas        | recalibrar la priorización               | 403       |
      | AdminTarifas        | crear un plan de cargue                  | permitido |
      | AdminBiblioteca     | crear un plan de cargue                  | 403       |
      | Admin               | borrar un vehículo del catálogo          | permitido |
      | AdminTarifas        | borrar un vehículo del catálogo          | 403       |

  # ---------------------------------------------------- trabajos pesados

  # Insumo real: tests/Nexo.Domain.Tests/TestData/pliego-300-lineas.json (320 líneas).
  @asincrono
  Escenario: Cargar un pliego responde de inmediato
    Dado un pliego de 300 líneas
    Cuando se carga
    Entonces la respuesta es 202 antes de costear nada
    Y trae un identificador de trabajo con estado Pendiente
    Y trae la ruta donde consultar su estado

  @asincrono
  Escenario: El identificador del 202 se puede consultar de inmediato
    Dado un pliego recién aceptado con 202
    Cuando el cliente consulta el trabajo antes de que arranque
    Entonces recibe 200 con estado Pendiente
    Y no recibe 404

  @asincrono
  Escenario: El estado se puede reconstruir sin haber recibido ningún aviso
    Dado un trabajo en curso
    Y un cliente que nunca se conectó al canal de avisos
    Cuando consulta el trabajo por su identificador
    Entonces obtiene el estado completo, el avance y la lista de pendientes
    Y no necesitó ningún aviso para saberlo

  # Insumo real: las 3 líneas con fuenteCae=true (ids 318, 319 y 320) del mismo
  # pliego-300-lineas.json.
  @asincrono
  Escenario: Un pliego con líneas sin resolver termina en estado parcial
    Dado un pliego donde una fuente externa falla para 3 líneas
    Cuando esas líneas agotan sus reintentos y el trabajo termina
    Entonces el estado es Parcial y no Fallido
    Y las 297 líneas restantes quedan costeadas
    Y cada línea pendiente aparece en la lista de pendientes con tipo Tecnica y su motivo

  @asincrono
  Escenario: Una línea sin tarifa espera a una persona y no se reintenta
    Dado una línea de un pliego para la que no hay tarifa válida
    Cuando el servicio la procesa
    Entonces aparece en la lista de pendientes con tipo Manual y su motivo
    Y cuenta en requierenTarifaManual, no en pendientesTecnicas
    Y no se reintenta

  @asincrono
  Escenario: Vencer la ventana automática cierra el trabajo en parcial
    Dado un trabajo con líneas que todavía no tienen desenlace
    Cuando vence la ventana automática del trabajo
    Entonces el trabajo cierra en Parcial con lo que hay
    Y las líneas que faltan conservan su motivo
    Y la bandeja recibe un aviso trabajo-terminado

  # ---------------------------------------------------- avisos en vivo

  @avisos
  Escenario: El aviso de avance no trae la lista de pendientes
    Dado un cliente suscrito a un trabajo con líneas pendientes
    Cuando le llega un aviso de avance
    Entonces trae el estado, los contadores y la secuencia
    Y no trae la lista de pendientes, que se obtiene con la consulta

  @avisos
  Escenario: Un aviso rezagado no hace retroceder la barra de progreso
    Dado un cliente suscrito a un trabajo que ya pintó la secuencia 195
    Cuando le llega un aviso con secuencia 191, entregado tarde
    Entonces lo descarta y la pantalla no cambia
    Y el siguiente aviso con secuencia 196 sí se pinta

  @avisos
  Escenario: Al reconectar, la consulta deja al cliente sincronizado
    Dado un cliente que estuvo desconectado y se perdió varios avisos
    Cuando consulta el trabajo por su identificador
    Entonces obtiene el estado actual y su secuencia
    Y desde ahí descarta los avisos con secuencia menor o igual

  @avisos @plan-cargue
  Escenario: El desenlace de un plan de cargue llega por su propio canal
    Dado un cliente suscrito a un plan de cargue en el canal de planes de cargue
    Cuando el Cubicador devuelve el plan generado
    Entonces recibe un aviso con el identificador del plan, su estado y su puntaje
    Y el aviso no trae las colocaciones, que se obtienen con la consulta del plan

  @avisos @plan-cargue
  Escenario: El rechazo de un plan de cargue llega con su motivo
    Dado un cliente suscrito a un plan de cargue
    Cuando el Cubicador rechaza el plan
    Entonces recibe un aviso con estado error
    Y el aviso trae el mensaje del Cubicador como motivo

  # ---------------------------------------------------- priorización

  @priorizacion
  Escenario: La bandeja se reordena al recalcular el puntaje por paso del tiempo
    Dado un trabajo de recálculo programado en el broker
    Cuando vence el plazo
    Entonces se pide refrescar el puntaje de las solicitudes activas
    Y el plazo siguiente queda programado

  @priorizacion
  Escenario: La bandeja se reordena al recalcular el puntaje por cambio de parámetros
    Dado un Admin que cambia el horizonte de urgencia de 240 a 4000 horas
    Cuando guarda el cambio
    Entonces la respuesta es inmediata y no espera el recálculo
    Y se pide refrescar el puntaje sin esperar el plazo
    Y la bandeja recibe un aviso parametros-recalibrados
    Y el componente de urgencia de una solicitud a 30 días de su deadline pasa de 0 a 82

  @priorizacion
  Escenario: Cambiar un peso mueve el puntaje
    Dado que el total del puntaje es la suma ponderada de urgencia, valor, probabilidad y tipo
    Cuando el Admin cambia el peso de urgencia de 0.40 a 0.60 y ajusta otro para que sumen 1.0
    Entonces se revisan todas las solicitudes activas
    Y las que no tienen los 4 componentes iguales quedan con un puntaje distinto

  @priorizacion
  Escenario: Pesos que no suman uno se rechazan
    Dado un Admin que envía pesos que suman 1.3
    Cuando guarda el cambio
    Entonces la respuesta es 400 PARAMETROS_SCORE_INVALIDOS
    Y los parámetros guardados no cambiaron

  @priorizacion
  Escenario: El recálculo falla sin afectar mi trabajo
    Dado un recálculo del puntaje que falla
    Cuando se agotan sus reintentos
    Entonces el mensaje queda en la cola de error para alertar al equipo
    Y el API sigue atendiendo con normalidad
    Y el plazo siguiente sigue programado

  # ---------------------------------------------------- bandeja

  @bandeja
  Escenario: Una solicitud nueva entra priorizada a la bandeja
    Dado un cliente con la bandeja abierta y suscrito al canal
    Cuando se da de alta una solicitud
    Entonces recibe un aviso con motivo solicitud-nueva
    Y al reconsultar con sus filtros la ve en la posición que le toca por puntaje

  @bandeja
  Escenario: El aviso es el mismo para todos y la reconsulta respeta lo que cada uno ve
    Dado un Comercial y un AdminTarifas con la bandeja abierta y suscritos al canal
    Cuando otro Comercial da de alta una solicitud
    Entonces los dos reciben el mismo aviso solicitud-nueva
    Y al reconsultar, el AdminTarifas ve la solicitud nueva
    Y el Comercial no la ve, porque solo ve las solicitudes que creó

  @bandeja
  Escenario: Rechazar una solicitud avisa a la bandeja
    Dado un cliente con la bandeja abierta y suscrito al canal
    Cuando se rechaza una solicitud
    Entonces recibe un aviso con motivo solicitud-rechazada

  @bandeja
  Escenario: Un pliego grande produce un solo aviso de bandeja
    Dado un pliego de 300 líneas costeándose
    Cuando termina el trabajo
    Entonces se recibe UN aviso de bandeja con motivo trabajo-terminado, no uno por línea
    Y el canal de avance sí recibió uno por cambio

  @bandeja
  Escenario: La bandeja se refresca aunque no cambie ningún puntaje guardado
    Dado que la urgencia depende del momento y el orden se mueve solo
    Cuando corre el recálculo periódico sin actualizar ninguna fila
    Entonces igual se avisa con motivo refresco-periodico

  @bandeja
  Escenario: La consulta de la bandeja sigue siendo el respaldo
    Dado un cliente que nunca se conectó al canal de la bandeja
    Cuando consulta la bandeja
    Entonces obtiene el orden correcto y completo
    Y no necesitó ningún aviso para saberlo

  # ---------------------------------------------------- entrega y notificación

  @notificacion
  Escenario: El correo falla y la entrega no se afecta
    Dado un relay de correo caído
    Cuando se entrega una cotización
    Entonces la respuesta es inmediata y la cotización queda Entregada
    Y la respuesta dice que la notificación quedó encolada, sin decir si salió ni a quién

  @notificacion
  Escenario: La notificación de entrega es interna
    Dado una cotización entregada
    Cuando el consumidor manda el correo
    Entonces el destinatario es el usuario que creó la solicitud
    Y ninguna dirección del cliente puede ser destinataria

  @notificacion
  Escenario: Un rechazo sin arreglo no gasta reintentos
    Dado un adjunto que pesa más de lo que el relay admite
    Cuando el consumidor intenta enviar el correo
    Entonces no se reintenta
    Y el fallo queda en la cola de error al primer intento

  @notificacion
  Escenario: Un fallo pasajero se reintenta antes de alertar
    Dado un relay de correo que no responde
    Cuando el consumidor intenta enviar el correo
    Entonces se reintenta de forma escalonada
    Y solo al agotar los intentos el fallo queda en la cola de error

  @notificacion
  Escenario: El correo nunca se manda dentro de la petición de entrega
    Dado el caso de uso que entrega la cotización
    Cuando se revisan sus dependencias
    Entonces no tiene ninguna salida de correo entre ellas

  @conexos @notificacion
  Escenario: Agregar un conexo a una cotización entregada avisa al comercial
    Dado una cotización Entregada de una solicitud creada por un Comercial
    Cuando un AdminTarifas le agrega un servicio conexo
    Entonces la cotización queda con la marca de modificada tras la entrega
    Y su estado y su fecha de entrega no cambian
    Y el Comercial recibe un correo de cotización actualizada con el nuevo total

  @conexos @notificacion
  Escenario: Quien creó la solicitud no recibe aviso de su propio cambio
    Dado una cotización Entregada de una solicitud creada por un Comercial
    Cuando ese mismo Comercial le quita un servicio conexo
    Entonces la cotización queda con la marca de modificada tras la entrega
    Y no se envía ningún correo

  @conexos
  Escenario: Una cotización con resultado no admite cambios de conexos
    Dado una cotización marcada como Ganada
    Cuando alguien intenta agregarle o quitarle un servicio conexo
    Entonces la respuesta es 409 COTIZACION_CERRADA
    Y la cotización no cambia
