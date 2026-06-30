using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PampelGames.GoreSimulator;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("KriptoFX Death Blood Pool")]
    [Tooltip("Trascina qui alcuni prefab da: Assets/KriptoFX/VolumetricBloodFX/Prefabs/ (Blood1-Blood15)")]
    public GameObject[] deathBloodPrefabs;
    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;

    [Header("Hit Animation")]
    public float hitAnimCooldown = 1.5f;
    public AnimationClip[] hitReactionClips;
    private float _lastHitTime = -999f;

    [Header("Knockback")]
    [Range(0f, 5f)]   public float knockbackForce    = 2f;
    [Range(0f, 0.5f)] public float knockbackDuration = 0.2f;

    [Header("Death Transition")]
    [Range(0f, 1f)] public float deathBlendDuration = 0.4f;


    public bool IsDead { get; private set; }
    public bool IsHit  { get; private set; }
    private Vector3 _deathVelocity;
    private bool _trailStarted = false;
    private int _hitStateCount = 9;
    private string _currentHitState = "";

    void Start()
    {
        currentHealth = maxHealth;

        // CRITICO: usare GetComponentInChildren, non GetComponent.
        // In questo FBX Mixamo l'Animator è su un oggetto figlio, non sul root.
        // Con GetComponent si ottiene null → animator.enabled = false non viene mai
        // chiamato → LateUpdate dell'Animator sovrascrive le ossa ogni frame →
        // busto visivamente fisso in aria anche se la fisica funziona.
        animator = GetComponentInChildren<Animator>();

        // Forza l'Animator a scrivere SEMPRE i transform delle ossa, anche fuori schermo.
        // CullUpdateTransforms (default) può bloccare l'aggiornamento in VR se il frustum
        // non include lo zombi → ossa restano in T-pose al momento del ragdoll.
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }

        agent = GetComponent<NavMeshAgent>();
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();
        SetRagdollActive(false);

        Debug.Log($"[RD-Start] Rigidbody trovati: {ragdollRigidbodies.Length}");

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;
            if (rb.gameObject.GetComponent<HitReaction>() == null)
                rb.gameObject.AddComponent<HitReaction>();
        }
    }

    void SetRagdollActive(bool active)
    {
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == this.gameObject)
            {
                rb.isKinematic = true;
                continue;
            }
            rb.isKinematic = !active;
        }
        foreach (Collider col in ragdollColliders)
        {
            if (col.gameObject == this.gameObject)
            {
                // Root capsule: attivo solo quando ragdoll è spento
                col.enabled = !active;
            }
            else
            {
                // Collider delle ossa sempre abilitati: permettono ai proiettili di colpire
                // anche con ragdoll spento. I Rigidbody kinematic impediscono già la fisica.
                col.enabled = true;
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        currentHealth -= damage;
        Debug.Log($"[EnemyHealth] TakeDamage {damage} → HP rimanenti: {currentHealth}");
        TryPlayHitAnimation();
        if (currentHealth <= 0) Die();
    }

    public void TakeDamage(float damage, Vector3 hitDirection, Vector3 hitPoint, Collider hitCollider)
    {
        if (IsDead) return;
        currentHealth -= damage;
        Debug.Log($"[EnemyHealth] TakeDamage {damage} su '{hitCollider.name}' → HP rimanenti: {currentHealth}");

        HitReaction hitReaction = hitCollider.GetComponent<HitReaction>();
        if (hitReaction == null)
            hitReaction = FindNearestBoneHitReaction(hitPoint);
        hitReaction?.ApplyHitForce(hitDirection, 150f);

        // Knockback fisico — spinge l'NPC nella direzione del colpo
        if (!IsDead) StartCoroutine(ApplyKnockback(hitDirection));

        TryPlayHitAnimation();

        // Scia di sangue disabilitata temporaneamente
        // if (!_trailStarted) { _trailStarted = true; StartCoroutine(BloodTrailCoroutine()); }

        if (currentHealth <= 0) Die();
    }

    public void TakeDamage(float damage, Vector3 hitDirection, Collider hitCollider)
    {
        TakeDamage(damage, hitDirection, hitCollider.transform.position, hitCollider);
    }

    IEnumerator ApplyKnockback(Vector3 hitDirection)
    {
        if (agent == null || !agent.enabled) yield break;
        Vector3 push = new Vector3(hitDirection.x, 0f, hitDirection.z).normalized * knockbackForce;
        float elapsed = 0f;
        while (elapsed < knockbackDuration && !IsDead)
        {
            if (agent.enabled) agent.Move(push * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void TryPlayHitAnimation()
    {
        if (animator == null) return;
        if (Time.time - _lastHitTime < hitAnimCooldown) return;
        _lastHitTime = Time.time;
        _currentHitState = "hit" + Random.Range(0, _hitStateCount);
        StartCoroutine(PlayHitCoroutine());
    }

    IEnumerator PlayHitCoroutine()
    {
        animator.applyRootMotion = false;
        animator.ResetTrigger("Hit");
        animator.CrossFadeInFixedTime(_currentHitState, 0.15f);
        StartCoroutine(PauseAgentDuringHit());
        yield break;
    }

    IEnumerator PauseAgentDuringHit()
    {
        IsHit = true;
        Vector3 posAtHit = transform.position;
        if (agent != null) agent.enabled = false;

        yield return new WaitForSeconds(0.2f);

        float safety = 0f;
        while (!IsDead && safety < 3f)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.normalizedTime >= 1f) break;
            safety += Time.deltaTime;
            yield return null;
        }

        if (!IsDead && animator != null)
        {
            animator.applyRootMotion = false;
            animator.ResetTrigger("Hit");
            animator.CrossFadeInFixedTime("walk", 0.5f);
        }

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(posAtHit);
        }
        IsHit = false;
    }

    HitReaction FindNearestBoneHitReaction(Vector3 point)
    {
        HitReaction nearest = null;
        float minDist = float.MaxValue;
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;
            HitReaction hr = rb.GetComponent<HitReaction>();
            if (hr == null) continue;
            float d = Vector3.Distance(rb.transform.position, point);
            if (d < minDist) { minDist = d; nearest = hr; }
        }
        return nearest;
    }

    void DetachBoneDecals()
    {
        var toDetach = new List<Transform>();
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;
            foreach (Transform child in rb.transform)
            {
                // Stacca SOLO oggetti con un Renderer (decal visivi, sangue, ecc.)
                // NON staccare le ossa pure (solo Transform) — sono usate dal
                // SkinnedMeshRenderer per deformare la mesh. Staccarle congela
                // visivamente torso, mani e piedi anche se la fisica funziona.
                if (child.GetComponent<Renderer>() != null)
                    toDetach.Add(child);
            }
        }
        foreach (Transform t in toDetach)
            t.SetParent(null);
    }

    // Riconfigura i connectedAnchor di tutti i CharacterJoint sulla posa ATTUALE.
    // NOTA: autoConfigureConnectedAnchor=true NON ricalcola a runtime (solo all'Awake).
    // Calcolo manuale: porta l'anchor dell'osso in world space, poi nel local space del
    // connectedBody → errore di posizione vincolo = 0 → nessuna forza correttiva esplosiva.
    void FixJointAnchors()
    {
        foreach (CharacterJoint joint in GetComponentsInChildren<CharacterJoint>())
        {
            if (joint.connectedBody == null) continue;
            joint.autoConfigureConnectedAnchor = false;
            // Posizione world dell'anchor sull'osso corrente
            Vector3 worldAnchor = joint.transform.TransformPoint(joint.anchor);
            // Stessa posizione nel local space del connectedBody → errore zero
            joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(worldAnchor);

            // Verifica diagnostica (deve stampare 0 dopo la correzione)
            Vector3 a1 = joint.transform.TransformPoint(joint.anchor);
            Vector3 a2 = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
            float dist = Vector3.Distance(a1, a2);
            if (dist > 0.005f)
                Debug.LogWarning($"[RD-AnchorErr] {joint.name}→{joint.connectedBody.name}: errore residuo {dist:F4}m (BUG)");
        }
    }

    void Die()
    {
        IsDead = true;
        Debug.Log("[EnemyHealth] Die() chiamato — zombi morto.");

        // Cattura velocità prima di disabilitare l'agente — serve per il momentum del ragdoll
        _deathVelocity = agent != null ? agent.velocity : Vector3.zero;

        if (agent != null) agent.enabled = false;
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null) enemyAI.enabled = false;

        // La pozza di sangue a terra viene creata gradualmente dal cadavere che
        // continua a sanguinare (GroundDeathRoutine), non più un decal piazzato qui.

        // Forza tutti i SkinnedMeshRenderer ad aggiornarsi sempre.
        // Con updateWhenOffscreen=false (default Unity), quando le ossa del busto
        // escono dai bounds cached della mesh, Unity smette di deformare la mesh
        // → il busto appare visivamente "congelato in aria" anche se la fisica
        // funziona correttamente (Rigidbody cade ma la mesh non si aggiorna).
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;

        foreach (HitReaction hr in GetComponentsInChildren<HitReaction>())
            hr.ResetReaction();

        StartCoroutine(ActivateRagdollNextFrame());
    }

    // Pozza di morte: quad piatti di geometria reale (stabili, non ondeggiano con
    // la camera), che restano a terra 60 secondi. Niente spray esplosivo KriptoFX.
    void SpawnDeathBloodPool()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit, 2f,
                ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
        if (groundHit.collider.GetComponentInParent<EnemyHealth>() != null) return;

        Material poolMat = GetPoolMaterial();
        if (poolMat == null) return;

        // Un solo quad pulito, ben staccato dal pavimento per evitare z-fighting
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        quad.name = "DeathBloodPool";

        quad.transform.position = groundHit.point + groundHit.normal * 0.03f;
        quad.transform.rotation = Quaternion.FromToRotation(Vector3.forward, groundHit.normal)
                                * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float size = Random.Range(0.55f, 0.8f);
        quad.transform.localScale = new Vector3(size, size, 1f);

        var rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial = poolMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        Destroy(quad, 60f);
    }

    // Materiale pozza condiviso (creato una volta, riusato da tutti i nemici)
    private static Material _poolMat;
    static Material GetPoolMaterial()
    {
        if (_poolMat != null) return _poolMat;

        Texture2D tex = BuildPoolTexture(256);
        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Unlit/Transparent");
        if (s == null) return null;
        _poolMat = new Material(s);
        _poolMat.mainTexture = tex;
        if (_poolMat.HasProperty("_MainTex")) _poolMat.SetTexture("_MainTex", tex);
        if (_poolMat.HasProperty("_BaseMap")) _poolMat.SetTexture("_BaseMap", tex);
        _poolMat.renderQueue = 3000;
        return _poolMat;
    }

    static Texture2D BuildPoolTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x - cx) / (size * 0.5f);
            float ny = (y - cy) / (size * 0.5f);
            float d  = Mathf.Sqrt(nx * nx + ny * ny);
            float a  = Mathf.Atan2(ny, nx);
            float edge = 0.82f + 0.10f * Mathf.Sin(a * 6f) + 0.06f * Mathf.Sin(a * 11f + 1f);
            Color c;
            if (d < edge * 0.7f)
                c = new Color(0.22f, 0.01f, 0.01f, 0.95f);
            else if (d < edge)
            {
                float t = (d - edge * 0.7f) / (edge * 0.3f);
                c = new Color(0.26f, 0.02f, 0.02f, Mathf.Lerp(0.95f, 0f, t * t));
            }
            else c = Color.clear;
            px[y * size + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // [3] Scia di sangue: gocce piccole a terra mentre lo zombi cammina ferito
    IEnumerator BloodTrailCoroutine()
    {
        if (deathBloodPrefabs == null || deathBloodPrefabs.Length == 0) yield break;

        while (!IsDead)
        {
            yield return new WaitForSeconds(Random.Range(0.4f, 0.9f));
            if (IsDead) break;

            Vector3 origin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f,
                    ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<EnemyHealth>() == null)
                {
                    var prefab = deathBloodPrefabs[Random.Range(0, deathBloodPrefabs.Length)];
                    if (prefab != null)
                    {
                        GameObject drop = Instantiate(prefab, hit.point, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                        drop.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
                        var s = drop.GetComponent<BFX_BloodSettings>();
                        if (s != null) { s.AutomaticGroundHeightDetection = true; s.AnimationSpeed = 1.5f; }
                        Destroy(drop, 20f);
                    }
                }
            }
        }
    }

    IEnumerator ActivateRagdollNextFrame()
    {
        // Fase 1: lascia girare l'animazione di morte per deathBlendDuration secondi
        // Il personaggio mantiene la forma animata mentre inizia a cedere
        if (animator != null) animator.applyRootMotion = false;
        yield return new WaitForSeconds(deathBlendDuration);

        // Fase 2: disabilita l'animator e congela la posa corrente
        Debug.Log($"[RD-Anim] animator={(animator != null ? animator.gameObject.name : "NULL")}");
        if (animator != null) animator.enabled = false;

        // Sincronizza i transform col mondo fisico
        Physics.SyncTransforms();

        // Ricalcola gli anchor dei joint sulla posa corrente
        FixJointAnchors();

        DetachBoneDecals();
        SetRagdollActive(true);

        // Fase 3: applica il momentum — aspetta un FixedUpdate per PhysX
        yield return new WaitForFixedUpdate();

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject || rb.isKinematic) continue;
            rb.WakeUp();

            // Applica velocità di morte (momentum della camminata) + leggera spinta verso il basso
            Vector3 momentum = _deathVelocity * 0.6f + Vector3.down * 2f;
            rb.AddForce(momentum, ForceMode.VelocityChange);
        }

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        StartCoroutine(GroundDeathRoutine());
    }

    // Il cadavere resta a terra e dopo 60 secondi scompare.
    // (La pozza di sangue che si allargava è stata rimossa per ora — verrà
    //  reimplementata meglio in seguito.)
    IEnumerator GroundDeathRoutine()
    {
        yield return new WaitForSeconds(60f);
        Destroy(gameObject);
    }
}
