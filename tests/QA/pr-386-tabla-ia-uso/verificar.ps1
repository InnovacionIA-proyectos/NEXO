# Verificacion QA de la PR #386 (tabla db_NEXO.tbl_ia_uso). Cierra el issue #352.
#
# La PR es solo DDL (docs/Database/352_ia_uso.sql + ESQUEMA_NEXO.sql), asi que lo que hay que probar es que el SQL SE EJECUTA
# y deja la tabla como se describe, por los DOS caminos que usa el proyecto (ESQUEMA_NEXO.sql es la fuente de la verdad, no hay
# migraciones de EF):
#   A) base EXISTENTE: esquema de antes de la PR + script 352 (idempotente, dos corridas) + reversa (DROP TABLE) + reaplicar.
#   B) base NUEVA: ESQUEMA_NEXO.sql completo de la PR sobre una base vacia.
# y que la estructura resultante de A y B sea IDENTICA. Ademas ejercita las restricciones (CHECK, FK, NOT NULL) con datos ficticios.
#
# NO toca ninguna base de datos corporativa: levanta un SQL Server LOCAL y descartable en Docker (sin puertos publicados,
# contrasena aleatoria que no se imprime) y lo elimina al terminar, incluso si algo falla. Se prueba en 2019 y 2022 porque la
# version de la base corporativa no esta documentada en el repo.
#
# Correr desde la raiz del repo (necesita Docker y las imagenes mcr.microsoft.com/mssql/server ya descargadas, o red para bajarlas):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-386-tabla-ia-uso/verificar.ps1

param([string[]]$Imagenes = @("mcr.microsoft.com/mssql/server:2019-latest", "mcr.microsoft.com/mssql/server:2022-latest"))

$ErrorActionPreference = "Continue"   # los comandos nativos (docker, git) se validan por $LASTEXITCODE
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
Set-Location $repoRoot

$commitDeLaPr = "114493a"   # el commit propio de la PR: su padre es el esquema "de antes"
$trabajo = Join-Path ([System.IO.Path]::GetTempPath()) ("qa-pr386-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $trabajo | Out-Null

$script:fallos = New-Object System.Collections.Generic.List[string]
function Comprobar([string]$nombre, [bool]$ok, [string]$detalle = "") {
    if ($ok) { Write-Host ("  OK    " + $nombre) }
    else { Write-Host ("  FALLO " + $nombre + $(if ($detalle) { " -> " + $detalle } else { "" })); $script:fallos.Add($nombre) }
}

# --- archivos de trabajo (bytes crudos, sin BOM ni re-codificacion) ---
$esquemaPr   = Join-Path $repoRoot "docs/Database/ESQUEMA_NEXO.sql"
$script352   = Join-Path $repoRoot "docs/Database/352_ia_uso.sql"
$esquemaAntes = Join-Path $trabajo "esquema_antes.sql"
# OJO: en cmd el caracter ^ es de escape ("sha^:ruta" se leia como "sha:ruta", o sea el archivo DE la PR); por eso se usa ~1.
cmd /c "git show $commitDeLaPr~1:docs/Database/ESQUEMA_NEXO.sql > `"$esquemaAntes`""
if (-not (Test-Path $esquemaAntes) -or (Get-Item $esquemaAntes).Length -lt 1000) { Write-Error "No se pudo obtener el esquema previo a la PR ($commitDeLaPr~1)."; exit 1 }
Comprobar "el esquema previo a la PR NO contiene tbl_ia_uso" (-not (Select-String -Path $esquemaAntes -SimpleMatch "tbl_ia_uso" -Quiet))
Comprobar "el esquema de la PR SI contiene tbl_ia_uso" (Select-String -Path $esquemaPr -SimpleMatch "tbl_ia_uso" -Quiet)

# --- T-SQL de apoyo ---
$sqlConteos = "SET NOCOUNT ON; SELECT CAST((SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]') AND name LIKE N'IX_ia_uso_%') AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')) AS varchar)"

$sqlEstructura = @'
SET NOCOUNT ON;
SELECT k, v FROM (
  SELECT 'col' AS k, c.name + '|' + t.name + '|' + CAST(c.max_length AS varchar) + '|' + CAST(c.precision AS varchar) + '|' + CAST(c.scale AS varchar) + '|null=' + CAST(c.is_nullable AS varchar) + '|' + ISNULL(dc.name, '') + '|' + ISNULL(dc.definition, '') AS v
    FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
    LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE c.object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')
  UNION ALL SELECT 'ck', name + '|' + definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')
  UNION ALL SELECT 'fk', name + '|' + OBJECT_NAME(referenced_object_id) + '|' + CAST(delete_referential_action AS varchar) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')
  UNION ALL SELECT 'pk', name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]') AND type = 'PK'
  UNION ALL SELECT 'ix', i.name + '|' + (SELECT STRING_AGG(c.name + CASE WHEN ic.is_included_column = 1 THEN '(inc)' ELSE '' END, ',') WITHIN GROUP (ORDER BY ic.is_included_column, ic.key_ordinal, c.name)
                                         FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id)
    FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]') AND i.name IS NOT NULL AND i.is_primary_key = 0
) x ORDER BY k, v;
'@

# Cada caso devuelve el numero de error de SQL Server (0 = se inserto). 547 = violacion de CHECK/FK, 515 = NULL en columna NOT NULL.
$sqlCasos = @'
SET NOCOUNT ON;
DECLARE @u uniqueidentifier = NEWID();
INSERT INTO [db_NEXO].[tbl_usuarios] ([Id],[Nombre],[Email],[Rol]) VALUES (@u, N'Usuario ficticio QA', N'qa@ejemplo.invalid', N'rol-ficticio');
DECLARE @r TABLE (caso nvarchar(60), error int);
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'claude-sonnet-5-5', N'ok'); INSERT @r VALUES (N'minimo_sin_usuario_ni_costo', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'minimo_sin_usuario_ni_costo', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[UsuarioId],[Modelo],[TokensEntrada],[TokensSalida],[TokensCacheLectura],[TokensCacheEscritura],[CostoEstimadoUsd],[LatenciaMs],[Resultado]) VALUES (NEWID(), N'AsistenteCotizacion', @u, N'claude-sonnet-5-5', 1200, 350, 100, 50, 0.012345, 840, N'ok'); INSERT @r VALUES (N'completo_con_usuario_valido', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'completo_con_usuario_valido', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[TokensEntrada],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', -1, N'ok'); INSERT @r VALUES (N'tokens_entrada_negativos', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'tokens_entrada_negativos', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[TokensSalida],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', -1, N'ok'); INSERT @r VALUES (N'tokens_salida_negativos', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'tokens_salida_negativos', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[TokensCacheLectura],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', -1, N'ok'); INSERT @r VALUES (N'cache_lectura_negativos', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'cache_lectura_negativos', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[TokensCacheEscritura],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', -1, N'ok'); INSERT @r VALUES (N'cache_escritura_negativos', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'cache_escritura_negativos', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[LatenciaMs],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', -1, N'ok'); INSERT @r VALUES (N'latencia_negativa', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'latencia_negativa', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[CostoEstimadoUsd],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', -0.01, N'ok'); INSERT @r VALUES (N'costo_negativo', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'costo_negativo', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[UsuarioId],[Modelo],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', NEWID(), N'm', N'ok'); INSERT @r VALUES (N'usuario_inexistente', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'usuario_inexistente', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[Resultado]) VALUES (NEWID(), NULL, N'm', N'ok'); INSERT @r VALUES (N'funcion_nula', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'funcion_nula', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_ia_uso] ([Id],[Funcion],[Modelo],[Resultado]) VALUES (NEWID(), N'ImportacionExcel', N'm', NULL); INSERT @r VALUES (N'resultado_nulo', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'resultado_nulo', ERROR_NUMBER()); END CATCH;
BEGIN TRY DELETE FROM [db_NEXO].[tbl_usuarios] WHERE [Id] = @u; INSERT @r VALUES (N'borrar_usuario_con_uso_registrado', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'borrar_usuario_con_uso_registrado', ERROR_NUMBER()); END CATCH;
SELECT caso + '|' + CAST(error AS varchar) FROM @r;
SELECT 'filas_validas|' + CAST(COUNT(*) AS varchar) FROM [db_NEXO].[tbl_ia_uso];
SELECT 'defaults_ok|' + CAST(COUNT(*) AS varchar) FROM [db_NEXO].[tbl_ia_uso] WHERE [UsuarioId] IS NULL AND [TokensEntrada] = 0 AND [TokensSalida] = 0 AND [TokensCacheLectura] = 0 AND [TokensCacheEscritura] = 0 AND [LatenciaMs] = 0 AND [CostoEstimadoUsd] IS NULL AND [CreatedAt] IS NOT NULL;
'@

$esperados = [ordered]@{
    minimo_sin_usuario_ni_costo = 0; completo_con_usuario_valido = 0
    tokens_entrada_negativos = 547; tokens_salida_negativos = 547; cache_lectura_negativos = 547; cache_escritura_negativos = 547
    latencia_negativa = 547; costo_negativo = 547; usuario_inexistente = 547
    funcion_nula = 515; resultado_nulo = 515; borrar_usuario_con_uso_registrado = 547
}

foreach ($imagen in $Imagenes) {
    $etiqueta = ($imagen -split ":")[-1]
    Write-Host ""
    Write-Host "================ SQL Server $etiqueta ================"
    $contenedor = "qa-pr386-sql-" + [guid]::NewGuid().ToString("N").Substring(0, 8)
    $clave = "Qa!" + [guid]::NewGuid().ToString("N") + "aZ9"
    try {
        docker run -d --name $contenedor -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$clave" $imagen | Out-Null
        if ($LASTEXITCODE -ne 0) { Comprobar "arrancar el contenedor $etiqueta" $false "docker run fallo"; continue }

        $sqlcmd = $null
        foreach ($ruta in "/opt/mssql-tools18/bin/sqlcmd", "/opt/mssql-tools/bin/sqlcmd") {
            docker exec $contenedor test -x $ruta; if ($LASTEXITCODE -eq 0) { $sqlcmd = $ruta; break }
        }
        if (-not $sqlcmd) { Comprobar "encontrar sqlcmd en la imagen" $false; continue }

        # Por defecto se aborta al primer error (-b). $tolerante = $true sigue pasado los errores y los devuelve en la salida: se usa
        # para ESQUEMA_NEXO.sql, que (ver README) ya trae errores propios que no son de esta PR.
        function Sql([string[]]$argumentos, [bool]$tolerante = $false) {
            $banderas = @("-S", "localhost", "-U", "sa", "-C", "-f", "65001", "-h", "-1", "-W", "-s", "|")
            if (-not $tolerante) { $banderas += "-b" }
            docker exec -e "SQLCMDPASSWORD=$clave" $contenedor $sqlcmd @banderas @argumentos 2>&1
        }
        function SqlArchivo([string]$base, [string]$archivo, [bool]$tolerante = $false) {
            $destino = "/tmp/" + [System.IO.Path]::GetFileName($archivo)
            docker cp $archivo ("{0}:{1}" -f $contenedor, $destino) | Out-Null
            Sql @("-d", $base, "-i", $destino) $tolerante
        }
        # Mensajes de error de SQL Server de una salida de sqlcmd (la linea que sigue a "Msg N, Level ..."), sin numeros de linea ni nombre de servidor.
        function ErroresDe($salida) {
            $l = @($salida | ForEach-Object { "$_" })
            for ($i = 0; $i -lt $l.Count - 1; $i++) { if ($l[$i] -match '^Msg \d+, Level') { ($l[$i] -replace ', Server [^,]+', '' -replace ', Line \d+', '') + " :: " + $l[$i + 1] } }
        }

        $listo = $false
        for ($i = 0; $i -lt 60 -and -not $listo; $i++) { Sql @("-Q", "SELECT 1") | Out-Null; if ($LASTEXITCODE -eq 0) { $listo = $true } else { Start-Sleep -Seconds 3 } }
        Comprobar "SQL Server $etiqueta acepta conexiones" $listo
        if (-not $listo) { continue }
        $version = (Sql @("-Q", "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(30))") | Select-Object -First 1)
        Write-Host ("  version del motor: " + $version)

        # ---------------- A) base EXISTENTE: esquema previo + script 352 ----------------
        Write-Host "-- A) base existente: esquema previo a la PR + script 352"
        Sql @("-Q", "CREATE DATABASE qa386a") | Out-Null
        Sql @("-d", "qa386a", "-Q", "CREATE SCHEMA db_NEXO") | Out-Null
        $salida = SqlArchivo "qa386a" $esquemaAntes $true
        $erroresAntes = @(ErroresDe $salida)
        Write-Host ("  errores PREEXISTENTES del esquema previo a la PR: " + $erroresAntes.Count)
        $erroresAntes | ForEach-Object { Write-Host ("    " + $_) }
        # Mismo defecto medido sobre el esquema PREVIO a la PR: demuestra que es anterior y no lo introduce esta PR.
        $fkAntes = (Sql @("-d", "qa386a", "-Q", "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.foreign_keys WHERE SCHEMA_NAME(schema_id) = N'db_NEXO'") | Select-Object -First 1)
        $ixAntesN = (Sql @("-d", "qa386a", "-Q", "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.schema_id = SCHEMA_ID(N'db_NEXO') AND i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type > 0 AND i.name IS NOT NULL") | Select-Object -First 1)
        $fkDecl0 = @(Select-String -Path $esquemaAntes -Pattern '^ALTER TABLE .*FOREIGN KEY').Count
        $ixDecl0 = @(Select-String -Path $esquemaAntes -Pattern '^CREATE (UNIQUE )?INDEX').Count
        Write-Host ("  [PREEXISTENTE, esquema PREVIO a la PR] FK declaradas: " + $fkDecl0 + " | creadas: " + $fkAntes + " ; CREATE INDEX declarados: " + $ixDecl0 + " | indices secundarios creados: " + $ixAntesN)
        $conteo = (Sql @("-d", "qa386a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: antes del script no existe la tabla (0|0|0|0)" ($conteo -eq "0|0|0|0") $conteo

        $salida = SqlArchivo "qa386a" $script352
        Comprobar "A: el script 352 corre sin errores" ($LASTEXITCODE -eq 0) (($salida | Select-Object -First 3) -join " / ")
        $conteo = (Sql @("-d", "qa386a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: tras el script: 1 tabla, 12 columnas, 2 indices, 1 FK (1|12|2|1)" ($conteo -eq "1|12|2|1") $conteo

        $salida = SqlArchivo "qa386a" $script352
        Comprobar "A: el script es idempotente (segunda corrida sin errores)" ($LASTEXITCODE -eq 0) (($salida | Select-Object -First 3) -join " / ")
        $conteo2 = (Sql @("-d", "qa386a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: la segunda corrida no duplica nada (1|12|2|1)" ($conteo2 -eq "1|12|2|1") $conteo2

        $estructuraA = Sql @("-d", "qa386a", "-Q", $sqlEstructura)
        Comprobar "A: la consulta de estructura devuelve filas" (($estructuraA | Measure-Object).Count -ge 17) ("filas=" + ($estructuraA | Measure-Object).Count)

        # Restricciones, con datos ficticios, sobre la base de A.
        $archivoCasos = Join-Path $trabajo "casos.sql"; [System.IO.File]::WriteAllText($archivoCasos, $sqlCasos, (New-Object System.Text.UTF8Encoding($false)))
        $res = @{}
        foreach ($linea in (SqlArchivo "qa386a" $archivoCasos)) { if ($linea -match '^([a-z_]+)\|(\d+)$') { $res[$Matches[1]] = [int]$Matches[2] } }
        foreach ($caso in $esperados.Keys) {
            Comprobar ("A: " + $caso + " -> error " + $esperados[$caso]) ($res.ContainsKey($caso) -and $res[$caso] -eq $esperados[$caso]) ("obtenido=" + $res[$caso])
        }
        Comprobar "A: quedan solo las 2 filas validas insertadas (las invalidas no entraron)" ($res["filas_validas"] -eq 2) ("filas=" + $res["filas_validas"])
        Comprobar "A: los valores por defecto se aplican (0 en cifras, costo y usuario NULL, CreatedAt con valor)" ($res["defaults_ok"] -eq 1) ("coinciden=" + $res["defaults_ok"])

        # Reversa documentada en el script: DROP TABLE, y volver a aplicar.
        Sql @("-d", "qa386a", "-Q", "DROP TABLE [db_NEXO].[tbl_ia_uso]") | Out-Null
        Comprobar "A: la reversa (DROP TABLE) funciona" ($LASTEXITCODE -eq 0)
        $conteo3 = (Sql @("-d", "qa386a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: tras la reversa no queda la tabla (0|0|0|0)" ($conteo3 -eq "0|0|0|0") $conteo3
        $salida = SqlArchivo "qa386a" $script352
        Comprobar "A: se puede volver a aplicar tras la reversa" ($LASTEXITCODE -eq 0)

        # ---------------- B) base NUEVA: ESQUEMA_NEXO.sql completo ----------------
        Write-Host "-- B) base nueva: ESQUEMA_NEXO.sql completo de la PR"
        Sql @("-Q", "CREATE DATABASE qa386b") | Out-Null
        Sql @("-d", "qa386b", "-Q", "CREATE SCHEMA db_NEXO") | Out-Null
        $salida = SqlArchivo "qa386b" $esquemaPr $true
        $erroresPr = @(ErroresDe $salida)
        Write-Host ("  errores del ESQUEMA_NEXO.sql de la PR: " + $erroresPr.Count)
        $nuevos = @(Compare-Object $erroresAntes $erroresPr | Where-Object { $_.SideIndicator -eq "=>" })
        Comprobar "B: la PR NO agrega errores nuevos al ESQUEMA_NEXO.sql (los mismos que ya tenia)" ($nuevos.Count -eq 0) (($nuevos | ForEach-Object { $_.InputObject }) -join " / ")
        Comprobar "B: ningun error del esquema de la PR menciona tbl_ia_uso" (-not ($erroresPr -match "ia_uso"))

        # DEFECTO PREEXISTENTE (informativo, no es de la PR): el error de la FK de OutboxMessage aborta el lote, y entre los dos GO del
        # archivo estan TODAS las FK y TODOS los indices. Se mide cuanto de lo declarado queda realmente creado en una base nueva.
        $fkDeclaradas = @(Select-String -Path $esquemaPr -Pattern '^ALTER TABLE .*FOREIGN KEY').Count
        $ixDeclarados = @(Select-String -Path $esquemaPr -Pattern '^CREATE (UNIQUE )?INDEX').Count
        $fkCreadas = (Sql @("-d", "qa386b", "-Q", "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.foreign_keys WHERE SCHEMA_NAME(schema_id) = N'db_NEXO'") | Select-Object -First 1)
        $ixCreados = (Sql @("-d", "qa386b", "-Q", "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.schema_id = SCHEMA_ID(N'db_NEXO') AND i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type > 0 AND i.name IS NOT NULL") | Select-Object -First 1)
        Write-Host ("  [PREEXISTENTE] FK declaradas fuera de las tablas: " + $fkDeclaradas + " | FK creadas en la base nueva (todas las del esquema): " + $fkCreadas)
        Write-Host ("  [PREEXISTENTE] CREATE INDEX declarados: " + $ixDeclarados + " | indices secundarios creados en la base nueva: " + $ixCreados)

        # Para verificar las sentencias PROPIAS de la PR (que quedaron sin ejecutar por ese aborto), se corren aparte, una por lote.
        # (La FK de tbl_ia_uso SI se crea en la corrida principal porque va antes del error; queda cubierta por la comparacion de
        #  estructura A vs B. Lo que se corre aparte son solo los 2 indices, que van despues del error y se perdian.)
        $sentenciasPr = @(Select-String -Path $esquemaPr -Pattern '^(ALTER TABLE|CREATE (UNIQUE )?INDEX).*tbl_ia_uso' | ForEach-Object { $_.Line })
        Comprobar "B: la PR declara 1 FK y 2 indices de tbl_ia_uso en ESQUEMA_NEXO.sql" ($sentenciasPr.Count -eq 3) ("sentencias=" + $sentenciasPr.Count)
        $indicesPr = @($sentenciasPr | Where-Object { $_ -match '^CREATE' })
        $ixAntes = (Sql @("-d", "qa386b", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "B: tras el esquema completo la tabla de la PR esta SIN sus 2 indices (efecto del defecto preexistente, 1|12|0|1)" ($ixAntes -eq "1|12|0|1") $ixAntes
        $archivoAparte = Join-Path $trabajo "sentencias_pr.sql"
        [System.IO.File]::WriteAllText($archivoAparte, (($indicesPr | ForEach-Object { $_ + "`nGO" }) -join "`n"), (New-Object System.Text.UTF8Encoding($false)))
        $salida = SqlArchivo "qa386b" $archivoAparte $true
        $erroresAparte = @(ErroresDe $salida)
        Comprobar "B: los 2 CREATE INDEX propios de la PR corren sin errores si se ejecutan aparte" ($erroresAparte.Count -eq 0) ($erroresAparte -join " / ")
        $conteoB = (Sql @("-d", "qa386b", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "B: la tabla existe con 12 columnas, 2 indices y 1 FK (1|12|2|1)" ($conteoB -eq "1|12|2|1") $conteoB
        $estructuraB = Sql @("-d", "qa386b", "-Q", $sqlEstructura)

        # ---------------- A y B deben dejar la MISMA tabla ----------------
        $dif = Compare-Object @($estructuraA) @($estructuraB)
        Comprobar "A y B dejan una estructura identica (columnas, defaults, CHECK, FK, PK, indices)" (($dif | Measure-Object).Count -eq 0) (($dif | ForEach-Object { $_.SideIndicator + " " + $_.InputObject }) -join " / ")
    }
    finally {
        docker rm -f $contenedor 2>&1 | Out-Null
        Write-Host ("  contenedor " + $contenedor + " eliminado")
    }
}

Remove-Item -LiteralPath $trabajo -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ""
if ($script:fallos.Count -eq 0) { Write-Host "=== Verificacion completa: TODO OK ===" }
else { Write-Host ("=== Verificacion con " + $script:fallos.Count + " fallo(s): " + ($script:fallos -join "; ")); exit 1 }
