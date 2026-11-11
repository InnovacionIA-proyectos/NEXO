# -*- coding: utf-8 -*-
"""Verifica enlaces relativos [texto](ruta) y [texto](ruta#ancla) en todos los .md del repo
(rama pr454), confirmando que la ruta destino existe en el working tree y, si trae ancla,
que el documento destino tiene un encabezado que genera ese slug (regla de GitHub: minusculas,
espacios -> guiones, se quitan los caracteres que no sean letra/numero/guion/espacio)."""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).parent
LINK_RE = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*)$", re.MULTILINE)


def slug(texto):
    texto = texto.strip().lower()
    texto = re.sub(r"[^\w\s\-]", "", texto, flags=re.UNICODE)
    texto = re.sub(r"\s+", "-", texto)
    return texto


def anclas_de(ruta):
    try:
        contenido = ruta.read_text(encoding="utf-8")
    except Exception:
        return set()
    return {slug(m.group(2)) for m in HEADING_RE.finditer(contenido)}


def main():
    md_files = [p for p in ROOT.rglob("*.md") if ".git" not in p.parts]
    print(f"archivos .md encontrados: {len(md_files)}")
    total_enlaces = 0
    rotos = []
    anclas_rotas = []
    for md in md_files:
        try:
            contenido = md.read_text(encoding="utf-8")
        except Exception as e:
            print(f"  no se pudo leer {md}: {e}")
            continue
        for m in LINK_RE.finditer(contenido):
            destino = m.group(1).strip()
            if destino.startswith(("http://", "https://", "mailto:", "#")):
                continue  # externo o ancla al mismo documento (no cruzamos eso aqui)
            total_enlaces += 1
            ancla = None
            ruta_parte = destino
            if "#" in destino:
                ruta_parte, ancla = destino.split("#", 1)
            if not ruta_parte:
                continue
            destino_path = (md.parent / ruta_parte).resolve()
            if not destino_path.exists():
                rotos.append((str(md.relative_to(ROOT)), destino))
                continue
            if ancla and destino_path.suffix == ".md":
                slugs = anclas_de(destino_path)
                if slug(ancla.replace("-", " ")) not in slugs and ancla.lower() not in slugs:
                    anclas_rotas.append((str(md.relative_to(ROOT)), destino))
    print(f"enlaces relativos revisados: {total_enlaces}")
    print(f"enlaces rotos (ruta no existe): {len(rotos)}")
    for r in rotos:
        print(f"  ROTO: {r[0]} -> {r[1]}")
    print(f"anclas posiblemente rotas: {len(anclas_rotas)}")
    for r in anclas_rotas:
        print(f"  ANCLA?: {r[0]} -> {r[1]}")
    if rotos:
        sys.exit(1)


if __name__ == "__main__":
    main()
