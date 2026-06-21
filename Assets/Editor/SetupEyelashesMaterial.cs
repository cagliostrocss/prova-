using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SetupEyelashesMaterial
{
    [MenuItem("Tools/Setup Eyelashes Material")]
    static void Setup()
    {
        FixCharacter("Cittadino1", "Assets/models/Cittadino1/");
        FixCharacter("zombif1",    "Assets/models/zombif1/");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[EyelashFix] Completato per entrambi i personaggi. Salva con Ctrl+S.");
    }

    static void FixCharacter(string goName, string texFolder)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogWarning($"[EyelashFix] {goName} non trovato in scena."); return; }

        // Trova la diffuse texture principale del personaggio
        string[] diffuseGuids = AssetDatabase.FindAssets("t:Texture2D", new[]{ texFolder });
        Texture2D diffuseTex = null;
        Texture2D normalTex  = null;
        foreach (var guid in diffuseGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("packed0_diffuse") || path.Contains("packed0_Diffuse"))
            {
                diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                // Abilita alpha come trasparenza sulla diffuse
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && !imp.alphaIsTransparency)
                {
                    imp.alphaIsTransparency = true;
                    imp.SaveAndReimport();
                }
            }
            if (path.Contains("packed0_normal") || path.Contains("packed0_Normal"))
                normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Crea materiale Eyelashes (Alpha Clip, double-sided)
        string eyelashMatPath = texFolder + goName + "_Eyelashes_Mat.mat";
        var eyelashMat = AssetDatabase.LoadAssetAtPath<Material>(eyelashMatPath);
        if (eyelashMat == null)
        {
            eyelashMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            eyelashMat.name = goName + "_Eyelashes_Mat";
            AssetDatabase.CreateAsset(eyelashMat, eyelashMatPath);
        }
        SetAlphaClip(eyelashMat, diffuseTex, normalTex, 0.3f);

        // Crea materiale Eyes (opaco, molto liscio e riflettente)
        string eyeMatPath = texFolder + goName + "_Eyes_Mat.mat";
        var eyeMat = AssetDatabase.LoadAssetAtPath<Material>(eyeMatPath);
        if (eyeMat == null)
        {
            eyeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            eyeMat.name = goName + "_Eyes_Mat";
            AssetDatabase.CreateAsset(eyeMat, eyeMatPath);
        }
        SetEyeMaterial(eyeMat, diffuseTex, normalTex);

        // Assegna materiali ai mesh giusti
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string nameLower = smr.name.ToLower();
            if (nameLower.Contains("eyelash") || nameLower.Contains("hair") || nameLower.Contains("lash"))
            {
                smr.sharedMaterial = eyelashMat;
                EditorUtility.SetDirty(smr);
                Debug.Log($"[EyelashFix] {goName}/{smr.name} → Eyelashes mat (alpha clip)");
            }
            else if (nameLower.Contains("eye") && !nameLower.Contains("eyelash") && !nameLower.Contains("lash"))
            {
                smr.sharedMaterial = eyeMat;
                EditorUtility.SetDirty(smr);
                Debug.Log($"[EyelashFix] {goName}/{smr.name} → Eyes mat (smooth/reflective)");
            }
        }
    }

    static void SetAlphaClip(Material mat, Texture2D diffuse, Texture2D normal, float cutoff)
    {
        mat.SetFloat("_Surface",   0f);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff",    cutoff);
        mat.SetFloat("_Cull",      0f);   // double sided
        mat.SetFloat("_Smoothness", 0.1f);
        mat.SetFloat("_Metallic",  0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 2450;
        if (diffuse) { mat.SetTexture("_BaseMap", diffuse); mat.SetTexture("_MainTex", diffuse); }
        if (normal)  { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
        EditorUtility.SetDirty(mat);
    }

    static void SetEyeMaterial(Material mat, Texture2D diffuse, Texture2D normal)
    {
        mat.SetFloat("_Surface",    0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Smoothness", 0.9f);   // occhi bagnati = molto lisci
        mat.SetFloat("_Metallic",   0f);
        mat.SetFloat("_Cull",       2f);
        mat.SetFloat("_EnvironmentReflections", 1f);
        if (diffuse) { mat.SetTexture("_BaseMap", diffuse); mat.SetTexture("_MainTex", diffuse); }
        if (normal)  { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
        EditorUtility.SetDirty(mat);
    }
}
