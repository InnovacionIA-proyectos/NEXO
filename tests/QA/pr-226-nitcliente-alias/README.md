# PR #226 — NitCliente como alias de Nit en la búsqueda de clientes/prospectos

## Qué se verificó

Que `clsClienteBusquedaDto.NitCliente` (alias calculado de `Nit`, agregado para que
Cotifly pueda leer el NIT con su propio nombre de campo) devuelve siempre el mismo
valor que `Nit`, tanto para clientes reales (`DimBPSolicitante`) como para prospectos
(`tblProspectos`), sin romper el campo `nit` que ya consume Captura de Solicitudes.

## Cómo correrlo

```bash
# Build + las 3 suites, incluidas las 2 aserciones nuevas de NitCliente en
# clsBuscarClientesQueryTests (cliente real y prospecto).
docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml --env-file .env.pruebas-reales.local \
  run --rm pruebas-reales-completas
```

`.env.pruebas-reales.local` (no versionado, gitignored) debe traer al menos
`ConnectionStrings__Nexo` y `Database__Esquema` apuntando a la base real de pruebas
(`dbInformesAutomatizados_Pruebas`, esquema `db_NEXO`).

## Resultado esperado

`Nexo.Domain.Tests` y `Nexo.Application.Tests` en verde completo;
`Nexo.Api.Tests` en verde (2 omitidos a propósito, `clsMailSenderRealTests`).

## Verificación en vivo del contrato (opcional)

```bash
# Con el API corriendo localmente contra la base real (ver docs/Testing/PRUEBAS-LOCALES.md):
curl -s "http://localhost:5080/api/captura-solicitudes/clientes?q=<termino_real>" \
  -H "X-Portal-Usuario-Id: <id_de_un_usuario_valido>" | jq '.[] | {nit, nitCliente, origen}'
# nitCliente debe ser idéntico a nit en cada fila, para "cliente" y "prospecto".
```

**Resultado real verificado (2026-09-28):** confirmado con la suite completa,
197/197 en `Nexo.Api.Tests` (2 omitidos) contra la base real corporativa.
