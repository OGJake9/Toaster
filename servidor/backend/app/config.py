"""Configuración leída de variables de entorno (archivo .env, que NUNCA va al repo)."""
import os


def _obligatoria(nombre: str) -> str:
    valor = os.environ.get(nombre, "").strip()
    if not valor:
        raise RuntimeError(f"Falta la variable de entorno obligatoria {nombre}")
    return valor


def _booleana(nombre: str, defecto: bool) -> bool:
    return os.environ.get(nombre, str(defecto)).strip().lower() in ("1", "true", "si", "yes")


# Servidor
BIND_HOST = os.environ.get("BIND_HOST", "127.0.0.1")
BIND_PORT = int(os.environ.get("BIND_PORT", "8000"))
# IPs desde las que se aceptan cabeceras X-Forwarded-For (solo Nginx del frontend)
FORWARDED_ALLOW_IPS = os.environ.get("FORWARDED_ALLOW_IPS", "127.0.0.1")
MOSTRAR_DOCS = _booleana("MOSTRAR_DOCS", False)

# Base de datos (MySQL en sg-vargas-db, solo por TLS)
DB_HOST = _obligatoria("DB_HOST")
DB_PORT = int(os.environ.get("DB_PORT", "3306"))
DB_NOMBRE = os.environ.get("DB_NOMBRE", "toaster_db")
DB_USUARIO = _obligatoria("DB_USUARIO")
DB_CLAVE = _obligatoria("DB_CLAVE")
DB_CA = os.environ.get("DB_CA", "/certs/mysql-ca.pem")

# Sesión (JWT firmado en cookie httpOnly)
JWT_SECRETO = _obligatoria("JWT_SECRETO")
if len(JWT_SECRETO) < 32:
    raise RuntimeError("JWT_SECRETO debe tener al menos 32 caracteres")
SESION_MINUTOS = int(os.environ.get("SESION_MINUTOS", "120"))
# true en producción (HTTPS por Cloudflare). false solo para probar desde el Editor por http.
COOKIE_SEGURA = _booleana("COOKIE_SEGURA", True)

# Tosti (Ollama en sg-vargas-ai)
OLLAMA_URL = os.environ.get("OLLAMA_URL", "http://127.0.0.1:11434").rstrip("/")
OLLAMA_MODELO = os.environ.get("OLLAMA_MODELO", "gemma3:4b")

# Juego
META_TOSTADAS = int(os.environ.get("META_TOSTADAS", "300"))

# Registro
LOG_ARCHIVO = os.environ.get("LOG_ARCHIVO", "/var/log/tostadora/backend.log")
