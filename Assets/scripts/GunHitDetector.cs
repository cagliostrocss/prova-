using UnityEngine;

// Aggiungere sull'oggetto PistolNEW.
// Nell'evento OnShoot: rimuovere EnemyHealth.TakeDamage(25), aggiungere GunHitDetector.OnGunFired()
public class GunHitDetector : MonoBehaviour
{
    [Tooltip("Punto bocca della canna. Se vuoto, cerca automaticamente tra i figli.")]
    public Transform muzzlePoint;
    public float damagePerShot = 25f;
    public float maxRange = 150f;

    void Start()
    {
        if (muzzlePoint != null) return;
        foreach (string n in new[] { "Muzzle", "MuzzleFlash", "MuzzlePoint", "Barrel" })
        {
            Transform found = transform.Find(n);
            if (found != null) { muzzlePoint = found; return; }
        }
        muzzlePoint = transform;
        Debug.LogWarning("[GunHitDetector] Nessun muzzle trovato, uso il transform dell'arma.");
    }

    public void OnGunFired()
    {
        // RaycastAll per non fermarci sui proiettili fisici in volo (slow-motion)
        RaycastHit[] hits = Physics.RaycastAll(
            muzzlePoint.position, muzzlePoint.forward, maxRange,
            ~(1 << 2), QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Salta i proiettili BNG in volo — bloccano il raycast in slow-motion
            if (hit.collider.GetComponentInParent<BNG.Projectile>() != null) continue;
            if (hit.collider.GetComponentInParent<BNG.Bullet>() != null) continue;

            Debug.Log("[GunHitDetector] Colpito: " + hit.collider.name
                      + " layer=" + LayerMask.LayerToName(hit.collider.gameObject.layer));

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                Debug.Log("[GunHitDetector] Osso: " + hit.collider.gameObject.name);
                enemy.TakeDamage(damagePerShot, muzzlePoint.forward, hit.point, hit.collider);
            }
            return; // primo oggetto solido trovato: fermati qui
        }
    }
}
