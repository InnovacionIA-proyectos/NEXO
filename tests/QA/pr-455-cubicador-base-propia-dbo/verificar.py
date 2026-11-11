"""
.SINOPSIS
Verificacion de la PR #455: "[S7-HU-31.3] Cubicador: base propia dbCubicador_Pruebas
con esquema dbo".

.QUE VERIFICA
El Cubicador pasa su ESQUEMA POR DEFECTO de dbo_cubicador a dbo, para el dia en que
se traslade de la base compartida dbTarifario_Nexo_Pruebas a su propia base
dbCubicador_Pruebas (junto con el traslado de NEXO, #428). Esta PR NO ejecuta el
traslado: solo deja el codigo, los tests, el script de verificacion y la
documentacion listos para ese dia. La propia PR dice explicitamente "Se mergea
el dia del traslado, no antes".

Comparacion siempre contra el merge-base real 48748c7 (la punta de QA justo
despues de mergear la PR #454), no contra origin/QA directo (que para cuando se
corrio esta verificacion ya iba 3 commits mas adelante) - asi se aislan los
cambios propios de esta PR de los de PRs posteriores.

.COMO SE VERIFICO EN VIVO
1. db.py / modelos_catalogo.py / verificar_mapeo_catalogo.py: los 3 (y SOLO esos
   3) resuelven `CUBICADOR_DB_ESQUEMA` con `(os.environ.get(...) or "dbo")` -
   confirmado con `or`, una variable vacia ("") es falsy en Python y cae al
   default igual que si no existiera. Con un valor no vacio, `os.environ.get`
   devuelve ese valor (gana la variable). Confirmado con `grep` en todo
   cubicador/ que no hay "dbo_cubicador"/"dbo" hardcodeado en ningun SQL a mano
   fuera de esos 3 archivos (y comentarios/docstrings).
2. 16 modelos confirmados: 6 en modelos.py (hereda Base/db.py) + 10 en
   modelos_catalogo.py (hereda BaseCatalogo) = 16. Ninguno declara su propio
   esquema, todos lo heredan de MetaData(schema=ESQUEMA) de su clase base.
3. worker/main.py importa `SessionLocal`/`crear_tablas` directo de
   infrastructure.persistence.db - mismo modulo, misma fuente de verdad.
4. tests/infrastructure/test_esquema_y_migraciones.py: 12 tests (contando los 3
   parametrizados de test_el_catalogo_resuelve_el_mismo_esquema_que_db), cubren
   los 4 casos que afirma la PR: default=dbo, vacia=dbo, explicita gana (valor
   DISTINTO del default para que la prueba sea real), y consistencia entre
   db.py/modelos_catalogo.py verificada en un SUBPROCESO aparte (no solo una
   funcion en el mismo proceso, para evitar falsos positivos por modulos ya
   importados). `python -m pytest tests/infrastructure/test_esquema_y_migraciones.py`
   -> 12 passed. Coincide exacto con "12/12" de la PR.
5. Resto de la suite de Cubicador (`python -m pytest -q --ignore=.../test_esquema_y_migraciones.py`):
   416 passed, 1 skipped = 417 tests, NO 421 como afirma la PR (diferencia de 4).
   Suite completa junta: 428 passed + 1 skipped = 429 tests coleccionados
   (confirmado con --collect-only). Es un hallazgo NO bloqueante: un numero mal
   reportado en la descripcion, no una falla real - los 417 (416+1 skip) SI
   pasan limpio.
6. verificar_aislamiento.sql: confirmado de SOLO LECTURA (ningun INSERT/UPDATE/
   DELETE/CREATE/ALTER en el archivo, solo SELECT). Cubre las 16 tablas (10
   catalogo + iaSombra + 5 motor 3D), valida k_max=25 con nota explicita de que
   en la base compartida era 10, y revisa permisos esperados (db_datareader/
   db_datawriter si, db_ddladmin/db_owner marcados como "mas de lo necesario").
7. ESQUEMA_CUBICADOR.sql: diff contra el merge-base es SOLO 2 comentarios (el
   encabezado y una nota sobre cuando se crean los vehiculos preset) - 0 lineas
   de DDL real cambiadas.
8. Los 2 scripts retirados (copia_local/4_revisar_cubicador.py,
   5_volcar_esquema.py) confirmados ausentes del working tree de pr455, y 0
   referencias colgantes a ellos en el resto del repo (git grep).
9. .env.dokploy: confirmado que TANTO cubicador-api COMO cubicador-worker
   quedan con DATABASE_URL -> dbCubicador_Pruebas y CUBICADOR_DB_ESQUEMA=dbo
   (no solo uno de los dos servicios).
10. Documentacion consistente: database/README.md (tabla "Donde vive hoy"),
    docs/database/DICCIONARIO-DATOS.md (nota de k_max), docs/decisiones/
    DECISIONES-NEXO.md e historico de ESTUDIO-CUBICADOR-IA.md (nota de que
    iaSombra no se copia en el traslado), DECISIONES_TECNICAS.md,
    HOJA-DE-RUTA-CUBICADOR.md, ARQUITECTURA.md, DOKPLOY.md: todos actualizados
    de forma consistente entre si, sin contradicciones.
11. El resto de archivos .py/.md listados en el diff de GitHub pero con 0
    diferencia real contra el merge-base 48748c7 (.gitattributes, .gitignore,
    README.md raiz, CONTRATO.md, build/e2e/recorrido.py, explicacion/prompt.py,
    interpretacion/prompt.py, rabbitmq.py, 2_copiar.py, worker/main.py en su
    mayor parte) corresponden a cambios ya introducidos por la PR #454 (que ya
    esta mergeada) - GitHub los lista porque compara contra un ancestro mas
    viejo, no porque esta PR los toque de verdad.
12. git merge-tree entre origin/QA (actual) y pr455: sin marcas de conflicto.

.LIMITACIONES CONOCIDAS
- NO se intento conectar a dbCubicador_Pruebas real, ni se corrio
  verificar_aislamiento.sql contra ninguna base real: eso es parte del
  checklist "antes de mergear" de la propia PR, que requiere el dia
  coordinado del traslado.
- NO se verifico el estado actual de las variables de entorno reales en
  Dokploy (si CUBICADOR_DB_ESQUEMA=dbo_cubicador sigue explicita hoy en
  cubicador-api/cubicador-worker) - no es observable desde este entorno.
- El "ensayo de traslado" con los 4 casos contra bases reales que describe la
  PR lo hizo el autor manualmente; aqui se confirmo la LOGICA de codigo que
  produce esos 4 comportamientos (tests 3, 4, 5a-c arriba), no se repitio
  contra las bases reales especificas.

.ESTADO DEL CHECKLIST "ANTES DE MERGEAR" DE LA PROPIA PR (no se resuelve aqui)
- [x] Mergear la #454 -> YA ESTA, confirmado (merge-base 48748c7 es posterior
      al merge de la #454).
- [ ] Correr verificar_aislamiento.sql contra la dbCubicador_Pruebas real, como
      userCubicador -> PENDIENTE, requiere infraestructura real.
- [ ] CUBICADOR_DB_ESQUEMA=dbo_cubicador en cubicador-api y cubicador-worker
      (la PR dice "ya esta") -> NO VERIFICABLE desde aqui (estado real de
      Dokploy).

IMPORTANTE: esta PR trae en su propia descripcion la instruccion de NO
mergearse hasta el dia coordinado del traslado (paso a paso de produccion
incluido: detener api/worker, vaciar colas de RabbitMQ, cambiar DATABASE_URL/
CUBICADOR_DB_ESQUEMA en Dokploy, redesplegar, Y RECIEN AHI mergear este PR).
"""

print(__doc__)
print("Ver el cuerpo de este docstring para el detalle completo de la verificacion.")
print("RESULTADO: codigo limpio, 1 hallazgo numerico no bloqueante (pytest 417 vs 421 afirmado).")
print("NO MERGEAR hasta el dia coordinado del traslado - asi lo pide la propia PR.")
