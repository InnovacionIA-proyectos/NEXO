# Seeders de datos — poblado manual, una sola vez

Esta carpeta guarda los datos ya calculados para poblar catálogos que **no se crean
ni se pueblan por migración de EF Core** (este proyecto no usa migraciones — ver
`docs/Database/ESQUEMA_NEXO.sql` y `DOKPLOY.md`, sección "Esquema de base de datos").
El flujo para cualquier catálogo de esta carpeta es siempre el mismo, en dos pasos
manuales:

1. Crear la tabla corriendo `docs/Database/ESQUEMA_NEXO.sql` contra la base.
2. Poblarla con el CSV correspondiente de esta carpeta (`BULK INSERT` u otra forma
   de carga masiva).

Ninguno de estos catálogos tiene un comando ni un endpoint que los vuelva a poblar
en la aplicación — el cálculo/cruce que produjo los datos se hizo una vez, con
consultas directas contra las fuentes, y quedó congelado en el CSV.

## `municipios.csv`

**Poblado UNA SOLA VEZ (S4-HU-09.1) — sin sincronización periódica.** Si el Data
Warehouse corporativo (`tblSAPDANE`/`tblZonasTarificacionRelacion`/Tarifario SAP)
agrega una ciudad nueva o cambia una zona después de esta foto, este CSV **no se
entera solo** — hay que volver a correr el cruce a mano y regenerar el archivo. No
existe ningún proceso automático que lo mantenga sincronizado.

- **Tabla destino:** `db_NEXO.tbl_municipios` (columnas: `Id`, `CodigoDane`,
  `Municipio`, `Departamento`, `Pais`, `NombreTarifasPlaza`, `ZonaTarificacion` — ver
  `ESQUEMA_NEXO.sql` para los tipos).
- **De dónde salió:** cruce de `tblSAPDANE` (códigos DANE/DIVIPOLA) +
  `tblZonasTarificacionRelacion` + `tblSAPTarifas`/`tblSAPTarifasLog` del datacenter
  corporativo (`dbSolucionesTI`), corrido una vez con consultas SQL directas — no
  con un adaptador de la app (ver `docs/diccionario-datos-cabinafletes.md`, catálogo
  `municipios`, para el detalle completo del cruce y las excepciones).
- **Filas:** 3.804 (Colombia 1.128, Venezuela 1.424, Ecuador 1.252) — ya aplicadas
  contra `dbInformesAutomatizados_Pruebas.db_NEXO` (2026-09-26).
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `municipios.csv` con `BULK INSERT` (o el método de carga masiva que
  prefiera quien administre esa base) contra `db_NEXO.tbl_municipios`.
