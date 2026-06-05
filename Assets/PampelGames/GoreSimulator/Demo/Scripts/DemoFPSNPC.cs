using System;
using UnityEngine;

namespace PampelGames.GoreSimulator.Demo
{
    public class DemoFPSNPC : MonoBehaviour
    {
        public GoreSimulator goreSimulator;

        private void Awake()
        {
            goreSimulator.OnDeath += () =>
            {
                goreSimulator.ExecuteRagdoll();
            };
        }
    }
}
