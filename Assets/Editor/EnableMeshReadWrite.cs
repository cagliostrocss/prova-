using UnityEditor;
using UnityEngine;

public class EnableMeshReadWrite
{
    [MenuItem("Tools/Enable Read-Write on Cittadino1")]
    static void Enable()
    {
        string path = "Assets/models/Cittadino1/Cittadino1.fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) { Debug.LogError("Importer non trovato: " + path); return; }
        importer.isReadable = true;
        importer.SaveAndReimport();
        Debug.Log("Read/Write abilitato e reimportato: " + path);
    }
}
