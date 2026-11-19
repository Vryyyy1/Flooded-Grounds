using UnityEngine;
using StarterAssets;

/// <summary>
/// Controls player prone / crawling mechanics.
/// Features:
/// - Press C (or Left Control) to toggle prone crawling.
/// - Lowers camera height smoothly into the tall grass (0.38m).
/// - CharacterController dimensions shrink (0.50m height) to crawl under low obstacles.
/// - Realistic first-person crawling animation (rhythmic elbow crawl camera sway & viewmodel crawl bob).
/// - Dynamic stealth: detects tall grass and dense foliage, reducing zombie detection radius by up to 85%!
/// - Prevents standing up if an obstacle / low ceiling is directly overhead.
/// - Space bar stands up when unobstructed.
/// - Feeds 'IsCrawling' bool and 'CrawlSpeed' float to character Animator.
/// </summary>
public class PlayerCrawl : MonoBehaviour
{
    public static PlayerCrawl Instance { get; private set; }

    [Header("Input Keys")]
    [Tooltip("Primary key to toggle crawl.")]
    public KeyCode crawlKey = KeyCode.C;

    [Tooltip("Secondary key to toggle crawl (e.g. Left Control).")]
    public KeyCode secondaryKey = KeyCode.LeftControl;

    [Header("Crawl State")]
    [Tooltip("Whether the character is currently crawling.")]
    public bool isCrawling = false;

    [Tooltip("Speed while crawling in m/s.")]
    public float crawlSpeed = 1.6f;

    [Header("Controller Dimensions")]
    public float normalHeight = 1.8f;
    public Vector3 normalCenter = new Vector3(0f, 0.9f, 0f);
    public float crawlHeight = 0.50f;
    public Vector3 crawlCenter = new Vector3(0f, 0.25f, 0f);
    public float transitionSpeed = 8f;

    [Header("Camera Height")]
    public float normalCameraY = 1.55f;
    public float crawlCameraY = 0.38f;

    [Header("Stealth / Grass Hiding")]
    [Tooltip("Whether the player is currently concealed in tall grass / foliage.")]
    public bool isInGrass = false;
    [Tooltip("Detection multiplier applied to zombies (lower is stealthier).")]
    public float stealthFactor = 1.0f;

    [Header("Head Clearance Check")]
    public LayerMask obstacleLayers = ~((1 << 2) | (1 << 8)); // Ignore Raycast and Player

    private CharacterController _characterController;
    private CapsuleCollider _capsuleCollider;
    private Animator _animator;
    private FirstPersonController _fpController;
    private Transform _cameraRoot;

    private float _originalWalkSpeed = 2.0f;
    private float _originalSprintSpeed = 5.335f;
    private float _originalJumpTimeout = 0.1f;
    private float _grassCheckTimer = 0f;
    private float _crawlCycle = 0f;
    private float _standBlockedTimer = 0f;

    private static readonly int IsCrawlingHash = Animator.StringToHash("IsCrawling");
    private static readonly int CrawlSpeedHash = Animator.StringToHash("CrawlSpeed");

    private void Awake()
    {
        Instance = this;

        _characterController = GetComponent<CharacterController>();
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _animator = GetComponentInChildren<Animator>();
        _fpController = GetComponent<FirstPersonController>();

        // Locate Camera Root (Cinemachine camera target)
        if (_fpController != null && _fpController.CinemachineCameraTarget != null)
        {
            _cameraRoot = _fpController.CinemachineCameraTarget.transform;
        }

        if (_cameraRoot == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PlayerCameraRoot" || t.name == "CinemachineCameraTarget")
                {
                    _cameraRoot = t;
                    break;
                }
            }
        }

        if (_cameraRoot == null && Camera.main != null && Camera.main.transform.parent != null)
        {
            _cameraRoot = Camera.main.transform.parent;
        }

        if (_fpController != null)
        {
            _originalWalkSpeed = _fpController.MoveSpeed;
            _originalSprintSpeed = _fpController.SprintSpeed;
            _originalJumpTimeout = _fpController.JumpTimeout;
        }

        if (_characterController != null)
        {
            if (_characterController.height > 1.2f)
            {
                normalHeight = _characterController.height;
                normalCenter = _characterController.center;
            }
            else
            {
                normalHeight = 1.8f;
                normalCenter = new Vector3(0f, 0.9f, 0f);
            }
        }

        if (_cameraRoot != null)
        {
            normalCameraY = _cameraRoot.localPosition.y;
            if (normalCameraY < 1.0f) normalCameraY = 1.55f;
        }
    }

    private void Update()
    {
        if (Instance == null) Instance = this;

        // Prevent crawling underwater while swimming
        if (_fpController != null && _fpController.IsSwimming)
        {
            if (isCrawling)
            {
                SetCrawling(false);
            }
            ApplyDimensionsAndCameraAnimation();
            return;
        }

        // Toggle Crawl on C or Left Control
        if (Input.GetKeyDown(crawlKey) || Input.GetKeyDown(secondaryKey))
        {
            ToggleCrawl();
        }

        // Space / Jump stands up if crawling and unobstructed
        if (isCrawling && Input.GetKeyDown(KeyCode.Space))
        {
            if (CanStandUp())
            {
                SetCrawling(false);
            }
            else
            {
                _standBlockedTimer = 1.5f;
            }
        }

        if (_standBlockedTimer > 0f)
        {
            _standBlockedTimer -= Time.deltaTime;
        }

        // Periodically verify if hidden in tall grass / foliage
        _grassCheckTimer -= Time.deltaTime;
        if (_grassCheckTimer <= 0f)
        {
            _grassCheckTimer = 0.2f;
            UpdateGrassConcealment();
        }

        // Smoothly adjust CharacterController dimensions and camera height
        ApplyDimensionsAndCameraAnimation();

        // Update Animator parameters safely (only when an active AnimatorController is playing)
        if (_animator == null)
        {
            foreach (var anim in GetComponentsInChildren<Animator>(true))
            {
                if (anim != null && anim.runtimeAnimatorController != null && !anim.transform.name.Contains("FP_Character_Hands"))
                {
                    _animator = anim;
                    break;
                }
            }
        }
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            _animator.SetBool(IsCrawlingHash, isCrawling);

            Vector3 horizontalVelocity = _characterController != null ? 
                new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z) : Vector3.zero;
            _animator.SetFloat(CrawlSpeedHash, horizontalVelocity.magnitude);
        }
    }

    public void ToggleCrawl()
    {
        if (isCrawling)
        {
            if (CanStandUp())
            {
                SetCrawling(false);
            }
            else
            {
                _standBlockedTimer = 1.5f;
                Debug.Log("[PlayerCrawl] Cannot stand up - obstacle or low ceiling directly overhead!");
            }
        }
        else
        {
            SetCrawling(true);
        }
    }

    public void SetCrawling(bool crawl)
    {
        isCrawling = crawl;

        if (_fpController != null)
        {
            if (isCrawling)
            {
                _fpController.MoveSpeed = crawlSpeed;
                _fpController.SprintSpeed = crawlSpeed * 1.35f;
                _fpController.JumpTimeout = 999f;
            }
            else
            {
                _fpController.MoveSpeed = _originalWalkSpeed;
                _fpController.SprintSpeed = _originalSprintSpeed;
                _fpController.JumpTimeout = _originalJumpTimeout;
            }
        }
    }

    public bool CanStandUp()
    {
        // Check for overhead obstructions above the player's current crawl height up to standing height
        float currentH = _characterController != null ? _characterController.height : crawlHeight;
        Vector3 startPos = transform.position + Vector3.up * (currentH + 0.05f);
        float distance = Mathf.Max(0.1f, normalHeight - currentH - 0.05f);

        RaycastHit[] hits = Physics.SphereCastAll(startPos, 0.18f, Vector3.up, distance, obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.isTrigger) continue;

            // Ignore player's own colliders, child objects, weapon, bag, etc.
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.CompareTag("Player"))
            {
                continue;
            }

            // Real overhead obstacle detected (bridge ceiling, tunnel, low beam, etc.)
            return false;
        }

        return true;
    }

    private void UpdateGrassConcealment()
    {
        if (!isCrawling)
        {
            isInGrass = false;
            stealthFactor = 1.0f;
            return;
        }

        bool inGrass = false;

        // 1. Check Terrain grass detail density at player position
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            TerrainData td = terrain.terrainData;
            Vector3 relPos = transform.position - terrain.transform.position;
            int mapX = Mathf.FloorToInt((relPos.x / td.size.x) * td.detailWidth);
            int mapY = Mathf.FloorToInt((relPos.z / td.size.z) * td.detailHeight);

            if (mapX >= 0 && mapX < td.detailWidth && mapY >= 0 && mapY < td.detailHeight)
            {
                for (int l = 0; l < td.detailPrototypes.Length; l++)
                {
                    int[,] layer = td.GetDetailLayer(mapX, mapY, 1, 1, l);
                    if (layer != null && layer[0, 0] > 0)
                    {
                        inGrass = true;
                        break;
                    }
                }
            }
        }

        // 2. Also check nearby foliage / bush colliders
        if (!inGrass)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.35f, 1.2f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                string n = hits[i].gameObject.name.ToLower();
                if (n.Contains("grass") || n.Contains("bush") || n.Contains("plant") || n.Contains("foliage") || n.Contains("reed") || n.Contains("weed"))
                {
                    inGrass = true;
                    break;
                }
            }
        }

        isInGrass = inGrass;

        // In grass: 85% detection reduction (stealthFactor 0.15)
        // In open ground while prone: 65% detection reduction (stealthFactor 0.35)
        stealthFactor = inGrass ? 0.15f : 0.35f;
    }

    private void ApplyDimensionsAndCameraAnimation()
    {
        float targetHeight = isCrawling ? crawlHeight : normalHeight;
        Vector3 targetCenter = isCrawling ? crawlCenter : normalCenter;
        float targetCamY = isCrawling ? crawlCameraY : normalCameraY;

        if (_characterController != null)
        {
            _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, Time.deltaTime * transitionSpeed);
            _characterController.center = Vector3.Lerp(_characterController.center, targetCenter, Time.deltaTime * transitionSpeed);
        }

        if (_capsuleCollider != null)
        {
            _capsuleCollider.height = _characterController != null ? _characterController.height : targetHeight;
            _capsuleCollider.center = _characterController != null ? _characterController.center : targetCenter;
        }

        if (_cameraRoot != null)
        {
            Vector3 pos = _cameraRoot.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetCamY, Time.deltaTime * transitionSpeed);

            // Realistic first-person crawling camera sway & bob while moving
            if (isCrawling)
            {
                Vector3 hVel = _characterController != null ? 
                    new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z) : Vector3.zero;
                float moveSpeed = hVel.magnitude;

                if (moveSpeed > 0.15f)
                {
                    _crawlCycle += Time.deltaTime * 5.0f;
                    // Alternating elbow push/pull bobbing
                    float camBobY = -Mathf.Abs(Mathf.Sin(_crawlCycle)) * 0.025f;
                    float camBobX = Mathf.Sin(_crawlCycle) * 0.018f;

                    pos.x = Mathf.Lerp(pos.x, camBobX, Time.deltaTime * 10f);
                    pos.y += camBobY * Time.deltaTime * 15f;
                }
                else
                {
                    // Gentle breathing while prone on ground
                    float breathY = Mathf.Sin(Time.time * 1.6f) * 0.004f;
                    pos.x = Mathf.Lerp(pos.x, 0f, Time.deltaTime * 8f);
                    pos.y += breathY * Time.deltaTime * 5f;
                }
            }
            else
            {
                pos.x = Mathf.Lerp(pos.x, 0f, Time.deltaTime * 10f);
            }

            _cameraRoot.localPosition = pos;
        }
    }

    private void OnGUI()
    {
        if (StarterAssets.Boat4EscapeInteraction.Instance != null && StarterAssets.Boat4EscapeInteraction.Instance.isEscaping) return;

        if (isCrawling)
        {
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.box);
            badgeStyle.fontSize = 12;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleCenter;

            if (isInGrass)
            {
                badgeStyle.normal.textColor = new Color(0.3f, 1f, 0.4f, 1f); // Vibrant tactical green
                GUI.Box(new Rect(Screen.width * 0.5f - 110f, Screen.height - 75f, 220f, 28f), "[C] PRONE - HIDDEN IN GRASS", badgeStyle);
            }
            else
            {
                badgeStyle.normal.textColor = new Color(1f, 0.85f, 0.2f, 1f); // Tactical amber
                GUI.Box(new Rect(Screen.width * 0.5f - 95f, Screen.height - 75f, 190f, 28f), "[C] PRONE (LOW PROFILE)", badgeStyle);
            }

            if (_standBlockedTimer > 0f)
            {
                GUIStyle warnStyle = new GUIStyle(GUI.skin.box);
                warnStyle.fontSize = 12;
                warnStyle.fontStyle = FontStyle.Bold;
                warnStyle.normal.textColor = Color.red;
                warnStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height - 110f, 260f, 26f), "CANNOT STAND - CEILING OVERHEAD", warnStyle);
            }
        }
    }
}
