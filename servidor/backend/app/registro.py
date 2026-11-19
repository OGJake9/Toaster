"""
Registro de eventos. Todo evento de seguridad se escribe en una sola línea con
formato clave=valor para que Wazuh lo pueda decodificar:

    2026-11-19 10:00:00 tostadora-backend WARNING evento=login_fallido usuario=ana ip=1.2.3.4

Nunca se registran contraseñas, tokens ni mensajes completos del jugador.
"""
import logging
import os
import re

from . import config

_formato = logging.Formatter("%(asctime)s tostadora-backend %(levelname)s %(message)s",
                             datefmt="%Y-%m-%d %H:%M:%S")

log = logging.getLogger("tostadora")
log.setLevel(logging.INFO)
log.propagate = False

_consola = logging.StreamHandler()
_consola.setFormatter(_formato)
log.addHandler(_consola)

try:
    os.makedirs(os.path.dirname(config.LOG_ARCHIVO), exist_ok=True)
    _archivo = logging.FileHandler(config.LOG_ARCHIVO, encoding="utf-8")
    _archivo.setFormatter(_formato)
    log.addHandler(_archivo)
except OSError as e:  # el contenedor sigue funcionando aunque no pueda escribir el archivo
    log.warning("evento=log_sin_archivo detalle=%s", type(e).__name__)

_INSEGURO = re.compile(r"[^A-Za-z0-9_.:\-]")


def limpio(valor: object, largo: int = 40) -> str:
    """Evita inyección en los logs: solo caracteres seguros y largo acotado."""
    return _INSEGURO.sub("_", str(valor))[:largo]


def evento(nombre: str, nivel: int = logging.WARNING, **datos: object) -> None:
    partes = [f"evento={limpio(nombre)}"] + [f"{k}={limpio(v)}" for k, v in datos.items()]
    log.log(nivel, " ".join(partes))
