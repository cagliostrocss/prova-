using UnityEngine;
using BNG;

/// <summary>
/// Imposta i caricatori dell'AmmoDispenser a un valore molto alto all'avvio
/// e li ricarica automaticamente quando stanno per finire.
/// Aggiungilo sullo stesso GameObject che ha AmmoDispenser.
/// </summary>
[RequireComponent(typeof(AmmoDispenser))]
public class InfiniteAmmoHelper : MonoBehaviour
{
    [Tooltip("Quanti caricatori mantenere disponibili. 999 = praticamente infiniti.")]
    public int ReserveCount = 999;

    private AmmoDispenser _dispenser;

    void Awake()
    {
        _dispenser = GetComponent<AmmoDispenser>();
        Refill();
    }

    void Update()
    {
        // Ricarica quando scende sotto 10 (per sicurezza)
        if (_dispenser.CurrentPistolClips < 10)   _dispenser.CurrentPistolClips   = ReserveCount;
        if (_dispenser.CurrentRifleClips < 10)    _dispenser.CurrentRifleClips    = ReserveCount;
        if (_dispenser.CurrentShotgunShells < 10) _dispenser.CurrentShotgunShells = ReserveCount;
    }

    void Refill()
    {
        _dispenser.CurrentPistolClips   = ReserveCount;
        _dispenser.CurrentRifleClips    = ReserveCount;
        _dispenser.CurrentShotgunShells = ReserveCount;
    }
}
