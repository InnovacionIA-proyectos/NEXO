# QA — PR #384: Puerto y adaptador de Anthropic para las funciones de IA

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/384
**Autor**: ivasco-dev
**Rama**: `350-puerto-adaptador-anthropic` → `QA`
**Cierra**: #350
**Depende de**: ADR de #383 (ya mergeada). **#385 está apilada sobre esta PR** (contiene su commit `d240fc7`): mergear en el orden #384 → #385.

## Qué cambia

- `IAsistenteIaPort` (Application) y `clsAnthropicAdapter` (Infrastructure) sobre el SDK oficial de Anthropic para .NET (paquete NuGet `Anthropic 13.3.0`).
- Sección de configuración `Anthropic` (`clsOpcionesAnthropic`): llave solo por entorno, modelo y esfuerzo configurables, timeout, reintentos y un interruptor por función, **todo apagado por defecto**.
- Sin llave o con la función apagada la aplicación arranca y el puerto responde `503` con código estable (`IA_NO_CONFIGURADA` / `IA_FUNCION_DESHABILITADA`) sin crear el cliente ni tocar la red.
- Errores del proveedor traducidos a `503` con código; sin llave ni contenido en mensajes ni logs. Salida estructurada validada; sin `temperature`, `top_p` ni `top_k`.
- Registro en el composition root (`clsDependencyInjection.cs`) y bloque nuevo en `.env.example` (llave vacía, funciones en `false`).

## Cómo se verificó

1. Checkout de la rama con `QA` integrada: **sin conflictos**.
2. Revisión del código real (`clsAnthropicAdapter.cs`, `clsOpcionesAnthropic.cs`, DI, `.env.example`, tests):
   - Los logs registran función, modelo, tokens, latencia y resultado; nunca la llave ni el contenido de la petición. Ante un error solo se registra el código, el tipo de excepción y el estado HTTP.
   - Sin llave el adaptador no construye el `AnthropicClient`; no hay lectura de `ANTHROPIC_API_KEY` ni de otras variables de entorno en `src/` ni en los tests de IA (el cliente solo se crea con la llave de las opciones).
   - **Ningún test puede llegar a la red real**: la única construcción del adaptador en los tests (`clsAnthropicAdapterTests.cs`) siempre pasa un `HttpClient` falso y la URL `http://proveedor.prueba`; las pruebas de configuración usan configuración en memoria y un puerto falso reutilizable (`clsAsistenteIaFalso`).
3. Escaneo de secretos del diff (patrones `sk-ant-`, token GitHub, clave AWS, llave privada PEM): 3 coincidencias, **las 3 son llaves de prueba** (28 caracteres, con texto como «falsa»/«prueba»; una llave real de Anthropic es bastante más larga). Sin secretos reales.
4. Paquete NuGet nuevo: leído su `.nuspec` en la caché local tras restaurar — autor `Anthropic`, repositorio `anthropics/anthropic-sdk-csharp`, licencia Apache-2.0, paquete firmado.
5. Corrida con `ANTHROPIC_API_KEY` y `Anthropic__ApiKey` forzadas a vacío (así nada podía autenticarse contra Anthropic):
   - Build Release: 0 errores.
   - Tests de IA: **30/30** ✅
   - `Nexo.Domain.Tests` **218/218** ✅ · `Nexo.Application.Tests` **533/533** ✅
   - `Nexo.Api.Tests` completo contra la BD real: **775 pasados, 10 omitidos** de 785, 0 fallos ✅ (regresión del arranque, porque la PR toca el composition root). Los 10 omitidos son los de siempre en este ambiente: 6 E2E de Cubicador, 2 de `clsMailSenderRealTests`, 2 de `clsHistoricoSapPaginacionTests`.

## Prueba de mutación

Se eliminó temporalmente de `CompletarAsync` la guarda de «función apagada» (`IA_FUNCION_DESHABILITADA`) y se corrieron los 30 tests de IA: **2 fallan** (`Con_la_funcion_apagada_responde_IA_FUNCION_DESHABILITADA_sin_llamar_al_proveedor` y `Con_llave_las_funciones_nacen_apagadas_y_responden_IA_FUNCION_DESHABILITADA`). Después se restauró el archivo (`git status` limpio salvo esta carpeta). Los tests de la propiedad de seguridad central, «todo nace apagado», no son vacuos.

## Datos de prueba

Ningún test de esta PR escribe en la BD ni llama a un proveedor externo; solo arrancan la aplicación con configuración en memoria.

## Observaciones (no bloqueantes)

1. **Modelo por defecto.** Esta PR deja `claude-sonnet-4-6` (coherente con el ADR de #383) y los tests lo asertan; #385 lo cambia a `claude-sonnet-5-5` y debe actualizar esas aserciones. Por estar apilada no hay conflicto si se mergea en orden.
2. **Revisión de #385 pendiente:** al ser apilada, su diff real son solo los cambios propios de #385.
3. **Verificación contra la API real:** esta PR no se probó (a propósito) contra `api.anthropic.com`; la compatibilidad real del SDK con el modelo y los parámetros (`output_config`, `thinking: adaptive`) solo se comprueba con una llamada real, que requiere llave y las aprobaciones de §9 del ADR. Queda para quien active la primera función.

## Resultado

Sin hallazgos bloqueantes.
