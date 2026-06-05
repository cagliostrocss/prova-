using UnityEngine;
using BNG;
using PampelGames.GoreSimulator;

/// <summary>
/// Gestisce danno + effetti visivi per ogni sparo.
/// Chiamato da onShootEvent di RaycastWeapon (sempre attivo).
///
/// Zombi: decal scuro programmativo (NO prefab BNG → zero particelle arancioni)
///        + splash di sangue + gocce che colano
/// Muri : BulletHole BNG normale
/// </summary>
public class GunHitDetector : MonoBehaviour
{
    [Tooltip("Punto bocca della canna. Se vuoto, cerca tra i figli.")]
    public Transform muzzlePoint;
    public float damagePerShot = 25f;
    public float maxRange = 150f;

    [Header("KriptoFX Blood Asset")]
    [Tooltip("Trascina qui: Assets/KriptoFX/VolumetricBloodFX/Prefabs/AttachedBloodDecal.prefab")]
    public GameObject attachedBloodDecalPrefab;

    [Tooltip("Trascina qui alcuni prefab da: Assets/KriptoFX/VolumetricBloodFX/Prefabs/SimplePlaneDecals/")]
    public GameObject[] hitBloodSplatterPrefabs;

    private RaycastWeapon _weapon;
    private GameObject    _bulletHolePrefab;
    private Material      _bloodMat;
    private Material      _woundMat;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Auto-trova muzzle
        if (muzzlePoint == null)
        {
            foreach (string n in new[] { "Muzzle", "MuzzleFlash", "MuzzlePoint", "Barrel" })
            {
                Transform t = transform.Find(n);
                if (t != null) { muzzlePoint = t; break; }
            }
            if (muzzlePoint == null) muzzlePoint = transform;
        }

        // Salva e azzera HitFXPrefab di RaycastWeapon
        _weapon = GetComponent<RaycastWeapon>();
        if (_weapon != null)
        {
            _bulletHolePrefab   = _weapon.HitFXPrefab;
            _weapon.HitFXPrefab = null;
        }

        _bloodMat = BuildMaterial(new Color(0.8f, 0f, 0f, 1f));

        // Crea il materiale ferita con texture circolare generata in codice:
        // il materiale BNG ha l'arancione baked nella texture → inutile sovrascrivere il colore.
        // Con la nostra texture controlliamo esattamente forma e colore.
        _woundMat = BuildCircularWoundMaterial();
    }

    void OnDestroy()
    {
        if (_bloodMat != null) Destroy(_bloodMat);
        if (_woundMat != null) Destroy(_woundMat);
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
            if (hit.collider.GetComponentInParent<Bullet>()    != null) continue;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(damagePerShot, muzzlePoint.forward, hit.point, hit.collider);

                // Gore Simulator: taglia la bone più vicina al punto di impatto.
                // 30% di probabilità per non smembrare lo zombi al primo colpo.
                var gore = enemy.GetComponent<GoreSimulator>();
                if (gore != null && Random.value < 0.15f)
                    gore.ExecuteCut(hit.point, muzzlePoint.forward * 6f);

                Transform bone = FindNearestBone(enemy, hit.point);

                // Posizione ferita: bone center offset verso il player
                // (NON usa hit.point del root collider che è troppo lontano/dentro la mesh)
                Vector3 woundPos;
                Vector3 woundNormal;
                if (bone != null)
                {
                    Vector3 toShooter = (muzzlePoint.position - bone.position).normalized;
                    woundPos   = bone.position + toShooter * 0.08f;
                    woundNormal = toShooter;
                }
                else
                {
                    woundPos   = hit.point;
                    woundNormal = hit.normal;
                }

                SpawnWoundDecal(woundPos, woundNormal, bone);
                SpawnAttachedBloodDecal(woundPos, woundNormal, bone);
                SpawnBloodBurst(woundPos, woundNormal);
                if (bone != null) SpawnBloodDrip(bone, woundPos);
                SpawnFloorBloodSplatter(enemy);
            }
            else
            {
                SpawnDefaultBulletHole(hit);
            }

            return;
        }
    }

    // ── DECAL FERITA ──────────────────────────────────────────────────────────
    // USA FleshWound.cs per il tracking: NON è figlio della bone nella gerarchia
    // → DetachBoneDecals() non lo tocca → segue correttamente il ragdoll

    void SpawnWoundDecal(Vector3 worldPos, Vector3 normal, Transform bone)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<MeshCollider>());

        float size = Random.Range(0.07f, 0.12f);   // visibile in VR
        quad.transform.localScale = Vector3.one * size;

        var rend = quad.GetComponent<Renderer>();
        if (_woundMat != null) rend.material = _woundMat;

        // FleshWound traccia la bone via LateUpdate senza SetParent
        // → invisibile a DetachBoneDecals (non è nella gerarchia delle ossa)
        var wound = quad.AddComponent<FleshWound>();
        if (bone != null)
            wound.Init(bone, worldPos, Quaternion.LookRotation(normal), 30f);
        else
        {
            quad.transform.position = worldPos;
            quad.transform.rotation = Quaternion.LookRotation(normal);
            Destroy(quad, 10f);
        }
    }

    // ── SPLASH DI SANGUE ALL'IMPATTO ──────────────────────────────────────────

    void SpawnBloodBurst(Vector3 worldPos, Vector3 normal)
    {
        GameObject obj = new GameObject("BloodBurst");
        obj.transform.position = worldPos;
        obj.transform.rotation = Quaternion.LookRotation(normal);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main             = ps.main;
        main.duration        = 0.12f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.9f, 0f, 0f, 1f),
                                   new Color(0.4f, 0f, 0f, 1f));
        main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 30;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10, (short)20) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 40f;
        sh.radius    = 0.005f;

        ApplyMat(ps, _bloodMat);
        ps.Play();
        Destroy(obj, 1.5f);
    }

    // ── GOCCE CHE COLANO ─────────────────────────────────────────────────────

    void SpawnBloodDrip(Transform bone, Vector3 worldPos)
    {
        GameObject obj = new GameObject("BloodDrip");
        obj.transform.position = worldPos;
        // FleshWound per tracking senza gerarchia → DetachBoneDecals non lo tocca
        var tracker = obj.AddComponent<FleshWound>();
        tracker.Init(bone, worldPos, Quaternion.identity, 6f);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main             = ps.main;
        main.duration        = 4f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.01f, 0.1f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.8f, 0f, 0f, 1f),
                                   new Color(0.3f, 0f, 0f, 1f));
        main.gravityModifier = 3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 60;

        var em = ps.emission;
        em.rateOverTime = new ParticleSystem.MinMaxCurve(3f, 6f);
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)4, (short)7) });

        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.003f;

        var vel   = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x     = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        vel.y     = new ParticleSystem.MinMaxCurve(-0.25f, -0.05f);
        vel.z     = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

        ApplyMat(ps, _bloodMat);
        ps.Play();
        Destroy(obj, 6f);
    }

    // ── KRIPTOFX: DECAL ATTACCATO ALLA BONE ──────────────────────────────────
    // La root di AttachedBloodDecal NON ha Renderer → DetachBoneDecals() non lo stacca
    // Il prefab segue il ragdoll restando figlio della bone

    void SpawnAttachedBloodDecal(Vector3 worldPos, Vector3 normal, Transform bone)
    {
        if (attachedBloodDecalPrefab == null || bone == null) return;

        GameObject go = Instantiate(attachedBloodDecalPrefab);
        go.transform.position     = worldPos;
        go.transform.localScale   = Vector3.one * Random.Range(0.8f, 1.4f);
        go.transform.LookAt(worldPos + normal, Vector3.up);
        go.transform.Rotate(90f, 0f, 0f);
        go.transform.parent = bone;    // segue il ragdoll
        Destroy(go, 25f);
    }

    // ── KRIPTOFX: SCHIZZO DI SANGUE A TERRA ──────────────────────────────────

    void SpawnFloorBloodSplatter(EnemyHealth zombie)
    {
        if (hitBloodSplatterPrefabs == null || hitBloodSplatterPrefabs.Length == 0) return;

        // Parte da un punto DENTRO il root collider → Physics non rileva il collider
        // stesso dall'interno → il primo hit è il pavimento reale
        Vector3 origin = zombie.transform.position + Vector3.up * 0.3f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3f,
                                               ~(1 << 2), QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;

            var prefab = hitBloodSplatterPrefabs[Random.Range(0, hitBloodSplatterPrefabs.Length)];
            if (prefab == null) return;

            float angle = Mathf.Atan2(h.normal.x, h.normal.z) * Mathf.Rad2Deg + 180f;
            GameObject go = Instantiate(prefab, h.point, Quaternion.Euler(0f, angle + 90f, 0f));

            var settings = go.GetComponent<BFX_BloodSettings>();
            if (settings != null)
                settings.AnimationSpeed = Random.Range(0.8f, 1.2f);

            Destroy(go, 20f);
            return;
        }
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
        // 1. Trova il Transform più vicino fra TUTTI i figli (compresi gli intermedi)
        //    → posizionamento ferita preciso (non solo le bone con Rigidbody)
        float minDist = float.MaxValue;
        Transform nearestTransform = null;
        foreach (Transform t in zombie.GetComponentsInChildren<Transform>())
        {
            if (t == zombie.transform) continue;
            float d = Vector3.Distance(t.position, worldPos);
            if (d < minDist) { minDist = d; nearestTransform = t; }
        }

        if (nearestTransform == null) return null;

        // 2. Per il tracking ragdoll serve un Rigidbody:
        //    risali la gerarchia dal transform trovato fino a trovare un antenato con RB
        Transform t2 = nearestTransform;
        while (t2 != null && t2 != zombie.transform)
        {
            if (t2.GetComponent<Rigidbody>() != null) return t2;
            t2 = t2.parent;
        }

        return nearestTransform; // fallback
    }

    static Material BuildMaterial(Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");

        if (s == null) return null;
        var mat = new Material(s);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);
        return mat;
    }

    /// <summary>
    /// Crea un materiale con texture circolare scura generata in codice.
    /// Centro nero (foro), anello rosso sangue, bordo sfumato trasparente.
    /// Usa Sprites/Default che supporta RGBA trasparente in qualsiasi pipeline.
    /// </summary>
    static Material BuildCircularWoundMaterial()
    {
        Texture2D tex = CreateWoundTexture(64);

        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Universal Render Pipeline/Unlit");

        if (s == null) return null;

        var mat = new Material(s);
        mat.mainTexture = tex;
        if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex",  tex);
        if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap",  tex);
        mat.color = Color.white;   // la texture controlla già il colore
        return mat;
    }

    /// <summary>
    /// Genera una texture circolare: foro scuro al centro, sangue rosso intorno, bordo trasparente.
    /// </summary>
    static Texture2D CreateWoundTexture(int size = 64)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;
        float coreR  = size * 0.20f;   // foro centrale scuro
        float bloodR = size * 0.36f;   // anello di sangue
        float outerR = size * 0.48f;   // bordo sfumato

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            Color c;
            if (d <= coreR)
            {
                c = new Color(0.02f, 0f, 0f, 1f);                              // foro
            }
            else if (d <= bloodR)
            {
                float t = (d - coreR) / (bloodR - coreR);
                c = Color.Lerp(new Color(0.05f, 0f, 0f, 1f),
                               new Color(0.20f, 0.01f, 0.01f, 1f), t);        // sangue
            }
            else if (d <= outerR)
            {
                float t = (d - bloodR) / (outerR - bloodR);
                float a = Mathf.Lerp(0.85f, 0f, t * t);
                c = new Color(0.12f, 0.01f, 0.01f, a);                        // sfumatura
            }
            else
            {
                c = Color.clear;
            }
            pixels[y * size + x] = c;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void ApplyMat(ParticleSystem ps, Material mat)
    {
        if (mat == null) return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null) r.material = mat;
    }
}
