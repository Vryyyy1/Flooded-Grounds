using System;
using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Implements the found-footage / bodycam camcorder camera HUD overlay matching the reference design:
    /// - 4 corner viewfinder framing brackets
    /// - Real-time running timecode (00:00:09:05)
    /// - REC status with blinking red recording dot
    /// - Battery icon with charge bars
    /// - DATE indicator
    /// And applies an authentic handheld / bodycam walking motion effect (vertical bob, horizontal sway, roll tilt, pitch nod, landing dip).
    /// </summary>
    public class CamcorderCameraEffect : MonoBehaviour
    {
        [Header("Camcorder Overlay Settings")]
        [Tooltip("Toggle the camcorder HUD overlay on/off")]
        public bool enableCamcorderOverlay = false;

        [Tooltip("Format for the bottom-right date text")]
        public string dateLabel = "DATE";
        public string customDateString = "";

        [Header("Walking Motion Effect Settings")]
        [Tooltip("Toggle the dynamic walking motion effect on/off")]
        public bool enableWalkMotion = true;

        [Tooltip("Master motion multiplier to easily dial down or increase overall intensity")]
        [Range(0.0f, 2.0f)]
        public float motionIntensity = 0.65f;

        [Tooltip("Cadence/step frequency while walking")]
        public float walkStepFrequency = 8.8f;

        [Tooltip("Cadence/step frequency while sprinting")]
        public float sprintStepFrequency = 12.0f;

        [Tooltip("Vertical bob height in meters")]
        public float verticalBobAmount = 0.009f;

        [Tooltip("Horizontal sway width in meters")]
        public float horizontalSwayAmount = 0.0055f;

        [Tooltip("Camera roll tilt angle on footstep in degrees")]
        public float rollTiltAngle = 0.38f;

        [Tooltip("Camera pitch nod on footstep in degrees")]
        public float pitchNodAngle = 0.22f;

        [Tooltip("Compression dip on landing in meters")]
        public float landingDipAmount = 0.015f;

        [Tooltip("Gentle idle breathing float in meters")]
        public float idleBreathAmount = 0.0012f;

        // Current Camera Offsets (read by FirstPersonController in LateUpdate)
        public Vector3 CurrentPositionOffset { get; private set; } = Vector3.zero;
        public Quaternion CurrentRotationOffset { get; private set; } = Quaternion.identity;

        // References
        private FirstPersonController _fpc;
        private CharacterController _characterController;
        private StarterAssetsInputs _starterInputs;

        // Motion calculation state
        private float _bobTimer = 0f;
        private float _motionWeight = 0f;
        private Vector3 _posVelocity = Vector3.zero;
        private float _landingDip = 0f;
        private float _landingDipVel = 0f;
        private bool _wasGrounded = true;

        // Textures for GUI
        private Texture2D _whiteTex;
        private Texture2D _redDotTex;
        private Texture2D _vignetteTex;
        private GUIStyle _timecodeStyle;
        private GUIStyle _recStyle;
        private GUIStyle _dateStyle;

        private void Awake()
        {
            enableCamcorderOverlay = false;
        }

        private void Start()
        {
            LocatePlayerReferences();
        }

        private void LocatePlayerReferences()
        {
            if (_fpc == null)
            {
                _fpc = GetComponentInParent<FirstPersonController>();
                if (_fpc == null) _fpc = FindAnyObjectByType<FirstPersonController>();
            }

            if (_fpc != null)
            {
                if (_characterController == null)
                {
                    _characterController = _fpc.GetComponent<CharacterController>();
                }
                if (_starterInputs == null)
                {
                    _starterInputs = _fpc.GetComponent<StarterAssetsInputs>();
                }
            }
        }

        private void Update()
        {
            if (_fpc == null) LocatePlayerReferences();

            UpdateWalkMotion();
        }

        private void UpdateWalkMotion()
        {
            // Yield camera motion to cinematic wake-up intro cutscene
            if (CinematicWakeUpIntro.IsIntroActive)
            {
                return;
            }

            if (!enableWalkMotion || _fpc == null || _characterController == null)
            {
                CurrentPositionOffset = Vector3.SmoothDamp(CurrentPositionOffset, Vector3.zero, ref _posVelocity, 0.08f);
                CurrentRotationOffset = Quaternion.Slerp(CurrentRotationOffset, Quaternion.identity, Time.deltaTime * 12f);
                return;
            }

            // Check velocity & movement states
            Vector3 hVel = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
            float speed = hVel.magnitude;

            bool isGrounded = _fpc.Grounded;
            bool isMoving = isGrounded && speed > 0.35f && !_fpc.IsSwimming;
            bool isSprinting = isMoving && (_starterInputs != null && _starterInputs.sprint);
            bool isAiming = PlayerWeaponManager.Instance != null && PlayerWeaponManager.Instance.isAiming;

            // Damping motion when aiming for shooting stability
            float targetWeight = isMoving ? 1.0f : 0.0f;
            if (isAiming) targetWeight *= 0.18f;

            _motionWeight = Mathf.MoveTowards(_motionWeight, targetWeight, Time.deltaTime * 6.5f);

            // Step cadence
            float stepFreq = isSprinting ? sprintStepFrequency : walkStepFrequency;
            float speedRatio = Mathf.Clamp(speed / (isSprinting ? Mathf.Max(1f, _fpc.SprintSpeed) : Mathf.Max(1f, _fpc.MoveSpeed)), 0.35f, 1.35f);

            if (isMoving)
            {
                _bobTimer += Time.deltaTime * stepFreq * speedRatio;
            }

            float effIntensity = Mathf.Max(0f, motionIntensity);

            // 1. Vertical Bob (two dips per stride cycle)
            float bobY = -Mathf.Abs(Mathf.Sin(_bobTimer)) * (isSprinting ? verticalBobAmount * 1.25f : verticalBobAmount) * _motionWeight * effIntensity;

            // 2. Horizontal Sway (one sway left, one sway right per stride)
            float bobX = Mathf.Cos(_bobTimer) * (isSprinting ? horizontalSwayAmount * 1.20f : horizontalSwayAmount) * _motionWeight * effIntensity;

            // 3. Roll Tilt (camera rolls gently into the supporting foot)
            float roll = Mathf.Sin(_bobTimer) * (isSprinting ? rollTiltAngle * 1.30f : rollTiltAngle) * _motionWeight * effIntensity;

            // 4. Pitch Nod (camera nods slightly downward on foot impact)
            float pitch = Mathf.Cos(_bobTimer * 2f) * (isSprinting ? pitchNodAngle * 1.20f : pitchNodAngle) * _motionWeight * effIntensity;

            // 5. Idle Handheld Breathing Float when stationary
            if (!isMoving)
            {
                float idleTimer = Time.time * 1.4f;
                float idleWeight = 1f - _motionWeight;
                bobY += Mathf.Sin(idleTimer) * idleBreathAmount * idleWeight * effIntensity;
                bobX += Mathf.Cos(idleTimer * 0.5f) * (idleBreathAmount * 0.5f) * idleWeight * effIntensity;
                roll += Mathf.Sin(idleTimer * 0.6f) * (rollTiltAngle * 0.25f) * idleWeight * effIntensity;
            }

            // 6. Jump Landing Impact Compression
            if (!_wasGrounded && isGrounded)
            {
                _landingDipVel = -landingDipAmount * effIntensity;
            }
            _wasGrounded = isGrounded;

            _landingDip = Mathf.SmoothDamp(_landingDip, 0f, ref _landingDipVel, 0.16f);
            bobY += _landingDip;

            // Smoothly interpolate position and rotation offsets
            Vector3 targetPos = new Vector3(bobX, bobY, 0f);
            CurrentPositionOffset = Vector3.SmoothDamp(CurrentPositionOffset, targetPos, ref _posVelocity, 0.075f);

            Quaternion targetRot = Quaternion.Euler(pitch, 0f, roll);
            CurrentRotationOffset = Quaternion.Slerp(CurrentRotationOffset, targetRot, Time.deltaTime * 12f);

            if (_fpc != null)
            {
                _fpc.CameraPositionOffset = CurrentPositionOffset;
                _fpc.CameraRotationOffset = CurrentRotationOffset;
            }
        }

        private void LateUpdate()
        {
            // Fallback direct camera transform update if FPC is not driving the offsets
            if (_fpc == null && transform.parent != null)
            {
                transform.localPosition = CurrentPositionOffset;
                transform.localRotation = CurrentRotationOffset;
            }
        }

        // =========================================================================
        // CAMCORDER VIEWFINDER HUD OVERLAY
        // =========================================================================
        private void InitTextures()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            if (_redDotTex == null)
            {
                _redDotTex = CreateSmoothCircleTexture(32, new Color(0.95f, 0.10f, 0.10f, 1.0f));
            }

            if (_vignetteTex == null)
            {
                _vignetteTex = CreateVignetteTexture(256);
            }
        }

        private static Texture2D CreateSmoothCircleTexture(int size, Color col)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 1.2f;
            Color[] pix = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius + 0.5f - d);
                    pix[y * size + x] = new Color(col.r, col.g, col.b, col.a * a);
                }
            }

            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateVignetteTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float maxDist = center * 1.4142f;
            Color[] pix = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float vignette = Mathf.Clamp01((dist - 0.72f) / 0.48f);
                    vignette = vignette * vignette;
                    pix[y * size + x] = new Color(0f, 0f, 0f, vignette * 0.35f);
                }
            }

            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            // Camcorder record effect completely removed per user request
            return;
        }
    }
}
