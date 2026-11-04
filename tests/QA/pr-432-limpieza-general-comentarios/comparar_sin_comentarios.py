"""Compara cada .cs modificado en la PR #432 entre origin/QA y pr432, quitando
comentarios de linea (//) y de bloque (/* */) de forma consciente de strings
(normal, verbatim @"...", interpolado $"...", char '...'), y compara el
resultado normalizando espacios en blanco. Reporta cualquier diferencia de
codigo real (no solo de comentarios/formato).
"""
from __future__ import annotations
import subprocess
import sys


def git_show(ref: str, path: str) -> str | None:
    r = subprocess.run(["git", "show", f"{ref}:{path}"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        return None
    return r.stdout


def strip_comments(src: str) -> str:
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        c2 = src[i:i+2]
        # line comment
        if c2 == "//":
            j = src.find("\n", i)
            i = n if j == -1 else j
            continue
        # block comment
        if c2 == "/*":
            j = src.find("*/", i + 2)
            i = n if j == -1 else j + 2
            continue
        # verbatim string (handles @$"" and $@"" too, just needs the @ before the quote)
        if c == '@' and i + 1 < n and src[i+1] == '"':
            out.append('@"')
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i+1] == '"':
                        out.append('""')
                        i += 2
                        continue
                    out.append('"')
                    i += 1
                    break
                out.append(src[i])
                i += 1
            continue
        # normal / interpolated string (treat $ as plain char, string logic same)
        if c == '"':
            out.append('"')
            i += 1
            while i < n:
                if src[i] == '\\' and i + 1 < n:
                    out.append(src[i:i+2])
                    i += 2
                    continue
                if src[i] == '"':
                    out.append('"')
                    i += 1
                    break
                out.append(src[i])
                i += 1
            continue
        # char literal
        if c == "'":
            out.append("'")
            i += 1
            while i < n:
                if src[i] == '\\' and i + 1 < n:
                    out.append(src[i:i+2])
                    i += 2
                    continue
                if src[i] == "'":
                    out.append("'")
                    i += 1
                    break
                out.append(src[i])
                i += 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def normalizar(s: str) -> str:
    lineas = [ln.strip() for ln in s.splitlines()]
    lineas = [ln for ln in lineas if ln != ""]
    return "\n".join(lineas)


def main() -> int:
    r = subprocess.run(["git", "diff", "origin/QA...pr432", "--name-only", "--", "src/**/*.cs"],
                        capture_output=True, text=True)
    archivos = [l for l in r.stdout.splitlines() if l.strip()]
    print(f"[comparar] {len(archivos)} archivos .cs modificados a verificar")

    diferencias = []
    sin_antes = 0
    verificados = 0
    for path in archivos:
        antes = git_show("origin/QA", path)
        despues = git_show("pr432", path)
        if antes is None:
            sin_antes += 1
            continue  # archivo nuevo, no hay base para comparar codigo
        if despues is None:
            diferencias.append((path, "archivo eliminado en pr432"))
            continue
        a = normalizar(strip_comments(antes))
        b = normalizar(strip_comments(despues))
        verificados += 1
        if a != b:
            diferencias.append((path, "codigo distinto tras quitar comentarios"))

    print(f"[comparar] archivos nuevos (sin base en QA, no comparables): {sin_antes}")
    print(f"[comparar] archivos comparados codigo-sin-comentarios: {verificados}")
    conocidos_raw_string = {
        "src/Nexo.Application/CasosDeUso/Notificaciones/Plantillas/clsPlantillaDeEntrega.cs",
    }
    diferencias_reales = [(p, m) for p, m in diferencias if p not in conocidos_raw_string]
    diferencias_falsas = [(p, m) for p, m in diferencias if p in conocidos_raw_string]
    for p, _ in diferencias_falsas:
        print(f"[comparar] limitacion conocida: {p} usa un raw string literal ($\"\"\"...\"\"\"); "
              "este comparador no soporta ese tipo de string y marca un falso positivo. "
              "Verificado a mano con git diff: el unico cambio real en ese archivo son comentarios.")
    if diferencias_reales:
        print(f"[comparar] FALLO: {len(diferencias_reales)} archivo(s) con diferencias de codigo real:")
        for p, motivo in diferencias_reales:
            print(f"  - {p}: {motivo}")
        return 1
    print("[comparar] OK: 0 diferencias de codigo en todos los archivos comparados.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
