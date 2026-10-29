# QA — PR #420: Ruta de NEXO para la explicación de la recomendación del Cubicador (#419)

## Resumen

Nuevo endpoint `GET /api/cubicacion/explicacion?idLineaSolicitud=&origenSistema=`
que expone por qué el Cubicador eligió una flota, en lenguaje natural
(texto generado por un modelo). Mismos parámetros, validación y roles que
el endpoint de consulta existente (`GET /api/cubicacion`).

- **`ICubicadorExplicacionPort`**: contrato explícito de que **nunca
  lanza** y es puramente opcional — sin configurar, con el Cubicador
  caído, sin responder, con una respuesta ilegible o sin recomendación
  para esa línea, devuelve `null` y la pantalla simplemente no muestra el
  bloque. Por aquí no pasa ninguna tarifa ni valor comercial, solo texto.
- **`clsCubicadorExplicacionAdapter`**: cliente HTTP **aparte** del de la
  recomendación normal (`clsCubicacionRapidaAdapter`), con su propio
  timeout configurable (`Cubicador3D:ExplicacionTimeoutSegundos`) —
  razonable porque el texto lo escribe un modelo y tarda más que la
  recomendación determinística. Cualquier excepción de red/tiempo/JSON se
  atrapa y traduce a `null` con un log de advertencia, nunca se propaga.
  Usa la misma llave dedicada que la recomendación
  (`Cubicador3D:RecomendacionesApiKey`).
- **`clsCubicadorExplicacionNoConfiguradoAdapter`**: sin
  `Cubicador3D:OutboundBaseUrl`/`RecomendacionesApiKey` configurados,
  devuelve `null` directamente — confirmado que no es un error, es el
  comportamiento esperado por diseño.
- El controller valida los parámetros obligatorios igual que el endpoint
  de consulta existente, y responde **siempre 200** con
  `{ "explicacion": texto | null }` (nunca 404/503 por esto).

## Qué se verificó

1. Diff aislado de la PR (1 commit, 10 archivos, merge limpio sin
   conflictos contra `QA`).
2. Revisado que el adaptador atrapa exactamente las excepciones esperadas
   (`HttpRequestException`, `TaskCanceledException`, `JsonException`,
   `NotSupportedException`) y respeta la cancelación real del request
   (no las enmascara si el cliente cerró la conexión).
3. Confirmado que los roles del nuevo endpoint
   (`Comercial,AdminBiblioteca,AdminTarifas,Admin`) son idénticos a los
   del endpoint de consulta ya existente.
4. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **1182/1182** (sin
   cambios: la lógica nueva vive en Api/Infrastructure).
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **1140/1163, 1 error** (22
   omitidas esperadas de siempre). Los **16 tests nuevos de esta PR**
   (`clsCubicadorExplicacionTests.cs`), aislados, pasan **16/16** en
   verde. El único error de la corrida completa es
   `clsServiciosConexosTests.Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio`,
   **el mismo hallazgo ya documentado en la issue #421** (acumulación de
   filas "Precinto" en la BD compartida, ajeno a esta PR) — confirmado
   reproduciendo exactamente el mismo error aislado.

## Datos de prueba

Ninguno manual: los tests nuevos no tocan la BD directamente (consultan
un `IServiciosConexosPort`/`ICubicadorExplicacionPort` mockeado en
Application, o llaman al endpoint HTTP en Api.Tests, que no persiste
nada propio).

## Resultado

Verificación técnica completa, sin hallazgos nuevos. El único error de la
suite completa ya está documentado y es ajeno a esta PR (issue #421).
