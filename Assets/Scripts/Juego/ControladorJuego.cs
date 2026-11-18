using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta la física con el estado del juego. Es dueño del EstadoJuego.
/// - Arranca en el menú (juego pausado) hasta que se inicia una partida.
/// - Crea una tostada en cada lanzamiento y suma puntos cuando se pega en la pared.
/// - Aplica la producción automática y abre/cierra la tienda.
/// - Si hay sesión en línea, guarda la partida en el servidor cada cierto tiempo y al salir.
/// </summary>
public class ControladorJuego : MonoBehaviour
{
    [SerializeField] private PalancaTostadora palanca;
    [SerializeField] private Tostada prefabTostada;
    [SerializeField] private ServicioAPI api;
    [SerializeField] private MenuInicio menu;
    [Tooltip("Cada cuántos segundos de juego se guarda la partida en el servidor")]
    [SerializeField] private float intervaloAutoguardado = 30f;

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();
    public bool TiendaAbierta { get; private set; }
    public bool EnMenu { get; private set; } = true;

    private float tiempoDesdeGuardado;
    private bool saliendo;

    private void OnEnable()
    {
        if (palanca != null) palanca.OnLanzamiento += AlLanzar;
    }

    private void OnDisable()
    {
        if (palanca != null) palanca.OnLanzamiento -= AlLanzar;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        ActualizarPausa();
    }

    private void Update()
    {
        if (EnMenu) return;

        // Producción automática (pausada si la tienda está abierta, porque Time.deltaTime = 0).
        Estado.Producir(Time.deltaTime);

        // Autoguardado en el servidor.
        if (Sesion.EnLinea && api != null)
        {
            tiempoDesdeGuardado += Time.deltaTime;
            if (tiempoDesdeGuardado >= intervaloAutoguardado)
            {
                tiempoDesdeGuardado = 0f;
                api.GuardarPartida(Estado, r =>
                {
                    if (!r.exito) Debug.LogWarning($"Autoguardado falló: {r.mensaje}");
                });
            }
        }

        if (Keyboard.current == null) return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
            AbrirTienda(!TiendaAbierta);
        else if (TiendaAbierta && Keyboard.current.escapeKey.wasPressedThisFrame)
            AbrirTienda(false);
    }

    // ===================== Partida =====================

    /// <summary>Lo llama el menú al entrar: con la partida cargada del servidor o una nueva.</summary>
    public void IniciarPartida(EstadoJuego estado)
    {
        // Limpiar tostadas que hubieran quedado de una partida anterior.
        foreach (Tostada t in FindObjectsByType<Tostada>(FindObjectsSortMode.None))
            Destroy(t.gameObject);

        Estado = estado ?? new EstadoJuego();
        tiempoDesdeGuardado = 0f;
        saliendo = false;
        TiendaAbierta = false;
        EnMenu = false;
        ActualizarPausa();
    }

    /// <summary>Botón "Guardar y salir" del HUD: guarda, cierra sesión y vuelve al menú.</summary>
    public void GuardarYSalir()
    {
        if (saliendo) return;
        saliendo = true;

        if (!Sesion.EnLinea || api == null)
        {
            Sesion.Cerrar();
            VolverAlMenu("");
            return;
        }

        TiendaAbierta = true;   // congela el juego mientras se guarda
        ActualizarPausa();

        api.GuardarPartida(Estado, r =>
        {
            string mensaje = r.exito ? "Partida guardada. Sesión cerrada."
                                     : $"No se pudo guardar: {r.mensaje}";
            api.CerrarSesion(_ =>
            {
                Sesion.Cerrar();
                VolverAlMenu(mensaje);
            });
        });
    }

    private void VolverAlMenu(string mensaje)
    {
        EnMenu = true;
        TiendaAbierta = false;
        ActualizarPausa();
        if (menu != null) menu.AvisarSalida(mensaje);
    }

    // ===================== Tienda =====================

    public void AbrirTienda(bool abrir)
    {
        if (EnMenu) return;
        TiendaAbierta = abrir;
        ActualizarPausa();
    }

    public void ComprarMejora(TipoMejora tipo)
    {
        if (Estado.Comprar(tipo))
            Debug.Log($"Comprado: {EstadoJuego.Mejoras[(int)tipo].nombre} → nivel {Estado.Nivel(tipo)}");
    }

    private void ActualizarPausa()
    {
        bool pausado = EnMenu || TiendaAbierta;
        Time.timeScale = pausado ? 0f : 1f;   // congela toda la física
        if (palanca != null) palanca.EntradaHabilitada = !pausado;
    }

    // ===================== Física → economía =====================

    private void AlLanzar(Vector2 posicion, Vector2 velocidad)
    {
        Estado.RegistrarLanzamiento();

        // Comparación teoría vs. simulación del resorte.
        // Sin amortiguamiento:  ½·k·x0² = ½·m·v²   →   v = x0·√(k/m)
        float k = Estado.ConstanteResorte();
        float x0 = palanca.CompresionAlSoltar;
        float vTeorica = x0 * Mathf.Sqrt(k / palanca.Masa);

        Debug.Log($"Lanzamiento #{Estado.lanzamientos} | k = {k:F1} N/m | x0 = {x0:F3} m | " +
                  $"v simulada = {velocidad.magnitude:F2} m/s | v teórica (sin c) = {vTeorica:F2} m/s");

        if (prefabTostada != null)
        {
            Tostada t = Instantiate(prefabTostada);
            t.Iniciar(posicion, velocidad, AlPegarseEnPared);
        }
    }

    private void AlPegarseEnPared(Tostada tostada)
    {
        int ganadas = Estado.RegistrarImpactoPared();
        Debug.Log($"¡Impacto! +{ganadas} | Tostadas: {Estado.tostadas:0}");
    }
}
