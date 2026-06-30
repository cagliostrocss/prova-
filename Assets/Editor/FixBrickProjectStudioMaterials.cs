using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class FixBrickProjectStudioMaterials
{
    [MenuItem("Tools/Fix Brick Project Studio Textures")]
    static void Fix()
    {
        // 1. Raccoglie tutte le texture del pacchetto indicizzate per nome (senza estensione)
        var textureMap = new Dictionary<string, Texture2D>();
        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Brick Project Studio" });
        foreach (string guid in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;
            string key = tex.name.ToLower();
            if (!textureMap.ContainsKey(key))
                textureMap[key] = tex;
        }

        Debug.Log($"[BPS Fix] Trovate {textureMap.Count} texture.");

        // 2. Itera tutti i materiali del pacchetto
        int matFixed = 0;
        int texAssigned = 0;
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Brick Project Studio" });

        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Assicura che usi URP Lit
            if (mat.shader == null || !mat.shader.name.Contains("Universal Render Pipeline"))
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");

            bool changed = false;

            // Prova a trasferire _MainTex → _BaseMap se _BaseMap è vuoto
            if (mat.HasProperty("_MainTex") && mat.HasProperty("_BaseMap"))
            {
                var mainTex = mat.GetTexture("_MainTex");
                var baseTex = mat.GetTexture("_BaseMap");
                if (mainTex != null && baseTex == null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    changed = true;
                    texAssigned++;
                }
            }

            // Se _BaseMap ancora vuoto, cerca per nome del materiale
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null)
            {
                string matNameLower = mat.name.ToLower();

                // Strategie di ricerca per nome
                Texture2D found = FindTexture(textureMap, matNameLower, "_a") ??
                                  FindTexture(textureMap, matNameLower, "_d") ??
                                  FindTexture(textureMap, matNameLower, "_albedo") ??
                                  FindTexture(textureMap, matNameLower, "_diffuse") ??
                                  FindTexture(textureMap, matNameLower, "") ;

                if (found != null)
                {
                    mat.SetTexture("_BaseMap", found);
                    mat.SetTexture("_MainTex", found);
                    changed = true;
                    texAssigned++;
                }
            }

            // Cerca e assegna Normal Map
            if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") == null)
            {
                string matNameLower = mat.name.ToLower();
                Texture2D normalTex = FindTexture(textureMap, matNameLower, "_n") ??
                                      FindTexture(textureMap, matNameLower, "_normal") ??
                                      FindTexture(textureMap, matNameLower, "_nor");
                if (normalTex != null)
                {
                    // Imposta come Normal Map
                    string texPath = AssetDatabase.GetAssetPath(normalTex);
                    var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.EnableKeyword("_NORMALMAP");
                    changed = true;
                }
            }

            // Cerca Metallic/Smoothness map
            if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") == null)
            {
                string matNameLower = mat.name.ToLower();
                Texture2D metalTex = FindTexture(textureMap, matNameLower, "_m") ??
                                     FindTexture(textureMap, matNameLower, "_metallic");
                if (metalTex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metalTex);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(mat);
                matFixed++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BPS Fix] Completato: {matFixed} materiali aggiornati, {texAssigned} texture assegnate.");
    }

    // Cerca texture il cui nome contiene il nome del materiale + suffisso
    static Texture2D FindTexture(Dictionary<string, Texture2D> map, string matName, string suffix)
    {
        // Rimuovi spazi e caratteri speciali per il matching
        string clean = matName.Replace(" ", "_").Replace("-", "_");

        // Prova corrispondenza esatta con suffisso
        string key = clean + suffix;
        if (map.TryGetValue(key, out var tex)) return tex;

        // Prova parti del nome (es. "adler dark wood" → cerca "adlerwood", "adler_wood")
        string[] words = clean.Split('_');
        if (words.Length > 1)
        {
            // Prova prime due parole
            string partial = words[0] + "_" + words[1] + suffix;
            if (map.TryGetValue(partial, out tex)) return tex;

            // Prova solo prima parola
            partial = words[0] + suffix;
            if (map.TryGetValue(partial, out tex)) return tex;
        }

        // Ricerca parziale: la texture contiene il nome del materiale
        if (!string.IsNullOrEmpty(suffix))
        {
            foreach (var kv in map)
                if (kv.Key.Contains(clean) && kv.Key.EndsWith(suffix))
                    return kv.Value;
        }

        return null;
    }
}
