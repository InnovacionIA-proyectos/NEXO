"""Excepciones de caso de uso. Application no conoce HTTP: no lleva status
code. Es la capa `api/` quien traduce el TIPO de excepcion a un codigo HTTP
(ver CONTRATO.md seccion 4). Equivalente a AppExceptions.cs de NEXO."""

from __future__ import annotations


class AppException(Exception):
    def __init__(self, codigo: str, mensaje: str, detalles: object | None = None) -> None:
        super().__init__(mensaje)
        self.codigo = codigo
        self.mensaje = mensaje
        self.detalles = detalles


class NotFoundException(AppException):
    """El recurso no existe (Api la traduce a 404)."""


class ValidationFailedException(AppException):
    """Input invalido por una regla de negocio (Api la traduce a 400)."""


class ConflictException(AppException):
    """Conflicto con el estado actual del recurso (Api la traduce a 409)."""


class ServiceUnavailableException(AppException):
    """Una dependencia externa no esta configurada o no responde (Api la traduce a 503)."""
