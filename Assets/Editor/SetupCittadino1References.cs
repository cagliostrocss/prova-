using UnityEditor;
using UnityEngine;

public class SetupCittadino1References
{
    [MenuItem("Tools/Setup Cittadino1 References")]
    static void Setup()
    {
        var go = GameObject.Find("Cittadino1");
        if (go == null) { Debug.LogError("Cittadino1 non trovato."); return; }

        // --- Animator Controller ---
        var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/animators/cittadino_animator.controller");
        if (animController == null)
            { Debug.LogError("cittadino_animator.controller non trovato."); }
        else
        {
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = animController;
                EditorUtility.SetDirty(anim);
                Debug.Log("[Setup] Animator Controller assegnato: " + animController.name);
            }
            else Debug.LogError("Animator non trovato su Cittadino1.");
        }

        // --- Blood Prefabs ---
        var health = go.GetComponent<EnemyHealth>();
        if (health == null) { Debug.LogError("EnemyHealth non trovato."); return; }

        string[] bloodPaths = new string[]
        {
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood1.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood2.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood3.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood4.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood5.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood6.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood7.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood8.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood9.prefab",
            "Assets/KriptoFX/VolumetricBloodFX/Prefabs/Blood10.prefab",
        };

        var prefabs = new GameObject[bloodPaths.Length];
        for (int i = 0; i < bloodPaths.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(bloodPaths[i]);
            if (prefabs[i] == null) Debug.LogWarning("[Setup] Prefab non trovato: " + bloodPaths[i]);
        }
        health.deathBloodPrefabs = prefabs;
        EditorUtility.SetDirty(health);
        Debug.Log("[Setup] Blood prefabs assegnati: " + prefabs.Length);

        // Salva
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[Setup] Completato. Salva la scena con Ctrl+S.");
    }
}
