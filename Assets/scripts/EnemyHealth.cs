using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;

    public bool IsDead { get; private set; }

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
        if (currentHealth <= 0) Die();
    }

    public void TakeDamage(float damage, Vector3 hitDirection, Vector3 hitPoint, Collider hitCollider)
    {
        if (IsDead) return;
        currentHealth -= damage;

        HitReaction hitReaction = hitCollider.GetComponent<HitReaction>();
        if (hitReaction == null)
            hitReaction = FindNearestBoneHitReaction(hitPoint);
        hitReaction?.ApplyHitForce(hitDirection, 150f);

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
        if (agent != null) agent.enabled = false;
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null) enemyAI.enabled = false;

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

        // Posizione dopo la fisica (prima di LateUpdate)
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;
            if (rb.name == "mixamorig:Hips" || rb.name == "mixamorig:Spine")
                Debug.Log($"[RD-3FX] {rb.name}: pos={rb.transform.position:F3} vel={rb.velocity:F3} |vel|={rb.velocity.magnitude:F3}");
        }

        // Traccia la posizione di Hips e Spine ogni 0.5s per 6 secondi
        // per vedere se cadono fino a terra o rimangono in aria
        for (int i = 0; i < 12; i++)
        {
            yield return new WaitForSeconds(0.5f);
            foreach (Rigidbody rb in ragdollRigidbodies)
            {
                if (rb.gameObject == gameObject) continue;
                if (rb.name == "mixamorig:Hips" || rb.name == "mixamorig:Spine")
                    Debug.Log($"[RD-T{i * 0.5f:F1}s] {rb.name}: Y={rb.transform.position.y:F3} vel.y={rb.velocity.y:F3} kinematic={rb.isKinematic}");
            }
        }

        Destroy(gameObject, 2f);
    }
}
