# ADR-001 — La identidad del portal viaja sin firma

**Estado:** aceptada · **Fecha:** 2026-08-24 · **Issue:** #7 · **Contrato:** v1.0.0

## Contexto

El portal de la Administración deja los datos de la persona en el navegador
después del inicio de sesión. Ahí hay identificador, nombre y la lista de
permisos.

Lo que no hay es un token de sesión, una firma, ni una fecha de vencimiento. Nada
que Nexo pueda verificar contra el servidor del portal para confirmar que quien
dice ser alguien realmente lo es.

Pedir esa pieza significa abrir una solicitud al equipo del portal y esperar. El
sprint dura dos semanas.

## Decisión

Nexo acepta el identificador que le envía el navegador sin verificarlo, y resuelve
los permisos contra la tabla de la administración.

## Qué protege y qué no

Esto **sí** cierra la falsificación de permisos. Alguien puede editar el
almacenamiento del navegador y ponerse el rol que quiera: Nexo no lo mira. Va a la
tabla con el identificador y usa lo que la tabla diga. El escenario que la
auditoría de QA dejó como criterio vigente, que un Comercial no pueda alcanzar
funciones de AdminTarifas manipulando el navegador, se cumple.

Esto **no** cierra la suplantación de identidad. Cambiar el identificador por el de
otra persona devuelve los permisos de esa persona. El sistema funciona
exactamente como está diseñado y aun así quien llamó no es quien dice.

También significa que la auditoría registra un autor que puede no ser real. Toda
traza de "quién ajustó esta tarifa" hereda esta debilidad.

## Por qué se acepta

El módulo corre en la red interna de la compañía. Solo lo alcanza personal de
Botero Soto, y la explotación exige abrir las herramientas de desarrollo del
navegador, entender qué es el almacenamiento local y conocer el identificador de
otra persona.

El riesgo residual se consideró bajo frente al costo de bloquear la integración
completa esperando una pieza que depende de otro equipo.

La decisión es del líder técnico y quedó registrada en la issue #7.

## Cuándo hay que volver a mirar esto

Cualquiera de estas cosas invalida el razonamiento de arriba:

- El módulo deja de estar solo en la red interna.
- Aparecen usuarios externos, contratistas o proveedores en el portal.
- La administración empieza a emitir tokens de sesión, por el motivo que sea.
- Una auditoría o una obligación de cumplimiento exige trazabilidad no repudiable.

## Qué costaría cerrarlo

Poco, y ese es el punto de haberlo diseñado así.

El identificador viaja en un encabezado propio, `X-Portal-Usuario-Id`, y nada
más allá del punto de entrada lo conoce. La resolución de permisos ya va contra
la base. El día que exista un token, se reemplaza ese encabezado por
`Authorization` y se agrega su verificación. El resto de la cadena, los seis
roles, la matriz de autorización y todos los controladores, no se entera.

## Alternativas descartadas

**Login propio en Nexo.** Otra contraseña más para el usuario, otro almacén de
credenciales que proteger, y contradice la decisión de que la identidad la
gobierna el portal.

**Confiar en la lista de permisos del navegador.** Más barato de implementar y
peor en todo lo demás: reabre la falsificación de roles, que es el agujero grande
y el que sí podíamos cerrar gratis.

**Esperar el token antes de seguir.** Bloquea la HU-02 y la HU-03 completas contra
un tercero sin fecha comprometida. Es el hotspot H2, y el sprint no lo aguanta.
