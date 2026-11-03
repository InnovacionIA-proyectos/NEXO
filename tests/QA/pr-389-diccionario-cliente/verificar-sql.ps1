# Verificacion QA del SQL de la PR #389 (tabla db_NEXO.tbl_diccionario_cliente). Cierra los issues #356 y #358.
#
# Prueba que el DDL SE EJECUTA y deja la tabla como se describe, por los dos caminos del proyecto (ESQUEMA_NEXO.sql es la fuente de
# la verdad; no hay migraciones de EF):
#   A) base EXISTENTE: esquema previo a la PR + docs/Database/356_diccionario_cliente.sql (idempotente) + reversa + reaplicar.
#   B) base NUEVA: ESQUEMA_NEXO.sql completo de la PR sobre una base vacia (con el defecto PREEXISTENTE de ese archivo, ver README
#      de la PR #386: un error en una FK de OutboxMessage aborta el lote y se pierden las FK/indices posteriores; por eso lo propio de
#      la PR se verifica ademas aparte, solo lo que falte).
# y que A y B dejen una estructura IDENTICA. Ejercita CHECK, FK, indice unico y NOT NULL con datos ficticios.
#
# NO toca ninguna base corporativa: SQL Server LOCAL y descartable en Docker (sin puertos publicados, contrasena aleatoria que no se
# imprime), eliminado al terminar aunque algo falle. Se prueba en 2019 y 2022 (la base de pruebas corporativa es la version 15 = 2019).
#
# Correr desde la raiz del repo (necesita Docker y las imagenes mcr.microsoft.com/mssql/server):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-389-diccionario-cliente/verificar-sql.ps1

param([string[]]$Imagenes = @("mcr.microsoft.com/mssql/server:2019-latest", "mcr.microsoft.com/mssql/server:2022-latest"))

$ErrorActionPreference = "Continue"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
Set-Location $repoRoot

$commitDeLaPr = "b520d90"   # commit propio de la PR: su padre es el esquema "de antes"
$tabla = "tbl_diccionario_cliente"
$trabajo = Join-Path ([System.IO.Path]::GetTempPath()) ("qa-pr389-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $trabajo | Out-Null

$script:fallos = New-Object System.Collections.Generic.List[string]
function Comprobar([string]$nombre, [bool]$ok, [string]$detalle = "") {
    if ($ok) { Write-Host ("  OK    " + $nombre) }
    else { Write-Host ("  FALLO " + $nombre + $(if ($detalle) { " -> " + $detalle } else { "" })); $script:fallos.Add($nombre) }
}

$esquemaPr = Join-Path $repoRoot "docs/Database/ESQUEMA_NEXO.sql"
$script356 = Join-Path $repoRoot "docs/Database/356_diccionario_cliente.sql"
$esquemaAntes = Join-Path $trabajo "esquema_antes.sql"
# OJO: en cmd el caracter ^ es de escape; por eso se usa ~1.
cmd /c "git show $commitDeLaPr~1:docs/Database/ESQUEMA_NEXO.sql > `"$esquemaAntes`""
if (-not (Test-Path $esquemaAntes) -or (Get-Item $esquemaAntes).Length -lt 1000) { Write-Error "No se pudo obtener el esquema previo a la PR ($commitDeLaPr~1)."; exit 1 }
Comprobar "el esquema previo a la PR NO contiene $tabla" (-not (Select-String -Path $esquemaAntes -SimpleMatch $tabla -Quiet))
Comprobar "el esquema de la PR SI contiene $tabla" (Select-String -Path $esquemaPr -SimpleMatch $tabla -Quiet)

$obj = "[db_NEXO].[$tabla]"
$sqlConteos = "SET NOCOUNT ON; SELECT CAST((SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'$obj')) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'$obj')) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'$obj') AND name LIKE N'IX_diccionario_cliente_%') AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'$obj')) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'$obj')) AS varchar)"

$sqlEstructura = @"
SET NOCOUNT ON;
SELECT k, v FROM (
  SELECT 'col' AS k, c.name + '|' + t.name + '|' + CAST(c.max_length AS varchar) + '|' + CAST(c.precision AS varchar) + '|' + CAST(c.scale AS varchar) + '|null=' + CAST(c.is_nullable AS varchar) + '|' + ISNULL(dc.name, '') + '|' + ISNULL(dc.definition, '') AS v
    FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
    LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE c.object_id = OBJECT_ID(N'$obj')
  UNION ALL SELECT 'ck', name + '|' + definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'$obj')
  UNION ALL SELECT 'fk', name + '|' + OBJECT_NAME(referenced_object_id) + '|' + CAST(delete_referential_action AS varchar) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'$obj')
  UNION ALL SELECT 'pk', name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'$obj') AND type = 'PK'
  UNION ALL SELECT 'ix', i.name + '|unique=' + CAST(i.is_unique AS varchar) + '|' + (SELECT STRING_AGG(c.name + CASE WHEN ic.is_included_column = 1 THEN '(inc)' ELSE '' END, ',') WITHIN GROUP (ORDER BY ic.is_included_column, ic.key_ordinal, c.name)
                                         FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id)
    FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'$obj') AND i.name IS NOT NULL AND i.is_primary_key = 0
) x ORDER BY k, v;
"@

# Cada caso devuelve el numero de error de SQL Server (0 = se inserto). 547 = violacion de CHECK/FK, 515 = NULL en columna NOT NULL,
# 2601 = clave duplicada en indice unico.
$sqlCasos = @'
SET NOCOUNT ON;
DECLARE @c uniqueidentifier = NEWID();
INSERT INTO [db_NEXO].[tbl_clientes] ([Id],[Nombre],[Nit]) VALUES (@c, N'Cliente ficticio QA', LEFT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 20));
DECLARE @r TABLE (caso nvarchar(70), error int);
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'CIUDAD ORIGEN', N'ciudad origen', N'columna', N'origen', NULL); INSERT @r VALUES (N'columna_general', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'columna_general', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto],[Ejemplo]) VALUES (NEWID(), @c, N'TURBO (4,5 TON)', N'turbo (4,5 ton)', N'valor', N'2_7', N'tipoVehiculo', N'ejemplo ficticio'); INSERT @r VALUES (N'valor_con_cliente_y_contexto', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'valor_con_cliente_y_contexto', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'TON', N'ton', N'unidad', N't', NULL); INSERT @r VALUES (N'unidad_general', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'unidad_general', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'x', N'x', N'raro', N'origen', NULL); INSERT @r VALUES (N'tipo_invalido', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'tipo_invalido', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'y', N'y', N'valor', N'2_7', NULL); INSERT @r VALUES (N'valor_sin_contexto', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'valor_sin_contexto', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'z', N'z', N'columna', N'origen', N'tipoVehiculo'); INSERT @r VALUES (N'columna_con_contexto', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'columna_con_contexto', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NEWID(), N'w', N'w', N'columna', N'origen', NULL); INSERT @r VALUES (N'cliente_inexistente', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'cliente_inexistente', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'Ciudad  Origen', N'ciudad origen', N'columna', N'destino', NULL); INSERT @r VALUES (N'duplicado_general_mismo_normalizado', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'duplicado_general_mismo_normalizado', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), @c, N'Turbo (4,5 ton)', N'turbo (4,5 ton)', N'valor', N'otro', N'tipoVehiculo'); INSERT @r VALUES (N'duplicado_de_cliente_mismo_normalizado', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'duplicado_de_cliente_mismo_normalizado', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'TURBO (4,5 TON)', N'turbo (4,5 ton)', N'valor', N'2_7', N'tipoVehiculo'); INSERT @r VALUES (N'mismo_texto_general_vs_cliente', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'mismo_texto_general_vs_cliente', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), @c, N'TURBO (4,5 TON)', N'turbo (4,5 ton)', N'valor', N'2_7', N'carroceria'); INSERT @r VALUES (N'mismo_texto_otro_contexto', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'mismo_texto_otro_contexto', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, NULL, N'n1', N'columna', N'origen', NULL); INSERT @r VALUES (N'termino_nulo', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'termino_nulo', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'n2', NULL, N'columna', N'origen', NULL); INSERT @r VALUES (N'normalizado_nulo', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'normalizado_nulo', ERROR_NUMBER()); END CATCH;
BEGIN TRY INSERT INTO [db_NEXO].[tbl_diccionario_cliente] ([Id],[ClienteId],[Termino],[TerminoNormalizado],[Tipo],[Concepto],[Contexto]) VALUES (NEWID(), NULL, N'n3', N'n3', N'columna', NULL, NULL); INSERT @r VALUES (N'concepto_nulo', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'concepto_nulo', ERROR_NUMBER()); END CATCH;
BEGIN TRY DELETE FROM [db_NEXO].[tbl_clientes] WHERE [Id] = @c; INSERT @r VALUES (N'borrar_cliente_con_terminos', 0); END TRY BEGIN CATCH INSERT @r VALUES (N'borrar_cliente_con_terminos', ERROR_NUMBER()); END CATCH;
SELECT caso + '|' + CAST(error AS varchar) FROM @r;
SELECT 'filas_validas|' + CAST(COUNT(*) AS varchar) FROM [db_NEXO].[tbl_diccionario_cliente];
SELECT 'defaults_ok|' + CAST(COUNT(*) AS varchar) FROM [db_NEXO].[tbl_diccionario_cliente] WHERE [Activo] = 1 AND [CreatedAt] IS NOT NULL AND [UpdatedAt] IS NULL;
'@

$esperados = [ordered]@{
    columna_general = 0; valor_con_cliente_y_contexto = 0; unidad_general = 0
    tipo_invalido = 547; valor_sin_contexto = 547; columna_con_contexto = 547; cliente_inexistente = 547
    duplicado_general_mismo_normalizado = 2601; duplicado_de_cliente_mismo_normalizado = 2601
    mismo_texto_general_vs_cliente = 0; mismo_texto_otro_contexto = 0
    termino_nulo = 515; normalizado_nulo = 515; concepto_nulo = 515
    borrar_cliente_con_terminos = 547
}

foreach ($imagen in $Imagenes) {
    $etiqueta = ($imagen -split ":")[-1]
    Write-Host ""
    Write-Host "================ SQL Server $etiqueta ================"
    $contenedor = "qa-pr389-sql-" + [guid]::NewGuid().ToString("N").Substring(0, 8)
    $clave = "Qa!" + [guid]::NewGuid().ToString("N") + "aZ9"
    try {
        docker run -d --name $contenedor -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$clave" $imagen | Out-Null
        if ($LASTEXITCODE -ne 0) { Comprobar "arrancar el contenedor $etiqueta" $false "docker run fallo"; continue }

        $sqlcmd = $null
        foreach ($ruta in "/opt/mssql-tools18/bin/sqlcmd", "/opt/mssql-tools/bin/sqlcmd") {
            docker exec $contenedor test -x $ruta; if ($LASTEXITCODE -eq 0) { $sqlcmd = $ruta; break }
        }
        if (-not $sqlcmd) { Comprobar "encontrar sqlcmd en la imagen" $false; continue }

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
        function ErroresDe($salida) {
            $l = @($salida | ForEach-Object { "$_" })
            for ($i = 0; $i -lt $l.Count - 1; $i++) { if ($l[$i] -match '^Msg \d+, Level') { ($l[$i] -replace ', Server [^,]+', '' -replace ', Line \d+', '') + " :: " + $l[$i + 1] } }
        }

        $listo = $false
        for ($i = 0; $i -lt 60 -and -not $listo; $i++) { Sql @("-Q", "SELECT 1") | Out-Null; if ($LASTEXITCODE -eq 0) { $listo = $true } else { Start-Sleep -Seconds 3 } }
        Comprobar "SQL Server $etiqueta acepta conexiones" $listo
        if (-not $listo) { continue }
        Write-Host ("  version del motor: " + (Sql @("-Q", "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(30))") | Select-Object -First 1))

        # ---------------- A) base EXISTENTE ----------------
        Write-Host "-- A) base existente: esquema previo a la PR + script 356"
        Sql @("-Q", "CREATE DATABASE qa389a") | Out-Null
        Sql @("-d", "qa389a", "-Q", "CREATE SCHEMA db_NEXO") | Out-Null
        $salida = SqlArchivo "qa389a" $esquemaAntes $true
        $erroresAntes = @(ErroresDe $salida)
        Write-Host ("  errores PREEXISTENTES del esquema previo a la PR: " + $erroresAntes.Count)
        $erroresAntes | ForEach-Object { Write-Host ("    " + $_) }
        $conteo = (Sql @("-d", "qa389a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: antes del script no existe la tabla (0|0|0|0|0)" ($conteo -eq "0|0|0|0|0") $conteo

        $salida = SqlArchivo "qa389a" $script356
        Comprobar "A: el script 356 corre sin errores" ($LASTEXITCODE -eq 0) (($salida | Select-Object -First 3) -join " / ")
        $conteo = (Sql @("-d", "qa389a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: tras el script: 1 tabla, 11 columnas, 2 indices, 1 FK, 2 CHECK (1|11|2|1|2)" ($conteo -eq "1|11|2|1|2") $conteo
        $salida = SqlArchivo "qa389a" $script356
        Comprobar "A: el script es idempotente (segunda corrida sin errores)" ($LASTEXITCODE -eq 0) (($salida | Select-Object -First 3) -join " / ")
        $conteo2 = (Sql @("-d", "qa389a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: la segunda corrida no duplica nada (1|11|2|1|2)" ($conteo2 -eq "1|11|2|1|2") $conteo2

        $estructuraA = Sql @("-d", "qa389a", "-Q", $sqlEstructura)
        Comprobar "A: la consulta de estructura devuelve filas" (($estructuraA | Measure-Object).Count -ge 16) ("filas=" + ($estructuraA | Measure-Object).Count)

        $archivoCasos = Join-Path $trabajo "casos.sql"; [System.IO.File]::WriteAllText($archivoCasos, $sqlCasos, (New-Object System.Text.UTF8Encoding($false)))
        $res = @{}
        foreach ($linea in (SqlArchivo "qa389a" $archivoCasos)) { if ($linea -match '^([a-z_]+)\|(\d+)$') { $res[$Matches[1]] = [int]$Matches[2] } }
        foreach ($caso in $esperados.Keys) {
            Comprobar ("A: " + $caso + " -> error " + $esperados[$caso]) ($res.ContainsKey($caso) -and $res[$caso] -eq $esperados[$caso]) ("obtenido=" + $res[$caso])
        }
        Comprobar "A: quedan solo las 5 filas validas (3 + 'general vs cliente' + 'otro contexto')" ($res["filas_validas"] -eq 5) ("filas=" + $res["filas_validas"])
        Comprobar "A: los valores por defecto se aplican (Activo=1, CreatedAt con valor, UpdatedAt NULL)" ($res["defaults_ok"] -eq 5) ("coinciden=" + $res["defaults_ok"])

        Sql @("-d", "qa389a", "-Q", "DROP TABLE $obj") | Out-Null
        Comprobar "A: la reversa (DROP TABLE) funciona" ($LASTEXITCODE -eq 0)
        $conteo3 = (Sql @("-d", "qa389a", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "A: tras la reversa no queda la tabla (0|0|0|0|0)" ($conteo3 -eq "0|0|0|0|0") $conteo3
        $salida = SqlArchivo "qa389a" $script356
        Comprobar "A: se puede volver a aplicar tras la reversa" ($LASTEXITCODE -eq 0)

        # ---------------- B) base NUEVA ----------------
        Write-Host "-- B) base nueva: ESQUEMA_NEXO.sql completo de la PR"
        Sql @("-Q", "CREATE DATABASE qa389b") | Out-Null
        Sql @("-d", "qa389b", "-Q", "CREATE SCHEMA db_NEXO") | Out-Null
        $salida = SqlArchivo "qa389b" $esquemaPr $true
        $erroresPr = @(ErroresDe $salida)
        Write-Host ("  errores del ESQUEMA_NEXO.sql de la PR: " + $erroresPr.Count)
        $nuevos = @(Compare-Object $erroresAntes $erroresPr | Where-Object { $_.SideIndicator -eq "=>" })
        Comprobar "B: la PR NO agrega errores nuevos al ESQUEMA_NEXO.sql (los mismos que ya tenia)" ($nuevos.Count -eq 0) (($nuevos | ForEach-Object { $_.InputObject }) -join " / ")
        Comprobar "B: ningun error del esquema de la PR menciona $tabla" (-not ($erroresPr -match "diccionario_cliente"))

        $sentenciasPr = @(Select-String -Path $esquemaPr -Pattern "^(ALTER TABLE|CREATE (UNIQUE )?INDEX).*$tabla" | ForEach-Object { $_.Line })
        Comprobar "B: la PR declara 1 FK y 2 indices de $tabla en ESQUEMA_NEXO.sql" ($sentenciasPr.Count -eq 3) ("sentencias=" + $sentenciasPr.Count)
        $conteoAntesB = (Sql @("-d", "qa389b", "-Q", $sqlConteos) | Select-Object -First 1)
        Write-Host ("  [informativo] tras el esquema completo, la tabla de la PR queda (tabla|columnas|indices|fk|check) = " + $conteoAntesB + "  (esperado sin el defecto preexistente: 1|11|2|1|2)")

        # Solo lo que FALTE de lo propio de la PR (por el aborto preexistente), una sentencia por lote y protegida con IF NOT EXISTS.
        $lotes = foreach ($s in $sentenciasPr) {
            if ($s -match '\[(FK_[A-Za-z0-9_]+)\]') { "IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'$($Matches[1])') " + $s + "`nGO" }
            elseif ($s -match '\[(IX_[A-Za-z0-9_]+)\]') { "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'$($Matches[1])' AND object_id = OBJECT_ID(N'$obj')) " + $s + "`nGO" }
        }
        $archivoAparte = Join-Path $trabajo "sentencias_pr.sql"
        [System.IO.File]::WriteAllText($archivoAparte, ($lotes -join "`n"), (New-Object System.Text.UTF8Encoding($false)))
        $salida = SqlArchivo "qa389b" $archivoAparte $true
        $erroresAparte = @(ErroresDe $salida)
        Comprobar "B: las sentencias propias de la PR que faltaban corren sin errores si se ejecutan aparte" ($erroresAparte.Count -eq 0) ($erroresAparte -join " / ")
        $conteoB = (Sql @("-d", "qa389b", "-Q", $sqlConteos) | Select-Object -First 1)
        Comprobar "B: la tabla queda con 11 columnas, 2 indices, 1 FK y 2 CHECK (1|11|2|1|2)" ($conteoB -eq "1|11|2|1|2") $conteoB
        $estructuraB = Sql @("-d", "qa389b", "-Q", $sqlEstructura)

        $dif = Compare-Object @($estructuraA) @($estructuraB)
        Comprobar "A y B dejan una estructura identica (columnas, defaults, CHECK, FK, PK, indices e indice unico)" (($dif | Measure-Object).Count -eq 0) (($dif | ForEach-Object { $_.SideIndicator + " " + $_.InputObject }) -join " / ")
    }
    finally {
        docker rm -f $contenedor 2>&1 | Out-Null
        Write-Host ("  contenedor " + $contenedor + " eliminado")
    }
}

Remove-Item -LiteralPath $trabajo -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ""
if ($script:fallos.Count -eq 0) { Write-Host "=== Verificacion SQL completa: TODO OK ===" }
else { Write-Host ("=== Verificacion SQL con " + $script:fallos.Count + " fallo(s): " + ($script:fallos -join "; ")); exit 1 }
