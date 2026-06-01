using UnityEngine;

public class EnemyDamageReceiver : MonoBehaviour
{
    private EnemyHealth enemyHealth;

    void Start()
    {
        enemyHealth = GetComponentInParent<EnemyHealth>();
    }

    public void ReceiveDamage(float damage, Vector3 hitDirection, Collider hitCollider)
    {
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage, hitDirection, hitCollider);
        }
    }
}