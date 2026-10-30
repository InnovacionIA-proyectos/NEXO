# PR #228 (revertida) + #229 — Catálogo de municipios S4-HU-09.1

## Contexto

La PR #228 original creaba `tbl_municipios` vía migración de EF Core + comando de
siembra en vivo contra `dbSolucionesTI`. Se revirtió (PR #229) y se reintrodujo con
un enfoque más simple, acordado con Ismael: sin migraciones ni comando de siembra,
solo esquema manual (`docs/Database/ESQUEMA_NEXO.sql`) + CSV estático ya calculado
(`docs/Database/seeder/municipios.csv`, 3.804 filas), cargado una sola vez con
`BULK INSERT` contra la base real.

## Qué se verificó

1. Que la tabla física `db_NEXO.tbl_municipios` sigue existiendo en la base real
   con sus 3.804 filas (creada durante el #228 original, antes del revert de
   código) — el revert es a nivel de código/git, no borra la tabla física.
2. Que el nuevo `GET /api/captura-solicitudes/municipios` (solo lectura, sin
   migración ni comando de siembra) lee esos datos ya existentes sin problema.
3. Que el CSV nuevo (`municipios.csv`) no trae nada sensible — solo datos
   geográficos/de zonificación (código DANE, municipio, departamento, país, zona).

## Cómo correrlo

```bash
./verify-tabla-municipios.sh
```

Requiere `sqlcmd`/Docker y las credenciales reales de `dbInformesAutomatizados_Pruebas`
(10.10.40.85) — el script las lee de variables de entorno, nunca las hardcodea.

```bash
export NEXO_DB_HOST="10.10.40.85,1433"
export NEXO_DB_USER="userInformesOw"
export NEXO_DB_PASSWORD="<pedir a Kevin/backend>"
export NEXO_DB_NAME="dbInformesAutomatizados_Pruebas"
./verify-tabla-municipios.sh
```

## Resultado real verificado (2026-09-28)

```
Filas
-----------
      3804
```

Y la suite completa (`pruebas-reales-completas`) en verde: `Nexo.Domain.Tests`
133/133, `Nexo.Application.Tests` 168/168, `Nexo.Api.Tests` 197/197 (2 omitidos).
