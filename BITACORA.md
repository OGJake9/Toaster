# Bitácora de desarrollo — Tostadora Incremental

**Materia:** Simulación — Ingeniería Multimedia, UMNG
**Autor:** Jhon Jairo Vargas Avellaneda
**Motor:** Unity 6 LTS (build Web)
**Restricción:** sin Rigidbody ni Colliders; toda la física está programada a mano.

## Físicas implementadas
| Física | Dónde se usa | Script | Estado |
|---|---|---|---|
| Masa-resorte-amortiguador | Palanca de la tostadora (velocidad de salida de la tostada) | `Scripts/Fisica/` | Hecho |
| Movimiento parabólico | Vuelo de la tostada | `Scripts/Fisica/` | Pendiente |
| Colisiones | Choque con pared y suelo, tostada pegada y deslizándose | `Scripts/Fisica/` | Pendiente |

---

## Registro

<!-- Copia este bloque para cada sesión de trabajo. La entrada más reciente va arriba. -->

### 2026-11-09 — Sesión 1: configuración inicial
- **Qué se hizo:** creación del proyecto Unity (Universal 2D), estructura de carpetas, repositorio Git y esta bitácora. Build Web de prueba con escena vacía.
- **Física trabajada:** ninguna (configuración).
- **Problemas y solución:** —
- **Próximo paso:** clase `EstadoJuego` y resorte de la palanca.
- **Commits:** <!-- pega aquí el enlace o el hash del commit -->

### 2026-11-09 — Sesión 2: palanca masa-resorte-amortiguador
- **Qué se hizo:** clase `EstadoJuego` (economía serializable a JSON, separada de Unity) y `PalancaTostadora`: palanca modelada como m·x'' + c·x' + k·x = 0, integrada con Euler semi-implícito en FixedUpdate, sin Rigidbody. Mejora del resorte (k crece 25 % por nivel).
- **Física trabajada:** masa-resorte-amortiguador. Se compara la velocidad simulada con la teórica sin amortiguamiento v = x0·√(k/m); la simulada es menor por la disipación del amortiguador.
- **Problemas y solución:** la escena no quedaba en el commit si no se guardaba antes (Ctrl+S).
- **Próximo paso:** tostada en movimiento parabólico y colisiones con pared y suelo.
