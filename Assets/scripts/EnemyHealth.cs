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

    public bool IsDead { get; private set; }
    private bool _trailStarted = false;

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
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
                col.enabled = !active;
            else
                col.enabled = active;
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        currentHealth -= damage;
        Debug.Log($"[EnemyHealth] TakeDamage {damage} → HP rimanenti: {currentHealth}");
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

        // [3] Avvia scia di sangue al primo colpo ricevuto
        if (!_trailStarted) { _trailStarted = true; StartCoroutine(BloodTrailCoroutine()); }

        if (currentHealth <= 0) Die();
    }

    public void TakeDamage(float damage, Vector3 hitDirection, Collider hitCollider)
    {
        TakeDamage(damage, hitDirection, hitCollider.transform.position, hitCollider);
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
        if (agent != null) agent.enabled = false;
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null) enemyAI.enabled = false;

        // Pool di sangue a terra dalla morte (KriptoFX)
        SpawnDeathBloodPool();

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

    void SpawnDeathBloodPool()
    {
        if (deathBloodPrefabs == null || deathBloodPrefabs.Length == 0) return;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit, 2f,
                ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
        if (groundHit.collider.GetComponentInParent<EnemyHealth>() != null) return;

        float baseAngle = Mathf.Atan2(groundHit.normal.x, groundHit.normal.z) * Mathf.Rad2Deg + 180f;

        // [4] Spawn 3 pozze sovrapposte per una macchia di morte più grande e drammatica
        int poolCount = 3;
        for (int i = 0; i < poolCount; i++)
        {
            var prefab = deathBloodPrefabs[Random.Range(0, deathBloodPrefabs.Length)];
            if (prefab == null) continue;

            Vector3 offset = new Vector3(Random.Range(-0.15f, 0.15f), 0, Random.Range(-0.15f, 0.15f));
            float angle    = baseAngle + Random.Range(-30f, 30f);
            GameObject go  = Instantiate(prefab, groundHit.point + offset, Quaternion.Euler(0, angle + 90f, 0));
            go.transform.localScale = Vector3.one * Random.Range(1.5f, 2.8f);

            var settings = go.GetComponent<BFX_BloodSettings>();
            if (settings != null)
            {
                settings.AutomaticGroundHeightDetection = true;
                settings.AnimationSpeed = Random.Range(0.5f, 0.85f);
            }

            Destroy(go, 30f);
        }
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
        yield return null;

        // --- DIAGNOSTICA PRE-ATTIVAZIONE ---
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;
            if (rb.name == "mixamorig:Hips" || rb.name == "mixamorig:Spine")
                Debug.Log($"[RD-PRE] {rb.name}: pos={rb.transform.position:F3} kinematic={rb.isKinematic}");
        }

        // LOG: conferma se l'Animator è stato trovato e viene effettivamente spento
        Debug.Log($"[RD-Anim] animator={(animator != null ? animator.gameObject.name + " obj=" + animator.gameObject.GetInstanceID() : "NULL")}");
        if (animator != null) animator.enabled = false;
        Debug.Log($"[RD-Anim] dopo disable: enabled={animator?.enabled}");

        // Sincronizza i transform col mondo fisico (necessario dopo aggiornamenti Animator)
        Physics.SyncTransforms();

        // Ricalcola gli anchor dei joint sulla posa corrente
        FixJointAnchors();

        DetachBoneDecals();
        SetRagdollActive(true);

        // Gore Simulator: segnala la morte DOPO che il ragdoll BNG è attivo.
        // Non chiamiamo ExecuteRagdoll() perché conflitterebbe con il nostro ragdoll.
        // Usiamo NotifyDeath() se esiste, altrimenti ignoriamo.

        // --- DIAGNOSTICA POST-ATTIVAZIONE ---
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;
            if (rb.name == "mixamorig:Hips" || rb.name == "mixamorig:Spine")
                Debug.Log($"[RD-POST] {rb.name}: kinematic={rb.isKinematic} gravity={rb.useGravity} constraints={rb.constraints}");
        }

        // IMPORTANTE: AddForce chiamata nello stesso frame di isKinematic=false
        // viene ignorata da PhysX (il body non è ancora reinizializzato come dinamico).
        // Aspettiamo un FixedUpdate → PhysX ha processato il cambio di stato →
        // la forza viene applicata correttamente.
        yield return new WaitForFixedUpdate();

        // Sveglia tutte le ossa ed applica una velocità iniziale verso il basso.
        // Senza questo, le gambe (pendolo) arrivano a terra in ~0.1s mentre il busto
        // cade in caduta libera (~0.5s) — visivamente sembra che il busto galleggi.
        // Con -3 m/s tutto il corpo collassa insieme entro ~0.3s.
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject || rb.isKinematic) continue;
            rb.WakeUp();
            rb.AddForce(Vector3.down * 3f, ForceMode.VelocityChange);
        }

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Destroy(gameObject, 5f);
    }
}
