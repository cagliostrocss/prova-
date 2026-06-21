using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SetupPostProcessing
{
    [MenuItem("Tools/Setup Post Processing")]
    static void Setup()
    {
        System.IO.Directory.CreateDirectory("Assets/Settings");
        string profilePath = "Assets/Settings/PostProcessingProfile.asset";

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.5f);
        bloom.scatter.Override(0.7f);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.2f);
        color.contrast.Override(15f);
        color.saturation.Override(10f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.25f);
        vignette.smoothness.Override(0.4f);

        AssetDatabase.CreateAsset(profile, profilePath);
        AssetDatabase.SaveAssets();

        var existing = GameObject.Find("Global Post Processing");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("Global Post Processing");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.profile = profile;

        foreach (var cam in Object.FindObjectsOfType<Camera>())
        {
            var urpData = cam.GetUniversalAdditionalCameraData();
            if (urpData != null)
            {
                urpData.renderPostProcessing = true;
                EditorUtility.SetDirty(cam.gameObject);
                Debug.Log("[PP] Abilitato su: " + cam.name);
            }
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[PP] Global Volume creato. Salva con Ctrl+S.");
    }
}
