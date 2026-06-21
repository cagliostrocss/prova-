using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// v2
public class RebuildCittadino1Ragdoll
{
    [MenuItem("Tools/Rebuild Cittadino1 Ragdoll")]
    static void Rebuild()
    {
        var go = GameObject.Find("Cittadino1");
        if (go == null) { Debug.LogError("Cittadino1 non trovato."); return; }

        // Unpack prefab instance così possiamo aggiungere componenti alle ossa figlie
        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Debug.Log("[Ragdoll] Prefab unpacked.");
        }

        // 1. Rimuovi tutti i CharacterJoint, Rigidbody (tranne root), Collider esistenti sulle ossa
        RemoveExistingRagdoll(go);

        // 2. Trova ossa per nome
        var bones = new Dictionary<string, Transform>();
        foreach (var t in go.GetComponentsInChildren<Transform>())
            if (!bones.ContainsKey(t.name))
                bones[t.name] = t;

        // 3. Aggiungi Rigidbody su ogni osso con massa proporzionale
        var boneSetup = new (string bone, float mass, string parent,
            float lowTwist, float highTwist, float swing1, float swing2,
            Vector3 axis, Vector3 swingAxis)[]
        {
            // bone, massa, parent, lowTwist, highTwist, swing1, swing2, axis, swingAxis
            ("mixamorig:Hips",          3.125f, null,               0,    0,   0,   0, Vector3.right, Vector3.up),
            ("mixamorig:Spine",         0.667f, "mixamorig:Hips",  -20,  20,  10,   0, Vector3.right, Vector3.forward),
            ("mixamorig:Spine1",        0.667f, "mixamorig:Spine", -20,  20,  10,   0, Vector3.right, Vector3.forward),
            ("mixamorig:Spine2",        0.667f, "mixamorig:Spine1",-20,  20,  10,   0, Vector3.right, Vector3.forward),
            ("mixamorig:Head",          0.667f, "mixamorig:Spine2",-20,  70,  30,   0, Vector3.right, Vector3.up),
            ("mixamorig:LeftUpLeg",     1.333f, "mixamorig:Hips",  -20,  80,  20,   0, Vector3.right, Vector3.up),
            ("mixamorig:LeftLeg",       0.667f, "mixamorig:LeftUpLeg", -80, 0,  5,  0, Vector3.right, Vector3.up),
            ("mixamorig:LeftFoot",      0.667f, "mixamorig:LeftLeg", -30, 10, 10,  0, Vector3.right, Vector3.up),
            ("mixamorig:RightUpLeg",    1.333f, "mixamorig:Hips",  -20,  80,  20,   0, Vector3.right, Vector3.up),
            ("mixamorig:RightLeg",      0.667f, "mixamorig:RightUpLeg", -80, 0,  5, 0, Vector3.right, Vector3.up),
            ("mixamorig:RightFoot",     0.667f, "mixamorig:RightLeg", -30, 10, 10, 0, Vector3.right, Vector3.up),
            ("mixamorig:LeftArm",       0.667f, "mixamorig:Spine2", -25, 120, 50,  0, Vector3.right, Vector3.forward),
            ("mixamorig:LeftForeArm",   0.667f, "mixamorig:LeftArm", -70, 10, 50, 0, Vector3.right, Vector3.up),
            ("mixamorig:RightArm",      0.667f, "mixamorig:Spine2", -25, 120, 50,  0, Vector3.right, Vector3.forward),
            ("mixamorig:RightForeArm",  0.667f, "mixamorig:RightArm",-70, 10, 50, 0, Vector3.right, Vector3.up),
        };

        // Aggiungi Rigidbody
        var rigidbodies = new Dictionary<string, Rigidbody>();
        foreach (var setup in boneSetup)
        {
            if (!bones.TryGetValue(setup.bone, out Transform boneT)) continue;
            var rb = boneT.GetComponent<Rigidbody>();
            if (rb == null) rb = boneT.gameObject.AddComponent<Rigidbody>();
            rb.mass        = setup.mass;
            rb.drag        = 0f;
            rb.angularDrag = 0.05f;
            rb.isKinematic = true;
            rigidbodies[setup.bone] = rb;
        }

        // Aggiungi CapsuleCollider e CharacterJoint
        foreach (var setup in boneSetup)
        {
            if (!bones.TryGetValue(setup.bone, out Transform boneT)) continue;

            // Collider
            var col = boneT.GetComponent<CapsuleCollider>();
            if (col == null) col = boneT.gameObject.AddComponent<CapsuleCollider>();
            SetColliderForBone(setup.bone, col, boneT);

            // CharacterJoint (non su Hips — è il root)
            if (setup.parent == null) continue;
            if (!bones.TryGetValue(setup.parent, out Transform parentT)) continue;
            if (!rigidbodies.TryGetValue(setup.parent, out Rigidbody parentRb)) continue;

            var joint = boneT.GetComponent<CharacterJoint>();
            if (joint == null) joint = boneT.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentRb;
            joint.axis          = setup.axis;
            joint.swingAxis     = setup.swingAxis;
            joint.autoConfigureConnectedAnchor = true;
            joint.lowTwistLimit  = MakeLimit(setup.lowTwist);
            joint.highTwistLimit = MakeLimit(setup.highTwist);
            joint.swing1Limit    = MakeLimit(setup.swing1);
            joint.swing2Limit    = MakeLimit(setup.swing2);
            joint.enablePreprocessing = true;
        }

        // HitReaction su ogni osso
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.gameObject == go) continue;
            var hr = rb.GetComponent<HitReaction>();
            if (hr == null) rb.gameObject.AddComponent<HitReaction>();
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Ragdoll] Cittadino1 ragdoll ricreato con " + boneSetup.Length + " ossa. Salva con Ctrl+S.");
    }

    static void RemoveExistingRagdoll(GameObject root)
    {
        // Rimuovi solo sulle ossa figlie (non sul root)
        foreach (var j in root.GetComponentsInChildren<CharacterJoint>())
            Object.DestroyImmediate(j);

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
            if (rb.gameObject != root) Object.DestroyImmediate(rb);

        foreach (var col in root.GetComponentsInChildren<CapsuleCollider>())
            if (col.gameObject != root) Object.DestroyImmediate(col);

        foreach (var hr in root.GetComponentsInChildren<HitReaction>())
            Object.DestroyImmediate(hr);

        Debug.Log("[Ragdoll] Componenti ragdoll precedenti rimossi.");
    }

    static void SetColliderForBone(string boneName, CapsuleCollider col, Transform bone)
    {
        col.isTrigger = false;
        // Dimensioni approssimative per tipo di osso
        if (boneName.Contains("Hips"))
        { col.radius = 0.1f;  col.height = 0.2f;  col.direction = 1; col.center = new Vector3(0, 0.05f, 0); }
        else if (boneName.Contains("Spine"))
        { col.radius = 0.09f; col.height = 0.2f;  col.direction = 1; col.center = new Vector3(0, 0.05f, 0); }
        else if (boneName.Contains("Head"))
        { col.radius = 0.09f; col.height = 0.18f; col.direction = 1; col.center = Vector3.zero; }
        else if (boneName.Contains("UpLeg"))
        { col.radius = 0.06f; col.height = 0.35f; col.direction = 1; col.center = new Vector3(0, -0.15f, 0); }
        else if (boneName.Contains("Leg") && !boneName.Contains("Up"))
        { col.radius = 0.05f; col.height = 0.3f;  col.direction = 1; col.center = new Vector3(0, -0.12f, 0); }
        else if (boneName.Contains("Foot"))
        { col.radius = 0.03f; col.height = 0.12f; col.direction = 2; col.center = new Vector3(0, 0, 0.04f); }
        else if (boneName.Contains("Arm") && !boneName.Contains("Fore"))
        { col.radius = 0.05f; col.height = 0.25f; col.direction = 0; col.center = new Vector3(boneName.Contains("Left") ? 0.1f : -0.1f, 0, 0); }
        else if (boneName.Contains("ForeArm"))
        { col.radius = 0.04f; col.height = 0.22f; col.direction = 0; col.center = new Vector3(boneName.Contains("Left") ? 0.09f : -0.09f, 0, 0); }
        else
        { col.radius = 0.04f; col.height = 0.18f; col.direction = 1; }
    }

    static SoftJointLimit MakeLimit(float angle)
    {
        return new SoftJointLimit { limit = angle, bounciness = 0, contactDistance = 0 };
    }
}
