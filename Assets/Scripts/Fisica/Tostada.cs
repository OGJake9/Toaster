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

    private Estado estado = Estado.Volando;
    private Vector2 posicion;
    private Vector2 velocidad;
    private float temporizador;
    private Action<Tostada> alPegarseEnPared;

    private Vector2 MitadTamano => Vector2.Scale(tamanoBase, (Vector2)transform.lossyScale) * 0.5f;

    /// <summary>Se llama justo después de crear la tostada.</summary>
    public void Iniciar(Vector2 posicionInicial, Vector2 velocidadInicial, Action<Tostada> callbackPared)
    {
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

    private void Detener()
    {
        velocidad = Vector2.zero;
        estado = Estado.EnSuelo;
        temporizador = tiempoVidaEnSuelo;
    }
}
