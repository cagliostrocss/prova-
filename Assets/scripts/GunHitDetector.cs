using UnityEngine;
using System.Collections.Generic;
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

    [Header("Ferite sul corpo (stile Quake/L4D2)")]
    [Tooltip("Numero massimo di ferite per nemico. Le più vecchie vengono riciclate.")]
    public int maxWoundsPerEnemy = 24;
    [Range(0.02f, 0.2f)] public float woundSizeMin = 0.045f;
    [Range(0.02f, 0.3f)] public float woundSizeMax = 0.09f;
    [Tooltip("Quanto il decal viene tirato verso l'osso (0=sul collider, 1=centro osso). Alza se i fori fluttuano.")]
    [Range(0f, 0.9f)] public float woundInset = 0.45f;

    [Header("Macchia di sangue sull'indumento")]
    [Tooltip("Dimensione iniziale della macchia che si espande.")]
    [Range(0.05f, 0.4f)] public float soakStartSize = 0.08f;
    [Tooltip("Dimensione finale della macchia espansa.")]
    [Range(0.1f, 0.8f)]  public float soakEndSize = 0.28f;
    [Tooltip("Secondi per la massima espansione della macchia.")]
    [Range(0.5f, 8f)]    public float soakGrowTime = 3f;

    private RaycastWeapon _weapon;
    private GameObject    _bulletHolePrefab;
    private Material      _bloodMat;
    private Material[]    _woundMats;
    private Material[]    _soakMats;
    private float         _lastDamageTime = -999f;
    private int           _shotCount = 0;
    private EnemyHealth   _trackedEnemy;
    private readonly Dictionary<EnemyHealth, Queue<GameObject>> _enemyWounds = new();

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

        // Genera alcune varianti di texture-ferita per non avere decal tutti uguali
        _woundMats = new Material[5];
        for (int i = 0; i < _woundMats.Length; i++)
            _woundMats[i] = BuildWoundMaterial(CreateWoundTexture(128, i * 977 + 31));

        // Varianti per la macchia di sangue (sfumata, morbida). Disegnate SOTTO il foro.
        _soakMats = new Material[4];
        for (int i = 0; i < _soakMats.Length; i++)
        {
            _soakMats[i] = BuildWoundMaterial(CreateSoakTexture(128, i * 613 + 17));
            if (_soakMats[i] != null) _soakMats[i].renderQueue = 3040;
        }
    }

    void OnDestroy()
    {
        if (_bloodMat != null) Destroy(_bloodMat);
        if (_woundMats != null)
            foreach (var m in _woundMats) if (m != null) Destroy(m);
        if (_soakMats != null)
            foreach (var m in _soakMats) if (m != null) Destroy(m);
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

                // Spruzzo momentaneo all'impatto (juice)
                SpawnBloodBurst(woundPos, woundNormal, isHeadshot);

                // Ferita PERSISTENTE dipinta nello spazio UV della mesh (stile L4D2):
                // resta incollata al vestito e si deforma con la pelle, non scivola mai.
                PaintGoreWound(enemy, hit.point, isHeadshot);

                // Schizzo a terra KriptoFX stabile (SimplePlaneDecals piatti), dura 60s
                SpawnGroundBloodSplatter(hit.point);
            }
            else
            {
                SpawnDefaultBulletHole(hit);
            }

            return;
        }
    }

    // ── FERITA DIPINTA NELLO SPAZIO UV (GoreWoundPainter) ─────────────────────

    void PaintGoreWound(EnemyHealth enemy, Vector3 worldPoint, bool isHeadshot)
    {
        var painter = enemy.GetComponent<GoreWoundPainter>();
        if (painter == null) painter = enemy.gameObject.AddComponent<GoreWoundPainter>();
        painter.PaintWound(worldPoint, isHeadshot ? 1.6f : 1f, 1f);
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
                float scale = isHeadshot ? Random.Range(0.5f, 0.8f) : Random.Range(0.25f, 0.45f);
                go.transform.localScale = Vector3.one * scale;
                var s1 = go.GetComponent<BFX_BloodSettings>();
                if (s1 != null) s1.AnimationSpeed = 2.5f;   // spruzzo più rapido
                DisableProjectorDecal(go);                  // rimuove il decal che ondeggia
                Destroy(go, 2f);

                // Burst uscita proiettile (ridotto)
                var exitPrefab = hitBloodBurstPrefabs[Random.Range(0, hitBloodBurstPrefabs.Length)];
                if (exitPrefab != null)
                {
                    Vector3 exitPos = worldPos - normal * 0.28f;
                    GameObject exitGo = Instantiate(exitPrefab, exitPos, Quaternion.LookRotation(-normal));
                    exitGo.transform.localScale = Vector3.one * Random.Range(0.15f, 0.3f);
                    var s2 = exitGo.GetComponent<BFX_BloodSettings>();
                    if (s2 != null) s2.AnimationSpeed = 2.5f;
                    Destroy(exitGo, 2f);
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

    // ── FERITA PERSISTENTE SUL CORPO (stile Quake 2 / L4D2) ───────────────────
    // Crea un quad con texture di sangue nel punto colpito, orientato sulla
    // superficie e ancorato all'osso tramite FleshWound (segue il personaggio
    // sia da vivo che durante il ragdoll, senza distorsioni da scala).
    // Le ferite si accumulano fino a maxWoundsPerEnemy.

    void SpawnFleshWoundDecal(Vector3 surfacePos, Vector3 surfaceNormal, Transform bone, bool isHeadshot)
    {
        if (bone == null || _woundMats == null || _woundMats.Length == 0) return;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col  = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        quad.name = "FleshWound_Decal";

        // Orienta il quad disteso sulla superficie, faccia verso l'esterno,
        // con rotazione casuale attorno alla normale per varietà
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, surfaceNormal)
                       * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        Vector3 pos = surfacePos + surfaceNormal * 0.01f;   // anti z-fighting

        quad.transform.position   = pos;
        quad.transform.rotation   = rot;
        float baseSize = Random.Range(woundSizeMin, woundSizeMax);
        if (isHeadshot) baseSize *= 1.4f;
        quad.transform.localScale = new Vector3(baseSize, baseSize, baseSize);

        var rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial    = _woundMats[Random.Range(0, _woundMats.Length)];
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows    = false;

        // Ancoraggio all'osso senza parenting → niente distorsioni da scala
        var fw = quad.AddComponent<FleshWound>();
        fw.Init(bone, pos, rot, 9999f);   // persistente; si auto-distrugge quando l'osso sparisce

        // Accumulo con limite per nemico
        if (_trackedEnemy != null)
        {
            if (!_enemyWounds.TryGetValue(_trackedEnemy, out var q))
            {
                q = new Queue<GameObject>();
                _enemyWounds[_trackedEnemy] = q;
            }
            q.Enqueue(quad);
            while (q.Count > maxWoundsPerEnemy)
            {
                var old = q.Dequeue();
                if (old != null) Destroy(old);
            }
        }
    }

    // Texture-ferita procedurale: foro scuro, sangue, bordo sfumato e schizzi.
    static Texture2D CreateWoundTexture(int size, int seed)
    {
        var rng = new System.Random(seed);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;

        float h1 = 5f  + (float) rng.NextDouble() * 6f;
        float h2 = 11f + (float) rng.NextDouble() * 8f;
        float p1 = (float) rng.NextDouble() * 6.28f;
        float p2 = (float) rng.NextDouble() * 6.28f;
        float baseEdge = 0.60f + (float) rng.NextDouble() * 0.12f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x - cx) / (size * 0.5f);
            float ny = (y - cy) / (size * 0.5f);
            float d  = Mathf.Sqrt(nx * nx + ny * ny);
            float a  = Mathf.Atan2(ny, nx);
            float jitter = 0.14f * Mathf.Sin(a * h1 + p1) + 0.09f * Mathf.Sin(a * h2 + p2);
            float edge   = baseEdge + jitter;

            Color c;
            if (d < edge * 0.25f)
                c = new Color(0.07f, 0.0f, 0.0f, 0.97f);
            else if (d < edge * 0.60f)
                c = new Color(0.28f, 0.02f, 0.02f, 0.95f);
            else if (d < edge)
            {
                float t  = (d - edge * 0.60f) / (edge * 0.40f);
                float al = Mathf.Lerp(0.92f, 0f, t * t);
                c = new Color(0.34f, 0.03f, 0.02f, al);
            }
            else c = Color.clear;

            px[y * size + x] = c;
        }

        int drops = 6 + rng.Next(9);
        for (int i = 0; i < drops; i++)
        {
            float a    = (float) rng.NextDouble() * 6.28f;
            float dist = (0.6f + (float) rng.NextDouble() * 0.42f) * size * 0.5f;
            int dx = (int) (cx + Mathf.Cos(a) * dist);
            int dy = (int) (cy + Mathf.Sin(a) * dist);
            int r  = 1 + rng.Next(3);
            for (int yy = -r; yy <= r; yy++)
            for (int xx = -r; xx <= r; xx++)
            {
                int sx = dx + xx, sy = dy + yy;
                if (sx < 0 || sy < 0 || sx >= size || sy >= size) continue;
                if (xx * xx + yy * yy > r * r) continue;
                px[sy * size + sx] = new Color(0.28f, 0.02f, 0.02f, 0.85f);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static Material BuildWoundMaterial(Texture2D tex)
    {
        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Unlit/Transparent");
        if (s == null) return null;

        var mat = new Material(s);
        mat.mainTexture = tex;
        if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color       = Color.white;
        mat.renderQueue = 3050;
        return mat;
    }

    // ── FERITA VOLUMETRICA KRIPTOFX (sangue che si espande sul corpo) ─────────
    // Istanzia il prefab AttachedBloodDecal (proiettore a cubo) e lo ancora
    // all'osso colpito tramite FleshWound, così segue il personaggio senza le
    // distorsioni causate dalla scala non uniforme dei rig Mixamo.
    // Lo shader BFX_ShaderProperies anima la proprietà _Cutout → il sangue
    // si espande gradualmente sull'indumento (effetto del video dell'asset).

    void SpawnVolumetricWound(Vector3 hitPoint, Vector3 surfaceNormal, Transform bone, bool isHeadshot)
    {
        if (attachedBloodDecalPrefab == null || bone == null) return;

        GameObject go = Instantiate(attachedBloodDecalPrefab);
        Transform t = go.transform;

        // Orientamento come nella demo dell'asset: il proiettore guarda lungo la
        // normale della superficie (transform.up = asse di proiezione del decal).
        t.position = hitPoint;
        t.rotation = Quaternion.identity;
        t.LookAt(hitPoint + surfaceNormal, Vector3.up);
        t.Rotate(90f, 0f, 0f);
        // Rotazione casuale attorno alla normale per varietà del pattern
        t.RotateAround(hitPoint, surfaceNormal, Random.Range(0f, 360f));

        float s = Random.Range(0.6f, 0.95f) * (isHeadshot ? 1.35f : 1f);
        t.localScale = Vector3.one * s;

        // Parentela diretta all'osso (metodo della demo dell'asset): il decal
        // eredita esattamente il transform dell'osso → niente lag di un frame,
        // l'asse di proiezione resta solidale al corpo anche durante il ragdoll.
        t.SetParent(bone, true);

        // Accumulo con limite per nemico
        if (_trackedEnemy != null)
        {
            if (!_enemyWounds.TryGetValue(_trackedEnemy, out var q))
            {
                q = new Queue<GameObject>();
                _enemyWounds[_trackedEnemy] = q;
            }
            q.Enqueue(go);
            while (q.Count > maxWoundsPerEnemy)
            {
                var old = q.Dequeue();
                if (old != null) Destroy(old);
            }
        }
    }

    // ── MACCHIA DI SANGUE CHE SI ESPANDE SULL'INDUMENTO (fallback procedurale) ─
    // Quad sfumato rosso che cresce nel tempo attorno alla ferita, ancorato
    // all'osso. Disegnato SOTTO il foro di proiettile (render queue inferiore).

    void SpawnBloodSoak(Vector3 surfacePos, Vector3 surfaceNormal, Transform bone, bool isHeadshot)
    {
        if (bone == null || _soakMats == null || _soakMats.Length == 0) return;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col  = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        quad.name = "BloodSoak_Decal";

        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, surfaceNormal)
                       * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        Vector3 pos = surfacePos + surfaceNormal * 0.004f;   // appena sotto il foro

        quad.transform.position = pos;
        quad.transform.rotation = rot;

        var rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial    = _soakMats[Random.Range(0, _soakMats.Length)];
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows    = false;

        var fw = quad.AddComponent<FleshWound>();
        fw.Init(bone, pos, rot, 9999f);

        float startS = soakStartSize * (isHeadshot ? 1.3f : 1f);
        float endS   = soakEndSize   * (isHeadshot ? 1.4f : 1f);
        var soak = quad.AddComponent<BloodSoak>();
        soak.Init(startS, endS, soakGrowTime);

        // Accumulo con lo stesso limite delle ferite
        if (_trackedEnemy != null)
        {
            if (!_enemyWounds.TryGetValue(_trackedEnemy, out var q))
            {
                q = new Queue<GameObject>();
                _enemyWounds[_trackedEnemy] = q;
            }
            q.Enqueue(quad);
            while (q.Count > maxWoundsPerEnemy * 2)
            {
                var old = q.Dequeue();
                if (old != null) Destroy(old);
            }
        }
    }

    // Texture macchia: cerchio rosso morbido senza foro, sfuma ai bordi.
    static Texture2D CreateSoakTexture(int size, int seed)
    {
        var rng = new System.Random(seed);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;

        float h1 = 4f + (float) rng.NextDouble() * 5f;
        float p1 = (float) rng.NextDouble() * 6.28f;
        float baseEdge = 0.70f + (float) rng.NextDouble() * 0.15f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x - cx) / (size * 0.5f);
            float ny = (y - cy) / (size * 0.5f);
            float d  = Mathf.Sqrt(nx * nx + ny * ny);
            float a  = Mathf.Atan2(ny, nx);
            float edge = baseEdge + 0.10f * Mathf.Sin(a * h1 + p1);

            Color c;
            if (d < edge)
            {
                float t  = d / edge;
                // Centro più saturo, bordo sfumato; alpha massimo moderato così tinge il tessuto
                float al = Mathf.Lerp(0.78f, 0f, t * t);
                c = new Color(0.40f, 0.02f, 0.02f, al);
            }
            else c = Color.clear;

            px[y * size + x] = c;
        }

        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
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

    // Disabilita il child "Decal" (proiettore di profondità BFX_Decal che ondeggia
    // con la camera), lasciando solo lo spruzzo aereo del sangue volumetrico.
    static void DisableProjectorDecal(GameObject bloodInstance)
    {
        foreach (Transform t in bloodInstance.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf("Decal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                t.gameObject.SetActive(false);
        }
    }

    // ── SCHIZZO A TERRA KRIPTOFX STABILE (SimplePlaneDecals) ───────────────────
    // Usa i prefab SimplePlaneDecals dell'asset, che usano lo shader BFX_Decal_Plane:
    // sono decal PIATTI di geometria reale (non a proiezione di profondità come i
    // Blood volumetrici) → NON ondeggiano con la camera. Durano 60 secondi.

    void SpawnGroundBloodSplatter(Vector3 hitPoint)
    {
        if (hitBloodSplatterPrefabs == null || hitBloodSplatterPrefabs.Length == 0) return;

        Vector3 origin = hitPoint + Vector3.up * 0.3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 4f,
                                               ~(1 << 2), QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;

            var prefab = hitBloodSplatterPrefabs[Random.Range(0, hitBloodSplatterPrefabs.Length)];
            if (prefab == null) return;

            float angle = Mathf.Atan2(h.normal.x, h.normal.z) * Mathf.Rad2Deg + 180f;
            GameObject go = Instantiate(prefab, h.point, Quaternion.Euler(0f, angle + 90f, 0f));
            go.transform.localScale = Vector3.one * Random.Range(0.7f, 1.1f);

            var settings = go.GetComponent<BFX_BloodSettings>();
            if (settings != null)
            {
                settings.AutomaticGroundHeightDetection = true;
                settings.AnimationSpeed = 2f;            // appare rapido
                settings.DecalLifeTimeSeconds = 60f;     // resta a terra un minuto
            }
            Destroy(go, 62f);
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
