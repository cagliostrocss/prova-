using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuoyancyObject : MonoBehaviour
{
    [Header("Buoyancy")]
    [Range(-2f, 2f)] public float floatHeight       = 0f;
    [Range(0f, 20f)] public float buoyancyForce     = 10f;

    [Header("Water Resistance")]
    [Range(0f, 10f)] public float waterDrag         = 3f;
    [Range(0f, 10f)] public float waterAngularDrag  = 1f;

    // Assigned automatically when entering a WaterVolume trigger
    [HideInInspector] public WaterVolume waterVolume;

    private Rigidbody _rb;
    private float     _defaultDrag;
    private float     _defaultAngularDrag;
    private bool      _inWater;

    void Awake()
    {
        _rb                 = GetComponent<Rigidbody>();
        _defaultDrag        = _rb.drag;
        _defaultAngularDrag = _rb.angularDrag;
    }

    void OnTriggerEnter(Collider other)
    {
        var vol = other.GetComponent<WaterVolume>();
        if (vol == null) return;
        waterVolume          = vol;
        _inWater             = true;
        _rb.drag             = waterDrag;
        _rb.angularDrag      = waterAngularDrag;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<WaterVolume>() == null) return;
        waterVolume          = null;
        _inWater             = false;
        _rb.drag             = _defaultDrag;
        _rb.angularDrag      = _defaultAngularDrag;
    }

    void FixedUpdate()
    {
        if (!_inWater || waterVolume == null) return;

        float surfaceY    = waterVolume.GetSurfaceY() + floatHeight;
        float submergedBy = surfaceY - transform.position.y;

        if (submergedBy > 0f)
        {
            // Force proportional to submersion depth, counteracts gravity
            float force = Mathf.Clamp(submergedBy * buoyancyForce, 0f, buoyancyForce * 2f);
            _rb.AddForce(Vector3.up * force, ForceMode.Acceleration);
        }
    }
}
