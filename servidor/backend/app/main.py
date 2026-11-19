"""
API del juego Tostadora Incremental (G09).

Rutas (todas bajo /api, Nginx del frontend hace de proxy):
  POST /api/auth/registro   POST /api/auth/login   POST /api/auth/logout
  GET  /api/partida         PUT  /api/partida      (validación anti-trampa)
  GET  /api/ranking
  POST /api/tosti                                  (NPC con IA, con filtros)
  GET  /api/salud
"""
import logging

import pymysql
from fastapi import Depends, FastAPI, HTTPException, Request, Response
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field, field_validator

from . import config, economia, seguridad, tosti
from .db import conexion
from .registro import evento, log

app = FastAPI(
    title="Tostadora Incremental API",
    docs_url="/api/docs" if config.MOSTRAR_DOCS else None,
    redoc_url=None,
    openapi_url="/api/openapi.json" if config.MOSTRAR_DOCS else None,
)


# ===================== Modelos =====================

class Credenciales(BaseModel):
    username: str = Field(pattern=r"^[A-Za-z0-9_]{3,20}$")
    password: str = Field(min_length=8, max_length=64)

    @field_validator("password")
    @classmethod
    def maximo_bcrypt(cls, v: str) -> str:
        if len(v.encode("utf-8")) > 72:  # bcrypt solo usa los primeros 72 bytes
            raise ValueError("contraseña demasiado larga")
        return v


class Partida(BaseModel):
    """Mismos nombres de campo que EstadoJuego.cs (JsonUtility)."""
    tostadas: float = Field(ge=0, le=1e15)
    tostadasTotales: float = Field(ge=0, le=1e15)
    lanzamientos: int = Field(ge=0, le=10**12)
    impactos: int = Field(ge=0, le=10**12)
    niveles: list[int] = Field(min_length=4, max_length=4)


class MensajeTosti(BaseModel):
    mensaje: str | None = Field(default=None, max_length=500)
    evento: str | None = Field(default=None, max_length=30)


# ===================== Errores =====================

@app.exception_handler(RequestValidationError)
async def datos_invalidos(request: Request, exc: RequestValidationError):
    # No devolvemos el detalle interno de la validación
    return JSONResponse(status_code=422, content={"detail": "Datos inválidos."})


@app.exception_handler(pymysql.MySQLError)
async def error_bd(request: Request, exc: pymysql.MySQLError):
    evento("error_bd", logging.ERROR, tipo=type(exc).__name__)
    return JSONResponse(status_code=503, content={"detail": "Servicio no disponible. Intenta más tarde."})


@app.middleware("http")
async def cabeceras_seguridad(request: Request, call_next):
    respuesta = await call_next(request)
    respuesta.headers["Cache-Control"] = "no-store"
    respuesta.headers["X-Content-Type-Options"] = "nosniff"
    return respuesta


def registrar_sospechoso(cur, usuario_id: int | None, tipo: str, detalle: str) -> None:
    cur.execute("INSERT INTO eventos_sospechosos (usuario_id, tipo, detalle) VALUES (%s, %s, %s)",
                (usuario_id, tipo, detalle[:500]))


# ===================== Rutas =====================

@app.get("/api/salud")
def salud():
    return {"estado": "ok"}


@app.post("/api/auth/registro", status_code=201)
def registrar(datos: Credenciales, request: Request):
    ip = seguridad.ip_cliente(request)
    if seguridad.registros_por_ip.bloqueado(ip):
        evento("registro_limitado", ip=ip)
        raise HTTPException(429, "Demasiadas cuentas creadas. Intenta más tarde.")

    hash_clave = seguridad.hashear(datos.password)
    try:
        with conexion() as con, con.cursor() as cur:
            cur.execute("INSERT INTO usuarios (username, password_hash) VALUES (%s, %s)",
                        (datos.username, hash_clave))
            cur.execute("INSERT INTO partidas (usuario_id) VALUES (%s)", (cur.lastrowid,))
    except pymysql.err.IntegrityError:
        raise HTTPException(409, "Ese nombre de usuario ya existe.")

    seguridad.registros_por_ip.registrar(ip)
    evento("registro", logging.INFO, usuario=datos.username, ip=ip)
    return {"detail": "Cuenta creada."}


@app.post("/api/auth/login")
def login(datos: Credenciales, request: Request, respuesta: Response):
    ip = seguridad.ip_cliente(request)
    clave_limite = f"{datos.username.lower()}|{ip}"
    if seguridad.login_fallidos.bloqueado(clave_limite):
        evento("login_bloqueado", usuario=datos.username, ip=ip)
        raise HTTPException(429, "Demasiados intentos. Espera 15 minutos.")

    with conexion() as con, con.cursor() as cur:
        cur.execute("SELECT id, username, password_hash FROM usuarios WHERE username = %s",
                    (datos.username,))
        fila = cur.fetchone()
        if not seguridad.verificar(datos.password, fila["password_hash"] if fila else None):
            seguridad.login_fallidos.registrar(clave_limite)
            evento("login_fallido", usuario=datos.username, ip=ip)
            raise HTTPException(401, "Usuario o contraseña incorrectos.")

        cur.execute("UPDATE usuarios SET ultimo_login = NOW() WHERE id = %s", (fila["id"],))
        # El reloj anti-trampa arranca al iniciar sesión: el tiempo desconectado no cuenta.
        cur.execute("UPDATE partidas SET actualizado_en = NOW() WHERE usuario_id = %s", (fila["id"],))

    seguridad.login_fallidos.reiniciar(clave_limite)
    seguridad.poner_cookie(respuesta, seguridad.crear_token(fila["id"], fila["username"]))
    evento("login_ok", logging.INFO, usuario=fila["username"], ip=ip)
    return {"username": fila["username"]}


@app.post("/api/auth/logout")
def logout(respuesta: Response):
    seguridad.borrar_cookie(respuesta)
    return {"detail": "Sesión cerrada."}


def _leer_estado(cur, usuario_id: int) -> tuple[economia.Estado, float] | None:
    cur.execute(
        "SELECT tostadas, tostadas_totales, lanzamientos, impactos, nivel_resorte, nivel_mantequilla, "
        "nivel_recarga, nivel_ayudante, TIMESTAMPDIFF(SECOND, actualizado_en, NOW()) AS segundos "
        "FROM partidas WHERE usuario_id = %s", (usuario_id,))
    f = cur.fetchone()
    if not f:
        return None
    estado = economia.Estado(
        tostadas=f["tostadas"], tostadas_totales=f["tostadas_totales"],
        lanzamientos=f["lanzamientos"], impactos=f["impactos"],
        niveles=[f["nivel_resorte"], f["nivel_mantequilla"], f["nivel_recarga"], f["nivel_ayudante"]])
    return estado, float(f["segundos"] or 0)


@app.get("/api/partida")
def cargar_partida(usuario: dict = Depends(seguridad.usuario_actual)):
    with conexion() as con, con.cursor() as cur:
        leido = _leer_estado(cur, usuario["id"])
    if not leido:
        raise HTTPException(404, "Partida no encontrada.")
    e, _ = leido
    return {"tostadas": e.tostadas, "tostadasTotales": e.tostadas_totales,
            "lanzamientos": e.lanzamientos, "impactos": e.impactos, "niveles": e.niveles}


@app.put("/api/partida")
def guardar_partida(datos: Partida, request: Request, usuario: dict = Depends(seguridad.usuario_actual)):
    nuevo = economia.Estado(datos.tostadas, datos.tostadasTotales, datos.lanzamientos,
                            datos.impactos, list(datos.niveles))
    with conexion() as con, con.cursor() as cur:
        leido = _leer_estado(cur, usuario["id"])
        if not leido:
            raise HTTPException(404, "Partida no encontrada.")
        viejo, segundos = leido

        errores = economia.validar(viejo, nuevo, segundos)
        if errores:
            detalle = "; ".join(errores)
            registrar_sospechoso(cur, usuario["id"], "trampa", detalle)
            con.commit()
            evento("trampa", usuario=usuario["username"], ip=seguridad.ip_cliente(request),
                   ganancia=int(nuevo.tostadas_totales - viejo.tostadas_totales), segundos=int(segundos))
            raise HTTPException(422, "Progreso rechazado: no es posible en ese tiempo.")

        cur.execute(
            "UPDATE partidas SET tostadas=%s, tostadas_totales=%s, lanzamientos=%s, impactos=%s, "
            "nivel_resorte=%s, nivel_mantequilla=%s, nivel_recarga=%s, nivel_ayudante=%s, "
            "actualizado_en=NOW() WHERE usuario_id=%s",
            (nuevo.tostadas, nuevo.tostadas_totales, nuevo.lanzamientos, nuevo.impactos,
             *nuevo.niveles, usuario["id"]))
    return {"detail": "Partida guardada."}


@app.get("/api/ranking")
def ranking():
    with conexion() as con, con.cursor() as cur:
        cur.execute("SELECT u.username, p.tostadas_totales FROM partidas p "
                    "JOIN usuarios u ON u.id = p.usuario_id "
                    "WHERE p.tostadas_totales > 0 ORDER BY p.tostadas_totales DESC LIMIT 10")
        filas = cur.fetchall()
    return {"jugadores": [{"username": f["username"], "tostadasTotales": f["tostadas_totales"]}
                          for f in filas]}


@app.post("/api/tosti")
def hablar_con_tosti(datos: MensajeTosti, request: Request,
                     usuario: dict = Depends(seguridad.usuario_actual)):
    clave = str(usuario["id"])
    if seguridad.tosti_seguidos.bloqueado(clave) or seguridad.tosti_por_ventana.bloqueado(clave):
        raise HTTPException(429, "Tosti necesita respirar. Espera un momento.")
    seguridad.tosti_seguidos.registrar(clave)
    seguridad.tosti_por_ventana.registrar(clave)

    ip = seguridad.ip_cliente(request)
    mensaje = tosti.limpiar_mensaje(datos.mensaje) if datos.mensaje else None
    evento_juego = datos.evento if datos.evento in tosti.EVENTOS_VALIDOS else None

    with conexion() as con, con.cursor() as cur:
        # Capa 1: filtro de entrada
        if mensaje and tosti.es_inyeccion(mensaje):
            registrar_sospechoso(cur, usuario["id"], "prompt_injection_entrada", mensaje)
            evento("prompt_injection", usuario=usuario["username"], ip=ip, capa="entrada")
            return {"respuesta": tosti.RESPUESTA_SEGURA}

        leido = _leer_estado(cur, usuario["id"])
        if not leido:
            raise HTTPException(404, "Partida no encontrada.")
        pista = tosti.elegir_pista(leido[0])  # Capa 4

        try:
            respuesta = tosti.preguntar_a_ollama(pista, evento_juego, mensaje)  # Capa 2
        except Exception as e:  # Ollama apagado o lento: el juego sigue funcionando
            evento("tosti_sin_ia", logging.ERROR, tipo=type(e).__name__)
            return {"respuesta": tosti.RESPUESTA_SIN_IA}

        # Capa 3: filtro de salida
        if tosti.fuga_de_instrucciones(respuesta):
            registrar_sospechoso(cur, usuario["id"], "prompt_injection_salida", mensaje or "")
            evento("prompt_injection", usuario=usuario["username"], ip=ip, capa="salida")
            return {"respuesta": tosti.RESPUESTA_SEGURA}

    return {"respuesta": respuesta}


log.info("evento=inicio_api docs=%s", config.MOSTRAR_DOCS)
