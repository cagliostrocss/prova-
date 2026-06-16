using UnityEngine;
using BNG;
using PampelGames.GoreSimulator;

public class GunHitDetector : MonoBehaviour
{
    [Tooltip("Punto bocca della canna. Se vuoto, cerca tra i figli.")]
    public Transform muzzlePoint;
    public float damagePerShot = 25f;
    public float maxRange = 150f;

    [Header("KriptoFX — Effetti sangue")]
    [Tooltip("Prefab blood burst all'impatto: Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood1..Blood15")]
    public GameObject[] hitBloodBurstPrefabs;

    [Tooltip("Decal attaccato alla bone: Assets/KriptoFX/VolumetricBloodFX/Prefabs/AttachedBloodDecal.prefab")]
    public GameObject attachedBloodDecalPrefab;

    [Tooltip("Schizzo a terra: Assets/KriptoFX/VolumetricBloodFX/Prefabs/SimplePlaneDecals/Blood1..Blood15")]
    public GameObject[] hitBloodSplatterPrefabs;

    [Tooltip("Secondi minimi tra un colpo e il prossimo che applica danno. Previene raffica accidentale.")]
    public float minTimeBetweenShots = 0.35f;

    private RaycastWeapon _weapon;
    private GameObject    _bulletHolePrefab;
    private Material      _bloodMat;
    private float         _lastDamageTime = -999f;
    private int           _shotCount = 0;
    private EnemyHealth   _trackedEnemy;

    void Start()
    {
        if (muzzlePoint == null)
        {
            foreach (string n in new[] { "Muzzle", "MuzzleFlash", "MuzzlePoint", "Barrel" })
            {
                Transform t = transform.Find(n);
                if (t != null) { muzzlePoint = t; break; }
            }
            if (muzzlePoint == null) muzzlePoint = transform;
        }

        _weapon = GetComponent<RaycastWeapon>();
        if (_weapon != null)
        {
            _bulletHolePrefab   = _weapon.HitFXPrefab;
            _weapon.HitFXPrefab = null;
        }

        _bloodMat = BuildBloodMaterial();
    }

    void OnDestroy()
    {
        if (_bloodMat != null) Destroy(_bloodMat);
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void OnGunFired()
    {
        RaycastHit[] hits = Physics.RaycastAll(
            muzzlePoint.position, muzzlePoint.forward, maxRange,
            ~(1 << 2), QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<Projectile>() != null) continue;
            if (hit.collider.GetComponentInParent<Bullet>()     != null) continue;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                if (enemy.IsDead) return;

                // Rate limiter: ignora colpi troppo ravvicinati (arma automatica accidentale)
                if (Time.time - _lastDamageTime < minTimeBetweenShots)
                {
                    Debug.Log($"[GHD] Colpo ignorato (troppo vicino: {(Time.time - _lastDamageTime)*1000:F0}ms < {minTimeBetweenShots*1000:F0}ms)");
                    return;
                }
                _lastDamageTime = Time.time;

                // Traccia i colpi su questo nemico (per decal che si accumulano)
                if (enemy != _trackedEnemy) { _trackedEnemy = enemy; _shotCount = 0; }
                _shotCount++;

                // Rileva headshot (bone o collider il cui nome contiene "head")
                bool isHeadshot = hit.collider.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0;

                enemy.TakeDamage(damagePerShot, muzzlePoint.forward, hit.point, hit.collider);

                Transform bone = FindNearestBone(enemy, hit.point);
                if (!isHeadshot && bone != null)
                    isHeadshot = bone.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0;

                Vector3 woundPos, woundNormal;
                if (bone != null)
                {
                    Vector3 toShooter = (muzzlePoint.position - bone.position).normalized;
                    woundPos   = bone.position + toShooter * 0.14f;
                    woundNormal = toShooter;
                }
                else
                {
                    woundPos   = hit.point;
                    woundNormal = hit.normal;
                }

                SpawnBloodBurst(woundPos, woundNormal, isHeadshot);
                SpawnAttachedBloodDecal(woundPos, woundNormal, bone);
                SpawnBloodDrip(bone, woundPos);
                SpawnGroundBloodSplatter(enemy.transform.position);
            }
            else
            {
                SpawnDefaultBulletHole(hit);
            }

            return;
        }
    }

    // ── BURST DI SANGUE ALL'IMPATTO ───────────────────────────────────────────
    // Usa KriptoFX Blood1-Blood15. Se non assegnati, fallback particelle.
    // isHeadshot=true → burst extra più grande + scala maggiore.
    // Sempre spawna un piccolo burst "uscita proiettile" sul lato opposto.

    void SpawnBloodBurst(Vector3 worldPos, Vector3 normal, bool isHeadshot = false)
    {
        if (hitBloodBurstPrefabs != null && hitBloodBurstPrefabs.Length > 0)
        {
            var prefab = hitBloodBurstPrefabs[Random.Range(0, hitBloodBurstPrefabs.Length)];
            if (prefab != null)
            {
                // [1] Burst principale (entrata proiettile)
                Quaternion rot = Quaternion.LookRotation(normal);
                GameObject go  = Instantiate(prefab, worldPos, rot);
                float scale    = isHeadshot ? Random.Range(1.8f, 2.5f) : Random.Range(0.9f, 1.3f);
                go.transform.localScale = Vector3.one * scale;
                Destroy(go, 5f);

                // [2] Headshot: secondo burst ancora più grande per l'effetto dramatico
                if (isHeadshot)
                {
                    var prefab2 = hitBloodBurstPrefabs[Random.Range(0, hitBloodBurstPrefabs.Length)];
                    if (prefab2 != null)
                    {
                        GameObject go2 = Instantiate(prefab2, worldPos + normal * 0.05f,
                                                     Quaternion.LookRotation(normal) * Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(-20f, 20f), 0));
                        go2.transform.localScale = Vector3.one * Random.Range(2.0f, 3.0f);
                        Destroy(go2, 5f);
                    }
                }

                // [3] Piccolo burst "uscita proiettile" sul lato opposto del corpo
                var exitPrefab = hitBloodBurstPrefabs[Random.Range(0, hitBloodBurstPrefabs.Length)];
                if (exitPrefab != null)
                {
                    Vector3 exitPos = worldPos - normal * 0.28f;
                    GameObject exitGo = Instantiate(exitPrefab, exitPos, Quaternion.LookRotation(-normal));
                    exitGo.transform.localScale = Vector3.one * Random.Range(0.4f, 0.7f);
                    Destroy(exitGo, 4f);
                }

                return;
            }
        }

        // Fallback procedurale
        GameObject obj = new GameObject("BloodBurst");
        obj.transform.position = worldPos;
        obj.transform.rotation = Quaternion.LookRotation(normal);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main             = ps.main;
        main.duration        = 0.15f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(isHeadshot ? 0.06f : 0.03f, isHeadshot ? 0.16f : 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.85f, 0f, 0f, 1f),
                                   new Color(0.35f, 0f, 0f, 1f));
        main.gravityModifier = 2.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = isHeadshot ? 120 : 60;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, isHeadshot ? (short)60 : (short)25,
                                                           isHeadshot ? (short)80 : (short)40) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = isHeadshot ? 75f : 55f;
        sh.radius    = 0.01f;

        ApplyMat(ps, _bloodMat);
        ps.Play();
        Destroy(obj, 2f);
    }

    // ── DECAL ATTACCATO ALLA BONE (KriptoFX AttachedBloodDecal) ──────────────

    void SpawnAttachedBloodDecal(Vector3 worldPos, Vector3 normal, Transform bone)
    {
        if (attachedBloodDecalPrefab == null || bone == null) return;

        // [5] Decal che si accumula: scala cresce con i colpi (0.9 → 1.1 → 1.3 → 1.5)
        float accumScale = Random.Range(0.7f, 1.1f) + _shotCount * 0.2f;
        accumScale = Mathf.Clamp(accumScale, 0.7f, 2.2f);

        GameObject go = Instantiate(attachedBloodDecalPrefab);
        go.transform.position   = worldPos;
        go.transform.localScale = Vector3.one * accumScale;
        go.transform.LookAt(worldPos + normal, Vector3.up);
        go.transform.Rotate(90f, 0f, 0f);
        go.transform.parent = bone;
        Destroy(go, 25f);
    }

    // ── GOCCE DI SANGUE CHE CADONO ────────────────────────────────────────────

    void SpawnBloodDrip(Transform bone, Vector3 worldPos)
    {
        if (bone == null) return;

        GameObject obj = new GameObject("BloodDrip");
        obj.transform.position = worldPos;
        var tracker = obj.AddComponent<FleshWound>();
        tracker.Init(bone, worldPos, Quaternion.identity, 8f);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main             = ps.main;
        main.duration        = 5f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.8f, 0f, 0f, 1f),
                                   new Color(0.3f, 0f, 0f, 1f));
        main.gravityModifier = 4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 80;

        var em = ps.emission;
        em.rateOverTime = new ParticleSystem.MinMaxCurve(8f, 14f);
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)8, (short)12) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.005f;

        ApplyMat(ps, _bloodMat);
        ps.Play();
        Destroy(obj, 8f);
    }

    // ── SCHIZZO A TERRA (KriptoFX) + MACCHIA PERSISTENTE ────────────────────

    void SpawnGroundBloodSplatter(Vector3 zombiePos)
    {
        Vector3 origin = zombiePos + Vector3.up * 0.3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3f,
                                               ~(1 << 2), QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;

            // KriptoFX: effetto animato di schizzo
            if (hitBloodSplatterPrefabs != null && hitBloodSplatterPrefabs.Length > 0)
            {
                var prefab = hitBloodSplatterPrefabs[Random.Range(0, hitBloodSplatterPrefabs.Length)];
                if (prefab != null)
                {
                    float angle = Mathf.Atan2(h.normal.x, h.normal.z) * Mathf.Rad2Deg + 180f;
                    GameObject go = Instantiate(prefab, h.point, Quaternion.Euler(0f, angle + 90f, 0f));
                    var settings = go.GetComponent<BFX_BloodSettings>();
                    if (settings != null) settings.AnimationSpeed = Random.Range(0.8f, 1.2f);
                    Destroy(go, 15f);
                }
            }

            // Macchia persistente: quad circolare piatto che rimane a terra
            SpawnPersistentBloodStain(h.point, h.normal);
            return;
        }
    }

    // Quad circolare piatto con texture di sangue — rimane a terra 120 secondi
    void SpawnPersistentBloodStain(Vector3 floorPoint, Vector3 floorNormal)
    {
        GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(stain.GetComponent<MeshCollider>());
        stain.name = "BloodStain";

        // Ruotato orizzontale sul pavimento, rotazione casuale attorno all'asse normale
        stain.transform.rotation = Quaternion.FromToRotation(Vector3.forward, floorNormal)
                                   * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        stain.transform.position = floorPoint + floorNormal * 0.012f;

        float size = Random.Range(0.25f, 0.55f);
        stain.transform.localScale = new Vector3(size, size, 1f);

        var rend = stain.GetComponent<Renderer>();
        rend.material  = BuildStainMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Destroy(stain, 120f);
    }

    static Material BuildStainMaterial()
    {
        Texture2D tex = CreateBloodStainTexture(128);

        // Sprites/Default è l'unico shader garantito trasparente su qualsiasi pipeline
        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) return null;

        var mat = new Material(s);
        mat.mainTexture = tex;
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.color = Color.white;
        mat.renderQueue = 3000;
        return mat;
    }

    static Texture2D CreateBloodStainTexture(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;

        // Genera forma organica con rumore
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x - cx) / (size * 0.5f);
            float ny = (y - cy) / (size * 0.5f);
            float d  = Mathf.Sqrt(nx * nx + ny * ny);

            // Bordo irregolare con pseudo-rumore
            float angle  = Mathf.Atan2(ny, nx);
            float jitter = 0.12f * Mathf.Sin(angle * 7f) + 0.08f * Mathf.Sin(angle * 13f + 1f);
            float edge   = 0.78f + jitter;

            Color c;
            if (d < edge * 0.55f)
                c = new Color(0.18f, 0.01f, 0.01f, 0.92f);   // centro scuro
            else if (d < edge)
            {
                float t = (d - edge * 0.55f) / (edge * 0.45f);
                float a = Mathf.Lerp(0.90f, 0f, t * t);
                c = new Color(0.22f, 0.02f, 0.02f, a);         // bordo sfumato
            }
            else
                c = Color.clear;

            pixels[y * size + x] = c;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ── BULLET HOLE NORMALE (muri / oggetti) ──────────────────────────────────

    void SpawnDefaultBulletHole(RaycastHit hit)
    {
        if (_bulletHolePrefab == null) return;
        Quaternion rot    = Quaternion.FromToRotation(Vector3.forward, hit.normal);
        GameObject impact = Instantiate(_bulletHolePrefab, hit.point, rot);
        impact.GetComponent<BulletHole>()?.TryAttachTo(hit.collider);
    }

    // ── UTILITY ───────────────────────────────────────────────────────────────

    static Transform FindNearestBone(EnemyHealth zombie, Vector3 worldPos)
    {
        float minDist = float.MaxValue;
        Transform nearestTransform = null;
        foreach (Transform t in zombie.GetComponentsInChildren<Transform>())
        {
            if (t == zombie.transform) continue;
            float d = Vector3.Distance(t.position, worldPos);
            if (d < minDist) { minDist = d; nearestTransform = t; }
        }
        if (nearestTransform == null) return null;

        Transform t2 = nearestTransform;
        while (t2 != null && t2 != zombie.transform)
        {
            if (t2.GetComponent<Rigidbody>() != null) return t2;
            t2 = t2.parent;
        }
        return nearestTransform;
    }

    static Material BuildBloodMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
        if (s == null) return null;

        var mat = new Material(s);
        Color c = new Color(0.75f, 0f, 0f, 1f);
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3001;
        return mat;
    }

    static void ApplyMat(ParticleSystem ps, Material mat)
    {
        if (mat == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null) r.material = mat;
    }
}
