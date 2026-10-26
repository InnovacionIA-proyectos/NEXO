# QA — PR #383: ADR de llave, datos que salen y modelo para usar Claude en NEXO

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/383
**Autor**: ivasco-dev
**Rama**: `349-adr-seguridad-datos-ia` → `QA`
**Refs**: #349 (la PR usa `refs`, no `closes`: el issue se cierra a mano cuando se aprueben las pendientes de §9 del ADR)

## Qué cambia

Solo documentación: `docs/decisiones/ia-claude-seguridad-y-datos.md` (98 líneas). Propone: llave solo en el servidor (una por servicio, con límite de gasto), clasificación de datos en tres niveles (A nunca sale / B minimizado / C libre), enmascarado del Excel, tratamiento del contenido no confiable, retención del proveedor, modelo por defecto y reglas de pruebas. Queda como **propuesta pendiente** de 6 aprobaciones (§9).

La lista de archivos que muestra GitHub incluye `src/` y un test: es arrastre de #343 por estar la rama desfasada respecto a `QA`. El aporte real (`git diff origin/QA...rama`) es un único archivo.

## Cómo se verificó

PR documental: no hay código de producto que ejecutar, así que la verificación es contrastar las afirmaciones del ADR con el repositorio y revisar el diff.

1. Checkout de la rama de la PR y `merge origin/QA --no-ff`: **sin conflictos**.
2. Diff real contra `QA`: 1 archivo, +98 / −0.
3. Búsqueda de secretos en el diff (patrones de llave de Anthropic, token de GitHub, clave AWS, llave privada PEM): **0 coincidencias**. El ADR solo nombra variables (`Anthropic__ApiKey`, `Anthropic__Model`, `Anthropic__Effort`), nunca valores.
4. Afirmaciones del ADR contrastadas con el repo:
   - `Cubicador3D__OutboundApiKey` existe (`.env.example`, `docker-compose.yml`). ✅
   - El Cubicador no acepta llamadas de navegador: no hay CORS (`cubicador/CONTRATO.md`). ✅
   - «`.env` y `.env.*` ya están en `.gitignore`»: el patrón existe, pero `.env.dokploy`, `.env.githubSECRETS` y `.env.example` **están versionados**. Se clasificó el contenido de los dos primeros sin imprimir valores: son plantillas, con contraseñas y valores en marcador `<...>`, sin credenciales reales. ⚠️ La frase es imprecisa (ver observaciones).
   - Los campos `Notas` y `Referencia` de la solicitud (nivel A): una búsqueda de `public string Notas/Referencia` en `src/Nexo.Domain` no los encontró con ese formato; no se confirmó el nombre exacto. ⚠️ No bloqueante.
5. Precios y comportamiento de la API de Anthropic citados en §6 (US$/millón de tokens, rechazo de `temperature` desde 2026-10-21): **no verificables desde el repo**; los valida quien apruebe §6 contra la documentación del proveedor.

## Observaciones (ninguna bloquea el merge de un ADR en estado «propuesta»)

1. **§6 contradice a #385.** El ADR fija `claude-sonnet-4-6` como modelo por defecto («lo pedido») y deja `claude-sonnet-5-5` como alternativa; #385 implementa `claude-sonnet-5-5` como `ModeloPorDefecto` (`clsOpcionesAnthropic.cs`, `.env.example`, `.env.dokploy`). Debe alinearse un lado u otro, o aclarar en el ADR que la aprobación de «modelo y esfuerzo» (§9, Producto) decide.
2. **§1, `.gitignore`.** Mejor decir que la llave real nunca se versiona y que lo versionado son plantillas con marcadores; los archivos `.env.*` ya rastreados no quedan protegidos por el patrón de ignore.
3. **Fecha del ADR (2026-10-27)** posterior al día de la revisión (2026-10-26).
4. **Aprobaciones de §9: 6 de 6 pendientes.** Mientras no se marquen, ninguna función de IA debe enviar datos reales a ningún ambiente. Es un requisito para las PRs #384–#386, no para este ADR.

## Resultado

Sin hallazgos bloqueantes: es un documento coherente, sin secretos y sin conflictos. Las observaciones 1 a 3 son correcciones de redacción y alineación.
