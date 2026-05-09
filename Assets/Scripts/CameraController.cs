using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float panSpeed = 30f;
    public float panBorderThickness = 10f;
    public bool enableEdgeScrolling = true;
    // Set to (0,0) to disable clamping
    public Vector2 panLimit = new Vector2(0f, 0f);

    [Header("Zoom")]
    public float scrollSpeed = 15f;
    public float minY = 5f;
    public float maxY = 120f;

    [Header("Rotation")]
    public float rotationSpeed = 100f;
    public Vector2 pitchLimit = new Vector2(10f, 80f);

    [Header("Smoothing")]
    public float moveSmoothTime = 0.1f;

    private Vector3 _targetPosition;
    private float _targetY;
    private Vector3 _moveVelocity;

    // Right-click drag state
    private Vector3 _dragOriginWorld;
    private Vector3 _dragOriginCameraPos;
    private bool _isDragging;

    private static readonly int GroundLayer = ~0;

    private void Start()
    {
        _targetPosition = transform.position;
        _targetY = transform.position.y;
    }

    private void Update()
    {
        HandleDragPan();
        HandleKeyAndEdgeMovement();
        HandleZoom();
        HandleRotation();
        ApplySmoothing();
    }

    // "Grab and drag" — the point you click stays under the cursor
    private void HandleDragPan()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (RaycastGround(out Vector3 hit))
            {
                _dragOriginWorld = hit;
                _dragOriginCameraPos = _targetPosition;
                _isDragging = true;
            }
        }

        if (Input.GetMouseButton(1) && _isDragging)
        {
            if (RaycastGround(out Vector3 currentHit))
            {
                Vector3 diff = _dragOriginWorld - currentHit;
                diff.y = 0;
                Vector3 newPos = _dragOriginCameraPos + diff;
                ApplyPanLimit(ref newPos);
                _targetPosition = newPos;
            }
        }

        if (Input.GetMouseButtonUp(1))
            _isDragging = false;
    }

    private void HandleKeyAndEdgeMovement()
    {
        if (_isDragging) return;

        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move -= Vector3.forward;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetKey(KeyCode.A)) move -= Vector3.right;

        if (enableEdgeScrolling)
        {
            if (Input.mousePosition.y >= Screen.height - panBorderThickness) move += Vector3.forward;
            if (Input.mousePosition.y <= panBorderThickness)                  move -= Vector3.forward;
            if (Input.mousePosition.x >= Screen.width  - panBorderThickness)  move += Vector3.right;
            if (Input.mousePosition.x <= panBorderThickness)                  move -= Vector3.right;
        }

        if (move == Vector3.zero) return;

        Vector3 flatForward = transform.forward; flatForward.y = 0; flatForward.Normalize();
        Vector3 flatRight   = transform.right;   flatRight.y   = 0; flatRight.Normalize();

        float zoomFactor = Mathf.InverseLerp(minY, maxY, _targetY);
        float speed = panSpeed * Mathf.Lerp(0.4f, 1.5f, zoomFactor);

        Vector3 worldMove = (flatForward * move.z + flatRight * move.x) * speed * Time.deltaTime;
        Vector3 newPos = _targetPosition + worldMove;
        ApplyPanLimit(ref newPos);
        _targetPosition = newPos;
    }

    private void HandleZoom()
    {
        // ScrollWheel returns ~0.1 per notch — no Time.deltaTime needed
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;

        _targetY -= scroll * scrollSpeed;
        _targetY = Mathf.Clamp(_targetY, minY, maxY);
    }

    private void HandleRotation()
    {
        if (!Input.GetMouseButton(2)) return;

        float yaw   = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
        float pitch = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

        transform.Rotate(Vector3.up, yaw, Space.World);

        float currentPitch = transform.eulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f;

        float newPitch = Mathf.Clamp(currentPitch - pitch, pitchLimit.x, pitchLimit.y);
        transform.rotation = Quaternion.Euler(newPitch, transform.eulerAngles.y, 0f);
    }

    private void ApplySmoothing()
    {
        Vector3 target = new Vector3(_targetPosition.x, _targetY, _targetPosition.z);
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, target, ref _moveVelocity, moveSmoothTime);
        transform.position = smoothed;
    }

    private bool RaycastGround(out Vector3 hitPoint)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // Intersect with the Y=0 plane
        if (ray.direction.y != 0f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f)
            {
                hitPoint = ray.origin + ray.direction * t;
                return true;
            }
        }
        hitPoint = Vector3.zero;
        return false;
    }

    private void ApplyPanLimit(ref Vector3 pos)
    {
        if (panLimit.x > 0) pos.x = Mathf.Clamp(pos.x, -panLimit.x, panLimit.x);
        if (panLimit.y > 0) pos.z = Mathf.Clamp(pos.z, -panLimit.y, panLimit.y);
    }
}
