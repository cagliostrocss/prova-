using UnityEngine;
using BNG;

/// <summary>
/// Simula il peso fisico di un'arma in VR.
///
/// COME FUNZIONA
///   BNG usa un ConfigurableJoint creato al momento del grab. ATTENZIONE: il joint
///   è sul Grabber (la mano), non sull'arma — per questo GetComponent sull'arma
///   restituisce sempre null e ogni tentativo precedente non aveva effetto.
///
///   Questo script gira in FixedUpdate con ExecutionOrder 100 (dopo Grabbable = 0)
///   e abbassa la rigidità del joint in proporzione al peso:
///     - slerpDrive (inerzia rotazionale): scala con w³ → lag visibile già a 2 kg,
///       molto pronunciato a 5 kg (risposta ~4 frame a 90 fps invece di < 1).
///     - xDrive/yDrive/zDrive (resistenza traslazionale): abbassati meno per non
///       far "galleggiare" la pistola distante dalla mano.
///   In aggiunta:
///     - Massa impostata a WeightKg → reazione fisica corretta sul rilascio.
///     - Centro di massa spostato in avanti → la canna tende ad abbassarsi (droop).
///
/// UTILIZZO
///   Aggiungi sull'oggetto arma (stesso GameObject di Grabbable).
///   Regola WeightKg: pistola ≈ 1.0, shotgun ≈ 2.5, fucile ≈ 3.5.
///   Se il lag non si sente ancora, aumenta LagMultiplier (default 1.0).
/// </summary>
[DefaultExecutionOrder(100)]   // dopo Grabbable (order 0) nello stesso FixedUpdate
[RequireComponent(typeof(Grabbable))]
[RequireComponent(typeof(Rigidbody))]
public class WeaponWeight : MonoBehaviour
{
    [Header("Peso")]
    [Tooltip("Peso percepito in kg. Pistola ≈ 1.0  Shotgun ≈ 2.5  Fucile ≈ 3.5")]
    public float WeightKg = 1.5f;

    [Header("Calibrazione lag")]
    [Tooltip("Moltiplicatore per il ritardo. 1.0 = default. Aumenta se non si sente nulla.")]
    [Range(0.1f, 5f)]
    public float LagMultiplier = 1.0f;

    [Header("Droop (abbassamento canna per gravità)")]
    [Tooltip("Offset del centro di massa verso la canna (asse Z locale). " +
             "Valori tipici: 0.05 – 0.20. Metti 0 per disabilitare.")]
    public float BarrelCenterOfMassOffset = 0.10f;

    // ── cache ────────────────────────────────────────────────────────────────
    private Grabbable _grabbable;
    private Rigidbody _rb;

    // Il ConfigurableJoint è sul Grabber (mano), NON sull'arma!
    private ConfigurableJoint _joint;
    private Grabber            _currentGrabber;

    // ── costanti BNG ─────────────────────────────────────────────────────────
    private const float BNG_MAX_SLERP = 9999f;
    private const float BNG_MAX_POS   = 99999f;

    // ── valori calcolati ──────────────────────────────────────────────────────
    private float _slerpSpring;
    private float _slerpDamper;
    private float _posSpring;
    private float _posDamper;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _rb        = GetComponent<Rigidbody>();
        RecalculateDriveValues();
    }

    void OnValidate()
    {
        WeightKg = Mathf.Max(0.1f, WeightKg);
        RecalculateDriveValues();
    }

    /// <summary>
    /// Calcola spring/damper in base al peso e al moltiplicatore.
    ///
    /// Formula slerpSpring: 2000 / (w³ × LagMultiplier)
    ///   A 5 kg (LagMul=1): spring ≈ 16  → risposta ~50 ms (~4 frame a 90 fps)
    ///   A 3 kg (LagMul=1): spring ≈ 74  → risposta ~25 ms (~2 frame)
    ///   A 1 kg (LagMul=1): spring ≈ 2000 → risposta ~4 ms (quasi istantaneo)
    ///
    /// Damper = 1.2 × critico per eliminare oscillazioni.
    /// </summary>
    void RecalculateDriveValues()
    {
        float w = Mathf.Max(0.1f, WeightKg);
        float m = Mathf.Max(0.01f, LagMultiplier);

        // Slerp drive (inerzia rotazionale) — scala con w³ per impatto percettibile
        float wCubed   = w * w * w;
        _slerpSpring   = Mathf.Max(10f, 2000f / (wCubed * m));
        // Damping ≈ 1.2x critico (leggermente sovrasmorzato → no oscillazioni)
        _slerpDamper   = 1.2f * Mathf.Sqrt(_slerpSpring * 0.04f);

        // Position drive (resistenza traslazionale)
        _posSpring     = Mathf.Max(500f, BNG_MAX_POS / (1f + w * 0.8f * m));
        _posDamper     = w * 1.5f;
    }

    void FixedUpdate()
    {
        if (!_grabbable.BeingHeld)
        {
            _joint          = null;
            _currentGrabber = null;
            return;
        }

        // ── Ottieni Grabber e Joint (sono sul GameObject della mano) ──────────
        // BNG crea il ConfigurableJoint sul Grabber al momento del grab,
        // collegandolo al Rigidbody dell'arma via connectedBody.
        if (_joint == null || _currentGrabber == null)
        {
            _currentGrabber = _grabbable.GetPrimaryGrabber();
            if (_currentGrabber == null) return;

            _joint = _currentGrabber.GetComponent<ConfigurableJoint>();
            if (_joint == null) return;     // GrabPhysics non è PhysicsJoint → esci
        }

        // ── 1. Slerp drive (inerzia rotazionale) ─────────────────────────────
        JointDrive slerp = _joint.slerpDrive;
        slerp.positionSpring = _slerpSpring;
        slerp.positionDamper = _slerpDamper;
        slerp.maximumForce   = float.MaxValue;
        _joint.slerpDrive    = slerp;

        // ── 2. Position drive (resistenza traslazionale) ─────────────────────
        JointDrive pos = new JointDrive
        {
            positionSpring = _posSpring,
            positionDamper = _posDamper,
            maximumForce   = float.MaxValue
        };
        _joint.xDrive = pos;
        _joint.yDrive = pos;
        _joint.zDrive = pos;

        // ── 3. Massa e centro di massa ────────────────────────────────────────
        if (!Mathf.Approximately(_rb.mass, WeightKg))
            _rb.mass = WeightKg;

        // Centro di massa spostato verso la canna → droop naturale per gravità
        if (BarrelCenterOfMassOffset != 0f)
        {
            Vector3 targetCoM = new Vector3(0f, 0f, BarrelCenterOfMassOffset);
            if (_rb.centerOfMass != targetCoM)
                _rb.centerOfMass = targetCoM;
        }
    }
}
