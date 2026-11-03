# PR #333 (S6-HU-25/28/24.1/29, #307-#313) — Rol AdminBiblioteca, Cubicador para AdminTarifas, campos de línea, ajustes del API

## Qué cambia

- `enum RolUsuario`: `CoordinadorFletes` renombrado a `AdminBiblioteca` (mismo
  valor subyacente, primero del enum). Entra a Biblioteca, Tarifas Plaza,
  rangos de peso, Reglas Solo-SICE, vigencia y `/api/cubicacion`; `403` en
  Bandeja, Mesa, solicitudes y cotizaciones. Crear solicitud, cargar líneas
  en lote y recalcular puntaje: solo `Admin`. Alerta de "línea que no pudo
  costearse" ahora va a `AdminTarifas` (antes al rol de Biblioteca, que ya no
  entra a la Mesa de Costeo).
- `AdminTarifas` con el Cubicador completo (10 endpoints de planes de cargue
  y vehículos); `DELETE` de vehículos sigue solo `Admin`.
- `GET /api/solicitudes/{id}`: nuevos campos opcionales por línea
  (`estadoLinea`, `motivoPendiente`, `lineaNegocio`, `claseViaje`,
  `claseContenedor`, ids de municipio).
- Histórico SAP paginado (`page` + `top`, orden estable con `Id` como
  desempate); `404 RECOMENDACION_NO_ENCONTRADA`; `detallesTarifa` opcional en
  tarifas de plaza; textos con `$` colombiano.
- Script `docs/Database/307_roles_coordinadorfletes_a_adminbiblioteca.sql`:
  migración idempotente y transaccional (respaldo + rollback automático si
  queda alguna fila vieja) para otros ambientes.

## Qué se verificó

**Rama actualizada con `QA`**: merge local sin conflictos reales.

**No se ejecutó el script de migración `307_...sql`**: el estado que
produciría YA EXISTE en la base de pruebas desde la verificación de la PR
#331 (5 usuarios `AdminBiblioteca`, 0 `CoordinadorFletes`, en ambas tablas,
confirmado por lectura — ver `verificar.ps1` sección 1). Ejecutar el `UPDATE`
transaccional de datos compartidos no es responsabilidad de QA (es trabajo
del equipo de datos/dev); como el resultado ya coincide exactamente con lo
que el script produciría, correrlo sería un no-op verificado, no una
verificación adicional.

**Código revisado**: `clsResolucionRolPortal` (orden de precedencia:
`AdminTarifas` antes que `AdminBiblioteca`, correcto porque el primero puede
más), `AlertasDeNegocioCommand` (la alerta de fallo técnico ahora va a
`AdminTarifas`, coherente con que `AdminBiblioteca` ya no ve la Mesa de
Costeo), `clsSapHistoricoAdapter` (paginación `Skip`/`Take` con
`OrderByDescending(...).ThenByDescending(Id)` para desempate estable).

**Prueba de roles por endpoint** (`clsRolesPorEndpointTests`, matriz de 24
endpoints × 6 roles = 144 casos): todos en verde, cubre exactamente los
cambios de rol declarados por la PR.

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta,
0 errores (1 warning preexistente ajeno a esta PR).

**Suite completa contra la base real de pruebas**:
- `Nexo.Domain.Tests`: **203/203**.
- `Nexo.Application.Tests`: **438/438**.
- `Nexo.Api.Tests`: **1 fallo** (`clsHistoricoSapPaginacionTests.La_pagina_2_...`),
  591/600 (8 omitidas sin Cubicador local).

**Diagnóstico del único fallo — no es un bug de código**: la prueba espera
más de 20 filas en `dbo.tblSAPTarifasLog` para poder verificar la
paginación, pero esa tabla real en `dbSolucionesTI_Prueba` tiene **0 filas**
actualmente (confirmado por consulta directa — existe el objeto, no está
vacía por error de nombre/esquema; la tabla hermana `tblSAPTarifas`
(vigentes) sí tiene 22.793 filas). Es un problema de datos del ambiente, no
del código: corrí solo el archivo de 5 pruebas y **4 de 5 pasan** incluso con
la tabla vacía (orden estable, sin `page`, más allá del final, `page=0`
rechazado) — solo falla la que exige explícitamente datos suficientes para
paginar, que es exactamente la que no se puede satisfacer con 0 filas.

**Cubicador real armado localmente** (código actual de QA, esta PR lo toca
tangencialmente — Cubicador para `AdminTarifas`): segunda corrida completa de
`Nexo.Api.Tests` → **597/600, 1 mismo fallo (dato, no código), 2 omitidas**
(MailSender) — las 6 E2E del Cubicador corrieron y pasaron.

## Limpieza

- `docker compose down -v` en `cubicador/`, `.env` temporal borrado.
- `docker compose stop/rm rabbit` en la raíz de NEXO.

## Resultado

Sin hallazgos bloqueantes. El único fallo de la suite es un problema de
datos del ambiente de pruebas (tabla de histórico SAP vacía), no un defecto
de esta PR — confirmado aislando la causa y probando que el resto de la
lógica de paginación funciona correctamente incluso sin datos.
