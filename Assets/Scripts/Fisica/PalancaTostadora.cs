using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Palanca de la tostadora modelada como sistema MASA-RESORTE-AMORTIGUADOR,
/// integrado a mano (sin Rigidbody ni Colliders).
///
///   m·x'' + c·x' + k·x = 0      →      a = (-k·x - c·v) / m
///
/// x = desplazamiento de la plataforma respecto al equilibrio (negativo = comprimido).
/// Mientras el jugador mantiene presionado Espacio, la plataforma baja y
/// comprime el resorte. Al soltar, el resorte la empuja hacia arriba; cuando la
/// plataforma cruza el punto de equilibrio subiendo, la tostada sale disparada con
/// la velocidad que tenga la plataforma en ese instante.
///
/// La dirección de lanzamiento es el eje "arriba" local de la tostadora:
/// rotando el objeto Tostadora en Z se apunta el disparo hacia la pared.
/// </summary>
public class PalancaTostadora : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto hijo que representa la plataforma donde va la tostada.")]
    [SerializeField] private Transform plataforma;
    [Tooltip("Controlador del juego, de donde se lee la constante k según las mejoras.")]
    [SerializeField] private ControladorJuego controlador;

    [Header("Parámetros físicos")]
    [Tooltip("Masa de la plataforma + tostada [kg]")]
    [SerializeField] private float masa = 0.2f;
    [Tooltip("Coeficiente de amortiguamiento c [N·s/m]")]
    [SerializeField] private float amortiguamiento = 0.8f;
    [Tooltip("Compresión máxima del resorte [m]")]
    [SerializeField] private float compresionMaxima = 0.3f;
    [Tooltip("Velocidad a la que baja la plataforma mientras se presiona [m/s]")]
    [SerializeField] private float velocidadCarga = 0.6f;
    [Tooltip("Recarga [s] solo si no hay controlador; con controlador la define la mejora Recarga rápida")]
    [SerializeField] private float tiempoRecarga = 0.8f;

    /// <summary>Se dispara al lanzar: (posición inicial, velocidad inicial).</summary>
    public event Action<Vector2, Vector2> OnLanzamiento;

    // Estado del oscilador
    private float x;                 // desplazamiento [m]
    private float v;                 // velocidad [m/s]
    private bool presionando;
    private bool presionandoAnterior;
    private bool tostadaCargada = true;
    private bool armada;             // true solo si el jugador comprimió el resorte
    private float temporizadorRecarga;
    private Vector3 posicionReposo;

    // Datos expuestos para depuración / informe
    public float Desplazamiento => x;
    public float Velocidad => v;
    public float Masa => masa;
    public float CompresionAlSoltar { get; private set; }
    public bool TostadaCargada => tostadaCargada;

    /// <summary>La tienda la desactiva para que el teclado no cargue el resorte.</summary>
    public bool EntradaHabilitada { get; set; } = true;

    /// <summary>0 = sin cargar, 1 = compresión máxima (para la barra de carga).</summary>
    public float CargaNormalizada => armada ? Mathf.Clamp01(-x / compresionMaxima) : 0f;

    private void Start()
    {
        posicionReposo = plataforma.localPosition;
    }

    private void Update()
    {
        // La entrada se lee en Update; la física se integra en FixedUpdate.
        // Solo Espacio: el clic del ratón queda libre para los botones de la interfaz.
        presionando = EntradaHabilitada
                      && Keyboard.current != null
                      && Keyboard.current.spaceKey.isPressed;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float k = controlador != null ? controlador.Estado.ConstanteResorte() : EstadoJuego.K_BASE;

        // Recarga de tostada
        if (!tostadaCargada)
        {
            temporizadorRecarga -= dt;
            if (temporizadorRecarga <= 0f) tostadaCargada = true;
        }

        // Detectar el instante en que se suelta (para comparar con la teoría)
        if (presionandoAnterior && !presionando)
            CompresionAlSoltar = -x;
        presionandoAnterior = presionando;

        if (presionando && tostadaCargada)
        {
            // El jugador empuja la palanca: desplazamiento controlado, sin dinámica.
            x = Mathf.MoveTowards(x, -compresionMaxima, velocidadCarga * dt);
            v = 0f;
            armada = true;
        }
        else
        {
            // Dinámica libre: masa-resorte-amortiguador, Euler semi-implícito.
            float xAnterior = x;
            float a = (-k * x - amortiguamiento * v) / masa;
            v += a * dt;   // primero la velocidad...
            x += v * dt;   // ...luego la posición con la velocidad nueva (más estable)

            // Lanzamiento: la plataforma cruza el equilibrio subiendo, y SOLO si el
            // jugador la armó. Sin esta condición, cada oscilación libre del resorte
            // (después del disparo) lanzaría tostadas débiles solas.
            if (armada && tostadaCargada && xAnterior < 0f && x >= 0f && v > 0f)
            {
                armada = false;
                tostadaCargada = false;
                temporizadorRecarga = controlador != null ? controlador.Estado.TiempoRecarga() : tiempoRecarga;

                Vector2 direccion = transform.up;   // eje de la tostadora (incluye su rotación)
                OnLanzamiento?.Invoke(plataforma.position, direccion * v);
            }
        }

        // La plataforma se mueve sobre el eje local Y de la tostadora.
        plataforma.localPosition = posicionReposo + Vector3.up * x;
    }
}
