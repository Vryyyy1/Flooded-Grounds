using UnityEngine;

/// <summary>
/// Receives all AnimationEvents fired by character animations (Locomotion, Landing, Weapons, Low Poly Shooter Pack).
/// Attach this to the Armature (where the Animator is) and PlayerCapsule.
/// Prevents "AnimationEvent has no receiver" errors that trigger Error Pause.
/// </summary>
public class FootstepHandler : MonoBehaviour
{
    [Header("Optional Audio")]
    public AudioClip[] footstepClips;
    public AudioClip landingClip;
    [Range(0f, 1f)] public float footstepVolume = 0.4f;

    [Header("Optional VFX")]
    public GameObject dustParticlePrefab;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.spatialBlend = 1f;
        _audioSource.playOnAwake = false;
    }

    // ─────────────────────────────────────────────────────────────
    // Locomotion & Landing AnimationEvents (StarterAssets)
    // ─────────────────────────────────────────────────────────────

    public void OnFootstep()
    {
        if (footstepClips != null && footstepClips.Length > 0)
        {
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip != null)
                _audioSource.PlayOneShot(clip, footstepVolume);
        }

        if (dustParticlePrefab != null)
        {
            GameObject vfx = Instantiate(dustParticlePrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }
    }

    public void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent != null && animationEvent.animatorClipInfo.weight < 0.3f) return;
        OnFootstep();
    }

    public void OnFootstepRight() => OnFootstep();
    public void OnFootstepLeft() => OnFootstep();
    public void OnFootstepRight(AnimationEvent evt) => OnFootstep(evt);
    public void OnFootstepLeft(AnimationEvent evt) => OnFootstep(evt);

    /// <summary>
    /// Fired by Walk_N_Land and Run_N_Land clips when feet hit the ground.
    /// </summary>
    public void OnLand()
    {
        if (landingClip != null)
        {
            _audioSource.PlayOneShot(landingClip, footstepVolume * 1.2f);
        }
        else if (footstepClips != null && footstepClips.Length > 0)
        {
            AudioClip clip = footstepClips[0];
            if (clip != null) _audioSource.PlayOneShot(clip, footstepVolume);
        }

        if (dustParticlePrefab != null)
        {
            GameObject vfx = Instantiate(dustParticlePrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }
    }

    public void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent != null && animationEvent.animatorClipInfo.weight < 0.3f) return;
        OnLand();
    }

    // ─────────────────────────────────────────────────────────────
    // Low Poly Shooter Pack / Weapon AnimationEvents
    // ─────────────────────────────────────────────────────────────

    public void OnEjectCasing()
    {
        // Handled directly or by PlayerWeaponManager
        var pwm = GetComponentInParent<StarterAssets.PlayerWeaponManager>();
        if (pwm != null && pwm.ejectShellCasings)
        {
            pwm.EjectShellCasing();
        }
    }

    public void OnEjectCasing(AnimationEvent evt) => OnEjectCasing();

    public void OnAmmunitionFill() { }
    public void OnAmmunitionFill(AnimationEvent evt) { }

    public void OnSlideBack() { }
    public void OnSlideBack(AnimationEvent evt) { }

    public void OnAnimationEndedReload() { }
    public void OnAnimationEndedReload(AnimationEvent evt) { }

    public void OnAnimationEndedHolster() { }
    public void OnAnimationEndedHolster(AnimationEvent evt) { }

    public void OnAnimationEndedInspect() { }
    public void OnAnimationEndedInspect(AnimationEvent evt) { }
}
