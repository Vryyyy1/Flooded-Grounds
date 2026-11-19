using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Cinematic first-person Wake-Up sequence played after pressing start:
    /// 1. Fades in from a black screen like eyes slowly opening on the damp wooden tracks.
    /// 2. Gradually rises from the damp track (propping up onto elbows, standing to full height).
    /// 3. Looks around the eerie woodland (panning left and right across the mist, autumn trees, and rafters).
    /// 4. Steps forward into the game (physically walking forward along the tracks with damp footsteps, raising weapon, and handing over full player control).
    /// Includes [SPACE] SKIP option anytime.
    /// </summary>
    public class CinematicWakeUpIntro : MonoBehaviour
    {
        public static CinematicWakeUpIntro Instance { get; private set; }
        public static bool IsIntroActive { get; private set; } = false;

        [Header("Cinematic State")]
        [SerializeField] private bool _isPlaying = false;
        [SerializeField] private float _introTimer = 0f;
        [SerializeField] private float _introDuration = 14.5f;

        // References
        private FirstPersonController _fpc;
        private CharacterController _characterController;
        private Camera _mainCamera;
        private Transform _camRoot;
        private Transform _weaponHolder;
        private AudioSource _audioSource;

        // Procedural Audio Clips
        private AudioClip _heartbeatClip;
        private AudioClip _gaspBreathClip;
        private AudioClip _exhaleClip;
        private AudioClip _footstepClip;
        private AudioClip _camcorderBeepClip;

        // Visual textures for fade, letterbox & eyelids
        private Texture2D _blackTex;
        private Texture2D _eyelidGradientTex;

        // Eyelid & screen fade state
        private float _blackScreenFadeAlpha = 1f; // 1 = pitch black, 0 = transparent
        private float _eyelidClosure = 1f;        // 1 = eyes fully closed, 0 = eyes fully open
        private float _letterboxProgress = 1f;     // 1 = widescreen bars on, 0 = slid off-screen
        private float _subtitleAlpha = 0f;
        private string _subtitleText = "Ugh... my head... What happened to this place?";

        // Step sound triggers
        private bool _playedStep1 = false;
        private bool _playedStep2 = false;

        // Mission objective prompt at the end
        private float _objectiveAlpha = 0f;

        // Skip protection
        private const float SkipGracePeriod = 1.5f;
        private int _startFrame = -100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (sceneName.Contains("Scene_A") || sceneName.Contains("SampleScene") || sceneName.Contains("ZombieApo"))
                {
                    GameObject go = new GameObject("CinematicWakeUpIntro");
                    go.AddComponent<CinematicWakeUpIntro>();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitTextures();
            InitProceduralAudio();

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D direct stereo sound
            _audioSource.ignoreListenerPause = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// RESTART reloads the scene: stop any running intro and drop stale references
        /// so the wake-up plays fresh when PLAY is pressed again.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            _isPlaying = false;
            IsIntroActive = false;
            _objectiveAlpha = 0f;
            _subtitleAlpha = 0f;
            _fpc = null;
            _characterController = null;
            _mainCamera = null;
            _camRoot = null;
            _weaponHolder = null;
        }

        private void InitTextures()
        {
            if (_blackTex == null)
            {
                _blackTex = new Texture2D(1, 1);
                _blackTex.SetPixel(0, 0, Color.black);
                _blackTex.Apply();
            }

            if (_eyelidGradientTex == null)
            {
                int h = 64;
                _eyelidGradientTex = new Texture2D(1, h, TextureFormat.RGBA32, false);
                _eyelidGradientTex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < h; y++)
                {
                    float t = (float)y / (h - 1);
                    float alpha = Mathf.Pow(t, 2.0f);
                    _eyelidGradientTex.SetPixel(0, y, new Color(0f, 0f, 0f, alpha));
                }
                _eyelidGradientTex.Apply();
            }
        }

        private void InitProceduralAudio()
        {
            if (_heartbeatClip == null) _heartbeatClip = CreateSynthHeartbeat();
            if (_gaspBreathClip == null) _gaspBreathClip = CreateSynthGaspBreath();
            if (_exhaleClip == null) _exhaleClip = CreateSynthExhale();
            if (_footstepClip == null) _footstepClip = CreateSynthFootstep();
            if (_camcorderBeepClip == null) _camcorderBeepClip = CreateSynthCamBeep();
        }

        private void LocatePlayerReferences()
        {
            if (_fpc == null)
            {
                var player = GameObject.Find("PlayerCapsule");
                if (player != null)
                {
                    _fpc = player.GetComponent<FirstPersonController>();
                }
                if (_fpc == null)
                {
                    _fpc = Object.FindAnyObjectByType<FirstPersonController>();
                }
            }

            if (_fpc != null && _characterController == null)
            {
                _characterController = _fpc.GetComponent<CharacterController>();
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_camRoot == null && _fpc != null)
            {
                _camRoot = _fpc.transform.Find("PlayerCameraRoot");
            }

            if (_weaponHolder == null && _camRoot != null)
            {
                _weaponHolder = _camRoot.Find("FP_WeaponHolder");
            }
        }

        /// <summary>
        /// Initiates the cinematic wake-up cutscene.
        /// </summary>
        public void StartIntroSequence()
        {
            LocatePlayerReferences();

            _isPlaying = true;
            IsIntroActive = true;
            _introTimer = 0f;
            _blackScreenFadeAlpha = 1f; // Starts in pure black
            _eyelidClosure = 1f;        // Eyes closed
            _letterboxProgress = 1f;
            _subtitleAlpha = 0f;
            _objectiveAlpha = 0f;
            _playedStep1 = false;
            _playedStep2 = false;
            _startFrame = Time.frameCount;

            // Block normal player inputs during the cutscene
            StarterAssetsInputs.InputBlocked = true;

            // Lower/hide weapon while waking up on the damp track
            if (_weaponHolder != null)
            {
                _weaponHolder.gameObject.SetActive(false);
            }

            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Disable camcorder overlay during intro
            if (_mainCamera != null)
            {
                var camcorder = _mainCamera.GetComponent<CamcorderCameraEffect>();
                if (camcorder != null)
                {
                    camcorder.enableCamcorderOverlay = false;
                }
            }

            // Immediately position camera low to the damp bridge track
            ApplyCameraPose(-1.18f, 4.0f, -6.0f, 24.0f);

            // Initial heartbeat in the dark
            PlaySound(_heartbeatClip, 0.95f);
        }

        private void Update()
        {
            if (!_isPlaying) return;

            // Freeze the cutscene while the pause menu is open (ESC)
            if (GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuOpen) return;

            LocatePlayerReferences();

            // Ensure input remains locked during the intro
            StarterAssetsInputs.InputBlocked = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Check for skip input ([SPACE], after a short grace period)
            if (WasSkipPressed())
            {
                FinishIntro(instant: false);
                return;
            }

            _introTimer += Time.deltaTime;

            float posY = -1.18f;
            float pitch = 4.0f;
            float yaw = -6.0f;
            float roll = 24.0f;
            float forwardStepSpeed = 0f;
            float stepBobY = 0f;
            float stepRoll = 0f;

            // Atmospheric audio triggers
            if (_introTimer >= 1.1f && _introTimer - Time.deltaTime < 1.1f)
            {
                PlaySound(_gaspBreathClip, 0.75f);
            }
            if (_introTimer >= 2.4f && _introTimer - Time.deltaTime < 2.4f)
            {
                PlaySound(_heartbeatClip, 0.85f);
            }
            if (_introTimer >= 6.8f && _introTimer - Time.deltaTime < 6.8f)
            {
                PlaySound(_exhaleClip, 0.65f);
            }

            // =====================================================================
            // CINEMATIC CHOREOGRAPHY
            // =====================================================================

            if (_introTimer < 4.2f)
            {
                // ================= 1. FADE IN FROM BLACK SCREEN LIKE EYES OPENING =================
                // 0.0 - 1.0s: Pure black screen, silence and low breath
                // 1.0 - 2.2s: First slit of light! Black screen fades out, eyelids open halfway, revealing damp tracks
                // 2.2 - 2.8s: Eyelids flutter and heavily blink shut
                // 2.8 - 4.2s: Eyes open wide and damp track comes into clear view

                if (_introTimer < 1.0f)
                {
                    _blackScreenFadeAlpha = 1f;
                    _eyelidClosure = 1f;
                }
                else if (_introTimer < 2.2f)
                {
                    float bt = (_introTimer - 1.0f) / 1.2f;
                    float ease = Mathf.SmoothStep(0f, 1f, bt);
                    _blackScreenFadeAlpha = Mathf.Lerp(1.0f, 0.0f, ease);
                    _eyelidClosure = Mathf.Lerp(1.0f, 0.45f, ease);
                }
                else if (_introTimer < 2.8f)
                {
                    _blackScreenFadeAlpha = 0f;
                    float bt = (_introTimer - 2.2f) / 0.6f;
                    _eyelidClosure = Mathf.Lerp(0.45f, 0.95f, Mathf.SmoothStep(0f, 1f, bt));
                }
                else
                {
                    _blackScreenFadeAlpha = 0f;
                    float bt = (_introTimer - 2.8f) / 1.4f;
                    _eyelidClosure = Mathf.Lerp(0.95f, 0.0f, Mathf.SmoothStep(0f, 1f, bt));
                }

                // Gentle breathing pulse right on the damp track
                float breath = Mathf.Sin(_introTimer * 3.2f) * 0.015f;
                posY = -1.18f + breath;
                pitch = 4.0f + Mathf.Sin(_introTimer * 1.8f) * 1.0f;
                yaw = -6.0f;
                roll = 24.0f + Mathf.Cos(_introTimer * 1.4f) * 1.2f;
            }
            else if (_introTimer < 7.5f)
            {
                // ================= 2. GRADUALLY RISING FROM THE DAMP TRACK =================
                // 4.2 - 5.8s: Propping up onto elbows & knees (Y rises from -1.18 to -0.65m, roll eases to 8 deg)
                // 5.8 - 7.5s: Standing up to full height (Y rises from -0.65 to 0.0m, roll levels to 0 deg)
                _blackScreenFadeAlpha = 0f;
                _eyelidClosure = Mathf.MoveTowards(_eyelidClosure, 0f, Time.deltaTime * 3f);

                if (_introTimer < 5.8f)
                {
                    float t = (_introTimer - 4.2f) / 1.6f;
                    float ease = Mathf.SmoothStep(0f, 1f, t);
                    posY = Mathf.Lerp(-1.18f, -0.65f, ease);
                    roll = Mathf.Lerp(24.0f, 8.0f, ease);
                    yaw = Mathf.Lerp(-6.0f, -18.0f, ease);
                    pitch = Mathf.Lerp(4.0f, 2.0f, ease);
                }
                else
                {
                    float t = (_introTimer - 5.8f) / 1.7f;
                    float ease = Mathf.SmoothStep(0f, 1f, t);
                    posY = Mathf.Lerp(-0.65f, 0.0f, ease);
                    roll = Mathf.Lerp(8.0f, 0.0f, ease);
                    yaw = Mathf.Lerp(-18.0f, 0.0f, ease);
                    pitch = Mathf.Lerp(2.0f, 0.0f, ease);

                    // Balance shift as feet firmly find the wooden track
                    float balance = Mathf.Sin(t * Mathf.PI * 2f) * 0.018f * (1f - t);
                    posY += balance;
                }
            }
            else if (_introTimer < 11.8f)
            {
                // ================= 3. LOOKING AROUND THE EERIE WOODLAND =================
                // Standing upright on the bridge. Slowly pans left to take in the eerie red autumn trees and mist,
                // then pans across to the right side of the woodland, and centers forward.
                posY = 0.0f;
                roll = 0.0f;

                float t = (_introTimer - 7.5f) / 4.3f; // 0 to 1

                if (t < 0.40f)
                {
                    // Slowly pan left into the eerie trees and wooden pillars
                    float pT = t / 0.40f;
                    float ease = Mathf.SmoothStep(0f, 1f, pT);
                    yaw = Mathf.Lerp(0.0f, -42.0f, ease);
                    pitch = Mathf.Lerp(0.0f, 5.0f, ease);
                }
                else if (t < 0.78f)
                {
                    // Smoothly pan across center to the right side of the woodland
                    float pT = (t - 0.40f) / 0.38f;
                    float ease = Mathf.SmoothStep(0f, 1f, pT);
                    yaw = Mathf.Lerp(-42.0f, 38.0f, ease);
                    pitch = Mathf.Lerp(5.0f, -2.5f, ease);
                }
                else
                {
                    // Smoothly center head down the bridge track
                    float pT = (t - 0.78f) / 0.22f;
                    float ease = Mathf.SmoothStep(0f, 1f, pT);
                    yaw = Mathf.Lerp(38.0f, 0.0f, ease);
                    pitch = Mathf.Lerp(-2.5f, 0.0f, ease);
                }

                // Subtitle display during looking around
                if (t > 0.15f && t < 0.70f)
                {
                    _subtitleAlpha = Mathf.MoveTowards(_subtitleAlpha, 1.0f, Time.deltaTime * 2.0f);
                }
                else
                {
                    _subtitleAlpha = Mathf.MoveTowards(_subtitleAlpha, 0f, Time.deltaTime * 2.5f);
                }
            }
            else if (_introTimer < _introDuration)
            {
                // ================= 4. STEPPING FORWARD INTO THE GAME =================
                // Character takes two steady steps forward along the damp track.
                // Camera bobs and tilts with each footstep on the damp wood.
                // Letterbox bars slide away, weapon smoothly raises into hands, and controls unlock.
                float t = (_introTimer - 11.8f) / (_introDuration - 11.8f); // 0 to 1
                yaw = 0.0f;
                pitch = 0.0f;

                // Forward stepping speed smoothly accelerates to walking speed
                forwardStepSpeed = Mathf.Lerp(1.1f, 2.3f, Mathf.Clamp01(t * 1.5f));

                // Step 1: Left foot at 12.3s
                if (_introTimer >= 12.3f && !_playedStep1)
                {
                    _playedStep1 = true;
                    PlaySound(_footstepClip, 0.85f);
                }

                // Step 2: Right foot at 13.2s
                if (_introTimer >= 13.2f && !_playedStep2)
                {
                    _playedStep2 = true;
                    PlaySound(_footstepClip, 0.90f);
                }

                // Natural walking head bob & sway during the step forward
                float stepCycle = (_introTimer - 11.8f) * 7.5f;
                stepBobY = -Mathf.Abs(Mathf.Sin(stepCycle)) * 0.032f;
                stepRoll = Mathf.Sin(stepCycle) * 1.4f;
                posY = stepBobY;
                roll = stepRoll;

                // Letterbox bars slide off-screen
                _letterboxProgress = Mathf.Lerp(1.0f, 0.0f, Mathf.Clamp01(t * 1.6f));
                _objectiveAlpha = Mathf.Lerp(0.0f, 1.0f, Mathf.Clamp01(t * 1.8f));

                // Unhide and draw weapon up around 12.6s
                if (_introTimer >= 12.6f && _weaponHolder != null && !_weaponHolder.gameObject.activeSelf)
                {
                    _weaponHolder.gameObject.SetActive(true);
                }

                // Camcorder HUD electronic chirp
                if (_introTimer >= 13.4f && _introTimer - Time.deltaTime < 13.4f)
                {
                    PlaySound(_camcorderBeepClip, 0.90f);
                }
            }
            else
            {
                // Cutscene fully complete! Hand over full control
                FinishIntro(instant: false);
                return;
            }

            // Physically move the character controller forward during the stepping phase
            if (forwardStepSpeed > 0.01f && _characterController != null && _characterController.enabled)
            {
                Vector3 forwardDir = _fpc != null ? _fpc.transform.forward : Vector3.forward;
                Vector3 moveDelta = (forwardDir * forwardStepSpeed + Vector3.down * 9.81f) * Time.deltaTime;
                _characterController.Move(moveDelta);
            }

            ApplyCameraPose(posY, pitch, yaw, roll);
        }

        private void ApplyCameraPose(float posY, float pitch, float yaw, float roll)
        {
            Vector3 posOffset = new Vector3(0f, posY, 0f);
            Quaternion rotOffset = Quaternion.Euler(pitch, yaw, roll);

            if (_fpc != null)
            {
                _fpc.CameraPositionOffset = posOffset;
                _fpc.CameraRotationOffset = rotOffset;
            }

            if (_mainCamera != null)
            {
                _mainCamera.transform.localPosition = posOffset;
                _mainCamera.transform.localRotation = rotOffset;
            }
        }

        private bool WasSkipPressed()
        {
            // Grace period: ignore the same SPACE/ENTER/click that pressed PLAY on the menu,
            // so the intro is never skipped the instant it begins.
            if (_introTimer < SkipGracePeriod || Time.frameCount <= _startFrame + 2) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Space)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Concludes the cinematic intro and hands full control to the player.
        /// </summary>
        public void FinishIntro(bool instant)
        {
            _isPlaying = false;
            IsIntroActive = false;

            _blackScreenFadeAlpha = 0f;
            _eyelidClosure = 0f;
            _letterboxProgress = 0f;
            _subtitleAlpha = 0f;

            // Reset camera offsets to neutral
            ApplyCameraPose(0f, 0f, 0f, 0f);

            // Restore weapon
            if (_weaponHolder != null)
            {
                _weaponHolder.gameObject.SetActive(true);
            }

            // Ensure Camcorder HUD overlay remains disabled
            if (_mainCamera != null)
            {
                var camcorder = _mainCamera.GetComponent<CamcorderCameraEffect>();
                if (camcorder != null)
                {
                    camcorder.enableCamcorderOverlay = false;
                }
            }

            // Unlock player input completely
            StarterAssetsInputs.InputBlocked = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Brief objective banner on screen for 4 seconds
            StartCoroutine(ShowObjectiveBannerCoroutine());
        }

        private IEnumerator ShowObjectiveBannerCoroutine()
        {
            _objectiveAlpha = 1.0f;
            yield return new WaitForSeconds(3.5f);

            while (_objectiveAlpha > 0f)
            {
                _objectiveAlpha -= Time.deltaTime * 1.5f;
                yield return null;
            }
            _objectiveAlpha = 0f;
        }

        private void OnGUI()
        {
            if (!_isPlaying && _objectiveAlpha <= 0.01f) return;

            GUI.depth = -90; // Render on top of scene and HUD

            float sw = Screen.width;
            float sh = Screen.height;

            // ================= 1. PURE BLACK SCREEN FADE-IN =================
            if (_blackScreenFadeAlpha > 0.005f)
            {
                GUI.color = new Color(0f, 0f, 0f, _blackScreenFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _blackTex);
            }

            // ================= 2. CINEMATIC WIDESCREEN LETTERBOX BARS =================
            if (_letterboxProgress > 0.01f)
            {
                float barHeight = sh * 0.11f * _letterboxProgress;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, sw, barHeight), _blackTex);
                GUI.DrawTexture(new Rect(0, sh - barHeight, sw, barHeight), _blackTex);
            }

            // ================= 3. EYES SLOWLY OPENING (EYELIDS BLINK) =================
            if (_eyelidClosure > 0.005f)
            {
                GUI.color = Color.black;
                float halfHeight = sh * 0.5f;
                float closeAmount = halfHeight * _eyelidClosure;

                // Top eyelid
                GUI.DrawTexture(new Rect(0, 0, sw, closeAmount), _blackTex);
                // Bottom eyelid
                GUI.DrawTexture(new Rect(0, sh - closeAmount, sw, closeAmount), _blackTex);

                // Soft eyelash edge feathering
                if (_eyelidGradientTex != null && closeAmount < halfHeight)
                {
                    float featherH = Mathf.Clamp(sh * 0.06f, 20f, 60f);
                    GUI.DrawTexture(new Rect(0, closeAmount, sw, featherH), _eyelidGradientTex, ScaleMode.StretchToFill);

                    Matrix4x4 origMatrix = GUI.matrix;
                    // Flip around the feather strip's own centre so the soft edge sits just above the bottom eyelid
                    float bottomFeatherTop = sh - closeAmount - featherH;
                    GUIUtility.ScaleAroundPivot(new Vector2(1f, -1f), new Vector2(sw * 0.5f, bottomFeatherTop + featherH * 0.5f));
                    GUI.DrawTexture(new Rect(0, bottomFeatherTop, sw, featherH), _eyelidGradientTex, ScaleMode.StretchToFill);
                    GUI.matrix = origMatrix;
                }
            }

            // Reset tint so subtitles / banners / prompts are not multiplied by black
            GUI.color = Color.white;

            // ================= 4. ATMOSPHERIC SUBTITLE =================
            if (_subtitleAlpha > 0.01f)
            {
                GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                {
                    font = StarterAssets.DialogueFontProvider.Regular,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.028f), 17, 28),
                    fontStyle = FontStyle.Italic,
                    alignment = TextAnchor.MiddleCenter
                };

                Color textColor = new Color(0.95f, 0.95f, 0.90f, _subtitleAlpha);
                Color shadowColor = new Color(0f, 0f, 0f, _subtitleAlpha * 0.90f);

                float subY = sh * 0.82f;
                DrawShadowText(new Rect(0, subY, sw, 40f), _subtitleText, subStyle, textColor, shadowColor);
            }

            // ================= 5. OBJECTIVE BANNER (HANDOVER) =================
            if (_objectiveAlpha > 0.01f)
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = StarterAssets.DialogueFontProvider.Bold,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.034f), 20, 32),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                GUIStyle descStyle = new GUIStyle(GUI.skin.label)
                {
                    font = StarterAssets.DialogueFontProvider.Regular,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.022f), 14, 20),
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter
                };

                Color redHeader = new Color(0.95f, 0.25f, 0.20f, _objectiveAlpha);
                Color whiteDesc = new Color(0.90f, 0.90f, 0.90f, _objectiveAlpha * 0.90f);
                Color shadowColor = new Color(0f, 0f, 0f, _objectiveAlpha * 0.85f);

                float startY = sh * 0.18f;
                DrawShadowText(new Rect(0, startY, sw, 40f), "▲ SURVIVE THE INFESTATION", titleStyle, redHeader, shadowColor);
                DrawShadowText(new Rect(0, startY + 36f, sw, 30f), "Search the abandoned area for supplies, weapons, and an escape route.", descStyle, whiteDesc, shadowColor);
            }

            // ================= 6. SKIP PROMPT =================
            if (_isPlaying && _introTimer > SkipGracePeriod)
            {
                GUIStyle skipStyle = new GUIStyle(GUI.skin.label)
                {
                    font = StarterAssets.DialogueFontProvider.Bold,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.020f), 12, 16),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                };

                float alpha = Mathf.Clamp01((_introTimer - SkipGracePeriod) * 1.5f);
                Color skipColor = new Color(0.75f, 0.75f, 0.75f, alpha * 0.75f);
                DrawShadowText(new Rect(sw - 260f, sh - 45f, 230f, 30f), "[SPACE] SKIP INTRO", skipStyle, skipColor, new Color(0f, 0f, 0f, alpha * 0.75f));
            }
        }

        private void DrawShadowText(Rect r, string text, GUIStyle style, Color textColor, Color shadowColor)
        {
            Color orig = style.normal.textColor;
            Color prevContent = GUI.contentColor;

            style.normal.textColor = shadowColor;
            GUI.contentColor = shadowColor;
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);

            style.normal.textColor = textColor;
            GUI.contentColor = textColor;
            GUI.Label(r, text, style);

            style.normal.textColor = orig;
            GUI.contentColor = prevContent;
        }

        private void PlaySound(AudioClip clip, float vol)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.97f, 1.03f);
                _audioSource.PlayOneShot(clip, vol);
            }
        }

        // ================= PROCEDURAL SYNTH AUDIO GENERATORS =================

        private static AudioClip CreateSynthHeartbeat()
        {
            int sampleRate = 22050;
            float length = 0.55f;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float thump1 = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 18f);
                float t2 = Mathf.Max(0f, t - 0.16f);
                float thump2 = Mathf.Sin(2f * Mathf.PI * 46f * t2) * Mathf.Exp(-t2 * 22f);
                data[i] = (thump1 * 0.85f + thump2 * 0.65f) * 0.9f;
            }

            AudioClip clip = AudioClip.Create("IntroHeartbeat", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthGaspBreath()
        {
            int sampleRate = 22050;
            float length = 1.1f;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (Random.value * 2f - 1f) * Mathf.Sin(t * Mathf.PI / length);
                float lowHum = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.35f;
                float env = Mathf.Pow(Mathf.Sin(t * Mathf.PI / length), 1.6f);
                data[i] = (noise * 0.45f + lowHum * 0.25f) * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create("IntroGaspBreath", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthExhale()
        {
            int sampleRate = 22050;
            float length = 0.95f;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (Random.value * 2f - 1f);
                float env = Mathf.Exp(-t * 4.2f) * (1f - Mathf.Exp(-t * 25f));
                float lowTone = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.25f;
                data[i] = (noise * 0.38f + lowTone * 0.20f) * env * 0.65f;
            }

            AudioClip clip = AudioClip.Create("IntroExhale", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthFootstep()
        {
            int sampleRate = 22050;
            float length = 0.28f;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);
                float thud = Mathf.Sin(2f * Mathf.PI * 74f * (1f - t * 0.8f) * t);
                float woodClick = Mathf.Sin(2f * Mathf.PI * 290f * t) * Mathf.Exp(-t * 55f);
                float scuff = (Random.value * 2f - 1f) * Mathf.Exp(-t * 20f) * 0.22f;
                data[i] = (thud * 0.72f + woodClick * 0.28f + scuff) * env * 0.85f;
            }

            AudioClip clip = AudioClip.Create("DampWoodFootstep", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthCamBeep()
        {
            int sampleRate = 22050;
            float length = 0.22f;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.10f ? 1400f : 1900f;
                float env = Mathf.Exp(-(t % 0.11f) * 22f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
            }

            AudioClip clip = AudioClip.Create("CamcorderBeep", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
