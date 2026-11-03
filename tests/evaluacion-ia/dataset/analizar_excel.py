"""Reproduce la detección de encabezados de clsExcelSolicitudesAdapter (analizar-excel) sobre una hoja de openpyxl."""
import datetime
import re

FILAS_BUSQUEDA = 15


def _texto(v):
    if v is None:
        return ''
    if isinstance(v, float) and v.is_integer():
        v = int(v)
    if isinstance(v, (datetime.datetime, datetime.date)):
        return v.strftime('%d/%m/%Y')
    return str(v).strip()


def _es_numerico(s):
    return re.fullmatch(r'[-+]?\d+([.,]\d+)?', s.replace(' ', '')) is not None


def analizar(ws):
    usadas = [(c.row, c.column) for fila in ws.iter_rows() for c in fila if _texto(c.value)]
    if not usadas:
        return None
    pf, uf = min(r for r, _ in usadas), max(r for r, _ in usadas)
    pc, uc = min(c for _, c in usadas), max(c for _, c in usadas)
    comb = {}
    for rango in ws.merged_cells.ranges:
        v = _texto(ws.cell(rango.min_row, rango.min_col).value)
        if not v:
            continue
        for r in range(rango.min_row, rango.max_row + 1):
            for c in range(rango.min_col, rango.max_col + 1):
                comb[(r, c)] = v

    def celda(r, c):
        return comb.get((r, c), _texto(ws.cell(r, c).value))

    fe = 0
    for r in range(pf, min(uf, pf + FILAS_BUSQUEDA) + 1):
        if len({celda(r, c).lower() for c in range(pc, uc + 1) if celda(r, c)}) >= 3:
            fe = r
            break
    if not fe:
        return None
    fs = 0
    if fe < uf:
        rot = [celda(fe, c) for c in range(pc, uc + 1) if celda(fe, c)]
        if len(rot) != len({x.lower() for x in rot}):
            sub = [celda(fe + 1, c) for c in range(pc, uc + 1) if celda(fe + 1, c)]
            if len(sub) >= 2 and all(not _es_numerico(v) for v in sub):
                fs = fe + 1
    enc = []
    for c in range(pc, uc + 1):
        t = celda(fe, c)
        if fs:
            s = celda(fs, c)
            if s and s.lower() != t.lower():
                t = s if not t else '%s · %s' % (t, s)
        enc.append(t if t else 'Columna %s' % _letra(c))
    filas = []
    for r in range(max(fe, fs) + 1, uf + 1):
        f = [celda(r, c) for c in range(pc, uc + 1)]
        if any(f):
            filas.append(f)
    return {'filaEncabezado': fe, 'filaSubEncabezado': fs or None, 'primeraColumna': _letra(pc),
            'encabezados': enc, 'filas': filas}


def _letra(n):
    s = ''
    while n:
        n, r = divmod(n - 1, 26)
        s = chr(65 + r) + s
    return s
