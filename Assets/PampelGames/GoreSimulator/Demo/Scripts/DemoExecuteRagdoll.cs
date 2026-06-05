using System;
using UnityEngine;

namespace PampelGames.GoreSimulator.Demo
{
    /// <summary>
    ///     A simple script to demonstrate ragdoll and reset of a character.
    /// </summary>
    public class DemoExecuteRagdoll : MonoBehaviour
    {
        
        public KeyCode RagdollKey = KeyCode.R;
        public KeyCode ResetKey = KeyCode.Space;
        private void Update()
        {
            if (Input.GetKeyDown(RagdollKey))
            {
                 FindFirstObjectByType<GoreSimulator>().ExecuteRagdoll();
            }
            
            if (Input.GetKeyDown(ResetKey))
            {
                FindFirstObjectByType<GoreSimulator>().ResetCharacter();
            }
        }
    }
}
