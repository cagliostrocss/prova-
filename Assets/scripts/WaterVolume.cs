using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WaterVolume : MonoBehaviour
{
    [Header("Surface")]
    public float waterSurfaceY = 0f;

    [Header("Underwater Post-Processing")]
    public Color  underwaterFogColor   = new Color(0.05f, 0.2f, 0.3f, 1f);
    [Range(0f, 1f)]
    public float  underwaterFogDensity = 0.08f;

    [Header("Physics")]
    [Range(0f, 10f)]
    public float dragMultiplier = 3f;

    // Reference assigned automatically or via Inspector
    public Volume postProcessVolume;

    // Cached URP overrides
    private ColorAdjustments _colorAdj;
    private Vignette         _vignette;

    // Saved original values
    private float _origSaturation;
    private float _origVignetteIntensity;
    private bool  _origVignetteActive;
    private Color _origVignetteColor;

    void Awake()
    {
        // Make sure we have a trigger collider
        var col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;

        // Find global post-process volume if not set
        if (postProcessVolume == null)
            postProcessVolume = FindObjectOfType<Volume>();

        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out _colorAdj);
            postProcessVolume.profile.TryGet(out _vignette);

            if (_colorAdj != null) _origSaturation = _colorAdj.saturation.value;
            if (_vignette  != null)
            {
                _origVignetteIntensity = _vignette.intensity.value;
                _origVignetteColor     = _vignette.color.value;
                _origVignetteActive    = _vignette.active;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ApplyUnderwaterFX();

        var swim = other.GetComponentInParent<SwimmingController>();
        if (swim != null) swim.EnterWater(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        RestoreNormalFX();

        var swim = other.GetComponentInParent<SwimmingController>();
        if (swim != null) swim.ExitWater();
    }

    void ApplyUnderwaterFX()
    {
        if (_colorAdj != null)
        {
            _colorAdj.saturation.Override(-30f);
            _colorAdj.colorFilter.Override(underwaterFogColor);
            _colorAdj.colorFilter.overrideState = true;
        }
        if (_vignette != null)
        {
            _vignette.active = true;
            _vignette.intensity.Override(underwaterFogDensity * 5f);
            _vignette.color.Override(underwaterFogColor);
        }
        RenderSettings.fogColor   = underwaterFogColor;
        RenderSettings.fogDensity = underwaterFogDensity;
        RenderSettings.fog        = true;
    }

    void RestoreNormalFX()
    {
        if (_colorAdj != null)
        {
            _colorAdj.saturation.Override(_origSaturation);
            _colorAdj.colorFilter.overrideState = false;
        }
        if (_vignette != null)
        {
            _vignette.active = _origVignetteActive;
            _vignette.intensity.Override(_origVignetteIntensity);
            _vignette.color.Override(_origVignetteColor);
        }
        RenderSettings.fog = false;
    }

    // Public getter for buoyancy
    public float GetSurfaceY() => waterSurfaceY;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
        var col = GetComponent<BoxCollider>();
        if (col != null)
            Gizmos.DrawCube(transform.position + col.center, col.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            transform.position + Vector3.left * 5f  + Vector3.up * (waterSurfaceY - transform.position.y),
            transform.position + Vector3.right * 5f + Vector3.up * (waterSurfaceY - transform.position.y));
    }
}
