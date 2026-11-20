using UnityEngine;

/// <summary>
/// Calcula, simulando la misma física del juego, qué porcentaje de carga del resorte hace que:
///   - la tostada caiga en el TARRO de mermelada (franja roja en la barra), y
///   - la tostada llegue DIRECTO a la pared (franja verde; solo con el resorte mejorado).
///
/// Para cada muestra de carga simula el resorte (PalancaTostadora.SimularSalida) y el vuelo
/// (Tostada.Simular). Se recalcula cuando cambia la constante k o cada pocos segundos.
/// Como cada mejora del resorte cambia la trayectoria, las franjas se mueven: el jugador tiene
/// que volver a afinar su tiro (skill shot).
/// </summary>
public class PredictorTiro : MonoBehaviour
{
    [SerializeField] private PalancaTostadora palanca;
    [SerializeField] private ControladorJuego controlador;
    [SerializeField] private Tostada prefabTostada;
    [Tooltip("Cuántas cargas distintas se simulan (más = franjas más precisas)")]
    [SerializeField] private int muestras = 150;

    /// <summary>Franja (mínimo, máximo) de carga 0..1 que cae en el tarro. (-1, -1) si no hay.</summary>
    public Vector2 ZonaTarro { get; private set; } = new Vector2(-1f, -1f);
    /// <summary>Franja de carga que llega directo a la pared. (-1, -1) si no hay.</summary>
    public Vector2 ZonaPared { get; private set; } = new Vector2(-1f, -1f);

    private float kCalculado = -1f;
    private float relojRecalculo;

    private void Update()
    {
        if (controlador.EnMenu || !palanca.Listo) return;

        float k = controlador.Estado.ConstanteResorte();
        relojRecalculo -= Time.unscaledDeltaTime;
        if (Mathf.Approximately(k, kCalculado) && relojRecalculo > 0f) return;

        kCalculado = k;
        relojRecalculo = 3f;
        Calcular(k);
    }

    private void Calcular(float k)
    {
        Vector2 mitad = prefabTostada.MitadTamano;
        float g = prefabTostada.Gravedad;
        int subpasos = prefabTostada.Subpasos;
        float cmax = palanca.CompresionMaxima;

        Vector2 tarro = new Vector2(-1f, -1f);
        Vector2 pared = new Vector2(-1f, -1f);

        for (int i = 1; i <= muestras; i++)
        {
            float carga = (float)i / muestras;
            if (!palanca.SimularSalida(carga * cmax, k, out float v, out Vector2 pos)) continue;

            Tostada.ResultadoTiro r = Tostada.Simular(pos, palanca.Direccion * v, mitad, g, subpasos, Time.fixedDeltaTime);
            if (r == Tostada.ResultadoTiro.Tarro) tarro = Extender(tarro, carga);
            else if (r == Tostada.ResultadoTiro.Pared) pared = Extender(pared, carga);
        }

        ZonaTarro = tarro;
        ZonaPared = pared;
    }

    private static Vector2 Extender(Vector2 zona, float carga)
    {
        return zona.x < 0f ? new Vector2(carga, carga) : new Vector2(zona.x, carga);
    }
}
