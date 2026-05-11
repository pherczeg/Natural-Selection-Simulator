using System;
using System.Collections.Generic;
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

    [Header("Creature Follow")]
    public bool enableCreatureFollow = true;
    public bool disableManualPanWhileFollowing = true;
    public bool recenterOnSelection = true;
    [Min(0f)] public float followBackOffset = 6f;
    [SerializeField] private CreatureSelectionManager selectionManager;

    private Vector3 _targetPosition;
    private float _targetY;
    private Vector3 _moveVelocity;
    private BaseCreatureBehaviour _followCreature;
    private bool _selectionSubscribed;

    // Right-click drag state
    private Vector3 _dragOriginWorld;
    private Vector3 _dragOriginCameraPos;
    private bool _isDragging;

    private void OnEnable()
    {
        AttachSelectionManager();
    }

    private void OnDisable()
    {
        DetachSelectionManager();
    }

    private void Start()
    {
        _targetPosition = transform.position;
        _targetY = transform.position.y;
        AttachSelectionManager();
    }

    private void Update()
    {
        if (!_selectionSubscribed)
        {
            AttachSelectionManager();
        }

        bool isFollowingCreature = UpdateCreatureFollowTarget();

        if (!isFollowingCreature || !disableManualPanWhileFollowing)
        {
            HandleDragPan();
            HandleKeyAndEdgeMovement();
        }
        else
        {
            _isDragging = false;
        }

        HandleZoom();
        HandleRotation();

        // Re-apply follow after zoom/rotation so the selected creature stays centered.
        if (isFollowingCreature)
        {
            UpdateCreatureFollowTarget();
        }

        ApplySmoothing();
    }

    private void AttachSelectionManager()
    {
        if (_selectionSubscribed)
            return;

        if (selectionManager == null)
        {
            selectionManager = CreatureSelectionManager.Instance ?? FindObjectOfType<CreatureSelectionManager>();
        }

        if (selectionManager == null)
        {
            GameObject selectionManagerObject = new GameObject("CreatureSelectionManager");
            selectionManager = selectionManagerObject.AddComponent<CreatureSelectionManager>();
        }

        selectionManager.SelectionChanged += HandleSelectionChanged;
        _selectionSubscribed = true;
        HandleSelectionChanged(selectionManager.SelectedCreature);
    }

    private void DetachSelectionManager()
    {
        if (!_selectionSubscribed || selectionManager == null)
            return;

        selectionManager.SelectionChanged -= HandleSelectionChanged;
        _selectionSubscribed = false;
    }

    private void HandleSelectionChanged(BaseCreatureBehaviour selectedCreature)
    {
        _followCreature = selectedCreature;

        if (recenterOnSelection && _followCreature != null)
        {
            UpdateCreatureFollowTarget();
        }
    }

    private bool UpdateCreatureFollowTarget()
    {
        if (!enableCreatureFollow)
        {
            _followCreature = null;
            return false;
        }

        if (_followCreature == null)
            return false;

        if (!_followCreature.gameObject.activeInHierarchy || _followCreature.IsDespawnQueued)
        {
            _followCreature = null;
            return false;
        }

        Vector3 followedPosition = _followCreature.transform.position;

        Vector3 followAnchor;
        if (!TryGetCenteredFollowAnchor(followedPosition, out followAnchor))
        {
            followAnchor = GetFallbackFollowAnchor(followedPosition);
        }

        _targetPosition.x = followAnchor.x;
        _targetPosition.z = followAnchor.z;
        ApplyPanLimit(ref _targetPosition);
        return true;
    }

    private bool TryGetCenteredFollowAnchor(Vector3 followedPosition, out Vector3 followAnchor)
    {
        followAnchor = Vector3.zero;

        Vector3 forward = transform.forward;
        float forwardY = forward.y;
        if (Mathf.Abs(forwardY) < 0.0001f)
            return false;

        float distanceAlongForward = (followedPosition.y - _targetY) / forwardY;
        if (distanceAlongForward <= 0.0001f || float.IsNaN(distanceAlongForward) || float.IsInfinity(distanceAlongForward))
            return false;

        followAnchor = followedPosition - forward * distanceAlongForward;
        return true;
    }

    private Vector3 GetFallbackFollowAnchor(Vector3 followedPosition)
    {
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();
        return followedPosition - flatForward * Mathf.Max(0f, followBackOffset);
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

public class CreatureSelectionManager : MonoBehaviour
{
    public static CreatureSelectionManager Instance { get; private set; }

    [Header("Selection")]
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private float selectionRayDistance = 1000f;
    [SerializeField] private bool deselectOnEmptyClick = true;
    [SerializeField] private KeyCode clearSelectionKey = KeyCode.Escape;
    [SerializeField] private bool enableKeyboardCycling = true;
    [SerializeField] private KeyCode previousSelectionKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode nextSelectionKey = KeyCode.RightArrow;
    [SerializeField] private bool wrapKeyboardSelection = true;

    public BaseCreatureBehaviour SelectedCreature { get; private set; }

    public event Action<BaseCreatureBehaviour> SelectionChanged;

    private readonly List<BaseCreatureBehaviour> activeCreatures = new List<BaseCreatureBehaviour>(256);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(clearSelectionKey))
        {
            ClearSelection();
        }

        if (Input.GetMouseButtonDown(0))
        {
            TrySelectFromMouse();
        }

        if (enableKeyboardCycling)
        {
            HandleKeyboardSelectionInput();
        }

        if (SelectedCreature != null && !IsSelectable(SelectedCreature))
        {
            ClearSelection();
        }
    }

    public void SelectCreature(BaseCreatureBehaviour creature)
    {
        if (!IsSelectable(creature))
        {
            ClearSelection();
            return;
        }

        if (SelectedCreature == creature)
            return;

        SelectedCreature = creature;
        SelectionChanged?.Invoke(SelectedCreature);
    }

    public void ClearSelection()
    {
        if (SelectedCreature == null)
            return;

        SelectedCreature = null;
        SelectionChanged?.Invoke(null);
    }

    private void TrySelectFromMouse()
    {
        Camera cam = selectionCamera != null ? selectionCamera : Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(0.01f, selectionRayDistance)))
        {
            BaseCreatureBehaviour creature = hit.collider != null
                ? hit.collider.GetComponentInParent<BaseCreatureBehaviour>()
                : null;

            if (creature != null)
            {
                SelectCreature(creature);
                return;
            }
        }

        if (deselectOnEmptyClick)
        {
            ClearSelection();
        }
    }

    private void HandleKeyboardSelectionInput()
    {
        bool selectPrevious = Input.GetKeyDown(previousSelectionKey);
        bool selectNext = Input.GetKeyDown(nextSelectionKey);

        if (selectPrevious == selectNext)
            return;

        TryCycleSelection(selectNext ? 1 : -1);
    }

    private void TryCycleSelection(int direction)
    {
        if (direction == 0)
            return;

        RefreshActiveCreatures();
        if (activeCreatures.Count == 0)
        {
            ClearSelection();
            return;
        }

        int currentIndex = SelectedCreature != null ? activeCreatures.IndexOf(SelectedCreature) : -1;
        int targetIndex;
        if (currentIndex < 0)
        {
            targetIndex = direction > 0 ? 0 : activeCreatures.Count - 1;
        }
        else
        {
            int candidateIndex = currentIndex + direction;
            targetIndex = wrapKeyboardSelection
                ? GetWrappedIndex(candidateIndex, activeCreatures.Count)
                : Mathf.Clamp(candidateIndex, 0, activeCreatures.Count - 1);
        }

        SelectCreature(activeCreatures[targetIndex]);
    }

    private void RefreshActiveCreatures()
    {
        activeCreatures.Clear();

        CreatureSpawner spawner = CreatureSpawner.Instance;
        if (spawner == null)
            return;

        AddSelectableCreatures(spawner.herbivorCreatures, activeCreatures);
        AddSelectableCreatures(spawner.predatorCreatures, activeCreatures);
        activeCreatures.Sort((first, second) => first.GetInstanceID().CompareTo(second.GetInstanceID()));
    }

    private static void AddSelectableCreatures(
        List<BaseCreatureBehaviour> source,
        List<BaseCreatureBehaviour> destination)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            BaseCreatureBehaviour creature = source[i];
            if (IsSelectable(creature))
            {
                destination.Add(creature);
            }
        }
    }

    private static int GetWrappedIndex(int index, int count)
    {
        if (count <= 0)
            return -1;

        if (index < 0)
            return count - 1;

        if (index >= count)
            return 0;

        return index;
    }

    private static bool IsSelectable(BaseCreatureBehaviour creature)
    {
        return creature != null &&
               creature.gameObject.activeInHierarchy &&
               !creature.IsDespawnQueued;
    }
}
