using System;
using UnityEngine;

/// <summary>
/// Tostada con física propia (sin Rigidbody ni Colliders). Tres estados:
///
/// 1. VOLANDO — movimiento parabólico:  a = (0, -g)
///    Integración Euler semi-implícita, con sub-pasos para que no atraviese la pared.
///
/// 2. DESLIZANDO EN LA PARED — al chocar de lado contra una Pared se pega
///    (choque perfectamente inelástico) y baja con fricción viscosa:
///       m·dv/dt = -m·g - b·v   →   velocidad terminal  v_t = m·g / b
///
/// TARRO DE MERMELADA — si el PRIMER contacto de la tostada es caer sobre el tarro, rebota
///    con restitución mayor que 1 (e > 1, aporta energía) y sale en un nuevo tiro parabólico
///    calculado para alcanzar la pared: problema inverso del movimiento parabólico.
///
/// 3. EN EL SUELO — al caer sobre una superficie rebota con coeficiente de
///    restitución e (v_y' = -e·v_y) y pierde velocidad horizontal por fricción.
///    Cuando el rebote es muy pequeño se detiene y desaparece después de un tiempo.
/// </summary>
public class Tostada : MonoBehaviour
{
    private enum Estado { Volando, DeslizandoPared, EnSuelo }

    [Header("Tamaño")]
    [Tooltip("Tamaño del sprite con escala 1. El Square de Unity mide 1x1 unidades.")]
    [SerializeField] private Vector2 tamanoBase = Vector2.one;

    [Header("Vuelo")]
    [SerializeField] private float gravedad = 9.81f;          // [m/s²]
    [Tooltip("Sub-pasos por FixedUpdate para evitar atravesar superficies a alta velocidad")]
    [SerializeField] private int subpasos = 4;

    [Header("Deslizamiento en la pared")]
    [SerializeField] private float masa = 0.05f;              // [kg]
    [Tooltip("Coeficiente de fricción viscosa b [N·s/m]. v_terminal = m·g/b")]
    [SerializeField] private float friccionViscosa = 2f;

    [Header("Suelo")]
    [Tooltip("Coeficiente de restitución e (0 = no rebota, 1 = rebote perfecto)")]
    [Range(0f, 1f)] [SerializeField] private float restitucion = 0.3f;
    [Tooltip("Fracción de la velocidad horizontal que se conserva en cada rebote")]
    [Range(0f, 1f)] [SerializeField] private float friccionSuelo = 0.5f;
    [Tooltip("Por debajo de esta velocidad vertical deja de rebotar [m/s]")]
    [SerializeField] private float umbralReposo = 0.3f;
    [SerializeField] private float tiempoVidaEnSuelo = 2f;

    [Header("Tarro de mermelada (potenciador)")]
    [Tooltip("Coeficiente de restitución del tarro (mayor que 1 = rebote potenciado)")]
    [SerializeField] private float restitucionMermelada = 1.5f;
    [SerializeField] private float vyMinMermelada = 7f;   // [m/s]
    [SerializeField] private float vyMaxMermelada = 8f;   // [m/s]

    private Estado estado = Estado.Volando;
    private Vector2 posicion;
    private Vector2 velocidad;
    private float temporizador;
    private Action<Tostada> alPegarseEnPared;
    private Action<Tostada> alRebotarEnTarro;
    private bool yaToco;   // el tarro solo funciona en la PRIMERA caída

    public float Gravedad => gravedad;
    public int Subpasos => subpasos;

    public Vector2 MitadTamano => Vector2.Scale(tamanoBase, (Vector2)transform.lossyScale) * 0.5f;

    /// <summary>Se llama justo después de crear la tostada.</summary>
    public void Iniciar(Vector2 posicionInicial, Vector2 velocidadInicial, Action<Tostada> callbackPared,
                        Action<Tostada> callbackTarro = null)
    {
        alRebotarEnTarro = callbackTarro;
        posicion = posicionInicial;
        velocidad = velocidadInicial;
        alPegarseEnPared = callbackPared;
        transform.position = posicion;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime / subpasos;
        for (int i = 0; i < subpasos; i++) Paso(dt);

        transform.position = posicion;

        if (estado == Estado.EnSuelo)
        {
            temporizador -= Time.fixedDeltaTime;
            if (temporizador <= 0f) Destroy(gameObject);
        }

        // Si se sale de la escena (falló todo), se elimina.
        if (posicion.y < -20f) Destroy(gameObject);
    }

    private void Paso(float dt)
    {
        switch (estado)
        {
            case Estado.Volando:
                // Movimiento parabólico: solo actúa la gravedad.
                velocidad.y -= gravedad * dt;
                posicion += velocidad * dt;
                ResolverColisiones();
                break;

            case Estado.DeslizandoPared:
                // Fricción viscosa contra la pared (la "mantequilla"): m·a = -m·g - b·v
                float a = -gravedad - (friccionViscosa / masa) * velocidad.y;
                velocidad.y += a * dt;
                posicion.y += velocidad.y * dt;
                ResolverColisiones();   // para detectar cuando llega al suelo
                break;

            case Estado.EnSuelo:
                break;
        }
    }

    private void ResolverColisiones()
    {
        foreach (ColisionAABB sup in ColisionAABB.Todas)
        {
            if (!ColisionAABB.Interseccion(posicion, MitadTamano, sup.Centro, sup.MitadTamano,
                                           out Vector2 normal, out float penetracion))
                continue;

            // 1) Separar: sacar la tostada de la superficie por el eje de menor penetración.
            posicion += normal * penetracion;

            // 2) Responder según el tipo de choque.
            bool choqueLateral = normal.x != 0f;
            bool cayendoEncima = normal.y > 0f;

            // Potenciador: solo si es la PRIMERA vez que la tostada toca algo y cae sobre el tarro.
            if (estado == Estado.Volando && sup.Tipo == TipoSuperficie.Mermelada && cayendoEncima
                && !yaToco && velocidad.y < 0f)
            {
                yaToco = true;
                ReboteMermelada();
                alRebotarEnTarro?.Invoke(this);
                continue;
            }
            yaToco = true;

            if (estado == Estado.Volando && choqueLateral && sup.Tipo == TipoSuperficie.Pared)
            {
                // Choque perfectamente inelástico: se pega y empieza a deslizar.
                velocidad = Vector2.zero;
                estado = Estado.DeslizandoPared;
                alPegarseEnPared?.Invoke(this);
            }
            else if (cayendoEncima)
            {
                if (estado == Estado.DeslizandoPared)
                {
                    Detener();   // terminó de deslizar y tocó el suelo
                }
                else if (velocidad.y < 0f)
                {
                    // Rebote con coeficiente de restitución.
                    velocidad.y = -restitucion * velocidad.y;
                    velocidad.x *= friccionSuelo;
                    if (velocidad.y < umbralReposo) Detener();
                }
            }
            else if (choqueLateral)
            {
                velocidad.x = 0f;   // golpe lateral contra algo que no es pared "pegajosa"
            }
            else if (normal.y < 0f && velocidad.y > 0f)
            {
                velocidad.y = 0f;   // golpe por debajo (techo)
            }
        }
    }

    /// <summary>
    /// Rebote potenciado del tarro. Velocidad vertical: v_y' = e·|v_y| con e > 1 (acotada).
    /// Velocidad horizontal: se despeja del tiro parabólico para que la tostada llegue a la
    /// pared justo a la altura de su centro:
    ///     y_obj - y0 = v_y·t - ½·g·t²   →   t = (v_y + √(v_y² - 2·g·Δy)) / g   →   v_x = Δx / t
    /// </summary>
    private void ReboteMermelada()
    {
        float vy = Mathf.Clamp(-velocidad.y * restitucionMermelada, vyMinMermelada, vyMaxMermelada);
        float vx = velocidad.x;
        ColisionAABB pared = BuscarPared();
        if (pared != null)
        {
            float dy = pared.Centro.y - posicion.y;
            float discriminante = vy * vy - 2f * gravedad * dy;
            if (discriminante > 0f)
            {
                float t = (vy + Mathf.Sqrt(discriminante)) / gravedad;
                float frenteDeLaPared = pared.Centro.x - pared.MitadTamano.x - MitadTamano.x;
                vx = (frenteDeLaPared - posicion.x) / t;
            }
        }
        velocidad = new Vector2(vx, vy);
    }

    private static ColisionAABB BuscarPared()
    {
        foreach (ColisionAABB sup in ColisionAABB.Todas)
            if (sup.Tipo == TipoSuperficie.Pared) return sup;
        return null;
    }

    public enum ResultadoTiro { Nada, Tarro, Pared }

    /// <summary>
    /// Simula un tiro con la MISMA integración que el juego (Euler semi-implícito con sub-pasos)
    /// y dice qué toca primero. Lo usa PredictorTiro para marcar en la barra la carga correcta.
    /// </summary>
    public static ResultadoTiro Simular(Vector2 pos, Vector2 vel, Vector2 mitad, float g, int subpasos, float dtFijo)
    {
        float dt = dtFijo / subpasos;
        for (int i = 0; i < 8000; i++)
        {
            vel.y -= g * dt;
            pos += vel * dt;
            foreach (ColisionAABB sup in ColisionAABB.Todas)
            {
                if (!ColisionAABB.Interseccion(pos, mitad, sup.Centro, sup.MitadTamano, out Vector2 n, out _))
                    continue;
                if (sup.Tipo == TipoSuperficie.Pared && n.x != 0f) return ResultadoTiro.Pared;
                if (sup.Tipo == TipoSuperficie.Mermelada && n.y > 0f && vel.y < 0f) return ResultadoTiro.Tarro;
                return ResultadoTiro.Nada;
            }
            if (pos.y < -20f) return ResultadoTiro.Nada;
        }
        return ResultadoTiro.Nada;
    }

    private void Detener()
    {
        velocidad = Vector2.zero;
        estado = Estado.EnSuelo;
        temporizador = tiempoVidaEnSuelo;
    }
}
