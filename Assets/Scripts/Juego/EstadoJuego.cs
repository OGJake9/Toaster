using System;
using UnityEngine;

/// <summary>
/// Estado puro del juego. NO es MonoBehaviour: solo datos y reglas de la economía.
/// Es serializable a JSON para guardar la partida y, más adelante, para que el
/// backend pueda recalcular y validar el progreso (anti-trampa).
/// </summary>
[Serializable]
public class EstadoJuego
{
    // ---------- Datos guardables ----------
    public double tostadas;          // moneda actual
    public long tostadasTotales;     // histórico acumulado (para ranking)
    public long lanzamientos;        // número de lanzamientos realizados
    public int nivelResorte;         // nivel de mejora del resorte de la palanca

    // ---------- Parámetros de balance ----------
    // Constantes compartidas: el servidor usará exactamente los mismos valores.
    public const float K_BASE = 80f;                 // constante del resorte base [N/m]
    public const float K_FACTOR_NIVEL = 1.25f;       // cada nivel multiplica k por este factor
    public const double COSTO_BASE_RESORTE = 10;     // costo del nivel 1 [tostadas]
    public const double COSTO_FACTOR_RESORTE = 1.6;  // crecimiento exponencial del costo

    // ---------- Reglas ----------

    /// <summary>Constante elástica actual del resorte según su nivel de mejora.</summary>
    public float ConstanteResorte()
    {
        return K_BASE * Mathf.Pow(K_FACTOR_NIVEL, nivelResorte);
    }

    /// <summary>Costo de comprar el siguiente nivel del resorte.</summary>
    public double CostoMejoraResorte()
    {
        return Math.Floor(COSTO_BASE_RESORTE * Math.Pow(COSTO_FACTOR_RESORTE, nivelResorte));
    }

    /// <summary>Se llama cada vez que la palanca lanza una tostada.</summary>
    public void RegistrarLanzamiento()
    {
        lanzamientos++;
    }

    /// <summary>Se llama cuando una tostada se pega en la pared: solo eso da puntos.</summary>
    public void RegistrarImpactoPared(int tostadasGanadas = 1)
    {
        tostadas += tostadasGanadas;
        tostadasTotales += tostadasGanadas;
    }

    /// <summary>Intenta comprar una mejora del resorte. Devuelve true si se pudo.</summary>
    public bool ComprarMejoraResorte()
    {
        double costo = CostoMejoraResorte();
        if (tostadas < costo) return false;

        tostadas -= costo;
        nivelResorte++;
        return true;
    }

    // ---------- Serialización ----------
    public string AJson() => JsonUtility.ToJson(this);
    public static EstadoJuego DesdeJson(string json) => JsonUtility.FromJson<EstadoJuego>(json);
}
