"""Conexión a MySQL. Siempre cifrada (TLS) y verificando el certificado del servidor."""
from contextlib import contextmanager

import pymysql
import pymysql.cursors

from . import config


@contextmanager
def conexion():
    con = pymysql.connect(
        host=config.DB_HOST,
        port=config.DB_PORT,
        user=config.DB_USUARIO,
        password=config.DB_CLAVE,
        database=config.DB_NOMBRE,
        charset="utf8mb4",
        cursorclass=pymysql.cursors.DictCursor,
        connect_timeout=5,
        read_timeout=10,
        write_timeout=10,
        # TLS obligatorio. Se verifica la cadena con la CA de MySQL (evita un servidor falso
        # en medio). El nombre no se verifica porque el certificado autogenerado de MySQL no
        # trae el hostname; la IP ya está protegida por Tailscale.
        ssl={"ca": config.DB_CA, "check_hostname": False},
        autocommit=False,
    )
    try:
        yield con
        con.commit()
    except Exception:
        con.rollback()
        raise
    finally:
        con.close()
