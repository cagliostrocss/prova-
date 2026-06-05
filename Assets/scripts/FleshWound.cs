using UnityEngine;

/// <summary>
/// Decal visivo di una ferita da arma da fuoco sulla carne dello zombi.
///
/// Come funziona:
///   Anziché usare la gerarchia Unity (SetParent), che distorce il quad se l'osso
///   ha scala non-uniforme (tipico nei rig Mixamo), memorizza la posizione e
///   rotazione in coordinate locali dell'osso e le riapplica ogni LateUpdate.
///   In questo modo il decal segue correttamente il ragdoll — sia da vivo che
///   dopo la caduta — senza distorsioni visive.
/// </summary>
public class FleshWound : MonoBehaviour
{
    private Transform  _bone;
    private Vector3    _localPos;
    private Quaternion _localRot;

    /// <summary>
    /// Inizializza il decal: memorizza l'offset locale rispetto all'osso colpito.
    /// worldPos/worldRot sono già la posizione/rotazione definitiva del decal.
    /// </summary>
    public void Init(Transform bone, Vector3 worldPos, Quaternion worldRot, float destroyAfter = 30f)
    {
        _bone = bone;

        // Memorizza posizione in spazio locale dell'osso
        // InverseTransformPoint tiene conto della scala → corretto anche per scale non-uniformi
        _localPos = bone.InverseTransformPoint(worldPos);

        // Memorizza rotazione locale: togli la rotazione dell'osso, lascia solo l'offset
        _localRot = Quaternion.Inverse(bone.rotation) * worldRot;

        // Posizione iniziale immediata (prima del LateUpdate)
        transform.position = worldPos;
        transform.rotation = worldRot;

        Destroy(gameObject, destroyAfter);
    }

    void LateUpdate()
    {
        // Se l'osso è stato distrutto (p.es. Destroy del GameObject zombie) rimuoviti
        if (_bone == null)
        {
            Destroy(gameObject);
            return;
        }

        // Ricostruisci posizione/rotazione world ogni frame in base alla bone
        // Questo fa seguire il ragdoll senza essere figli nella gerarchia
        transform.position = _bone.TransformPoint(_localPos);
        transform.rotation = _bone.rotation * _localRot;
    }
}
