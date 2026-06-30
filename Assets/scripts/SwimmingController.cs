using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(CharacterController))]
public class SwimmingController : MonoBehaviour
{
    [Header("Swimming")]
    [Range(0f, 10f)] public float swimSpeed             = 2f;
    [Range(0f, 10f)] public float verticalSpeed         = 1.5f;
    [Range(0f, 1f)]  public float underwaterGravityScale = 0.1f;
    [Range(0f, 1f)]  public float waterDrag             = 0.9f;

    [Header("Surface Bobbing")]
    [Range(0f, 0.5f)] public float surfaceBobbingStrength = 0.05f;
    [Range(0f, 5f)]   public float surfaceBobbingSpeed    = 1.5f;

    // State
    public bool IsUnderwater { get; private set; }

    private CharacterController _cc;
    private WaterVolume         _currentVolume;
    private Vector3             _velocity;
    private float               _bobbingTimer;

    // XR references (optional — works also without XR)
    private Transform _headTransform;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // Try to find the XR camera (head)
        var cam = GetComponentInChildren<Camera>();
        if (cam != null) _headTransform = cam.transform;
    }

    public void EnterWater(WaterVolume volume)
    {
        IsUnderwater   = true;
        _currentVolume = volume;
        _velocity      = Vector3.zero;
    }

    public void ExitWater()
    {
        IsUnderwater   = false;
        _currentVolume = null;
    }

    void Update()
    {
        if (!IsUnderwater || _currentVolume == null) return;

        // Direction from head (VR head look) or camera
        Vector3 forward = _headTransform != null
            ? _headTransform.forward
            : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Read input (works with Unity Input System and legacy)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = (forward * v + right * h) * swimSpeed;

        // Vertical movement (space = up, ctrl/c = down)
        if (Input.GetKey(KeyCode.Space))       move.y += verticalSpeed;
        if (Input.GetKey(KeyCode.LeftControl)) move.y -= verticalSpeed;

        // Reduced gravity underwater
        _velocity.y -= 9.81f * underwaterGravityScale * Time.deltaTime;

        // Bobbing at surface
        float surfY = _currentVolume.GetSurfaceY();
        if (Mathf.Abs(transform.position.y - surfY) < 0.5f)
        {
            _bobbingTimer += Time.deltaTime * surfaceBobbingSpeed;
            move.y += Mathf.Sin(_bobbingTimer) * surfaceBobbingStrength;
        }

        _velocity = Vector3.Lerp(_velocity + move * Time.deltaTime, Vector3.zero, waterDrag * Time.deltaTime);
        _cc.Move(_velocity);
    }
}
