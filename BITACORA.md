# Bitácora de desarrollo — Tostadora Incremental

**Materia:** Simulación — Ingeniería Multimedia, UMNG · **Grupo:** G09\
**Autor:** Jhon Jairo Vargas Avellaneda (trabajo individual)\
**Motor:** Unity 6 LTS (Universal 2D, C#) · **Builds:** Web (WebGL) y Windows\
**Restricción:** sin Rigidbody ni Colliders de Unity; toda la física está programada a mano.\
**Repositorio:** https://github.com/OGJake9/Toaster (cada sesión enlaza sus commits)

**El juego:** una tostadora con resorte lanza tostadas contra la pared de una cocina. Solo las tostadas que se pegan en la pared suman. Con las tostadas se compran mejoras (resorte más rígido, más tostadas por impacto, recarga más rápida, producción automática) hasta llegar a la meta de 300. Un tarro de mermelada en el camino funciona como *skill shot*: si la tostada cae sobre él, rebota potenciada hacia la pared. El juego se publicó además en línea con inicio de sesión, guardado en base de datos, ranking y un NPC con IA local ("Tosti") que da pistas, como parte del proyecto final de Seguridad Informática.

## Físicas implementadas

| # | Física | Dónde se usa | Script | Estado |
|---|---|---|---|---|
| 1 | Masa-resorte-amortiguador (m·x'' + c·x' + k·x = 0, Euler semi-implícito) | Palanca de la tostadora: define la velocidad de salida | `Fisica/PalancaTostadora.cs` | Hecho |
| 2 | Movimiento parabólico (a = −g, 4 sub-pasos por paso físico) | Vuelo de la tostada | `Fisica/Tostada.cs` | Hecho |
| 3 | Colisiones AABB propias (eje de menor penetración) | Pared (choque inelástico + deslizamiento con fricción viscosa), suelo (restitución) | `Fisica/ColisionAABB.cs`, `Fisica/Tostada.cs` | Hecho |
| 4 | Rebote con restitución e = 1,5 y tiro parabólico inverso | Tarro de mermelada: la v_x se despeja para llegar a la pared | `Fisica/Tostada.cs` | Hecho |
| 5 | Predicción por re-simulación | Franjas roja/verde de la barra de carga | `Fisica/PredictorTiro.cs` | Hecho |

## Resumen del avance

| Fecha | Sesión | Resultado | Commits |
|---|---|---|---|
| 2026-11-09 | 1 | Proyecto, repositorio, bitácora y build Web de prueba | `fe3d83e` |
| 2026-11-09 | 2 | Resorte masa-resorte-amortiguador y economía | `99398e6`, `223adf5` |
| 2026-11-09 | 3 | Tiro parabólico y colisiones AABB | `f57409f`, `8038b7d` |
| 2026-11-09 | 4 | Tienda, HUD y economía incremental | `7189943`, `c64a11c` |
| 2026-11-18 | 5 | Menú de inicio, login, ranking, modo sin conexión; IA local (Tosti) | `0dfdfac`, `af539c1` |
| 2026-11-18 | 6 | Servidores: base de datos y API endurecida | `48cd4df` |
| 2026-11-19 | 7 | Juego conectado al servidor, meta de 300 tostadas | `d39a9ac`, `68ceaa1` |
| 2026-11-19 | 8 | Sprites finales, tarro de mermelada, recarga progresiva, predictor | `90f4178` |
| 2026-11-20 | 9 | Build Web publicado en internet | `8e01e9d` |
| 2026-11-20 | 10 | Pruebas finales, correcciones de interfaz, ejecutable de Windows | `48b5f97` |

---

## Registro por sesión

### 2026-11-09 — Sesión 1: configuración inicial
- **Qué se hizo:** creación del proyecto Unity (Universal 2D), estructura de carpetas (`Scripts/Fisica`, `Scripts/Juego`, `Scripts/UI`), repositorio Git privado y esta bitácora. Build Web de prueba con escena vacía para confirmar que el flujo de publicación funciona desde el inicio.
- **Física trabajada:** ninguna (configuración).
- **Problemas y solución:** el primer `git init` se ejecutó por error en `C:\Windows\System32` → se borró el `.git` creado ahí y se repitió dentro de la carpeta del proyecto. Unity genera artefactos de Burst en `Data/` → se agregaron al `.gitignore`.
- **Próximo paso:** clase `EstadoJuego` y resorte de la palanca.
- **Commits:** `fe3d83e`

### 2026-11-09 — Sesión 2: palanca masa-resorte-amortiguador
- **Qué se hizo:** clase `EstadoJuego` (economía serializable a JSON, separada de Unity) y `PalancaTostadora`: la palanca se modela como m·x'' + c·x' + k·x = 0, integrada con Euler semi-implícito en `FixedUpdate`, sin Rigidbody. Escena con tostadora, plataforma y controlador.
- **Física trabajada:** masa-resorte-amortiguador (m = 0,2 kg, c = 0,8 N·s/m, compresión máxima 0,3 m). Se compara en consola la velocidad simulada con la teórica sin amortiguamiento, v = x₀·√(k/m); la simulada es menor por la energía que disipa el amortiguador.
- **Problemas y solución:** la escena no quedaba en el commit si no se guardaba antes → se adoptó Ctrl+S antes de cada commit.
- **Próximo paso:** tostada en movimiento parabólico y colisiones con pared y suelo.
- **Commits:** `99398e6`, `223adf5`

### 2026-11-09 — Sesión 3: movimiento parabólico y colisiones
- **Qué se hizo:** `Tostada` con tres estados (volando, deslizando en la pared, en el suelo) y `ColisionAABB` como caja de colisión propia en lugar de BoxCollider2D, con Gizmos para verlas en el editor. Solo suma puntos si la tostada se pega en la pared.
- **Física trabajada:** movimiento parabólico (a = −g, Euler semi-implícito con 4 sub-pasos para que la tostada no atraviese superficies a alta velocidad); detección AABB y resolución por el eje de menor penetración; choque perfectamente inelástico contra la pared; deslizamiento con fricción viscosa (m·dv/dt = −m·g − b·v, velocidad terminal m·g/b); rebote en el suelo con coeficiente de restitución.
- **Problemas y solución:** el resorte disparaba tostadas solo en cada oscilación después del lanzamiento → se agregó la condición de que el jugador lo haya armado. La pared quedaba demasiado cerca y la carga no importaba → k base subida a 80 N/m, pared más lejos y velocidad de carga bajada a 0,25 m/s. Las cajas de colisión no se veían → activar Gizmos en la vista Game.
- **Próximo paso:** economía incremental y UI.
- **Commits:** `f57409f`, `8038b7d`

### 2026-11-09 — Sesión 4: tienda y economía incremental
- **Qué se hizo:** tienda con 4 mejoras (Resorte reforzado, Mantequilla extra, Recarga rápida, Ayudante de cocina), costos que crecen exponencialmente y nivel máximo por mejora. HUD con contador, producción por segundo y barra de carga. Toda la interfaz se construye desde código (`InterfazJuego`, `FabricaUI`). La tienda pausa la simulación con `Time.timeScale = 0`.
- **Física trabajada:** la mejora del resorte cambia k en tiempo real, lo que altera la velocidad de salida y el alcance parabólico.
- **Problemas y solución:** el clic en los botones de la tienda también cargaba el resorte → la carga quedó solo con Espacio. Advertencia de API obsoleta (`FindFirstObjectByType`) → reemplazada por `FindAnyObjectByType`.
- **Próximo paso:** menú de inicio y conexión con servidores.
- **Commits:** `7189943`, `c64a11c`

### 2026-11-18 — Sesión 5: menú de inicio, login y NPC con IA
- **Qué se hizo:** menú de inicio con iniciar sesión, crear cuenta, ranking y "Jugar sin conexión" (`MenuInicio`), con validación de usuario (3–20 caracteres alfanuméricos) y contraseña (8–64). Cliente HTTP (`ServicioAPI`, `Sesion`) para hablar con el servidor. Se instaló un modelo de lenguaje local (Ollama con gemma3:4b) en un MacBook para el NPC "Tosti", que da pistas según el estado de la partida. Se diseñó su prompt de sistema y se probaron pistas e intentos de manipulación antes y después de las protecciones.
- **Física trabajada:** las pistas de Tosti se basan en la física del juego (p. ej., que más rigidez del resorte da más alcance).
- **Problemas y solución:** el Mac tenía solo 16 GB libres → limpieza de cachés, instaladores y carpetas `Library` de Unity hasta 37 GB, y regla de no bajar de 10 GB. El modelo revelaba sus instrucciones internas cuando se le pedía con cierto disfraz → filtros de entrada y de salida además del prompt. El filtro bloqueaba por error la pregunta "¿cuáles son las reglas del juego?" → se ajustó la expresión. Un comando de terminal fallaba por la expansión de `!` en zsh → se reescribió.
- **Próximo paso:** levantar la base de datos y la API.
- **Commits:** `0dfdfac`, `af539c1`

### 2026-11-18 — Sesión 6: servidores (base de datos y API)
- **Qué se hizo:** tres máquinas virtuales Ubuntu en el PC (base de datos, API y servidor web), acceso SSH solo con llave y una red privada (Tailscale) con reglas que solo permiten las conexiones necesarias. Base de datos MySQL con cuentas, partidas y registro de eventos sospechosos, conexión cifrada y copias de seguridad diarias cifradas. API en Python (FastAPI) dentro de un contenedor Docker: registro e inicio de sesión con contraseñas cifradas, guardado de partida, ranking y conversación con Tosti.
- **Física trabajada:** **anti-trampa basado en la simulación**: el servidor calcula cuántas tostadas es físicamente posible ganar en el tiempo transcurrido (tiempo de recarga, tostadas por impacto, producción automática) y rechaza guardados imposibles. Prueba: un guardado de 1 000 000 de tostadas en 1 s fue rechazado (máximo posible: 22).
- **Problemas y solución:** las máquinas virtuales no obtenían IP → el archivo `hosts.ics` de Windows tenía registros viejos; se limpiaron. El servicio de Multipass se colgaba → reinicio forzado. Falta de memoria con tres máquinas en un PC de 8 GB → 1 GB por máquina y Unity cerrado mientras corren. Pegar bloques largos en la consola fallaba o cerraba la ventana → archivos de configuración copiados con `scp` y scripts de instalación. La configuración de MySQL no se aplicaba por el orden alfabético de los archivos → renombrada. El contenedor no arrancaba por permisos de archivos copiados desde Windows → permisos corregidos en el Dockerfile.
- **Próximo paso:** conectar Unity con la API.
- **Commits:** `48cd4df`

### 2026-11-19 — Sesión 7: juego conectado al servidor
- **Qué se hizo:** inicio de sesión real desde Unity, carga y guardado de la partida en la base de datos (guardado automático cada 30 s y "Guardar y salir"), ranking real, panel de Tosti con campo de texto y comentarios automáticos en momentos clave (inicio, primer impacto, varios lanzamientos fallidos, cuando alcanza para comprar, compras y meta). Meta del *vertical slice*: 300 tostadas, con pantalla "¡COCINA COMPLETADA!" y tiempo de partida.
- **Física trabajada:** los eventos de la simulación (impacto en pared, lanzamientos sin acierto) se traducen en frases que el modelo entiende, para que las pistas sean concretas.
- **Problemas y solución:** Unity 6 bloquea conexiones HTTP ("Insecure connection not allowed") → se permitió solo en el perfil de desarrollo (cada Build Profile tiene su propia copia de Player Settings); la versión publicada usa HTTPS. GitHub rechazó el push por un instalador de 183 MB agregado por error → se sacó del commit y se excluyó `*.exe`. El modelo solo escuchaba en `127.0.0.1` → se configuró para escuchar en la IP de la red privada.
- **Próximo paso:** arte final y una mecánica que premie la habilidad.
- **Commits:** `d39a9ac`, `68ceaa1`

### 2026-11-19 — Sesión 8: arte final, tarro de mermelada y balance
- **Qué se hizo:** sprites finales generados por script (`Herramientas/generar_sprites.py`): tostadora con cara (Tosti), tostada, tarro de mermelada, pared de azulejos, mesón y fondo de cocina. Nueva mecánica: **tarro de mermelada como *skill shot***. Recarga progresiva: al inicio hay 3 s entre lanzamientos y la mejora "Recarga rápida" la reduce un 20 % por nivel (hasta 0,5 s). Barra de carga con franjas que indican dónde soltar y estado gris "Recargando...". Aviso "¡MERMELADA!" al acertar.
- **Física trabajada:**
  - **Rebote potenciado:** si el primer contacto de la tostada es caer sobre el tarro, rebota con restitución e = 1,5 (> 1, el tarro "impulsa"): v_y' = −e·v_y, acotada entre 7 y 8 m/s. Solo funciona en el primer contacto.
  - **Tiro parabólico inverso:** para que el rebote lleve a la pared, se despeja el tiempo de vuelo hasta la altura del centro de la pared, t = (v_y + √(v_y² − 2·g·Δy)) / g, y de ahí la velocidad horizontal, v_x = (x_pared − x) / t.
  - **Predictor:** `PredictorTiro` re-simula el resorte y el vuelo con la misma integración para 150 cargas entre 0 y 100 % y marca en la barra la franja que cae en el tarro (roja) y la que llega directo a la pared (verde). Se recalcula cuando cambia k.
  - **Balance con simulación numérica (Python):** con k = 80·1,2ⁿ N/m, a nivel 0 hay que soltar entre 66 % y 91 % de carga para caer en el tarro (a fondo se pasa); la franja se estrecha con el resorte (nivel 8: 28–38 %). Desde el nivel 6 del resorte la tostada llega directo a la pared sin el tarro (93–100 % de carga; nivel 8: 77–100 %). El factor por nivel se bajó de 1,25 a 1,2 para que ese cambio ocurra en los niveles altos.
- **Problemas y solución:** la primera propuesta del tarro dependía del azar → se descartó y se diseñó una ventana de carga concreta para premiar la habilidad. Los sprites se veían deformados por la escala de los objetos → se generaron imágenes "pre-aplastadas" que la escala devuelve a su proporción. La pared se movió al extremo derecho y se recalcularon las franjas.
- **Próximo paso:** publicar la versión Web.
- **Commits:** `90f4178`

### 2026-11-20 — Sesión 9: publicación Web
- **Qué se hizo:** build WebGL de producción, servidor web Nginx con cabeceras de seguridad, límite de peticiones y política de contenido estricta, y un túnel de Cloudflare que publica el juego por HTTPS sin abrir puertos en la red de la casa. El juego quedó jugable desde internet con inicio de sesión, guardado y Tosti. Solo el servidor web es accesible; la API, la base de datos y el modelo quedaron aislados (comprobado: el acceso directo a la API desde el PC se bloquea).
- **Física trabajada:** ninguna nueva; se verificó que la simulación se comporta igual en el navegador que en el editor.
- **Problemas y solución:** el primer build de producción tardó casi 1 hora → *Code Optimization = Shorter Build Time* y máquinas virtuales apagadas durante el build (~35 min). El build traía una carpeta de depuración (`BurstDebugInformation_DoNotShip`) → se excluye al publicar. Nginx seguía mostrando la página por defecto porque `reload` fallaba sin aviso al cambiar las direcciones de escucha → `restart`.
- **Próximo paso:** pruebas finales y ejecutable para la entrega.
- **Commits:** `8e01e9d`

### 2026-11-20 — Sesión 10: pruebas finales y entregables
- **Qué se hizo:** prueba completa de la versión en línea (menú, inicio de sesión, progreso cargado desde la base de datos, tienda, Tosti respondiendo). Correcciones detectadas en esa prueba. Build de Windows en ventana (1280×720) que se juega en modo sin conexión, empaquetado con un `LEEME.txt` (cómo abrirlo, controles y físicas implementadas) y probado descomprimiéndolo en otra carpeta.
- **Física trabajada:** revisión del comportamiento en el ejecutable final (lanzamiento, rebote en el tarro y franjas de la barra).
- **Problemas y solución:** la flecha "→" de la tienda no se dibujaba con la fuente del juego → reemplazada por "->". El contador y el nombre del jugador casi no se leían sobre la pared crema → texto en café oscuro y la meta en naranja quemado. Tosti no respondía en la prueba → el Mac había perdido la conexión a la red privada y la configuración del modelo; se reconectó y quedó documentado el orden de arranque (red primero, modelo después).
- **Commits:** `48b5f97`

---

## Dificultades principales y cómo se resolvieron

| Dificultad | Solución |
|---|---|
| Integrar el resorte sin que oscile disparando tostadas solas | Bandera "armada": solo dispara si el jugador cargó y soltó |
| Que la tostada no atraviese la pared a alta velocidad | 4 sub-pasos de integración por paso físico |
| Detectar y resolver colisiones sin colliders de Unity | AABB propio con resolución por el eje de menor penetración |
| Un tarro de mermelada que premie la habilidad, no el azar | Restitución e > 1 + tiro parabólico inverso hacia la pared + ventana de carga exacta mostrada por un predictor que re-simula la física |
| Equilibrar el progreso | Simulación numérica del alcance para cada nivel del resorte; factor por nivel ajustado a 1,2 |
| Evitar trampas en el progreso guardado | El servidor limita las ganancias a lo que la simulación permite en el tiempo transcurrido |
| Recursos limitados (PC de 8 GB, Mac con poco disco) | 1 GB por máquina virtual, Unity cerrado mientras corren, limpieza de disco y build optimizado |
| Unity 6 bloqueando HTTP | Permitido solo en el perfil de desarrollo; producción por HTTPS |

## Cómo ejecutar
- **Ejecutable (Windows):** descomprimir el `.zip`, abrir `Toaster.exe` y elegir "Jugar sin conexión". Espacio carga y suelta el resorte; T abre la tienda.
- **Versión en línea:** enlace indicado en la entrega (requiere los servidores encendidos; incluye cuentas, guardado, ranking y Tosti).
