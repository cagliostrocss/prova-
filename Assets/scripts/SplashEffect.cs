using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SplashEffect : MonoBehaviour
{
    [Header("Splash")]
    public GameObject splashPrefab;
    [Range(0f, 20f)] public float velocityThreshold = 2f;
    [Range(0f, 5f)]  public float splashLifetime    = 2f;
    [Range(0f, 3f)]  public float cooldown          = 0.5f;

    private float       _lastSplash = -99f;
    private WaterVolume _volume;

    void OnTriggerEnter(Collider other)
    {
        var vol = other.GetComponent<WaterVolume>();
        if (vol == null) return;
        _volume = vol;
        TrySpawnSplash();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<WaterVolume>() == null) return;
        _volume = null;
        TrySpawnSplash();
    }

    void TrySpawnSplash()
    {
        if (splashPrefab == null) return;
        if (Time.time - _lastSplash < cooldown) return;

        var rb    = GetComponent<Rigidbody>();
        float vel = rb != null ? rb.velocity.magnitude : 0f;
        if (vel < velocityThreshold) return;

        _lastSplash = Time.time;

        float surfY    = _volume != null ? _volume.GetSurfaceY() : transform.position.y;
        Vector3 spawnPos = new Vector3(transform.position.x, surfY, transform.position.z);
        var go = Instantiate(splashPrefab, spawnPos, Quaternion.identity);

        // Scale splash with impact velocity
        float scale = Mathf.Clamp(vel / 10f, 0.5f, 3f);
        go.transform.localScale = Vector3.one * scale;

        Destroy(go, splashLifetime);
    }
}
