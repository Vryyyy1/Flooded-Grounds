using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A complete, modern first-person CharacterController script using Unity's new Input System (UnityEngine.InputSystem).
/// Supports walking, sprinting, jumping, physics-based gravity, mouse look with pitch clamping, 
/// jump buffering, coyote time, and optional subtle head bobbing.
/// 
/// Works out-of-the-box with auto-generated default input bindings (WASD, Mouse, Space, Shift),
/// or you can assign your own InputActionReferences from an .inputactions asset in the Inspector.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Modern FPS/Modern First Person Controller")]
public class ModernFirstPersonController : MonoBehaviour
{
    [Header("Look Settings")]
    [Tooltip("Transform of the first-person camera.")]
    [SerializeField] private Transform playerCamera;
    [Tooltip("Mouse horizontal sensitivity.")]
    [SerializeField] private float mouseSensitivityX = 1.5f;
    [Tooltip("Mouse vertical sensitivity.")]
    [SerializeField] private float mouseSensitivityY = 1.5f;
    [Tooltip("Invert vertical mouse look.")]
    [SerializeField] private bool invertY = false;
    [Tooltip("Minimum vertical camera angle in degrees.")]
    [SerializeField] private float minPitch = -85.0f;
    [Tooltip("Maximum vertical camera angle in degrees.")]
    [SerializeField] private float maxPitch = 85.0f;
    [Tooltip("Smooth look interpolation factor (0 for raw input).")]
    [SerializeField] [Range(0f, 0.2f)] private float lookSmoothTime = 0.03f;
    [Tooltip("Lock and hide cursor on start.")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Movement Settings")]
    [Tooltip("Walking speed in meters per second.")]
    [SerializeField] private float walkSpeed = 4.5f;
    [Tooltip("Sprinting speed in meters per second.")]
    [SerializeField] private float sprintSpeed = 8.0f;
    [Tooltip("Acceleration and deceleration smoothing rate.")]
    [SerializeField] private float speedChangeRate = 12.0f;

    [Header("Jumping & Gravity")]
    [Tooltip("Peak jump height in meters (realistic human combat jump is ~0.75m).")]
    [SerializeField] private float jumpHeight = 0.75f;
    [Tooltip("Gravity acceleration (negative value). Realistic FPS gravity is -22 m/s².")]
    [SerializeField] private float gravity = -22.0f;
    [Tooltip("Small downward stick force applied when grounded to follow slopes cleanly.")]
    [SerializeField] private float groundedGravity = -4.0f;
    [Tooltip("Maximum downward falling velocity.")]
    [SerializeField] private float terminalVelocity = -50.0f;
    [Tooltip("Coyote time duration: window of time after walking off a ledge where jump is still allowed.")]
    [SerializeField] private float coyoteTimeDuration = 0.15f;
    [Tooltip("Jump buffer duration: window of time where a jump press before landing will trigger upon touching ground.")]
    [SerializeField] private float jumpBufferDuration = 0.15f;

    [Header("Ground Check")]
    [Tooltip("Offset relative to the character bottom for the ground check sphere.")]
    [SerializeField] private float groundCheckOffset = 0.1f;
    [Tooltip("Radius of the ground check sphere (should match or slightly exceed controller radius).")]
    [SerializeField] private float groundCheckRadius = 0.45f;
    [Tooltip("Layers recognized as walkable ground.")]
    [SerializeField] private LayerMask groundLayers = ~0; // Default: Everything

    [Header("Character Visual & Animation (Optional)")]
    [Tooltip("Animator component on character (auto-located in children if empty).")]
    [SerializeField] private Animator characterAnimator;
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDJump;
    private int _animIDFreeFall;
    private int _animIDMotionSpeed;

    [Header("Head Bobbing (Optional)")]
    [Tooltip("Toggle first-person procedural head bobbing.")]
    [SerializeField] private bool enableHeadBob = true;
    [Tooltip("Head bob frequency when walking.")]
    [SerializeField] private float walkBobFrequency = 10f;
    [Tooltip("Head bob horizontal & vertical amplitude when walking.")]
    [SerializeField] private float walkBobAmplitude = 0.035f;
    [Tooltip("Head bob frequency multiplier when sprinting.")]
    [SerializeField] private float sprintBobMultiplier = 1.35f;
    [Tooltip("Speed to return camera to rest position.")]
    [SerializeField] private float bobResetSpeed = 8f;

    [Header("Input Action References (Optional)")]
    [Tooltip("Optional reference to Move InputAction. If left empty, WASD/Stick fallback is auto-created.")]
    [SerializeField] private InputActionReference moveActionRef;
    [Tooltip("Optional reference to Look InputAction. If left empty, Mouse Delta/Stick fallback is auto-created.")]
    [SerializeField] private InputActionReference lookActionRef;
    [Tooltip("Optional reference to Jump InputAction. If left empty, Spacebar/Gamepad button fallback is auto-created.")]
    [SerializeField] private InputActionReference jumpActionRef;
    [Tooltip("Optional reference to Sprint InputAction. If left empty, LeftShift/Stick-press fallback is auto-created.")]
    [SerializeField] private InputActionReference sprintActionRef;

    // Components & Runtime State
    private CharacterController _characterController;
    private Vector3 _cameraInitialLocalPosition;
    private float _cameraPitch = 0.0f;
    private Vector2 _currentLookDelta;
    private Vector2 _lookVelocity;

    // Movement & Velocity
    private Vector3 _currentHorizontalVelocity;
    private float _verticalVelocity;
    private bool _isGrounded;

    // Jump helpers
    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;

    // Head bob timer
    private float _bobTimer;

    // Fallback actions (used if InputActionReferences are not supplied in Inspector)
    private InputAction _fallbackMoveAction;
    private InputAction _fallbackLookAction;
    private InputAction _fallbackJumpAction;
    private InputAction _fallbackSprintAction;

    // Properties to access active actions safely
    private InputAction MoveAction => moveActionRef != null ? moveActionRef.action : _fallbackMoveAction;
    private InputAction LookAction => lookActionRef != null ? lookActionRef.action : _fallbackLookAction;
    private InputAction JumpAction => jumpActionRef != null ? jumpActionRef.action : _fallbackJumpAction;
    private InputAction SprintAction => sprintActionRef != null ? sprintActionRef.action : _fallbackSprintAction;

    public bool IsGrounded => _isGrounded;
    public bool IsSprinting => SprintAction != null && SprintAction.IsPressed();
    public float CurrentSpeed => new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        // Auto-detect Camera if not assigned
        if (playerCamera == null)
        {
            Camera mainCam = GetComponentInChildren<Camera>();
            if (mainCam == null)
            {
                mainCam = Camera.main;
            }

            if (mainCam != null)
            {
                playerCamera = mainCam.transform;
            }
            else
            {
                Debug.LogWarning("[ModernFirstPersonController] No camera found in children or tagged MainCamera. Please assign PlayerCamera in the inspector.");
            }
        }

        if (playerCamera != null)
        {
            _cameraInitialLocalPosition = playerCamera.localPosition;
            _cameraPitch = playerCamera.localEulerAngles.x;
            // Normalize pitch to -180..180 range
            if (_cameraPitch > 180f) _cameraPitch -= 360f;
        }

        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>();
        }

        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDGrounded = Animator.StringToHash("Grounded");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDFreeFall = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

        InitializeFallbackInputs();
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLock(true);
        }
    }

    private void OnEnable()
    {
        EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
    }

    private void Update()
    {
        HandleCursorToggle();
        CheckGrounded();
        HandleMouseLook();
        HandleMovement();
        HandleGravityAndJump();
        HandleHeadBob();
    }

    #region Input Setup & Lifecycle

    private void InitializeFallbackInputs()
    {
        // Setup internal fallback actions if Inspector references were not assigned
        if (moveActionRef == null)
        {
            _fallbackMoveAction = new InputAction("FallbackMove", InputActionType.Value);
            _fallbackMoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _fallbackMoveAction.AddBinding("<Gamepad>/leftStick");
        }

        if (lookActionRef == null)
        {
            _fallbackLookAction = new InputAction("FallbackLook", InputActionType.Value);
            _fallbackLookAction.AddBinding("<Mouse>/delta");
            _fallbackLookAction.AddBinding("<Gamepad>/rightStick");
        }

        if (jumpActionRef == null)
        {
            _fallbackJumpAction = new InputAction("FallbackJump", InputActionType.Button);
            _fallbackJumpAction.AddBinding("<Keyboard>/space");
            _fallbackJumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        if (sprintActionRef == null)
        {
            _fallbackSprintAction = new InputAction("FallbackSprint", InputActionType.Button);
            _fallbackSprintAction.AddBinding("<Keyboard>/leftShift");
            _fallbackSprintAction.AddBinding("<Gamepad>/leftStickPress");
        }
    }

    private void EnableActions()
    {
        MoveAction?.Enable();
        LookAction?.Enable();
        JumpAction?.Enable();
        SprintAction?.Enable();
    }

    private void DisableActions()
    {
        MoveAction?.Disable();
        LookAction?.Disable();
        JumpAction?.Disable();
        SprintAction?.Disable();
    }

    private void HandleCursorToggle()
    {
        // Unlock on Escape, re-lock on left click
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLock(true);
        }
    }

    public void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    #endregion

    #region Ground Check

    private void CheckGrounded()
    {
        // Combine CharacterController's built-in isGrounded with a physics sphere check at base
        Vector3 spherePosition = new Vector3(
            transform.position.x,
            transform.position.y + groundCheckOffset,
            transform.position.z
        );

        bool sphereGrounded = Physics.CheckSphere(
            spherePosition,
            groundCheckRadius,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        _isGrounded = _characterController.isGrounded || sphereGrounded;

        if (characterAnimator != null)
        {
            characterAnimator.SetBool(_animIDGrounded, _isGrounded);
        }

        if (_isGrounded)
        {
            _coyoteTimeCounter = coyoteTimeDuration;
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }
    }

    #endregion

    #region Mouse Look

    private void HandleMouseLook()
    {
        if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 rawLook = LookAction != null ? LookAction.ReadValue<Vector2>() : Vector2.zero;

        // Smooth look interpolation (optional)
        if (lookSmoothTime > 0.001f)
        {
            _currentLookDelta = Vector2.SmoothDamp(_currentLookDelta, rawLook, ref _lookVelocity, lookSmoothTime);
        }
        else
        {
            _currentLookDelta = rawLook;
        }

        float mouseX = _currentLookDelta.x * mouseSensitivityX * 0.1f;
        // Invert Y direction: standard FPS convention (moving mouse up tilts camera up)
        float mouseY = _currentLookDelta.y * mouseSensitivityY * 0.1f * (invertY ? -1f : 1f);

        // Horizontal rotation: turn the whole character body (Yaw)
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation: tilt the camera up and down (Pitch)
        _cameraPitch += mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);
        playerCamera.localRotation = Quaternion.Euler(_cameraPitch, 0.0f, 0.0f);
    }

    #endregion

    #region Movement

    private void HandleMovement()
    {
        Vector2 input = MoveAction != null ? MoveAction.ReadValue<Vector2>() : Vector2.zero;

        // Clamp diagonal input magnitude to 1 to prevent faster diagonal walking
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        // Determine target speed
        bool isSprinting = SprintAction != null && SprintAction.IsPressed() && input.y > 0.1f; // Only sprint when moving forward
        float targetSpeed = (input == Vector2.zero) ? 0.0f : (isSprinting ? sprintSpeed : walkSpeed);

        // Calculate world-space move direction based on player transform orientation
        Vector3 targetDirection = (transform.right * input.x + transform.forward * input.y).normalized;
        Vector3 targetVelocity = targetDirection * targetSpeed;

        // Smoothly accelerate / decelerate towards target horizontal velocity
        _currentHorizontalVelocity = Vector3.MoveTowards(
            _currentHorizontalVelocity,
            targetVelocity,
            speedChangeRate * Time.deltaTime
        );

        // Move controller horizontally
        _characterController.Move(_currentHorizontalVelocity * Time.deltaTime);

        // Update Animator parameters
        if (characterAnimator != null)
        {
            characterAnimator.SetFloat(_animIDSpeed, _currentHorizontalVelocity.magnitude);
            characterAnimator.SetFloat(_animIDMotionSpeed, input.magnitude);
        }
    }

    #endregion

    #region Gravity & Jump

    private void HandleGravityAndJump()
    {
        // Jump buffer tracking
        if (JumpAction != null && JumpAction.WasPressedThisFrame())
        {
            _jumpBufferCounter = jumpBufferDuration;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }

        // Grounded state
        if (_isGrounded)
        {
            if (characterAnimator != null)
            {
                characterAnimator.SetBool(_animIDJump, false);
                characterAnimator.SetBool(_animIDFreeFall, false);
            }

            // Reset vertical velocity with small stick force when grounded
            if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = groundedGravity;
            }

            // Execute jump if buffered and within coyote time
            if (_jumpBufferCounter > 0.0f && _coyoteTimeCounter > 0.0f)
            {
                // Velocity needed to reach desired jump height: v = sqrt(2 * h * |g|)
                _verticalVelocity = Mathf.Sqrt(2.0f * jumpHeight * Mathf.Abs(gravity));
                _jumpBufferCounter = 0.0f;
                _coyoteTimeCounter = 0.0f;

                if (characterAnimator != null)
                {
                    characterAnimator.SetBool(_animIDJump, true);
                }
            }
        }
        else
        {
            // Apply gravity over time
            _verticalVelocity += gravity * Time.deltaTime;

            if (_verticalVelocity < -2.0f && characterAnimator != null)
            {
                characterAnimator.SetBool(_animIDFreeFall, true);
            }

            // Clamp to terminal velocity
            if (_verticalVelocity < terminalVelocity)
            {
                _verticalVelocity = terminalVelocity;
            }
        }

        // Apply vertical movement
        _characterController.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
    }

    #endregion

    #region Head Bobbing

    private void HandleHeadBob()
    {
        if (!enableHeadBob || playerCamera == null) return;

        float horizontalSpeed = new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude;

        // Only bob when grounded and moving above a minimal threshold
        if (_isGrounded && horizontalSpeed > 0.2f)
        {
            bool isSprinting = horizontalSpeed > (walkSpeed + 0.5f);
            float speedMultiplier = isSprinting ? sprintBobMultiplier : 1.0f;

            _bobTimer += Time.deltaTime * walkBobFrequency * speedMultiplier;

            // Calculate vertical and subtle lateral sinusoidal offset
            float bobOffsetY = Mathf.Sin(_bobTimer) * walkBobAmplitude;
            float bobOffsetX = Mathf.Cos(_bobTimer * 0.5f) * (walkBobAmplitude * 0.6f);

            Vector3 targetPosition = _cameraInitialLocalPosition + new Vector3(bobOffsetX, bobOffsetY, 0f);
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetPosition, bobResetSpeed * Time.deltaTime);
        }
        else
        {
            // Smoothly reset back to the original eye height when idle or mid-air
            _bobTimer = 0.0f;
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, _cameraInitialLocalPosition, bobResetSpeed * Time.deltaTime);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Visualize the ground check sphere in Editor Scene View
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 spherePosition = new Vector3(
            transform.position.x,
            transform.position.y + groundCheckOffset,
            transform.position.z
        );
        Gizmos.DrawWireSphere(spherePosition, groundCheckRadius);
    }

    #endregion
}
