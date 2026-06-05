using System;
using PampelGames.Shared.Tools;
using UnityEngine;

namespace PampelGames.GoreSimulator.Demo
{
    public class DemoFPSGrenade : MonoBehaviour
    {

        public float explosionTime = 2f;
        public float explosionRadius = 5f;
        public float explosionForce = 1000f;
        public GameObject particlePrefab;

        private void OnEnable()
        {
            // Convenient alternative to a coroutine
            PGScheduler.ScheduleTime(this, explosionTime, () =>
            {
                // Utilizing the internal pool.
                // Particles are despawned via the PGPoolableParticles component, attached to the Explosion prefab
                var _particles = PGPool.Get(particlePrefab);
                _particles.transform.position = transform.position;
                var particles = _particles.GetComponent<ParticleSystem>();
                particles.Play();
                ExplodeIGoreObjects();
                // Returning the grenade to the pool.
                PGPool.Release(gameObject);
            });
        }
        
        private void ExplodeIGoreObjects()
        {
            // Exploding all IGoreObjects in range
            var results = new Collider[100];
            var size = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, results);

            for (int i = 0; i < size; i++)
            {
                if (results[i].TryGetComponent<IGoreObject>(out var goreObject))
                {
                    goreObject.ExecuteExplosion(transform.position, explosionForce);
                }
            }
            
        }
    }
}
