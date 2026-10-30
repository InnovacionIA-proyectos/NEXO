# QA — PR #387: Registro de uso de la IA, tope diario por persona y consulta de gasto

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/387
**Autor**: ivasco-dev
**Rama**: `355-registro-uso-ia` → `QA`
**Cierra**: #355
**Depende de**: #350/#384 (adaptador) y #352/#386 (tabla `tbl_ia_uso`), ambas ya mergeadas. Es la base de #390 y #391, que están apiladas sobre ella.

## Qué cambia

- `clsAsistenteIaConRegistro`: decorador scoped sobre el adaptador de Anthropic. Antes de llamar aplica el **tope diario de tokens por persona** (día de Colombia, UTC-5); después registra cada llamada en `tbl_ia_uso` (función, usuario, modelo, tokens, costo estimado, latencia, resultado), **con éxito o con error y sin guardar contenido**. Lo que nunca llegó al proveedor (sin llave, IA o función apagada) no se registra.
- Al superar el tope: `429 IA_TOPE_DIARIO` con `details: { limiteTokensPorDia, tokensConsumidosHoy, reiniciaEn }`, sin llamar al proveedor.
- Interruptor general `Anthropic__Habilitada` (`503 IA_APAGADA`) y `Anthropic__LimiteTokensPorUsuarioDia` (500 000 por defecto; `0` lo desactiva).
- `GET /api/ia/uso/gasto` (solo Admin): gasto por mes (calendario de Colombia) y función, con costo estimado.
- Mapeo de `tbl_ia_uso` en `NexoDbContext`, `clsPreciosIa` (precios de lista a mano; un modelo sin precio queda con costo `null`), `clsLimiteExcedidoException` (429), `clsIaRespuestaNoUtilException` (lleva lo gastado aunque la respuesta no sirva) y documentación (`CONTRATO.md` §3.22, roles, guía de despliegue).
- Sin cambios de base de datos (usa la tabla de #386).

## Cómo se verificó

1. Checkout de la rama con `QA` integrada (ya con #384–#386): **sin conflictos**. Diff real: 25 archivos, +1081/−16.
2. Revisión del código: decorador, adaptador, repositorio, consulta de gasto, mapeo del `DbContext`, DI, middleware, precios y documentación. `CONTRATO.md` y la guía coinciden con el DTO y con los códigos de error reales.
3. Escaneo de secretos del diff: 1 coincidencia, una llave de prueba obvia (28 caracteres, «falsa»).
4. Corrida con `ANTHROPIC_API_KEY` y `Anthropic__ApiKey` forzadas a vacío (nada podía autenticarse contra Anthropic):
   - Build Release: 0 errores.
   - Tests de IA: **69/69** ✅ (incluye los de integración con `tbl_ia_uso` real, el 429 por middleware y el endpoint de gasto, con los 6 roles).
   - `Nexo.Domain.Tests` **218/218** ✅ · `Nexo.Application.Tests` **533/533** ✅
   - `Nexo.Api.Tests` completo contra la BD real: **814 pasados, 10 omitidos** de 824, 0 fallos ✅ (regresión por `DbContext`, DI y middleware). Los 10 omitidos son los de siempre en este ambiente.
5. Ningún test llama a la API real: el adaptador se construye siempre con un `HttpClient` falso.

## Pruebas de mutación (los tests no son vacuos)

- **Sin la verificación del tope diario** (quitar `VerificarTopeAsync`): fallan **3** tests — dos unitarios (`Al_superar_el_tope_responde_429...`, `El_consumo_se_cuenta_desde_el_inicio_del_dia_de_Colombia`) y el de integración con la base real (`Cada_llamada_se_registra_en_la_base_y_al_pasar_el_tope_responde_429...`).
- **Sin el interruptor general** (quitar `Habilitada` del adaptador): falla **1** test (`Con_el_interruptor_general_apagado_...responde_IA_APAGADA`).
- Código restaurado idéntico a la rama tras cada una (`git status` limpio salvo esta carpeta).

## Datos de prueba en la base real

Los tests de integración escriben en `tbl_ia_uso` filas con una marca única (modelo `prueba-uso-<guid>` o función `PRUEBA_<guid>`) y las borran en un `finally` con `ExecuteDelete` (la aplicación no tiene camino de borrado). Insertan filas bajo el `Id` de un usuario real de la BD de pruebas, solo mientras dura cada test. Comprobado con consultas de solo lectura de conteo: **0 filas de prueba residuales** tras la corrida completa y tras las mutaciones (incluido un test de integración que falló a propósito).

La tabla tiene **2 filas que no son de QA ni de los tests**: `ImportacionExcel`, resultado `IA_PETICION_RECHAZADA`, 0 tokens, con modelos `claude-sonnet-4-6` y `claude-sonnet-5-5`, creadas a las 21:19:50 y 21:20:45 UTC, más de un minuto antes de que arrancara la corrida de QA (21:21:43). Parecen intentos reales contra la API que el proveedor rechazó (400); la causa no se puede saber porque la tabla no guarda el mensaje, por diseño. QA no las tocó.

## Observaciones (no bloquean)

1. **El registro usa el `DbContext` de la petición.** `RegistrarAsync` llama a `SaveChangesAsync` sobre el contexto scoped de la solicitud, que guarda **también cualquier otro cambio que la petición tenga pendiente** en ese contexto. Hoy no tiene efecto: ningún consumidor escribe (por lectura de código, no se demostró con un test). Sí importará para las funciones con escritura (#373, «herramientas de escritura con confirmación»). Sugerencia: registrar el uso con su propio contexto de vida corta (`IDbContextFactory`) o su propio scope.
2. **Tabla de precios (`clsPreciosIa`).** En `claude-opus-5-5` la lectura de caché cuesta 0,20 con entrada a 4; en los demás modelos es el 10 % de la entrada (3→0,30; 2→0,20; 0,10→0,01), y aquí sería 0,40. Puede ser un error de tipeo o un precio real: QA no puede comprobar los precios desde el repo. Importa porque el costo se guarda y no se recalcula, así que un precio mal puesto queda congelado en las filas.
3. **El tope es blando** (la PR lo documenta): se verifica antes de llamar, así que llamadas simultáneas, o una sola llamada grande, pueden pasarlo. Se mide en tokens, no en dinero.
4. **El interruptor general exige redespliegue:** las opciones se leen al arrancar (`IOptions`), no en caliente. En un incidente tarda lo que tarde el despliegue; la guía ya dice «redesplegar».
5. **El puerto de IA pasa de singleton a scoped.** Si más adelante un servicio singleton o un `BackgroundService` lo consume, la validación de scopes lo rechazará. Hoy nada lo consume.
6. **Sigue abierto el hallazgo de #385:** el log del error del proveedor (`DetalleDelProveedor`) escribe el cuerpo del error y puede repetir contenido enviado. Esta PR no lo toca.
7. La PR dice «Cierra #355».

## Resultado

Sin hallazgos bloqueantes.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-387-registro-uso-ia/verificar.ps1
```

Requiere `.env.pruebas-reales.local` en la raíz del repo, VPN corporativa conectada y `tbl_ia_uso` aplicada en la base de pruebas.
