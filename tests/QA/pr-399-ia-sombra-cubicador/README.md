# QA — PR #399: Modo sombra de la IA en el Cubicador (#367)

## Resumen

Agrega al Cubicador (servicio Python) un modo sombra: cada `POST
/recomendaciones` agenda, en segundo plano, una comparación de lo que
propondría un modelo de Claude contra lo que ya decidió el motor determinista,
y registra la diferencia en una tabla nueva (`dbo_cubicador.iaSombra`). **No
cambia la respuesta al usuario ni la retrasa.**

- **Regla central verificada en el código** (`servicio.py`): `programar(...)`
  nunca lanza, decide en el acto si corresponde (interruptor apagado, sin
  llave, muestreo, cupo de pendientes, tope diario de tokens) y agenda la
  comparación como una tarea `asyncio` independiente que no comparte sesión de
  BD con la petición. En el router (`recomendaciones.py`), se llama **después**
  de `crear_recomendacion` y **sin `await`**, justo antes del `return` — la
  respuesta no depende de ella.
- **Comparación a ciegas**: el modelo no recibe la respuesta del motor
  (`comparador.py` solo se usa para comparar después, nunca se le pasa al
  prompt). El respeto de peso/volumen/unidades de la propuesta del modelo se
  **recalcula** con el catálogo, sin creerle al modelo — mismo criterio que el
  estudio comparativo de la PR #397.
- **Llave y gasto propios**: `ANTHROPIC_API_KEY`/`ANTHROPIC_WORKSPACE_ID` son
  de una *workspace* propia del Cubicador (`cubicador-api`), no se comparte con
  NEXO. La llave nunca se loguea ni aparece en `repr` (`ConfiguracionSombra.api_key`
  con `field(repr=False)`, y `anthropic_cliente.py` la enmascara explícitamente
  en los mensajes de error). Nace **apagada** (`IA_SOMBRA_HABILITADA=false` por
  defecto) y sin llave no se crea ningún cliente ni se toca la red.
- **Esquema nuevo y aditivo** (`cubicador/database/esquema/367_ia_sombra.sql`):
  tabla `dbo_cubicador.iaSombra`, idempotente (`IF OBJECT_ID... IS NULL`), con
  su propia verificación de solo lectura al final (cuenta tablas/columnas/
  índices/restricciones). No toca ninguna tabla existente.
- **Test de secretos endurecido** (`clsSecretosEnElRepositorioTests.cs`): ahora
  también detecta `ANTHROPIC_API_KEY` con valor asignado en archivos
  versionados, con sus propios casos de prueba.
- Documentación operativa actualizada: `docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md`
  gana la sección 7 (variables en Dokploy, cómo apagarlo, cómo verificar la
  salida de red) y `cubicador/.env.example` documenta cada variable con su
  valor por defecto (vacías/apagadas).

## Qué se verificó

1. Diff aislado de la PR (1 commit propio más los de integración con
   `QA`/#366, ya verificados en PRs anteriores — incluida la misma
   restauración de `AddDiccionarioClienteUseCases` en `clsProgram.cs`): el
   módulo `cubicador/application/ia_sombra/` completo, el cliente de Anthropic,
   el repositorio SQL, el esquema, el wiring en `dependencias.py`/`main.py`/
   `recomendaciones.py`, y el endurecimiento del test de secretos.
2. Revisado línea por línea que `servicio_sombra.programar(...)` en el router
   se llama sin `await` y después de armar la respuesta — confirma la regla
   "la respuesta no cambia ni se retrasa".
3. Revisado el manejo de la llave: nunca se escribe en logs (`_detalle`
   reemplaza la llave por `<llave>` antes de truncar el mensaje del
   proveedor), no tiene valor por defecto en `.env.example`, y el test de
   secretos del repo la cubre.
4. `367_ia_sombra.sql` revisado línea por línea: aditivo, idempotente, con
   `CHECK` para los campos JSON y una verificación de solo lectura.
5. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **901/901**.
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **874/887**, 13 omitidas: las
   esperadas de siempre (ya documentadas en PR #405).
9. `python -m pytest tests -q` dentro de `cubicador/` (con su propio
   `.venv`, instalando el paquete nuevo `anthropic` que trae esta PR) →
   **360/360**, incluidos los tests nuevos de `ia_sombra`
   (`test_servicio.py`, `test_comparador.py`, `test_anthropic_cliente.py`,
   `test_configuracion_propuesta_prompt.py`, `test_ia_sombra_api.py`).

## Datos de prueba

Ninguno contra la BD compartida de pruebas: no se aplicó
`367_ia_sombra.sql` ni se activó el modo sombra (se mantiene
`IA_SOMBRA_HABILITADA=false`, sin llave). La verificación fue por inspección
de código y por las suites de pruebas automatizadas (que usan un cliente de
IA simulado, nunca llaman al proveedor real).

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes. Pendiente de
autorización de Kevin para comentar/aprobar/mergear en GitHub.
