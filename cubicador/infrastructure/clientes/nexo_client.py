"""Implementa ILineaSolicitudPort: GET /api/lineas-solicitud/{id}/cubicacion
en NEXO (ver docs/CONTRATOS.md seccion E). NEXO es quien debe exponer este
endpoint - ver docs/DECISIONES_TECNICAS.md #8."""

from __future__ import annotations

import httpx

from application.ports.externos import LineaParaCubicacion, PosicionCarga


class NexoLineaSolicitudClient:
    def __init__(self, base_url: str, api_key: str, timeout: float = 10.0) -> None:
        self._base_url = base_url.rstrip("/")
        self._api_key = api_key
        self._timeout = timeout

    async def obtener(self, linea_solicitud_id: str) -> LineaParaCubicacion | None:
        url = f"{self._base_url}/api/lineas-solicitud/{linea_solicitud_id}/cubicacion"
        headers = {"Authorization": f"Bearer {self._api_key}"}
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            resp = await client.get(url, headers=headers)
        if resp.status_code == 404:
            return None
        resp.raise_for_status()
        data = resp.json()
        return LineaParaCubicacion(
            linea_id=linea_solicitud_id,
            posiciones=[
                PosicionCarga(
                    descripcion=p.get("descripcion"), cantidad=p["cantidad"],
                    largo_cm=p["largoCm"], ancho_cm=p["anchoCm"], alto_cm=p["altoCm"], peso_kg=p.get("pesoKg"),
                )
                for p in data.get("posiciones", [])
            ],
            origen=data.get("origen"), destino=data.get("destino"),
        )
