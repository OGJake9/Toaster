"""
Reglas de la economía del juego y validación ANTI-TRAMPA.

Las constantes son EXACTAMENTE las de Assets/Scripts/Juego/EstadoJuego.cs. Si se cambia
el balance en Unity, hay que cambiarlo aquí también.

El cliente (el navegador) no es confiable: alguien puede editar la memoria del juego o
mandar peticiones a mano. Por eso el servidor recalcula cuánto pudo ganar el jugador como
MÁXIMO en el tiempo transcurrido y rechaza cualquier guardado imposible.
"""
import math
from dataclasses import dataclass

# (nombre, costo base, factor de costo, nivel máximo) — igual que EstadoJuego.Mejoras
MEJORAS = [
    ("Resorte reforzado", 10, 1.6, 8),
    ("Mantequilla extra", 15, 1.7, 20),
    ("Recarga rápida", 20, 1.8, 8),
    ("Ayudante de cocina", 25, 1.5, 30),
]
RESORTE, MANTEQUILLA, RECARGA, AYUDANTE = range(4)

RECARGA_BASE = 0.8
RECARGA_FACTOR = 0.85
PRODUCCION_POR_AYUDANTE = 0.5

# Margen para no castigar a jugadores honestos (latencia, redondeos, guardado tardío)
TOLERANCIA = 1.5
HOLGURA_TOSTADAS = 20.0
HOLGURA_IMPACTOS = 3


def costo(tipo: int, nivel: int) -> float:
    _, base, factor, _ = MEJORAS[tipo]
    return math.floor(base * factor ** nivel)


def por_impacto(nivel_mantequilla: int) -> int:
    return 1 + nivel_mantequilla


def tiempo_recarga(nivel_recarga: int) -> float:
    return RECARGA_BASE * RECARGA_FACTOR ** nivel_recarga


def produccion(nivel_ayudante: int) -> float:
    return nivel_ayudante * PRODUCCION_POR_AYUDANTE


@dataclass
class Estado:
    tostadas: float
    tostadas_totales: float
    lanzamientos: int
    impactos: int
    niveles: list[int]


def validar(viejo: Estado, nuevo: Estado, segundos: float) -> list[str]:
    """Devuelve la lista de motivos de rechazo. Lista vacía = guardado válido."""
    errores: list[str] = []
    dt = max(1.0, segundos)

    # 1) Coherencia básica
    if len(nuevo.niveles) != len(MEJORAS):
        return ["cantidad de mejoras inválida"]
    for i, (n_nuevo, n_viejo) in enumerate(zip(nuevo.niveles, viejo.niveles)):
        maximo = MEJORAS[i][3]
        if not 0 <= n_nuevo <= maximo:
            errores.append(f"nivel {i} fuera de rango ({n_nuevo})")
        if n_nuevo < n_viejo:
            errores.append(f"nivel {i} bajó de {n_viejo} a {n_nuevo}")
    if nuevo.tostadas_totales < viejo.tostadas_totales - 1e-6:
        errores.append("las tostadas totales bajaron")
    if nuevo.lanzamientos < viejo.lanzamientos or nuevo.impactos < viejo.impactos:
        errores.append("los contadores bajaron")
    if nuevo.impactos > nuevo.lanzamientos:
        errores.append("más impactos que lanzamientos")
    if nuevo.tostadas > nuevo.tostadas_totales + 1e-6:
        errores.append("saldo mayor que el total histórico")
    if errores:
        return errores

    # 2) Ritmo de impactos: no se puede lanzar más rápido que el tiempo de recarga
    recarga_min = tiempo_recarga(nuevo.niveles[RECARGA])
    max_impactos = dt / recarga_min * TOLERANCIA + HOLGURA_IMPACTOS
    nuevos_impactos = nuevo.impactos - viejo.impactos
    nuevos_lanzamientos = nuevo.lanzamientos - viejo.lanzamientos
    if nuevos_lanzamientos > max_impactos:
        errores.append(f"{nuevos_lanzamientos} lanzamientos en {dt:.0f} s (máximo {max_impactos:.0f})")

    # 3) Tostadas ganadas: impactos × tostadas por impacto + producción del ayudante
    ganancia = nuevo.tostadas_totales - viejo.tostadas_totales
    max_activa = min(nuevos_impactos, max_impactos) * por_impacto(nuevo.niveles[MANTEQUILLA])
    max_pasiva = produccion(nuevo.niveles[AYUDANTE]) * dt
    limite = (max_activa + max_pasiva) * TOLERANCIA + HOLGURA_TOSTADAS
    if ganancia > limite:
        errores.append(f"ganó {ganancia:.0f} tostadas en {dt:.0f} s (máximo {limite:.0f})")

    # 4) Compras: el saldo final debe cuadrar con lo ganado menos lo gastado
    gasto = sum(costo(i, n) for i in range(len(MEJORAS))
                for n in range(viejo.niveles[i], nuevo.niveles[i]))
    saldo_maximo = viejo.tostadas + ganancia - gasto
    if saldo_maximo < -1.0:
        errores.append(f"compró mejoras por {gasto:.0f} sin tostadas suficientes")
    elif nuevo.tostadas > saldo_maximo + 1.0:
        errores.append(f"saldo {nuevo.tostadas:.0f} mayor que el posible {saldo_maximo:.0f}")

    return errores


def mejora_mas_barata(estado: Estado) -> tuple[str, float] | None:
    """La mejora disponible más barata (para las pistas de Tosti)."""
    opciones = [(MEJORAS[i][0], costo(i, estado.niveles[i]))
                for i in range(len(MEJORAS)) if estado.niveles[i] < MEJORAS[i][3]]
    return min(opciones, key=lambda o: o[1]) if opciones else None
