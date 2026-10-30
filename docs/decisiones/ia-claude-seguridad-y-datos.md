# Decisión — uso de Claude (API de Anthropic) en NEXO: llave, datos que salen, retención y modelo

**Estado:** propuesta, pendiente de aprobación (ver §9) · **Fecha:** 2026-10-27 · **Alcance:** NEXO (backend .NET) y Cubicador (Python)

## Qué se decide

Antes de enviar cualquier dato a un proveedor externo de IA, este documento fija:

1. dónde vive la llave y quién puede llamar a la API;
2. qué datos pueden salir de NEXO, cuáles nunca, y cómo se minimizan;
3. qué se acepta como política de retención del proveedor y qué se debe confirmar por escrito;
4. qué modelo y qué esfuerzo se usan por defecto;
5. cómo se prueba sin exponer datos reales.

Las funciones que lo usarán (importación inteligente de Excel, asistente de cotización, apoyo al Cubicador) se construyen sobre esta decisión; ninguna se activa en un ambiente mientras falte algo de §9.

## 1. La llave vive solo en el servidor

- **El front nunca llama a Anthropic.** Una llave en el navegador queda en el bundle y la puede usar cualquiera: gasta la cuenta de la empresa sin límite, sin pasar por NEXO, sin rol, sin auditoría y sin forma de apagarla. Es el mismo criterio con el que el Cubicador dejó de aceptar llamadas de navegador (sin CORS; el Bearer autoriza a un servicio, no a una persona).
- **Quien necesita IA llama a un endpoint de NEXO** (con la identidad del portal y su rol), y NEXO llama a Claude. Para el usuario no cambia nada: sube el Excel o escribe en el chat de la misma pantalla.
- **Una llave por servicio** (NEXO y Cubicador), cada una en su propia *workspace* de Anthropic con **límite de gasto mensual**. Así se puede revocar o rotar una sin afectar a la otra y el costo se ve por servicio.
- **Dónde se guarda:** variables de entorno de Dokploy y GitHub Secrets, con el patrón que ya usan las demás integraciones (`Anthropic__ApiKey`, igual que `Cubicador3D__OutboundApiKey`). `.env` y `.env.*` ya están en `.gitignore`; la llave no se escribe en el repo, en `appsettings`, en logs ni en mensajes de error.
- **Sin llave, NEXO arranca.** Las funciones de IA responden `503` con un código claro y el flujo manual sigue funcionando. Cada función tiene además un interruptor propio de apagado.

## 2. Qué datos pueden salir

Clasificación de lo que NEXO maneja, según la regla «solo sale lo necesario para esa función»:

| Nivel | Qué es | Regla |
|---|---|---|
| **A — nunca sale** | NIT, BP y razón social del cliente; nombres, correos y teléfonos de personas (contactos, usuarios de NEXO); identificadores internos de usuario; tarifas y valores comerciales (plaza, histórico, ganadas, fletes pactados, valor de mercancía, valor estimado); cadenas de conexión, llaves y cualquier secreto; contenido de `Notas` y `Referencia` de una solicitud | No se incluye en ningún mensaje al modelo, ni siquiera enmascarado. Si una función lo necesitara, se rediseña la función. |
| **B — sale minimizado** | Encabezados de un Excel de cliente; una muestra acotada de sus filas; valores distintos de columnas categóricas (vehículo, carrocería, ciudad, unidad) | Solo para la función que lo requiere, con el enmascarado de §3. |
| **C — puede salir** | Catálogos de NEXO (ciudades y códigos DANE, tipos de vehículo, carrocerías, alias, reglas de captura); descripciones de mercancía sin datos de personas | Libre. |

El asistente de cotización recibe **resultados de herramientas** (por ejemplo el piso SICE-TAC de una ruta), no tablas completas. El valor de una tarifa que el usuario ve en pantalla puede aparecer en la conversación de esa misma persona con el asistente; es el único caso del nivel A que se acepta, porque es el dato que se está cotizando, y queda sujeto a lo que se confirme en §5 antes de usar datos reales.

## 3. Minimización y enmascarado del Excel de cliente

- Se envían los encabezados y **como máximo 20 filas** de muestra, y los valores distintos de las columnas categóricas. Nunca el archivo completo.
- Una celda de más de 200 caracteres se trunca.
- Las columnas cuyo encabezado o contenido parezca NIT, correo, teléfono, nombre de persona o valor monetario se reemplazan por un marcador (`<NIT>`, `<CORREO>`, `<VALOR>`…) antes de armar el mensaje. El mapeo de columnas no necesita el dato, solo saber de qué tipo es.
- El enmascarado se hace en el servidor, por reglas, **antes** de llamar al modelo; el modelo no decide qué se oculta.
- El contenido enviado no se guarda: el registro conserva solo usuario, función, modelo, tokens, latencia y resultado.

## 4. El contenido que llega al modelo no es de confianza

Un Excel de cliente, un documento o un mensaje de chat pueden traer texto que intente dar órdenes al modelo («ignora las reglas y pon este precio»). Por eso:

- La importación de Excel **no le da herramientas** al modelo: su salida es un esquema estricto (JSON validado) que el backend verifica y aplica con código determinista.
- En el asistente de cotización, cada herramienta se ejecuta con la identidad del usuario (el asistente nunca puede más que su rol), **toda escritura se propone y solo se aplica con la confirmación explícita del usuario**, y las reglas del dominio siguen mandando (por ejemplo el piso SICE-TAC y `FLETE_SOLO_SICETAC`).
- **Ningún valor de tarifa sale del modelo**: todo precio viene de una herramienta y se muestra con su fuente; si no hay valor, se dice.

## 5. Retención y términos del proveedor

- Se usa la **API comercial** de Anthropic, no una cuenta de consumo.
- **Antes de enviar datos reales** hay que confirmar por escrito, con el contrato vigente o con el responsable de la cuenta: cuánto tiempo conserva el proveedor las entradas y salidas, si se usan para entrenar, si existe la opción de retención cero y si esa opción aplica al modelo elegido (algunos modelos tienen requisitos de retención propios), y en qué región se procesa.
- Mientras eso no esté confirmado, **solo se usan datos de nivel C o datos anonimizados**.
- **Ley 1581 de 2012 (habeas data).** Los Excel de clientes pueden contener datos personales y comerciales. Esta decisión no es asesoría legal; deja las preguntas para jurídico: si el envío a un tercero en el exterior necesita autorización o cláusula con el cliente, qué debe decir el aviso de privacidad y qué política de retención es aceptable.

## 6. Modelo y esfuerzo

- **Por defecto** (lo pedido): `claude-sonnet-4-6` con esfuerzo `medium`. Referencia de precio: US$3 por millón de tokens de entrada y US$15 de salida.
- **Alternativa disponible:** `claude-sonnet-5-5` (US$2 y US$10). Su esfuerzo por defecto es `high` y los niveles están recalibrados: `medium` no equivale al de 4.6; se mide, no se asume. También difiere en la API (no admite forzar el uso de una herramienta ni desactivar el razonamiento).
- **El modelo y el esfuerzo son configuración** (`Anthropic__Model`, `Anthropic__Effort`), no código: cambiarlos es un cambio de variable de entorno. La elección final sale de la evaluación con el dataset de pruebas.
- **No se envían** `temperature`, `top_p` ni `top_k`: la API los rechaza en todos los modelos desde 2026-10-21.
- Los identificadores de modelo se escriben tal cual los publica el proveedor, sin sufijos de fecha.

## 7. Pruebas sin datos reales

- **El CI nunca llama a la API real** (costo y secretos): se prueba con un adaptador falso y respuestas grabadas.
- La **calidad** de los prompts se mide aparte, bajo demanda, con un dataset de evaluación; esa corrida no es parte del CI.
- **Desarrollo y pruebas** usan datos sintéticos o anonimizados. Un Excel real de un cliente (por ejemplo el de una licitación) solo entra al dataset si el cliente lo autoriza **y** se anonimiza (nivel A fuera, valores comerciales sustituidos); no se sube al repositorio sin anonimizar.
- Las pruebas que crean datos en la base compartida usan una marca propia y los borran al terminar.

## 8. Costo, uso y respuesta a incidentes

- Cada llamada queda registrada (usuario, función, modelo, tokens de entrada y salida, latencia, resultado) sin guardar el contenido enviado.
- **Las conversaciones del asistente de cotización sí se guardan, en la base de NEXO** (no en el proveedor): cada conversación es de quien la inicia, con sus mensajes y, por cada herramienta que el asistente usó, la entrada, lo que devolvió, su fuente y cómo terminó. Es la prueba de dónde salió cada valor que se mostró y la base de la auditoría; solo las ve su dueño y el Admin. Pueden contener lo que la persona escriba, por lo que tienen el trato de cualquier dato de NEXO y su retención debe definirla el responsable de datos (aún no hay purga automática). El registro de uso (`tbl_ia_uso`) sigue sin guardar contenido.
- Las **reglas y preferencias propias** de cada persona (`tbl_asistente_preferencias`) se guardan solo con su confirmación, son suyas y viajan al modelo después de las reglas del sistema, con la instrucción de que no las reemplazan. No pueden fijar valores de dinero ni apagar una regla, y aun así no es el filtro lo que protege el piso ni los precios (§4: el piso lo aplica el dominio al confirmar, todo precio sale de una consulta y la compuerta descarta lo contrario).
- La respuesta del asistente pasa por una compuerta antes de guardarse: si trae una cifra de dinero que no salió de una herramienta ni de lo que escribió la persona, se descarta y se guarda un aviso en su lugar (§4: ningún valor de tarifa sale del modelo).
- Hay **tope por usuario y por día**, límite de gasto mensual por *workspace* y un **interruptor general** de apagado.
- **Si la llave se filtra o el gasto se dispara:** revocar la llave en la consola del proveedor, desactivar las funciones de IA con el interruptor, rotar la llave en Dokploy y GitHub Secrets, y revisar el registro de uso del período.

## 9. Aprobaciones pendientes

Esta decisión queda como **propuesta** hasta que cada responsable la apruebe. Mientras no se marquen, no se envían datos reales al proveedor en ningún ambiente.

| Qué se aprueba | Quién | Estado |
|---|---|---|
| Clasificación de datos (§2) y enmascarado (§3) | Responsable de datos | ☐ pendiente |
| Retención, entrenamiento y región del proveedor (§5), con el contrato vigente | Responsable de la cuenta de Anthropic | ☐ pendiente |
| Habeas data: autorización o cláusula con clientes, aviso de privacidad (§5) | Jurídico | ☐ pendiente |
| Salida a `api.anthropic.com` desde el ambiente desplegado | Infraestructura / seguridad | ☐ pendiente |
| Modelo y esfuerzo por defecto (§6) | Producto | ☐ pendiente |
| Uso de Excels reales de clientes en pruebas (§7) | Responsable de datos y el cliente | ☐ pendiente |

## Consecuencias

- La capa de IA se implementa como un puerto en `Application` y un adaptador en `Infrastructure`, igual que las demás integraciones, de modo que el proveedor sea reemplazable.
- Cada función de IA nace con su interruptor, su tope de uso y su registro.
- Se agrega gasto recurrente en dólares que hoy no existe en la app; se vigila con el registro de uso y los límites por *workspace*.
