using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Handles unlocking and escaping on Boat_4 using the looted Rust Key from the player's Backpack.
    /// Drives the cinematic escape sequence:
    /// 1. Outboard engine fires up with a rich mechanical rumble as the boat begins cruising into the ocean.
    /// 2. First-person player is seated at the stern with hands resting at the helm/wheel, watching the island shrink into the distance.
    /// 3. The camera slowly pulls back and drifts upward into a grand overhead shot, rendering the boat a small speck against the vast expanse of water.
    /// 4. The quiet hum of the boat's engine fades into gentle ocean waves rolling against the hull.
    /// 5. Vibrant blues and golds of the horizon slowly desaturate frame-by-frame until the screen is completely silent and pitch-black.
    /// 6. Cinematic title "YOU ESCAPED" fades in on the silent black screen.
    /// </summary>
    public class Boat4EscapeInteraction : MonoBehaviour
    {
        public static Boat4EscapeInteraction Instance { get; private set; }

        [Header("Boat 4 Escape Settings")]
        public float interactDistanceFromHull = 4.8f;
        public bool isUnlocked = false;
        public bool isEscaping = false;

        [Header("Cinematic Timing")]
        public float escapeDuration = 16f;

        // Player & Boat References
        private Transform _playerTransform;
        private CharacterController _playerController;
        private Renderer _boatRenderer;
        private GameObject _helmConsole;

        // Cinematic Camera & Cinemachine References
        private Camera _cinematicCamera;
        private Component _cinemachineBrain;
        private Component[] _disabledCinemachineCams;
        private Vector3 _camStartPos;
        private Quaternion _camStartRot;

        // Audio Sources & Procedural Audio
        private AudioSource _sfxAudioSource;
        private AudioSource _engineAudioSource;
        private AudioSource _waveAudioSource;

        // Post-Processing Desaturation & Fade
        private Volume _cutsceneVolume;
        private UnityEngine.Rendering.Universal.ColorAdjustments _colorAdjustments;
        private Texture2D _blackTex;
        private float _fadeToBlackAlpha = 0f;
        private float _desaturateAlpha = 0f;

        // State Trackers
        private float _escapeTimer = 0f;
        private bool _hasInitiatedCutscene = false;
        public bool IsPlayerInRange { get; private set; }

        private void Awake()
        {
            Instance = this;
            _boatRenderer = GetComponentInChildren<Renderer>();

            // 1. SFX AudioSource (Clicks, Engine Ignition)
            _sfxAudioSource = GetComponent<AudioSource>();
            if (_sfxAudioSource == null)
            {
                _sfxAudioSource = gameObject.AddComponent<AudioSource>();
                _sfxAudioSource.playOnAwake = false;
                _sfxAudioSource.spatialBlend = 0.25f;
            }

            // 2. Continuous Engine Hum Loop AudioSource
            _engineAudioSource = gameObject.AddComponent<AudioSource>();
            _engineAudioSource.playOnAwake = false;
            _engineAudioSource.loop = true;
            _engineAudioSource.spatialBlend = 0.2f;
            _engineAudioSource.clip = CreateEngineHumClip();

            // 3. Gentle Ocean Waves Loop AudioSource
            _waveAudioSource = gameObject.AddComponent<AudioSource>();
            _waveAudioSource.playOnAwake = false;
            _waveAudioSource.loop = true;
            _waveAudioSource.spatialBlend = 0.2f;
            _waveAudioSource.clip = CreateGentleWavesClip();

            // 4. Pure Black Overlay Texture for smooth fading
            _blackTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _blackTex.SetPixel(0, 0, Color.black);
            _blackTex.Apply();

            // 5. Setup runtime URP Post-Processing Volume for frame-by-frame color desaturation
            SetupPostProcessingVolume();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            if (isEscaping)
            {
                UpdateBoatEscapeCinematic();
                CheckEscapeInput();
                return;
            }

            if (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen)
            {
                IsPlayerInRange = false;
                return;
            }

            float distToHull = GetDistanceToBoatHull(_playerTransform.position);
            IsPlayerInRange = distToHull <= interactDistanceFromHull;

            if (IsPlayerInRange && WasInteractPressed())
            {
                TryInteractWithBoat4();
            }
        }

        public float GetDistanceToBoatHull(Vector3 worldPos)
        {
            if (_boatRenderer == null)
                _boatRenderer = GetComponentInChildren<Renderer>();

            if (_boatRenderer != null)
            {
                Vector3 closest = _boatRenderer.bounds.ClosestPoint(worldPos);
                return Vector3.Distance(worldPos, closest);
            }

            return Vector3.Distance(worldPos, transform.position);
        }

        public void TryInteractWithBoat4()
        {
            PlayerInventoryBag bag = PlayerInventoryBag.Instance;
            if (bag == null && _playerTransform != null)
            {
                bag = _playerTransform.GetComponent<PlayerInventoryBag>();
            }

            bool hasKey = isUnlocked || (bag != null && bag.HasItem(SurvivalToolType.RustKey));

            if (!hasKey)
            {
                PlayBoatKeyClickSound(220f, 0.14f);
                if (bag != null)
                {
                    bag.ShowToast("BOAT 4 IS LOCKED! FIND AND LOOT THE RUST KEY FIRST.");
                }
                return;
            }

            // Consume the looted Rust Key from inventory
            if (!isUnlocked && bag != null)
            {
                bag.ConsumeItem(SurvivalToolType.RustKey, 1);
            }

            isUnlocked = true;
            isEscaping = true;
            _escapeTimer = 0f;

            if (bag != null)
            {
                if (bag.IsBagOpen)
                {
                    bag.ToggleBag(false);
                }
                bag.ShowToast("RUST KEY UNLOCKED BOAT 4! ESCAPING THE ISLAND...");
            }

            // Play engine startup & rev sound
            PlayEngineIgnitionSound();

            // Begin the full cinematic escape
            InitiateEscapeCinematic();
        }

        private void InitiateEscapeCinematic()
        {
            if (_hasInitiatedCutscene) return;
            _hasInitiatedCutscene = true;

            // Compute boat heading: bow of Boat_4 is -transform.up due to -90 X FBX rotation
            Vector3 moveDir = -transform.up;
            if (Mathf.Abs(moveDir.y) > 0.7f) moveDir = -Vector3.forward;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 0.01f) moveDir = -Vector3.forward;
            moveDir.Normalize();

            // 1. Configure Seated Player Character at the stern
            if (_playerTransform != null)
            {
                // Disable first person movement controllers
                if (_playerController == null)
                    _playerController = _playerTransform.GetComponent<CharacterController>();
                if (_playerController != null) _playerController.enabled = false;

                var fpc = _playerTransform.GetComponent<FirstPersonController>();
                if (fpc != null) fpc.enabled = false;

#if ENABLE_INPUT_SYSTEM
                var pInput = _playerTransform.GetComponent<PlayerInput>();
                if (pInput != null) pInput.enabled = false;
#endif

                var inputs = _playerTransform.GetComponent<StarterAssetsInputs>();
                if (inputs != null)
                {
                    inputs.move = Vector2.zero;
                    inputs.look = Vector2.zero;
                    inputs.sprint = false;
                    inputs.jump = false;
                }

                // Hide first-person gun viewmodels
                var weaponMgr = _playerTransform.GetComponent<PlayerWeaponManager>();
                if (weaponMgr != null && weaponMgr.weaponHoldPoint != null)
                {
                    weaponMgr.weaponHoldPoint.gameObject.SetActive(false);
                }

                // Turn off player flashlight if active
                var flashlight = _playerTransform.GetComponentInChildren<FlashlightController>();
                if (flashlight != null) flashlight.enabled = false;

                // Ensure third-person body meshes (Armature_Mesh etc.) are fully rendered
                foreach (var r in _playerTransform.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.enabled = true;
                }

                // Calculate stern seating position
                Vector3 boatCenter = _boatRenderer != null ? _boatRenderer.bounds.center : transform.position;
                Vector3 sternPos = boatCenter - moveDir * 1.55f + Vector3.up * 0.35f;

                _playerTransform.position = sternPos;
                _playerTransform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);

                // Build rustic boat helm & steering wheel at the stern right in front of the character
                CreateBoatHelm(sternPos + moveDir * 0.65f + Vector3.up * 0.40f, moveDir);
            }

            // 2. Take over camera control from Cinemachine
            _cinematicCamera = Camera.main;
            if (_cinematicCamera != null)
            {
                // Disable CinemachineBrain so we can freely animate Camera.main
                var brain = _cinematicCamera.GetComponent("CinemachineBrain");
                if (brain != null)
                {
                    _cinemachineBrain = brain;
                    if (brain is Behaviour b) b.enabled = false;
                }

                // Disable any Cinemachine cameras in the scene
                var allVcams = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                System.Collections.Generic.List<Component> vcams = new System.Collections.Generic.List<Component>();
                foreach (var mb in allVcams)
                {
                    if (mb.GetType().FullName.Contains("CinemachineCamera") || mb.GetType().FullName.Contains("VirtualCamera"))
                    {
                        mb.enabled = false;
                        vcams.Add(mb);
                    }
                }
                _disabledCinemachineCams = vcams.ToArray();

                // Initial camera shot: positioned just behind the character at the stern, looking forward past the wheel across the water
                Vector3 boatCenter = _boatRenderer != null ? _boatRenderer.bounds.center : transform.position;
                Vector3 sternPos = boatCenter - moveDir * 1.55f + Vector3.up * 0.35f;

                _camStartPos = sternPos - moveDir * 2.4f + Vector3.up * 1.35f;
                _cinematicCamera.transform.position = _camStartPos;
                _camStartRot = Quaternion.LookRotation((sternPos + moveDir * 2.5f + Vector3.up * 0.7f) - _camStartPos, Vector3.up);
                _cinematicCamera.transform.rotation = _camStartRot;
            }

            // 3. Start audio tracks
            if (_engineAudioSource != null)
            {
                _engineAudioSource.volume = 0.75f;
                _engineAudioSource.Play();
            }

            if (_waveAudioSource != null)
            {
                _waveAudioSource.volume = 0.05f;
                _waveAudioSource.Play();
            }
        }

        private void CreateBoatHelm(Vector3 helmPos, Vector3 forwardDir)
        {
            if (_helmConsole != null) return;

            _helmConsole = new GameObject("Boat4_SteeringHelm");
            _helmConsole.transform.position = helmPos;
            _helmConsole.transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
            _helmConsole.transform.SetParent(transform);

            // Steering Column (weathered dark pillar)
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Helm_Column";
            column.transform.SetParent(_helmConsole.transform, false);
            column.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            column.transform.localRotation = Quaternion.Euler(15f, 0f, 0f); // Slight tilt towards the helm
            column.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            DestroyImmediate(column.GetComponent<Collider>());

            // Steering Wheel Hub
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Helm_WheelHub";
            hub.transform.SetParent(_helmConsole.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0.20f, -0.05f);
            hub.transform.localRotation = Quaternion.Euler(75f, 0f, 0f); // Facing the driver
            hub.transform.localScale = new Vector3(0.12f, 0.04f, 0.12f);
            DestroyImmediate(hub.GetComponent<Collider>());

            // Steering Wheel Rim (horizontal disk representing outer ring)
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Helm_WheelRim";
            rim.transform.SetParent(_helmConsole.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0.20f, -0.05f);
            rim.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            rim.transform.localScale = new Vector3(0.48f, 0.015f, 0.48f);
            DestroyImmediate(rim.GetComponent<Collider>());

            // Apply boat material to helm pieces
            Material boatMat = _boatRenderer != null ? _boatRenderer.sharedMaterial : null;
            if (boatMat != null)
            {
                foreach (var rend in _helmConsole.GetComponentsInChildren<Renderer>())
                {
                    rend.sharedMaterial = boatMat;
                }
            }
        }

        private void SetupPostProcessingVolume()
        {
            try
            {
                _cutsceneVolume = gameObject.AddComponent<Volume>();
                _cutsceneVolume.isGlobal = true;
                _cutsceneVolume.priority = 10000f;

                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _cutsceneVolume.profile = profile;

                if (!profile.TryGet(out _colorAdjustments))
                {
                    _colorAdjustments = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                }

                _colorAdjustments.saturation.overrideState = true;
                _colorAdjustments.saturation.value = 0f;

                _colorAdjustments.postExposure.overrideState = true;
                _colorAdjustments.postExposure.value = 0f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Boat4EscapeInteraction] URP Volume initialization notice: {ex.Message}");
            }
        }

        private void UpdateBoatEscapeCinematic()
        {
            _escapeTimer += Time.deltaTime;

            // 1. Boat Forward Cruise & Water Bobbing
            Vector3 moveDir = -transform.up;
            if (Mathf.Abs(moveDir.y) > 0.7f) moveDir = -Vector3.forward;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 0.01f) moveDir = -Vector3.forward;
            moveDir.Normalize();

            float cruiseSpeed = Mathf.Min(4.2f, 1.2f + _escapeTimer * 0.35f);
            float bobOffset = Mathf.Sin(_escapeTimer * 2.2f) * 0.05f;
            Vector3 delta = moveDir * (cruiseSpeed * Time.deltaTime);

            transform.position += new Vector3(delta.x, bobOffset * Time.deltaTime, delta.z);

            // Keep the character safely seated at the stern of the moving boat
            if (_playerTransform != null)
            {
                Vector3 boatCenter = _boatRenderer != null ? _boatRenderer.bounds.center : transform.position;
                Vector3 sternPos = boatCenter - moveDir * 1.55f + Vector3.up * 0.35f;
                _playerTransform.position = sternPos;
                _playerTransform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
            }

            // 2. Camera Cinematic Pullback & Overhead Drift
            if (_cinematicCamera != null)
            {
                Vector3 boatCenter = _boatRenderer != null ? _boatRenderer.bounds.center : transform.position;

                // From 1.5s to 12.0s: slowly pulls back along -moveDir and drifts upward into a high overhead shot
                float camProgress = Mathf.Clamp01((_escapeTimer - 1.2f) / 10.5f);
                float ease = Mathf.SmoothStep(0f, 1f, camProgress);

                float pullDist = Mathf.Lerp(2.4f, 72.0f, Mathf.Pow(ease, 1.35f));
                float pullHeight = Mathf.Lerp(1.35f, 38.0f, Mathf.Pow(ease, 1.20f));

                Vector3 targetCamPos = boatCenter - moveDir * pullDist + Vector3.up * pullHeight;
                _cinematicCamera.transform.position = Vector3.Lerp(_cinematicCamera.transform.position, targetCamPos, Time.deltaTime * 5f);

                // Look-at target tracks the boat center as it shrinks into a speck in the vast water
                Vector3 lookTarget = boatCenter + Vector3.up * 0.3f;
                Quaternion targetCamRot = Quaternion.LookRotation(lookTarget - _cinematicCamera.transform.position, Vector3.up);
                _cinematicCamera.transform.rotation = Quaternion.Slerp(_cinematicCamera.transform.rotation, targetCamRot, Time.deltaTime * 5f);
            }

            // 3. Audio Transition: Boat engine hum fades into gentle waves rolling against the hull -> complete silence
            // Engine fades down between 2.0s and 7.5s
            if (_engineAudioSource != null && _engineAudioSource.isPlaying)
            {
                float engineFade = _escapeTimer < 2.0f
                    ? 0.75f
                    : Mathf.Lerp(0.75f, 0.0f, Mathf.Clamp01((_escapeTimer - 2.0f) / 5.5f));
                _engineAudioSource.volume = engineFade;

                if (_escapeTimer >= 8.0f)
                {
                    _engineAudioSource.Stop();
                }
            }

            // Waves swell in as engine fades, then fade to complete silence as screen turns black (8.0s to 11.5s)
            if (_waveAudioSource != null && _waveAudioSource.isPlaying)
            {
                float waveVol;
                if (_escapeTimer < 5.5f)
                {
                    // Gentle waves swell in
                    waveVol = Mathf.Lerp(0.05f, 0.70f, Mathf.Clamp01((_escapeTimer - 1.5f) / 4.0f));
                }
                else
                {
                    // Waves fade to silence as darkness overtakes the scene
                    waveVol = Mathf.Lerp(0.70f, 0.0f, Mathf.Clamp01((_escapeTimer - 8.0f) / 3.5f));
                }

                _waveAudioSource.volume = waveVol;

                if (_escapeTimer >= 11.8f)
                {
                    _waveAudioSource.Stop();
                }
            }

            // 4. Frame-by-Frame Desaturation of horizon/ocean (5.0s to 9.5s)
            _desaturateAlpha = Mathf.Clamp01((_escapeTimer - 5.0f) / 4.5f);
            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = Mathf.Lerp(0f, -100f, _desaturateAlpha);
            }

            // 5. Fade to Black (7.5s to 11.0s)
            _fadeToBlackAlpha = Mathf.Clamp01((_escapeTimer - 7.5f) / 3.5f);
            if (_colorAdjustments != null)
            {
                _colorAdjustments.postExposure.value = Mathf.Lerp(0f, -4.5f, _fadeToBlackAlpha);
            }
        }

        private void CheckEscapeInput()
        {
            // Only allow restart / continue after the cutscene has played out to the black screen
            if (_escapeTimer < 2.0f) return;

            bool continuePressed = false;
            bool restartPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
                    continuePressed = true;
                if (Keyboard.current.rKey.wasPressedThisFrame)
                    restartPressed = true;
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    continuePressed = true;
                if (Input.GetKeyDown(KeyCode.R))
                    restartPressed = true;
            }
            catch { }

            if (restartPressed)
            {
                Scene active = SceneManager.GetActiveScene();
                SceneManager.LoadScene(active.buildIndex);
            }
            else if (continuePressed && _escapeTimer >= 11.0f)
            {
                // Restore camera and controllers if player wishes to continue exploring/sailing freely
                RestoreGameplayAfterCutscene();
            }
        }

        private void RestoreGameplayAfterCutscene()
        {
            isEscaping = false;
            _fadeToBlackAlpha = 0f;
            _desaturateAlpha = 0f;

            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = 0f;
                _colorAdjustments.postExposure.value = 0f;
            }

            if (_cinemachineBrain is Behaviour b) b.enabled = true;
            if (_disabledCinemachineCams != null)
            {
                foreach (var c in _disabledCinemachineCams)
                {
                    if (c is Behaviour vcam) vcam.enabled = true;
                }
            }

            if (_playerTransform != null)
            {
                if (_playerController != null) _playerController.enabled = true;
                var fpc = _playerTransform.GetComponent<FirstPersonController>();
                if (fpc != null) fpc.enabled = true;

#if ENABLE_INPUT_SYSTEM
                var pInput = _playerTransform.GetComponent<PlayerInput>();
                if (pInput != null) pInput.enabled = true;
#endif

                var weaponMgr = _playerTransform.GetComponent<PlayerWeaponManager>();
                if (weaponMgr != null && weaponMgr.weaponHoldPoint != null)
                {
                    weaponMgr.weaponHoldPoint.gameObject.SetActive(true);
                }
            }
        }

        private bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame))
                return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
                    return true;
            }
            catch { }

            return false;
        }

        private void FindPlayer()
        {
            if (PlayerInventoryBag.Instance != null)
            {
                _playerTransform = PlayerInventoryBag.Instance.transform;
                _playerController = _playerTransform.GetComponent<CharacterController>();
                return;
            }

            var fpc = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                _playerTransform = fpc.transform;
                _playerController = fpc.GetComponent<CharacterController>();
                return;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                _playerController = playerObj.GetComponent<CharacterController>();
            }
        }

        #region Audio Synthesis

        private void PlayBoatKeyClickSound(float freq, float duration)
        {
            if (_sfxAudioSource == null) return;
            int sampleRate = 22050;
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            AudioClip clip = AudioClip.Create("BoatKeyClick", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - ((float)i / samples);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.25f;
            }
            clip.SetData(data, 0);
            _sfxAudioSource.PlayOneShot(clip, 0.55f);
        }

        private void PlayEngineIgnitionSound()
        {
            if (_sfxAudioSource == null) return;
            int sampleRate = 22050;
            float duration = 1.8f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("BoatIgnition", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                if (t < 0.25f)
                {
                    // Metallic key click
                    float env = 1f - (t / 0.25f);
                    data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.30f;
                }
                else
                {
                    // Outboard motor ignition cough & rev
                    float starterEnv = Mathf.Clamp01((1.8f - t) / 1.55f);
                    float cough = Mathf.Sin(2f * Mathf.PI * 52f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 104f * t);
                    data[i] = cough * starterEnv * 0.35f;
                }
            }
            clip.SetData(data, 0);
            _sfxAudioSource.PlayOneShot(clip, 0.85f);
        }

        private AudioClip CreateEngineHumClip()
        {
            int sampleRate = 22050;
            float duration = 2.4f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("BoatEngineHumLoop", samples, 1, sampleRate, false);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Harmonic motor purr: fundamental 62 Hz + octave 124 Hz + 186 Hz harmonic
                float f1 = Mathf.Sin(2f * Mathf.PI * 62f * t);
                float f2 = 0.55f * Mathf.Sin(2f * Mathf.PI * 124f * t);
                float f3 = 0.25f * Mathf.Sin(2f * Mathf.PI * 186f * t);
                data[i] = (f1 + f2 + f3) * 0.28f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateGentleWavesClip()
        {
            int sampleRate = 22050;
            float duration = 4.0f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("BoatGentleWavesLoop", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            System.Random rand = new System.Random(42);

            float lastSample = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Rhythmic ocean swell envelopes
                float swell1 = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(2f * Mathf.PI * t / 2.6f) * 0.5f + 0.5f), 1.8f);
                float swell2 = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(2f * Mathf.PI * t / 1.3f + 1.2f) * 0.5f + 0.5f), 2.2f);
                float combinedSwell = swell1 * 0.65f + swell2 * 0.35f;

                // Filtered pink noise for soft water lapping against hull
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lastSample = (lastSample + (0.04f * white)) / 1.04f; // Soft low-pass

                data[i] = lastSample * combinedSwell * 0.45f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        #endregion

        #region GUI & Escape Victory Sequence

        private void OnGUI()
        {
            if (isEscaping)
            {
                DrawCinematicEscapeSequence();
                return;
            }

            if (!IsPlayerInRange) return;
            if (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen) return;
            if (SurvivalItemPickup.ActiveTarget != null) return;

            bool hasKey = isUnlocked || (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.HasItem(SurvivalToolType.RustKey));

            float width = 520f;
            float height = 42f;
            float promptY = Screen.height * 0.64f;
            Rect promptRect = new Rect((Screen.width - width) * 0.5f, promptY, width, height);

            GUI.color = new Color(0.04f, 0.07f, 0.06f, 0.90f);
            GUI.DrawTexture(promptRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = StarterAssets.DialogueFontProvider.Bold,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (hasKey)
            {
                style.normal.textColor = new Color(0.35f, 1.0f, 0.55f);
                GUI.Label(promptRect, "PRESS [E] TO USE RUST KEY & ESCAPE ON BOAT 4", style);
            }
            else
            {
                style.normal.textColor = new Color(1.0f, 0.72f, 0.28f);
                GUI.Label(promptRect, "BOAT 4 LOCKED — FIND THE RUST KEY TO UNLOCK & ESCAPE", style);
            }
        }

        private void DrawCinematicEscapeSequence()
        {
            // 1. Fullscreen Black Screen Fade
            if (_fadeToBlackAlpha > 0.001f && _blackTex != null)
            {
                Color prevCol = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _fadeToBlackAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTex);
                GUI.color = prevCol;
            }

            // 2. Cinematic "YOU ESCAPED" Title & Epilogue Text (appears once screen transitions to black)
            if (_escapeTimer >= 10.5f)
            {
                float titleAlpha = Mathf.Clamp01((_escapeTimer - 10.5f) / 1.8f);

                float centerX = Screen.width * 0.5f;
                float centerY = Screen.height * 0.42f;

                // Grand Title Style
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = StarterAssets.DialogueFontProvider.Bold,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.width * 0.048f), 38, 70),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                // Title Shadow
                GUIStyle titleShadowStyle = new GUIStyle(titleStyle);
                titleShadowStyle.normal.textColor = new Color(0f, 0f, 0f, titleAlpha * 0.85f);
                GUI.Label(new Rect(centerX - 400 + 3, centerY - 50 + 3, 800, 100), "YOU ESCAPED", titleShadowStyle);

                // Title Text (Gleaming ivory / survivor gold)
                titleStyle.normal.textColor = new Color(0.96f, 0.94f, 0.88f, titleAlpha);
                GUI.Label(new Rect(centerX - 400, centerY - 50, 800, 100), "YOU ESCAPED", titleStyle);

                // Epilogue Narrative Text (Fades in slightly after)
                if (_escapeTimer >= 12.0f)
                {
                    float epilogueAlpha = Mathf.Clamp01((_escapeTimer - 12.0f) / 1.5f);

                    GUIStyle epilogueStyle = new GUIStyle(GUI.skin.label)
                    {
                        font = StarterAssets.DialogueFontProvider.Regular,
                        fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.width * 0.014f), 15, 22),
                        fontStyle = FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter
                    };
                    epilogueStyle.normal.textColor = new Color(0.72f, 0.80f, 0.78f, epilogueAlpha);

                    GUI.Label(new Rect(centerX - 450, centerY + 58, 900, 32), "The island fades into the distance behind you.", epilogueStyle);
                    GUI.Label(new Rect(centerX - 450, centerY + 90, 900, 32), "You survived the infestation and escaped to open waters.", epilogueStyle);
                }

                // Interactive Navigation Hints
                if (_escapeTimer >= 13.5f)
                {
                    float hintAlpha = Mathf.Clamp01((_escapeTimer - 13.5f) / 1.2f);

                    GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
                    {
                        font = StarterAssets.DialogueFontProvider.Bold,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    hintStyle.normal.textColor = new Color(0.48f, 0.60f, 0.56f, hintAlpha);

                    GUI.Label(new Rect(centerX - 400, Screen.height - 95f, 800, 30),
                        "Press [SPACE] to Continue Sailing   •   Press [R] to Restart Scene", hintStyle);
                }
            }
        }

        #endregion
    }
}
