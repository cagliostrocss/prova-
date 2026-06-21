using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SetupGraphicsQuality
{
    [MenuItem("Tools/Setup Graphics Quality")]
    static void Setup()
    {
        FixGlossScale();
        FixLighting();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[GFX] Completato. Salva con Ctrl+S.");
    }

    static void FixGlossScale()
    {
        string[] matPaths = new[]
        {
            "Assets/models/Cittadino1/Cittadino1_Mat.mat",
            "Assets/models/Ch30_Body.mat",
            "Assets/models/Ch30_Body1.mat",
            "Assets/models/Ch30_Body1 1.mat",
            "Assets/models/ch30_body2.mat",
        };
        foreach (var path in matPaths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            mat.SetFloat("_GlossMapScale", 1f);
            mat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(mat);
            Debug.Log("[GFX] Gloss fix: " + mat.name);
        }
    }

    static void FixLighting()
    {
        if (GameObject.Find("Fill Light") == null)
        {
            var go = new GameObject("Fill Light");
            var l  = go.AddComponent<Light>();
            l.type      = LightType.Directional;
            l.intensity = 0.4f;
            l.color     = new Color(0.7f, 0.8f, 1f);
            go.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
            l.shadows   = LightShadows.None;
            EditorUtility.SetDirty(go);
            Debug.Log("[GFX] Fill Light aggiunta.");
        }
        if (GameObject.Find("Rim Light") == null)
        {
            var go = new GameObject("Rim Light");
            var l  = go.AddComponent<Light>();
            l.type      = LightType.Directional;
            l.intensity = 0.3f;
            l.color     = new Color(1f, 0.85f, 0.6f);
            go.transform.rotation = Quaternion.Euler(20f, 340f, 0f);
            l.shadows   = LightShadows.None;
            EditorUtility.SetDirty(go);
            Debug.Log("[GFX] Rim Light aggiunta.");
        }
        RenderSettings.ambientIntensity    = 1.2f;
        RenderSettings.reflectionIntensity = 0.6f;
    }
}
