using UnityEditor;
using UnityEngine;
using PampelGames.GoreSimulator;
using PampelGames.GoreSimulator.Editor;

public class InitCittadino1Gore
{
    [MenuItem("Tools/Setup Gore Cittadino1")]
    static void Setup()
    {
        var go = GameObject.Find("Cittadino1");
        if (go == null) { Debug.LogError("Cittadino1 non trovato nella scena."); return; }

        var gore = go.GetComponent<GoreSimulator>();
        if (gore == null) { Debug.LogError("GoreSimulator non trovato su Cittadino1."); return; }

        // Usa il mesh con la maggiore copertura ossa — su Fuse/Mixamo è spesso "Tops" o "Body"
        // Cerca in ordine: Tops, Body, primo SMR disponibile
        var allSmr = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer bestSmr = null;
        foreach (var s in allSmr)
            if (s.name == "Tops") { bestSmr = s; break; }
        if (bestSmr == null)
            foreach (var s in allSmr)
                if (s.name == "Body") { bestSmr = s; break; }
        if (bestSmr == null && allSmr.Length > 0) bestSmr = allSmr[0];
        if (bestSmr == null) { Debug.LogError("Nessun SkinnedMeshRenderer trovato."); return; }
        gore.smr = bestSmr;
        Debug.Log("SMR impostato: " + gore.smr.name);

        // Trova Hips
        var hips = go.transform.Find("mixamorig:Hips");
        if (hips == null) { Debug.LogError("mixamorig:Hips non trovato."); return; }

        // Imposta center
        gore.center = hips;

        // CRITICO: bonesListClasses[0].bone deve essere Hips
        // InitializeBoneSetup() controlla proprio questo prima di popolare le ossa
        if (gore.bonesListClasses == null || gore.bonesListClasses.Count == 0)
            gore.bonesListClasses = new System.Collections.Generic.List<BonesListClass> { new BonesListClass() };

        gore.bonesListClasses[0].bone = hips;
        Debug.Log("bonesListClasses[0].bone impostato a: " + hips.name);

        EditorUtility.SetDirty(gore);

        // Auto Setup Humanoid: popola tutta la lista ossa dai bones del SMR
        MeshEditorUtility.AutoSetupHumanoid(gore);

        EditorUtility.SetDirty(gore);
        Debug.Log("[InitCittadino1Gore] AutoSetup completato. bonesListClasses: " + gore.bonesListClasses.Count);
        Debug.Log("[InitCittadino1Gore] Ora clicca Initialize nel GoreSimulator Inspector di Cittadino1.");
    }
}
