using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PampelGames.GoreSimulator;

/// <summary>
/// Dipinge ferite di sangue direttamente nello spazio UV delle mesh del personaggio,
/// sfruttando lo shader UVTransformation di GoreSimulator (la stessa tecnica usata
/// dall'asset per i decal sui tagli).
///
/// Gestisce TUTTI gli SkinnedMeshRenderer del personaggio (corpo, testa, ecc.), così
/// anche le mesh separate — tipico dei modelli Fuse come Cittadino1, dove la faccia è
/// un mesh a sé — ricevono il sangue.
///
/// Poiché la maschera è in spazio UV, il sangue resta SOLIDALE alla mesh e si deforma
/// con la pelle: non scivola mai, nemmeno in animazione o ragdoll.
/// </summary>
public class GoreWoundPainter : MonoBehaviour
{
    [Header("Parametri ferita")]
    [Range(256, 4096)]   public int   maskResolution = 1024;
    [Range(0.01f, 0.2f)] public float woundRadius    = 0.045f;
    [Range(0.1f, 3f)]    public float woundStrength  = 1.3f;
    [Range(0f, 0.99f)]   public float hardness       = 0.4f;

    private class Layer
    {
        public SkinnedMeshRenderer smr;
        public RenderTexture       mask;
        public Material            decalMat;
    }

    private GoreSimulator   _gore;
    private Material        _uvTransform;
    private CommandBuffer   _cmd;
    private readonly List<Layer> _layers = new();
    private bool _ready;
    private bool _failed;

    void Awake()
    {
        _gore = GetComponent<GoreSimulator>();
    }

    bool EnsureSetup()
    {
        if (_ready)  return true;
        if (_failed) return false;

        if (_gore == null) _gore = GetComponent<GoreSimulator>();
        var refs = _gore != null ? _gore._defaultReferences : null;
        if (refs == null || refs.uvTransformation == null || refs.skinnedDecal == null)
        {
            Debug.LogWarning("[GoreWoundPainter] Risorse GoreSimulator mancanti (uvTransformation/skinnedDecal).");
            _failed = true;
            return false;
        }

        _uvTransform = refs.uvTransformation;
        _cmd = new CommandBuffer { name = "GoreWoundPaint" };

        // Tutti gli SkinnedMeshRenderer del personaggio (corpo, testa, ecc.)
        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;

            var mesh = smr.sharedMesh;

            // Submesh con tutti i triangoli (se non già presente)
            if (!TrianglesAlreadySet(mesh))
            {
                var allTris = mesh.triangles;
                int orig = mesh.subMeshCount;
                mesh.subMeshCount += 1;
                mesh.SetTriangles(allTris, orig);
            }

            var mask = new RenderTexture(maskResolution, maskResolution, 0, RenderTextureFormat.R8)
            {
                name = "GoreWoundMask_" + smr.name
            };
            mask.Create();
            ClearRT(mask);

            var decalMat = Instantiate(refs.skinnedDecal);
            decalMat.SetTexture(ShaderConstants.MaskTexture, mask);

            var mats = smr.sharedMaterials;
            System.Array.Resize(ref mats, mats.Length + 1);
            mats[mats.Length - 1] = decalMat;
            smr.sharedMaterials = mats;

            _layers.Add(new Layer { smr = smr, mask = mask, decalMat = decalMat });
        }

        if (_layers.Count == 0) { _failed = true; return false; }

        _ready = true;
        return true;
    }

    /// <summary>
    /// Dipinge una ferita nel punto mondo indicato su tutte le mesh.
    /// Solo le mesh la cui superficie è vicina al punto ricevono il sangue
    /// (la SphereMask sfuma in base alla distanza). Accumula sui colpi precedenti.
    /// </summary>
    public void PaintWound(Vector3 worldCenter, float radiusMul = 1f, float strengthMul = 1f)
    {
        if (!EnsureSetup()) return;

        _uvTransform.SetFloat(ShaderConstants.hardnessID, hardness);
        _uvTransform.SetFloat(ShaderConstants.strengthID, woundStrength * strengthMul);
        _uvTransform.SetFloat(ShaderConstants.radiusID,   woundRadius   * radiusMul);
        _uvTransform.SetInt(ShaderConstants.blendOpID,    (int) BlendOp.Add);
        _uvTransform.SetVector(ShaderConstants.centerID,  worldCenter);

        _cmd.Clear();
        foreach (var layer in _layers)
        {
            if (layer.smr == null) continue;
            _cmd.SetRenderTarget(layer.mask);
            var mesh = layer.smr.sharedMesh;
            for (int i = 0; i < mesh.subMeshCount; i++)
                _cmd.DrawRenderer(layer.smr, _uvTransform, i);
        }
        Graphics.ExecuteCommandBuffer(_cmd);
    }

    static void ClearRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    static bool TrianglesAlreadySet(Mesh mesh)
    {
        if (mesh.subMeshCount < 2) return false;
        int last = mesh.GetTriangles(mesh.subMeshCount - 1).Length;
        int sum  = 0;
        for (int i = 0; i < mesh.subMeshCount - 1; i++) sum += mesh.GetTriangles(i).Length;
        return sum == last;
    }

    void OnDestroy()
    {
        foreach (var layer in _layers)
        {
            if (layer.mask != null) { layer.mask.Release(); Destroy(layer.mask); }
            if (layer.decalMat != null) Destroy(layer.decalMat);
        }
        _layers.Clear();
        if (_cmd != null) _cmd.Release();
    }
}
