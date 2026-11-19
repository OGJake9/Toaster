"""Contraseñas (bcrypt), sesión (JWT en cookie httpOnly) y límites de intentos."""
import time
from collections import defaultdict, deque
from threading import Lock

import bcrypt
import jwt
from fastapi import HTTPException, Request, Response

from . import config

NOMBRE_COOKIE = "sesion"
RUTA_COOKIE = "/api"

# ---------------- Contraseñas ----------------

_COSTO_BCRYPT = 12
# Hash ficticio para comparar cuando el usuario no existe: así el tiempo de respuesta es
# igual exista o no el usuario (evita enumerar usuarios midiendo tiempos).
_HASH_FICTICIO = bcrypt.hashpw(b"usuario-inexistente", bcrypt.gensalt(_COSTO_BCRYPT))


def hashear(clave: str) -> str:
    return bcrypt.hashpw(clave.encode("utf-8"), bcrypt.gensalt(_COSTO_BCRYPT)).decode("ascii")


def verificar(clave: str, hash_guardado: str | None) -> bool:
    objetivo = hash_guardado.encode("ascii") if hash_guardado else _HASH_FICTICIO
    try:
        valido = bcrypt.checkpw(clave.encode("utf-8"), objetivo)
    except ValueError:
        return False
    return valido and hash_guardado is not None


# ---------------- Sesión ----------------

def crear_token(usuario_id: int, username: str) -> str:
    ahora = int(time.time())
    datos = {"sub": str(usuario_id), "usr": username, "iat": ahora,
             "exp": ahora + config.SESION_MINUTOS * 60}
    return jwt.encode(datos, config.JWT_SECRETO, algorithm="HS256")


def poner_cookie(respuesta: Response, token: str) -> None:
    respuesta.set_cookie(
        NOMBRE_COOKIE, token,
        max_age=config.SESION_MINUTOS * 60,
        path=RUTA_COOKIE,
        httponly=True,              # JavaScript no la puede leer (protege contra XSS)
        secure=config.COOKIE_SEGURA,  # solo viaja por HTTPS
        samesite="strict",          # no se envía desde otros sitios (protege contra CSRF)
    )


def borrar_cookie(respuesta: Response) -> None:
    respuesta.delete_cookie(NOMBRE_COOKIE, path=RUTA_COOKIE,
                            httponly=True, secure=config.COOKIE_SEGURA, samesite="strict")


def usuario_actual(request: Request) -> dict:
    """Dependencia de FastAPI: exige una sesión válida."""
    token = request.cookies.get(NOMBRE_COOKIE)
    if not token:
        raise HTTPException(401, "Inicia sesión para continuar.")
    try:
        datos = jwt.decode(token, config.JWT_SECRETO, algorithms=["HS256"],
                           options={"require": ["exp", "sub"]})
    except jwt.PyJWTError:
        raise HTTPException(401, "Tu sesión expiró. Vuelve a iniciar sesión.")
    return {"id": int(datos["sub"]), "username": datos.get("usr", "")}


# ---------------- Límites de intentos (en memoria) ----------------

class Limitador:
    """Ventana deslizante: como máximo `maximo` eventos por `ventana` segundos por clave."""

    def __init__(self, maximo: int, ventana: float):
        self.maximo = maximo
        self.ventana = ventana
        self._eventos: dict[str, deque] = defaultdict(deque)
        self._lock = Lock()

    def _limpiar(self, cola: deque, ahora: float) -> None:
        while cola and ahora - cola[0] > self.ventana:
            cola.popleft()

    def bloqueado(self, clave: str) -> bool:
        with self._lock:
            cola = self._eventos[clave]
            self._limpiar(cola, time.monotonic())
            return len(cola) >= self.maximo

    def registrar(self, clave: str) -> None:
        with self._lock:
            cola = self._eventos[clave]
            ahora = time.monotonic()
            self._limpiar(cola, ahora)
            cola.append(ahora)

    def reiniciar(self, clave: str) -> None:
        with self._lock:
            self._eventos.pop(clave, None)


# 5 logins fallidos por usuario+IP cada 15 minutos
login_fallidos = Limitador(maximo=5, ventana=15 * 60)
# Tosti: 20 mensajes cada 5 minutos y mínimo 3 segundos entre mensajes
tosti_por_ventana = Limitador(maximo=20, ventana=5 * 60)
tosti_seguidos = Limitador(maximo=1, ventana=3)
# Registro de cuentas: 5 por IP cada hora
registros_por_ip = Limitador(maximo=5, ventana=60 * 60)


def ip_cliente(request: Request) -> str:
    # Uvicorn ya reemplaza request.client con la IP real (X-Forwarded-For) solo si la
    # petición viene de Nginx (FORWARDED_ALLOW_IPS). Nadie más puede falsear esa cabecera.
    return request.client.host if request.client else "desconocida"
