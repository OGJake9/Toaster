# Bitácora de desarrollo — Tostadora Incremental

**Materia:** Simulación — Ingeniería Multimedia, UMNG
**Autor:** Jhon Jairo Vargas Avellaneda
**Motor:** Unity 6 LTS (build Web)
**Restricción:** sin Rigidbody ni Colliders; toda la física está programada a mano.

## Físicas implementadas
| Física | Dónde se usa | Script | Estado |
|---|---|---|---|
| Masa-resorte-amortiguador | Palanca de la tostadora (velocidad de salida de la tostada) | `Scripts/Fisica/` | Hecho |
| Movimiento parabólico | Vuelo de la tostada | `Scripts/Fisica/` | Hecho |
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

### 2026-11-09 — Sesión 3: movimiento parabólico y colisiones
- **Qué se hizo:** `Tostada` con tres estados (volando, deslizando en la pared, en el suelo) y `ColisionAABB` como caja de colisión propia en lugar de BoxCollider2D. Solo suma puntos si la tostada se pega en la pared. Constante base del resorte subida a 80 N/m para alejar la pared y que la carga importe.
- **Física trabajada:** movimiento parabólico (a = −g, Euler semi-implícito con 4 sub-pasos para que no atraviese superficies); detección AABB y resolución por el eje de menor penetración; choque perfectamente inelástico contra la pared; deslizamiento con fricción viscosa (m·dv/dt = −m·g − b·v, velocidad terminal m·g/b); rebote en el suelo con coeficiente de restitución.
- **Problemas y solución:** el resorte disparaba tostadas solas en cada oscilación después del lanzamiento → se agregó la condición de que el jugador lo haya armado. La carga se sentía poco decisiva → velocidad de carga bajada a 0.25 m/s.
- **Próximo paso:** economía incremental y UI (contador, tienda de mejoras, barra de carga).

### 2026-11-09 — Sesión 4: tienda y economía incremental
- **Qué se hizo:** pantalla de tienda con 4 mejoras (Resorte reforzado, Mantequilla extra, Recarga rápida, Ayudante de cocina), costos que crecen exponencialmente y nivel máximo por mejora. HUD con contador, producción por segundo y barra de carga. Toda la interfaz se construye desde código (`InterfazJuego`). La tienda pausa la simulación con Time.timeScale = 0.
- **Física trabajada:** la mejora del resorte cambia k en tiempo real (k = 80·1.25ⁿ N/m), lo que altera la velocidad de salida y el alcance parabólico.
- **Problemas y solución:** el clic en los botones de la tienda también cargaba el resorte → la carga quedó solo con Espacio. Advertencia de API obsoleta (FindFirstObjectByType) → reemplazada por FindAnyObjectByType.
- **Próximo paso:** despliegue en los nodos de seguridad.