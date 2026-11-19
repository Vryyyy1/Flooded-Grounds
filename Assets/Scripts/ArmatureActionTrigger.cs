using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Triggers action animations (such as Attack / Punch / Action) on an Armature character's Animator.
/// Supports Unity's new Input System (Left Mouse Click, E key, Gamepad button) or optional InputActionReference.
/// </summary>
[AddComponentMenu("Armature/Armature Action Trigger")]
public class ArmatureActionTrigger : MonoBehaviour
{
    [Header("Animator Setup")]
    [Tooltip("The Animator component on the Armature character. Auto-detected if left empty.")]
    [SerializeField] private Animator characterAnimator;
    [Tooltip("Name of the trigger parameter in the Animator Controller.")]
    [SerializeField] private string triggerParameterName = "Attack";

    [Header("Input Settings")]
    [Tooltip("Enable Left Mouse Click to trigger the action.")]
    [SerializeField] private bool triggerOnLeftClick = true;
    [Tooltip("Enable 'E' key to trigger the action.")]
    [SerializeField] private bool triggerOnEKey = true;
    [Tooltip("Optional custom InputAction reference (e.g. Attack from InputSystem_Actions).")]
    [SerializeField] private InputActionReference customActionRef;

    [Header("Action Timing & Cooldown")]
    [Tooltip("Minimum time (in seconds) between action triggers.")]
    [SerializeField] private float actionCooldown = 0.55f;

    [Header("Audio & Effects (Optional)")]
    [Tooltip("Optional sound effect to play when the action is triggered.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip actionSoundClip;
    [Tooltip("UnityEvent fired whenever the action triggers (useful for hitboxes, weapon effects, etc.).")]
    public UnityEvent onActionTriggered;

    private float _lastActionTime = -999f;
    private int _triggerHash;

    public bool CanPerformAction => Time.time >= (_lastActionTime + actionCooldown);

    private void Awake()
    {
        // Strictly only allow on Player. If on zombie / enemy, destroy immediately!
        bool isPlayer = CompareTag("Player") || transform.root.CompareTag("Player") || transform.root.name.Contains("Player");
        if (!isPlayer)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        // Auto-locate Animator on this object, in children, or in parent
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<Animator>();
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (characterAnimator == null) characterAnimator = GetComponentInParent<Animator>();

            if (characterAnimator == null)
            {
                Debug.LogWarning($"[ArmatureActionTrigger] No Animator found on '{gameObject.name}' or its hierarchy. Please assign CharacterAnimator.");
            }
        }

        _triggerHash = Animator.StringToHash(triggerParameterName);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (customActionRef != null && customActionRef.action != null)
        {
            customActionRef.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (customActionRef != null && customActionRef.action != null)
        {
            customActionRef.action.Disable();
        }
    }

    private void Update()
    {
        if (CheckInputPressed())
        {
            PerformAction();
        }
    }

    private bool CheckInputPressed()
    {
        // Check if player has a gun equipped
        var weaponMgr = GetComponentInParent<StarterAssets.PlayerWeaponManager>();
        if (weaponMgr == null) weaponMgr = GetComponentInChildren<StarterAssets.PlayerWeaponManager>();
        if (weaponMgr == null)
        {
            var pRoot = GameObject.Find("PlayerCapsule");
            if (pRoot != null) weaponMgr = pRoot.GetComponent<StarterAssets.PlayerWeaponManager>();
        }
        bool hasGun = (weaponMgr != null && weaponMgr.HasGunEquipped);

        // 1. Custom InputAction reference (only if unarmed)
        if (!hasGun && customActionRef != null && customActionRef.action != null && customActionRef.action.WasPressedThisFrame())
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        // 2. Mouse left click (ONLY allowed when unarmed / no gun equipped)
        if (!hasGun && triggerOnLeftClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        // 3. E key (can be used for melee punch if needed)
        if (triggerOnEKey && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !hasGun)
        {
            return true;
        }

        // 4. Gamepad button West / X (only if unarmed)
        if (!hasGun && Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
        {
            return true;
        }
#else
        if (!hasGun && triggerOnLeftClick && Input.GetMouseButtonDown(0)) return true;
        if (!hasGun && triggerOnEKey && Input.GetKeyDown(KeyCode.E)) return true;
#endif

        return false;
    }

    private bool HasParameter(Animator anim, int paramHash)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        var parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == paramHash) return true;
        }
        return false;
    }

    /// <summary>
    /// Executes the action animation on the Armature character.
    /// Can also be called directly from other scripts or UI buttons.
    /// </summary>
    public bool PerformAction()
    {
        if (!CanPerformAction) return false;

        _lastActionTime = Time.time;

        if (characterAnimator != null && HasParameter(characterAnimator, _triggerHash))
        {
            characterAnimator.ResetTrigger(_triggerHash);
            characterAnimator.SetTrigger(_triggerHash);
        }

        // Optional sound
        if (audioSource != null && actionSoundClip != null)
        {
            audioSource.PlayOneShot(actionSoundClip);
        }

        onActionTriggered?.Invoke();
        return true;
    }

    // AnimationEvent receivers for StarterAssets animation clips (Walk, Run, JumpLand)
    private void OnFootstep(AnimationEvent animationEvent)
    {
        // Silently handled or play footstep sound if available
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        // Silently handled or play landing sound if available
    }
}
