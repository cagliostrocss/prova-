using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering.Universal;

public class UpgradeMaterialsToURP
{
    [MenuItem("Tools/Upgrade Brick Project Studio Materials to URP")]
    static void Upgrade()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Brick Project Studio" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            if (mat.shader == null) continue;
            string shaderName = mat.shader.name;
            if (shaderName.Contains("Standard") || shaderName.Contains("Legacy") || shaderName.Contains("Diffuse"))
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                EditorUtility.SetDirty(mat);
                count++;
                Debug.Log($"Upgraded: {mat.name}");
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[URP Upgrade] Convertiti {count} materiali.");
    }
}
