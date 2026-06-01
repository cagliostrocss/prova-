using UnityEngine;

public class HitReaction : MonoBehaviour
{
    public float maxAngle = 25f;
    public float recoverySpeed = 8f;

    private EnemyHealth enemyHealth;
    private Rigidbody boneRigidbody;
    private Quaternion reactionRotation = Quaternion.identity;
    [HideInInspector] public float reactionStrength;

    // Delta applicato sull'osso nell'ULTIMO LateUpdate.
    // Necessario per rimuoverlo esattamente in ResetReaction()
    // prima che il ragdoll si attivi.
    private Quaternion lastAppliedDelta = Quaternion.identity;

    void Start()
    {
        enemyHealth = GetComponentInParent<EnemyHealth>();
        boneRigidbody = GetComponent<Rigidbody>();
    }

    public void ApplyHitForce(Vector3 worldHitDirection, float force)
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;

        Vector3 localDir = transform.InverseTransformDirection(worldHitDirection);
        Vector3 rotAxis = Vector3.Cross(Vector3.up, localDir.normalized);
        if (rotAxis.sqrMagnitude < 0.001f) rotAxis = Vector3.right;

        float angle = Mathf.Clamp(force * 0.12f, 8f, maxAngle);
        reactionRotation = Quaternion.AngleAxis(angle, rotAxis);
        reactionStrength = 1f;
    }

    public void ResetReaction()
    {
        reactionStrength = 0f;
        reactionRotation = Quaternion.identity;

        // RIMUOVE fisicamente il delta dall'osso prima del ragdoll.
        // Senza questo: l'osso ha ancora la rotazione residua dell'ultimo LateUpdate.
        // Il CharacterJoint interpreta quell'offset come violazione dei limiti
        // e genera forze correttive esplosive → effetto gomma.
        if (lastAppliedDelta != Quaternion.identity)
        {
            transform.localRotation = transform.localRotation * Quaternion.Inverse(lastAppliedDelta);
            lastAppliedDelta = Quaternion.identity;
        }
    }

    void LateUpdate()
    {
        // Quando reactionStrength scende a zero, azzera il delta tracciato
        if (reactionStrength < 0.01f)
        {
            lastAppliedDelta = Quaternion.identity;
            return;
        }
        if (enemyHealth != null && enemyHealth.IsDead) return;
        if (boneRigidbody != null && !boneRigidbody.isKinematic) return;

        reactionStrength = Mathf.MoveTowards(reactionStrength, 0f, Time.deltaTime * recoverySpeed);

        // L'Animator ha già aggiornato l'osso a animation_pose per questo frame.
        // Salviamo il delta che stiamo per aggiungere: ci serve per annullarlo in ResetReaction.
        lastAppliedDelta = Quaternion.Slerp(Quaternion.identity, reactionRotation, reactionStrength);
        transform.localRotation *= lastAppliedDelta;
    }
}
