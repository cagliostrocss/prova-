using UnityEngine;

/// <summary>
/// Fa espandere gradualmente una macchia di sangue sull'indumento attorno a una ferita.
/// Lo scaling parte piccolo e cresce fino alla dimensione finale, simulando il sangue
/// che imbeve il tessuto. Va su un quad che ha già FleshWound per l'ancoraggio all'osso
/// (FleshWound imposta solo posizione/rotazione, non la scala, quindi i due non confliggono).
/// </summary>
public class BloodSoak : MonoBehaviour
{
    private float _startScale;
    private float _endScale;
    private float _growDuration;
    private float _t;

    public void Init(float startScale, float endScale, float growDuration)
    {
        _startScale   = startScale;
        _endScale     = endScale;
        _growDuration = Mathf.Max(0.01f, growDuration);
        transform.localScale = Vector3.one * _startScale;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / _growDuration);
        // SmoothStep: espansione rapida all'inizio che rallenta verso la fine
        float s = Mathf.Lerp(_startScale, _endScale, Mathf.SmoothStep(0f, 1f, k));
        transform.localScale = new Vector3(s, s, s);

        if (k >= 1f) enabled = false;   // raggiunta la dimensione finale, smette di aggiornare
    }
}
