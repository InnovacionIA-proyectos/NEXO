#!/usr/bin/env python3
"""Extrae rutas+roles reales de los controllers de Nexo.Api y los compara
contra el indice de endpoints (seccion 0.4) de CONTRATO.md.
"""
import re
import sys
from pathlib import Path

API_DIR = Path("src/Nexo.Api")
CONTRATO = Path("CONTRATO.md")

HTTP_RE = re.compile(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]')
AUTH_RE = re.compile(r'\[Authorize(?:\(Roles\s*=\s*([^)]+)\))?\]')
ROUTE_RE = re.compile(r'\[Route\("([^"]+)"\)\]')
CONST_RE = re.compile(r'private\s+const\s+string\s+(\w+)\s*=\s*"([^"]*)"\s*;')
CONTROLLER_CLASS_RE = re.compile(r'\bclass\s+\w+Controller\b')


def resolver_roles_expr(expr, consts):
    expr = expr.strip()
    if expr.startswith('"') and expr.endswith('"'):
        return expr.strip('"')
    if expr in consts:
        return consts[expr]
    return f"<<NO_RESUELTO:{expr}>>"


def normalizar_roles(roles_str):
    if roles_str is None:
        return "Todos"
    return ",".join(sorted(r.strip() for r in roles_str.split(",") if r.strip()))


def parse_controller(path: Path):
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()

    consts = {}
    for m in CONST_RE.finditer(text):
        consts[m.group(1)] = m.group(2)

    class_line_idx = None
    for i, line in enumerate(lines):
        if CONTROLLER_CLASS_RE.search(line):
            class_line_idx = i
            break
    if class_line_idx is None:
        return None

    class_route = None
    class_roles_raw = "__NONE__"
    k = class_line_idx
    while k >= 0:
        line = lines[k]
        rm = ROUTE_RE.search(line)
        if rm and class_route is None:
            class_route = rm.group(1)
        am = AUTH_RE.search(line)
        if am and class_roles_raw == "__NONE__":
            class_roles_raw = am.group(1)
        if line.strip().startswith("namespace") or line.strip().startswith("using "):
            break
        k -= 1

    if class_route is None:
        return None

    NOATTR = object()
    ANY = object()

    def es_atributo_blanco_o_doc(s):
        t = s.strip()
        return t == "" or t.startswith("[") or t.startswith("///") or t.startswith("//")

    def buscar_authorize_del_bloque(idx):
        """Busca [Authorize(...)] en el bloque de atributos contiguo alrededor de la
        linea idx (que contiene el [HttpX]) -- escanea hacia atras y hacia adelante,
        porque el orden entre [HttpX] y [Authorize] no es fijo en este codebase."""
        j = idx - 1
        while j >= 0 and es_atributo_blanco_o_doc(lines[j]):
            am = AUTH_RE.search(lines[j])
            if am:
                return am
            j -= 1
        j = idx + 1
        while j < len(lines) and es_atributo_blanco_o_doc(lines[j]):
            am = AUTH_RE.search(lines[j])
            if am:
                return am
            j += 1
        return None

    endpoints = []
    for idx, line in enumerate(lines):
        hm = HTTP_RE.search(line)
        if hm:
            verbo = hm.group(1).upper()
            subruta = hm.group(2) or ""
            ruta = class_route.strip("/")
            if subruta:
                ruta += "/" + subruta.strip("/")
            ruta = re.sub(r':(guid|int|long|bool)\b', '', ruta)

            am = buscar_authorize_del_bloque(idx)
            if am is None:
                pending_authorize = NOATTR
            else:
                grp = am.group(1)
                pending_authorize = ANY if grp is None else grp

            if class_roles_raw == "__NONE__":
                clase_set = None
            elif class_roles_raw is None:
                clase_set = "ANY"
            else:
                clase_set = set(resolver_roles_expr(class_roles_raw, consts).split(","))

            if pending_authorize is NOATTR:
                metodo_set = None
            elif pending_authorize is ANY:
                metodo_set = "ANY"
            else:
                val = resolver_roles_expr(pending_authorize, consts)
                metodo_set = set(val.split(","))

            if metodo_set is None:
                efectivo = clase_set
            elif clase_set is None:
                efectivo = metodo_set
            elif clase_set == "ANY":
                efectivo = metodo_set
            elif metodo_set == "ANY":
                efectivo = clase_set
            else:
                efectivo = clase_set & metodo_set

            if efectivo == "ANY":
                roles_str = "Todos"
            elif efectivo is None:
                roles_str = "<<SIN_AUTORIZACION>>"
            else:
                roles_str = ",".join(sorted(efectivo))

            endpoints.append((verbo, ruta, roles_str, path.as_posix(), idx + 1))

    return endpoints


def extraer_tabla_contrato():
    text = CONTRATO.read_text(encoding="utf-8")
    filas = []
    seccion_actual = None
    for line in text.splitlines():
        sec_m = re.match(r'\*\*(§[\d.]+[^*]*)\*\*', line.strip())
        if sec_m:
            seccion_actual = sec_m.group(1)
            continue
        row_m = re.match(r'\|\s*`?(GET|POST|PUT|PATCH|DELETE|HUB)`?\s*\|\s*`?([^`|]+)`?\s*\|\s*([^|]+)\|', line)
        if row_m:
            verbo, ruta, roles = row_m.groups()
            ruta = ruta.strip()
            if ruta in ("Ruta",):
                continue
            filas.append((verbo.strip(), ruta, roles.strip(), seccion_actual))
    return filas


def norm_ruta(r):
    r = r.strip()
    r = re.sub(r'\{[a-zA-Z]+\}', '{id}', r)
    r = r.strip("/")
    if r.startswith("api/"):
        pass
    return r


# Endpoints fuera de los controllers de src/Nexo.Api (minimal APIs en clsProgram.cs) o
# cubiertos por el FallbackPolicy GLOBAL de autorizacion (clsProgram.cs ~linea 156: exige
# usuario autenticado + CUALQUIER rol valido de Nexo para todo endpoint sin [Authorize]
# propio) que este script no modela por diseno (solo parsea atributos de controllers).
# Verificados a mano leyendo clsProgram.cs antes de excluirlos aqui -- no se excluyen por
# conveniencia, cada uno tiene su justificacion.
RUTAS_FUERA_DE_CONTROLLERS = {
    ("GET", "health"): "app.MapHealthChecks(\"/health\", ...).AllowAnonymous() en clsProgram.cs -- coincide con 'Anonimo' en CONTRATO.md",
}
RUTAS_CON_FALLBACK_POLICY_GLOBAL = {
    ("GET", "api/sesion/actual"): "sin [Authorize] propio; cubierto por el FallbackPolicy global (RequireAuthenticatedUser + RequireClaim Role) en clsProgram.cs -- equivale a 'Todos' en CONTRATO.md",
}


def main():
    reales = []
    for f in sorted(API_DIR.rglob("cls*Controller.cs")):
        r = parse_controller(f)
        if r:
            reales.extend(r)

    tabla = extraer_tabla_contrato()

    print(f"[extractor] {len(reales)} endpoints reales extraidos de controllers")
    print(f"[extractor] {len(tabla)} filas extraidas de la tabla de indice en CONTRATO.md")

    reales_norm = {}
    for verbo, ruta, roles, archivo, linea in reales:
        key = (verbo, norm_ruta(ruta))
        reales_norm.setdefault(key, []).append((roles, archivo, linea))

    fallo = False

    print("\n=== Verificando cada fila del indice de CONTRATO.md contra el codigo real ===")
    no_encontradas = []
    rol_distinto = []
    excepciones_aplicadas = []
    for verbo, ruta, roles_doc, seccion in tabla:
        if verbo == "HUB":
            continue
        key = (verbo, norm_ruta(ruta))
        if key in RUTAS_FUERA_DE_CONTROLLERS:
            excepciones_aplicadas.append((seccion, verbo, ruta, RUTAS_FUERA_DE_CONTROLLERS[key]))
            continue
        if key not in reales_norm:
            no_encontradas.append((seccion, verbo, ruta, roles_doc))
            continue
        roles_reales_opts = [normalizar_roles(None if v[0] == "Todos" else v[0]) for v in reales_norm[key]]
        roles_doc_norm = normalizar_roles(None if roles_doc.strip() == "Todos" else roles_doc.strip())
        if "Bearer" in roles_doc or "Anónimo" in roles_doc or "Todos (" in roles_doc:
            continue
        if key in RUTAS_CON_FALLBACK_POLICY_GLOBAL and "<<SIN_AUTORIZACION>>" in roles_reales_opts:
            excepciones_aplicadas.append((seccion, verbo, ruta, RUTAS_CON_FALLBACK_POLICY_GLOBAL[key]))
            continue
        if roles_doc_norm not in roles_reales_opts:
            rol_distinto.append((seccion, verbo, ruta, roles_doc, roles_reales_opts, reales_norm[key]))

    if excepciones_aplicadas:
        print(f"\nOK (verificado a mano, ver comentario en este script): {len(excepciones_aplicadas)} fila(s) fuera del alcance del parser de controllers:")
        for seccion, verbo, ruta, motivo in excepciones_aplicadas:
            print(f"  [{seccion}] {verbo} {ruta} -- {motivo}")

    if no_encontradas:
        fallo = True
        print(f"\nFALLO: {len(no_encontradas)} rutas del indice NO existen (con ese verbo+ruta exactos) en el codigo real:")
        for seccion, verbo, ruta, roles_doc in no_encontradas:
            print(f"  [{seccion}] {verbo} {ruta}  (roles doc: {roles_doc})")
    else:
        print("OK: todas las rutas del indice existen en el codigo real.")

    if rol_distinto:
        fallo = True
        print(f"\nFALLO: {len(rol_distinto)} filas con roles que no coinciden:")
        for seccion, verbo, ruta, roles_doc, roles_reales_opts, detalle in rol_distinto:
            print(f"  [{seccion}] {verbo} {ruta}")
            print(f"    doc:   {roles_doc}")
            print(f"    real:  {roles_reales_opts}  -> {detalle}")
    else:
        print("OK: todos los roles documentados coinciden con el codigo real.")

    tabla_keys = {(v, norm_ruta(r)) for v, r, _, _ in tabla if v != "HUB"}
    reales_keys = set(reales_norm.keys())
    faltan_en_doc = sorted(reales_keys - tabla_keys)
    if faltan_en_doc:
        print(f"\nAVISO: {len(faltan_en_doc)} rutas reales no aparecen en el indice de CONTRATO.md:")
        for verbo, ruta in faltan_en_doc:
            print(f"  {verbo} /{ruta}")

    print("")
    if fallo:
        print("RESULTADO: HAY DISCREPANCIAS")
        sys.exit(1)
    else:
        print("RESULTADO: TODO OK")
        sys.exit(0)


if __name__ == "__main__":
    main()
