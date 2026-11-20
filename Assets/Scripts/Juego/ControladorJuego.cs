using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta la física con el estado del juego. Es dueño del EstadoJuego.
/// - Arranca en el menú (juego pausado) hasta que se inicia una partida.
/// - Crea una tostada en cada lanzamiento y suma puntos cuando se pega en la pared.
/// - Aplica la producción automática, abre/cierra la tienda y detecta la meta.
/// - Avisa a Tosti de los momentos importantes de la partida.
/// - Si hay sesión en línea, guarda la partida en el servidor cada cierto tiempo y al salir.
/// </summary>
public class ControladorJuego : MonoBehaviour
{
    [SerializeField] private PalancaTostadora palanca;
    [SerializeField] private Tostada prefabTostada;
    [SerializeField] private ServicioAPI api;
    [SerializeField] private MenuInicio menu;
    [SerializeField] private PanelTosti panelTosti;
    [Tooltip("Cada cuántos segundos de juego se guarda la partida en el servidor")]
    [SerializeField] private float intervaloAutoguardado = 30f;
    [Tooltip("Lanzamientos seguidos sin impacto antes de que Tosti dé una pista")]
    [SerializeField] private int lanzamientosParaAyuda = 5;

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();
    public bool TiendaAbierta { get; private set; }
    public bool EnMenu { get; private set; } = true;
    public bool MostrandoMeta { get; private set; }
    /// <summary>Segundos de juego activo (sin contar menú, tienda ni pantallas de pausa).</summary>
    public float TiempoPartida { get; private set; }
    /// <summary>Momento (tiempo real) del último rebote en el tarro, para el aviso "¡Mermelada!".</summary>
    public float UltimoReboteTarro { get; private set; } = -99f;

    private bool Pausado => EnMenu || TiendaAbierta || MostrandoMeta;
    private bool Escribiendo => panelTosti != null && panelTosti.Escribiendo;

    private float tiempoDesdeGuardado;
    private bool saliendo;
    private bool metaAnunciada;
    private bool avisoCompraDado;
    private int lanzamientosSinImpacto;
    private int impactosSesion;

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

        // Mientras se escribe a Tosti, Espacio no carga el resorte.
        if (palanca != null) palanca.EntradaHabilitada = !Pausado && !Escribiendo;

        if (!Pausado) TiempoPartida += Time.deltaTime;

        // Producción automática (pausada si hay pausa, porque Time.deltaTime = 0).
        Estado.Producir(Time.deltaTime);

        // Autoguardado en el servidor.
        if (Sesion.EnLinea && api != null)
        {
            tiempoDesdeGuardado += Time.deltaTime;
            if (tiempoDesdeGuardado >= intervaloAutoguardado) GuardarAhora(null);
        }

        RevisarHitos();

        if (Keyboard.current == null || Escribiendo || MostrandoMeta) return;

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
        TiempoPartida = 0f;
        saliendo = false;
        TiendaAbierta = false;
        MostrandoMeta = false;
        metaAnunciada = Estado.MetaAlcanzada();   // si ya la tenía, no se vuelve a anunciar
        avisoCompraDado = false;
        lanzamientosSinImpacto = 0;
        impactosSesion = 0;
        EnMenu = false;
        ActualizarPausa();

        if (panelTosti != null)
        {
            panelTosti.Reiniciar();
            panelTosti.Evento("inicio", true);
        }
    }

    /// <summary>Guarda ya en el servidor (si hay sesión). Siempre llama a alTerminar.</summary>
    public void GuardarAhora(Action<Resultado> alTerminar)
    {
        if (!Sesion.EnLinea || api == null)
        {
            alTerminar?.Invoke(new Resultado { exito = true });
            return;
        }

        tiempoDesdeGuardado = 0f;
        api.GuardarPartida(Estado, r =>
        {
            if (!r.exito)
            {
                Debug.LogWarning($"Guardado rechazado o fallido: {r.mensaje}");
                if (panelTosti != null) panelTosti.Mostrar($"Ups, el servidor no aceptó el último guardado. {r.mensaje}");
            }
            alTerminar?.Invoke(r);
        });
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
        MostrandoMeta = false;
        ActualizarPausa();
        if (menu != null) menu.AvisarSalida(mensaje);
    }

    /// <summary>Botón "¡Seguir tostando!" de la pantalla de meta.</summary>
    public void CerrarMeta()
    {
        MostrandoMeta = false;
        ActualizarPausa();
    }

    // ===================== Tienda =====================

    public void AbrirTienda(bool abrir)
    {
        if (EnMenu || MostrandoMeta) return;
        TiendaAbierta = abrir;
        ActualizarPausa();
    }

    public void ComprarMejora(TipoMejora tipo)
    {
        if (!Estado.Comprar(tipo)) return;

        Debug.Log($"Comprado: {EstadoJuego.Mejoras[(int)tipo].nombre} → nivel {Estado.Nivel(tipo)}");
        avisoCompraDado = false;
        if (panelTosti != null) panelTosti.Evento("compra");
    }

    private void ActualizarPausa()
    {
        Time.timeScale = Pausado ? 0f : 1f;   // congela toda la física
        if (palanca != null) palanca.EntradaHabilitada = !Pausado && !Escribiendo;
    }

    // ===================== Hitos para Tosti =====================

    private void RevisarHitos()
    {
        if (!metaAnunciada && Estado.MetaAlcanzada())
        {
            metaAnunciada = true;
            MostrandoMeta = true;
            ActualizarPausa();
            if (panelTosti != null) panelTosti.Evento("meta", true);
            return;
        }

        if (!avisoCompraDado && AlgunaMejoraComprable())
        {
            avisoCompraDado = true;
            if (panelTosti != null) panelTosti.Evento("puede_comprar");
        }
    }

    private bool AlgunaMejoraComprable()
    {
        for (int i = 0; i < EstadoJuego.Mejoras.Length; i++)
            if (Estado.PuedeComprar((TipoMejora)i)) return true;
        return false;
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
            t.Iniciar(posicion, velocidad, AlPegarseEnPared, AlRebotarEnTarro);
        }

        lanzamientosSinImpacto++;
        if (lanzamientosSinImpacto == lanzamientosParaAyuda && panelTosti != null)
            panelTosti.Evento("sin_impactos");
    }

    private void AlRebotarEnTarro(Tostada tostada)
    {
        UltimoReboteTarro = Time.unscaledTime;
        Debug.Log("¡Mermelada! Rebote potenciado hacia la pared.");
    }

    private void AlPegarseEnPared(Tostada tostada)
    {
        int ganadas = Estado.RegistrarImpactoPared();
        Debug.Log($"¡Impacto! +{ganadas} | Tostadas: {Estado.tostadas:0}");

        lanzamientosSinImpacto = 0;
        impactosSesion++;
        if (impactosSesion == 1 && panelTosti != null) panelTosti.Evento("primer_impacto");
    }
}
