using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Attaches a point light + indicator glow to the Radio prop and plays the interactive
    /// survivor broadcast dialogue when the player presses [E], then turns the radio and its light OFF.
    /// </summary>
    public class RadioInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Distance at which the player can press E to interact with the radio")]
        public float interactionRange = 3.8f;

        [Header("Dialogue Lines")]
        [TextArea(3, 5)]
        public string radioDialogueText = "Hello, is any body there? If you heard this, you must find some supply and survive, find a boat and give away on that place, we have many survivor here on east.";

        [TextArea(2, 3)]
        public string playerResponseText = "How can I go?";

        [TextArea(2, 4)]
        public string radioReplyText = "Find some place's where is safe and wait to the next call.";

        [Header("Dialogue Typography")]
        [Tooltip("Custom font used for all radio broadcast dialogue, quest logs, and subtitles.")]
        public Font dialogueFont;
        public Font dialogueBoldFont;

        [Header("Quest & Announcement UI Design")]
        [Tooltip("The title displayed at the top of the announcement panel (matching reference design).")]
        public string questName = "SURVIVE THE INFESTATION";

        [Tooltip("Bullet-point hints displayed underneath the radio announcement.")]
        public string[] questHints = new string[]
        {
            "Search abandoned villas & structures for weapons, ammo, and food",
            "Locate a seaworthy boat to escape toward the East Survivor Camp",
            "Find a safe place and wait for the next call"
        };

        [Tooltip("Key to toggle the Quest / Radio Announcement log anytime after discovering the radio.")]
        public KeyCode journalKey = KeyCode.J;

        [Header("Light Settings")]
        public Light radioLight;
        public Light dialSpotLight;
        public Color activeLightColor = new Color(1.0f, 0.92f, 0.20f); // Bright yellow radio glow
        public float activeLightIntensity = 0.45f;
        public float activeLightRange = 1.6f;

        [Header("State")]
        public bool isTurnedOff = false;

        private static readonly System.Collections.Generic.List<RadioInteraction> AllRadios =
            new System.Collections.Generic.List<RadioInteraction>();

        public static bool IsAnyRadioActiveForPlayer()
        {
            for (int i = 0; i < AllRadios.Count; i++)
            {
                var r = AllRadios[i];
                if (r != null && r.isActiveAndEnabled && (r._isDialoguePlaying || (!r.isTurnedOff && r._playerInRange)))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnEnable()
        {
            if (!AllRadios.Contains(this)) AllRadios.Add(this);
        }

        private void OnDisable()
        {
            AllRadios.Remove(this);
        }

        private bool _isDialoguePlaying = false;
        private bool _playerInRange = false;
        private bool _isJournalOpen = false;
        private bool _hasListenedToRadio = false;
        private static RadioInteraction _lastListenedRadio;
        private int _dialogueStage = 0; // 0 = none, 1 = radio speaking, 2 = player responding, 3 = radio instructions, 4 = radio turned off notice
        private string _currentSpeaker = "";
        private string _currentFullText = "";
        private int _visibleCharCount = 0;
        private bool _advanceRequested = false;

        private Transform _playerTransform;
        private Camera _mainCam;
        private AudioSource _audioSource;
        private AudioSource _ambientStaticSource;
        private AudioSource _cabinStaticSource;
        private Renderer _radioRenderer;
        private Renderer _ledRenderer;
        private MaterialPropertyBlock _propBlock;

        private GameObject _associatedCabin;
        private Bounds _cabinBounds;
        private bool _hasCabinBounds = false;
        private bool _isPlayerInsideCabin = false;
        public bool IsPlayerInsideCabin => _isPlayerInsideCabin;
        private float _cabinSearchRetryTimer = 0f;

        private Texture2D _bgTex;
        private Texture2D _borderTex;
        private Texture2D _speakerTagTex;
        private Texture2D _dividerTex;
        private Texture2D _signalBarOnTex;
        private Texture2D _signalBarOffTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSetupRadiosInScene()
        {
            int configuredCount = 0;

            // Find all transforms in the active scene and attach RadioInteraction to any Radio object
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                if (IsRadioObject(t.gameObject))
                {
                    // Avoid adding to child mesh if parent already has it
                    if (t.GetComponentInParent<RadioInteraction>() == null)
                    {
                        t.gameObject.AddComponent<RadioInteraction>();
                        configuredCount++;
                    }
                }
            }

            // If no Radio was placed in the scene yet, spawn one near the player's starting area
            if (configuredCount == 0 && Object.FindAnyObjectByType<RadioInteraction>() == null)
            {
                GameObject radioPrefab = Resources.Load<GameObject>("Radio");
                if (radioPrefab != null)
                {
                    Vector3 spawnPos = new Vector3(484.5f, 19.4f, 155.2f);
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player == null) player = GameObject.Find("PlayerCapsule");
                    if (player != null)
                    {
                        spawnPos = player.transform.position + player.transform.forward * 2.5f + Vector3.up * 0.5f;
                        if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~((1 << 2) | (1 << 8))))
                        {
                            spawnPos = hit.point;
                        }
                    }

                    GameObject instance = Object.Instantiate(radioPrefab, spawnPos, Quaternion.identity);
                    instance.name = "Radio";
                    if (instance.GetComponent<RadioInteraction>() == null)
                    {
                        instance.AddComponent<RadioInteraction>();
                    }
                }
            }
        }

        public static bool IsRadioObject(GameObject go)
        {
            if (go == null) return false;
            string n = go.name;
            if (n.Contains("RadioTower") || n.Contains("Struct_Radio") || n.Contains("RadioLight") || n.Contains("RadioLED"))
                return false;

            if (n == "Radio" || n.StartsWith("Radio ") || n.StartsWith("Radio(") || n.StartsWith("Radio_"))
                return true;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Radio")
                return true;

            return false;
        }

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            _radioRenderer = GetComponentInChildren<Renderer>();

            ConfigureSecondRadioDialogueIfNeeded();
            EnsureURPMaterial();
            SetupCollider();
            SetupRadioLight();
            SetupAudio();
            SetupCabinStaticAudio();
            InitGuiTextures();
        }

        private void Start()
        {
            ConfigureSecondRadioDialogueIfNeeded();
            LocatePlayer();
            FindAssociatedCabin();
            SetRadioPowerVisuals(!isTurnedOff);
            EnsureRadioAmbushSpawnerNearRadio();
        }

        private void ConfigureSecondRadioDialogueIfNeeded()
        {
            // The second radio on the porch table (named "Radio" at Z ~ 651.3, distinct from "Radio_1" at Z ~ 268.9)
            if (gameObject.name == "Radio" || transform.position.z > 500f)
            {
                radioDialogueText = "Beware people who's been infected are mutated, dont get infected and find a key, use the boat and escape";
                playerResponseText = "";
                radioReplyText = "";
                questName = "ESCAPE THE INFESTATION";
                questHints = new string[]
                {
                    "Beware of mutated infected — do not get infected",
                    "Find a key hidden in the area",
                    "Use the boat and escape"
                };
            }
        }

        private void EnsureURPMaterial()
        {
            if (_radioRenderer == null) return;
            Material mat = _radioRenderer.sharedMaterial;
            if (mat != null && mat.shader != null && mat.shader.name == "Standard")
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit != null)
                {
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                    mat.shader = urpLit;
                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
                    if (emissionMap != null)
                    {
                        mat.SetTexture("_EmissionMap", emissionMap);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private void SetupCollider()
        {
            Collider col = GetComponentInChildren<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.5f, 0.35f, 0.25f);
                box.center = new Vector3(0f, 0.16f, 0f);
            }
        }

        public void SetupRadioLight()
        {
            // Clamp if older high-intensity values were serialized on an existing scene component
            if (activeLightIntensity > 0.8f) activeLightIntensity = 0.45f;
            if (activeLightRange > 2.2f) activeLightRange = 1.6f;

            // 1. Main warm Point Light illuminating the radio subtly
            if (radioLight == null)
            {
                Transform existing = transform.Find("RadioLight");
                if (existing != null) radioLight = existing.GetComponent<Light>();
            }

            if (radioLight == null)
            {
                GameObject lightObj = new GameObject("RadioLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.22f, 0.12f);
                radioLight = lightObj.AddComponent<Light>();
            }

            radioLight.type = LightType.Point;
            radioLight.color = activeLightColor;
            radioLight.intensity = activeLightIntensity;
            radioLight.range = activeLightRange;
            radioLight.shadows = LightShadows.None;
            radioLight.enabled = !isTurnedOff;

            // 2. Secondary close-range dial glow light right on the front face of the radio
            if (dialSpotLight == null)
            {
                Transform existingDial = transform.Find("RadioDialLight");
                if (existingDial != null) dialSpotLight = existingDial.GetComponent<Light>();
            }

            if (dialSpotLight == null)
            {
                GameObject dialObj = new GameObject("RadioDialLight");
                dialObj.transform.SetParent(transform, false);
                dialObj.transform.localPosition = new Vector3(0f, 0.12f, 0.08f);
                dialSpotLight = dialObj.AddComponent<Light>();
            }

            dialSpotLight.type = LightType.Point;
            dialSpotLight.color = new Color(1.0f, 0.92f, 0.20f); // Bright yellow tuner glow
            dialSpotLight.intensity = 0.25f;
            dialSpotLight.range = 0.6f;
            dialSpotLight.shadows = LightShadows.None;
            dialSpotLight.enabled = !isTurnedOff;

            // 3. Small physical glowing LED indicator bulb on the radio
            Transform ledTrans = transform.Find("RadioLEDIndicator");
            if (ledTrans == null)
            {
                GameObject ledObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ledObj.name = "RadioLEDIndicator";
                ledObj.transform.SetParent(transform, false);
                ledObj.transform.localPosition = new Vector3(0.085f, 0.165f, 0.055f);
                ledObj.transform.localScale = Vector3.one * 0.018f;
                Collider ledCol = ledObj.GetComponent<Collider>();
                if (ledCol != null) Destroy(ledCol);
                _ledRenderer = ledObj.GetComponent<Renderer>();
                if (_ledRenderer != null)
                {
                    Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (urpUnlit == null) urpUnlit = Shader.Find("Unlit/Color");
                    Material ledMat = new Material(urpUnlit != null ? urpUnlit : Shader.Find("Standard"));
                    ledMat.color = new Color(1.0f, 0.92f, 0.15f);
                    if (ledMat.HasProperty("_BaseColor")) ledMat.SetColor("_BaseColor", new Color(1.0f, 0.92f, 0.15f));
                    _ledRenderer.material = ledMat;
                }
            }
            else
            {
                _ledRenderer = ledTrans.GetComponent<Renderer>();
            }
        }

        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.spatialBlend = 0.35f; // Clearly audible when nearby
            _audioSource.minDistance = 2.0f;
            _audioSource.maxDistance = 20.0f;
            _audioSource.playOnAwake = false;

            // Subtle looping radio standby static hum while radio is ON
            Transform staticChild = transform.Find("RadioStandbyHum");
            if (staticChild == null)
            {
                GameObject humObj = new GameObject("RadioStandbyHum");
                humObj.transform.SetParent(transform, false);
                _ambientStaticSource = humObj.AddComponent<AudioSource>();
            }
            else
            {
                _ambientStaticSource = staticChild.GetComponent<AudioSource>();
            }

            _ambientStaticSource.spatialBlend = 1.0f;
            _ambientStaticSource.minDistance = 1.2f;
            _ambientStaticSource.maxDistance = 10.0f;
            _ambientStaticSource.loop = true;
            _ambientStaticSource.volume = 0.14f;
            _ambientStaticSource.clip = CreateStandbyHumClip();
            if (!isTurnedOff)
            {
                _ambientStaticSource.Play();
            }
        }

        private void LocatePlayer()
        {
            if (_playerTransform == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) p = GameObject.Find("PlayerCapsule");
                if (p != null) _playerTransform = p.transform;
            }
            if (_mainCam == null)
            {
                _mainCam = Camera.main;
            }
        }

        private void Update()
        {
            LocatePlayer();
            UpdateCabinProximityAndStaticAudio();

            // Animate radio light while powered on
            if (!isTurnedOff && radioLight != null)
            {
                if (_isDialoguePlaying && (_dialogueStage == 1 || _dialogueStage == 3))
                {
                    // Dynamic voice modulation flicker while survivor is speaking over the radio
                    float voiceFlicker = 1.0f + Mathf.Sin(Time.time * 28f) * 0.25f + Mathf.Sin(Time.time * 43f) * 0.18f;
                    radioLight.intensity = activeLightIntensity * voiceFlicker;
                    if (dialSpotLight != null) dialSpotLight.intensity = 0.25f * voiceFlicker;
                }
                else
                {
                    // Gentle standby beacon pulse
                    float pulse = 1.0f + Mathf.Sin(Time.time * 3.2f) * 0.12f;
                    radioLight.intensity = activeLightIntensity * pulse;
                }
            }

            // Check distance to player
            _playerInRange = false;
            if (!isTurnedOff && _playerTransform != null)
            {
                float dist = Vector3.Distance(_playerTransform.position, transform.position);
                if (dist <= interactionRange)
                {
                    _playerInRange = true;
                }
                else if (dist <= interactionRange + 1.4f && _mainCam != null)
                {
                    // Also allow if looking directly at the radio from slightly further away
                    Vector3 toRadio = (transform.position - _mainCam.transform.position).normalized;
                    if (Vector3.Dot(_mainCam.transform.forward, toRadio) > 0.85f)
                    {
                        _playerInRange = true;
                    }
                }
            }

            // Toggle Quest Journal with [J] or close with Escape
            if (Input.GetKeyDown(journalKey))
            {
                _isJournalOpen = !_isJournalOpen;
            }
            if (_isJournalOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                _isJournalOpen = false;
            }

            // Check for [E] key press
            if (WasInteractKeyPressed())
            {
                if (!isTurnedOff && !_isDialoguePlaying && _playerInRange)
                {
                    _isJournalOpen = false;
                    StartCoroutine(PlayRadioDialogueRoutine());
                }
                else if (_isDialoguePlaying)
                {
                    _advanceRequested = true;
                }
            }
        }

        private bool WasInteractKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;
#endif
            return Input.GetKeyDown(KeyCode.E);
        }

        private Transform _ambushSpawnerTransform;
        private bool _hasSpawnedAmbushHorde = false;

        // Exact world coordinates of the Hedge Maze / Dirt Road / Church-Cabin Clearing shown in the reference screenshot
        private static readonly Vector3 HedgeMazeSpawnerCenter = new Vector3(506.0f, 16.80f, 598.0f);
        private static readonly Vector3[] HedgeMazeAmbushSpawnPoints = new Vector3[]
        {
            // Foreground grassy clearing in front of the Hedge Maze iron fence
            new Vector3(492.0f, 16.80f, 604.0f),
            new Vector3(498.0f, 16.80f, 606.0f),
            new Vector3(504.0f, 16.80f, 605.0f),
            new Vector3(486.0f, 16.80f, 603.0f),
            new Vector3(509.5f, 16.80f, 607.0f),
            new Vector3(495.0f, 16.80f, 601.5f),

            // Dirt road & open iron gate between the white wooden building (Cabin2) and Hedge Maze fence
            new Vector3(513.0f, 16.80f, 596.0f),
            new Vector3(515.0f, 16.80f, 588.0f),
            new Vector3(516.0f, 16.80f, 580.0f),
            new Vector3(516.0f, 16.80f, 572.0f),
            new Vector3(515.0f, 16.80f, 564.0f),
            new Vector3(512.5f, 16.80f, 602.0f),
            new Vector3(517.5f, 16.80f, 592.0f),

            // Hedge Maze roundabout & courtyard around the central autumn tree (blockouts_Roundabout2 at 484.75, 16.74, 572.36)
            new Vector3(484.75f, 16.80f, 576.5f),
            new Vector3(488.5f, 16.80f, 572.36f),
            new Vector3(481.0f, 16.80f, 572.36f),
            new Vector3(495.0f, 16.80f, 584.0f),
            new Vector3(478.0f, 16.80f, 586.0f)
        };

        private void EnsureRadioAmbushSpawnerNearRadio()
        {
            if (_ambushSpawnerTransform != null) return;

            string spawnerName = $"RadioAmbushSpawner_{gameObject.name}";
            GameObject existing = GameObject.Find(spawnerName);
            if (existing != null)
            {
                _ambushSpawnerTransform = existing.transform;
                return;
            }

            // Place the spawner at the Hedge Maze / Dirt Road clearing shown in the user's screenshot
            int groundMask = ~((1 << 2) | (1 << 4) | (1 << 8));
            Vector3 spawnerPos = HedgeMazeSpawnerCenter;
            float gY = SampleDryGroundY(spawnerPos, groundMask);
            if (gY > -900f) spawnerPos.y = gY + 0.05f;

            GameObject spawnerGo = new GameObject(spawnerName);
            spawnerGo.transform.position = spawnerPos;
            _ambushSpawnerTransform = spawnerGo.transform;
        }

        private void TriggerRadioAmbushSpawner()
        {
            if (_hasSpawnedAmbushHorde) return;
            _hasSpawnedAmbushHorde = true;

            LocatePlayer();
            EnsureRadioAmbushSpawnerNearRadio();

            Vector3 centerPos = _ambushSpawnerTransform != null ? _ambushSpawnerTransform.position : transform.position;

            // Load all requested monster prefabs (MND, Book Head Monster, Morbid Creature Mutant 1, Demon Horror Creature, Standard Zombie)
            GameObject zombiePrefab = LoadMonsterPrefab(
                "Assets/ZombieMale_AAB/Prefabs/URP/Zombie_Enemy.prefab",
                "Prefabs/Zombie_Enemy",
                "Zombie_Enemy");

            GameObject mndPrefab = LoadMonsterPrefab(
                "Assets/MN D/PREFABS/RUN Prefab 6.prefab",
                "Monsters/MND",
                null);

            GameObject bookHeadBloodPrefab = LoadMonsterPrefab(
                "Assets/BookHeadMonster/URP/Prefabs/BookHeadMonster_withBlood.prefab",
                "Monsters/BookHeadMonster_withBlood",
                null);

            GameObject bookHeadPrefab = LoadMonsterPrefab(
                "Assets/BookHeadMonster/URP/Prefabs/BookHeadMonster.prefab",
                "Monsters/BookHeadMonster",
                null);

            GameObject mutant1Prefab = LoadMonsterPrefab(
                "Assets/Mutant1/Prefab/Mutated1.prefab",
                "Monsters/Mutated1",
                null);

            GameObject mutant2Prefab = LoadMonsterPrefab(
                "Assets/Mutant1/Prefab/Mutated1_V2.prefab",
                "Monsters/Mutated1_V2",
                null);

            GameObject mutant3Prefab = LoadMonsterPrefab(
                "Assets/Mutant1/Prefab/Mutated1_V3.prefab",
                "Monsters/Mutated1_V3",
                null);

            GameObject demonPrefab = FindDemonHorrorCreaturePrefab();
            if (demonPrefab == null)
            {
                demonPrefab = mutant3Prefab != null ? mutant3Prefab : (mutant1Prefab != null ? mutant1Prefab : (mndPrefab != null ? mndPrefab : zombiePrefab));
            }

            RuntimeAnimatorController zombieController = null;
            if (zombiePrefab != null)
            {
                var zAnim = zombiePrefab.GetComponentInChildren<Animator>(true);
                if (zAnim != null) zombieController = zAnim.runtimeAnimatorController;
            }
            if (zombieController == null)
            {
#if UNITY_EDITOR
                zombieController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ZombieMale_AAB/Animations/ZombieAnimator.controller");
#endif
            }
            if (zombieController == null)
            {
                zombieController = Resources.Load<RuntimeAnimatorController>("ZombieAnimator");
            }

            // Build the 18-enemy ambush roster near the radio
            System.Collections.Generic.List<(GameObject prefab, string title, bool attachWeapon, bool isDemon)> roster =
                new System.Collections.Generic.List<(GameObject, string, bool, bool)>
            {
                // 4x MND
                (mndPrefab != null ? mndPrefab : zombiePrefab, "MND", false, false),
                (mndPrefab != null ? mndPrefab : zombiePrefab, "MND", true, false),
                (mndPrefab != null ? mndPrefab : zombiePrefab, "MND", false, false),
                (mndPrefab != null ? mndPrefab : zombiePrefab, "MND", true, false),

                // 4x Book Head Monster
                (bookHeadBloodPrefab != null ? bookHeadBloodPrefab : zombiePrefab, "BookHeadMonster", false, false),
                (bookHeadPrefab != null ? bookHeadPrefab : zombiePrefab, "BookHeadMonster", true, false),
                (bookHeadBloodPrefab != null ? bookHeadBloodPrefab : zombiePrefab, "BookHeadMonster", false, false),
                (bookHeadBloodPrefab != null ? bookHeadBloodPrefab : zombiePrefab, "BookHeadMonster", true, false),

                // 4x Morbid Creature Mutant 1
                (mutant1Prefab != null ? mutant1Prefab : zombiePrefab, "MorbidCreature_Mutant1", false, false),
                (mutant2Prefab != null ? mutant2Prefab : (mutant1Prefab != null ? mutant1Prefab : zombiePrefab), "MorbidCreature_Mutant1_V2", true, false),
                (mutant3Prefab != null ? mutant3Prefab : (mutant1Prefab != null ? mutant1Prefab : zombiePrefab), "MorbidCreature_Mutant1_V3", false, false),
                (mutant1Prefab != null ? mutant1Prefab : zombiePrefab, "MorbidCreature_Mutant1", true, false),

                // 4x Demon Horror Creature with Weapons
                (demonPrefab, "DemonHorrorCreature_Weapon", true, true),
                (demonPrefab, "DemonHorrorCreature_Weapon", true, true),
                (demonPrefab, "DemonHorrorCreature_Weapon", true, true),
                (demonPrefab, "DemonHorrorCreature_Weapon", true, true),

                // 2x Aggressive Zombies with Weapons
                (zombiePrefab, "AggressiveZombie", true, false),
                (zombiePrefab, "AggressiveZombie", false, false)
            };

            System.Collections.Generic.List<Vector3> usedPositions = new System.Collections.Generic.List<Vector3>();
            int spawnedCount = 0;

            for (int i = 0; i < roster.Count; i++)
            {
                var entry = roster[i];
                if (entry.prefab == null) continue;

                // Distribute around the radio spawner and radio (5.5m to 16m away on dry ground)
                Vector3 spawnPos = FindNonOverlappingDrySpawnNear(centerPos, transform.position, usedPositions, i, roster.Count);
                usedPositions.Add(spawnPos);

                Vector3 lookTarget = _playerTransform != null ? _playerTransform.position : transform.position;
                Vector3 toTarget = lookTarget - spawnPos;
                toTarget.y = 0f;
                Quaternion spawnRot = toTarget.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toTarget.normalized) : Quaternion.identity;

                GameObject instance = Instantiate(entry.prefab, spawnPos, spawnRot);
                instance.name = $"{entry.title}_RadioAmbush_{i + 1}";

                ConfigureSpawnedMonsterForCombat(instance, zombieController, entry.attachWeapon, entry.isDemon);
                spawnedCount++;
            }

            // Also aggro all existing zombies within 70m of the radio so they rush the player too
            ZombieAI[] existingZombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            foreach (var z in existingZombies)
            {
                if (z != null && !z.isDead && Vector3.Distance(z.transform.position, transform.position) <= 70f)
                {
                    z.MakeHighlyAggressive(_playerTransform);
                }
            }

            Debug.Log($"[RadioInteraction] Radio activated! Spawned {spawnedCount} highly aggressive ambush monsters near {gameObject.name}!");
        }

        private static GameObject LoadMonsterPrefab(string assetPath, string resourcePath, string fallbackResourcePath)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(assetPath))
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
#endif
            if (prefab == null && !string.IsNullOrEmpty(resourcePath))
            {
                prefab = Resources.Load<GameObject>(resourcePath);
            }
            if (prefab == null && !string.IsNullOrEmpty(fallbackResourcePath))
            {
                prefab = Resources.Load<GameObject>(fallbackResourcePath);
            }
            return prefab;
        }

        private static GameObject FindDemonHorrorCreaturePrefab()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                if (lower.Contains("demon") || (lower.Contains("horror") && !lower.Contains("bookhead")))
                {
                    GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (p != null && p.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                    {
                        return p;
                    }
                }
            }
#endif
            return Resources.Load<GameObject>("Monsters/DemonHorrorCreature");
        }

        private void ConfigureSpawnedMonsterForCombat(GameObject monster, RuntimeAnimatorController fallbackHumanoidController, bool attachWeapon, bool isDemonHorror)
        {
            if (monster == null) return;

            // 1. Ensure URP Lit materials on all renderers so no monster ever renders pink
            EnsureMonsterURPMaterials(monster);

            // 2. Normalize scale so monsters with 10x or 0.01x FBX scale (e.g., MN D) stand ~1.95m tall
            NormalizeMonsterHeight(monster, isDemonHorror ? 2.10f : 1.92f);

            // 3. Ensure Animator is configured before ZombieAI.Awake() caches bones
            Animator anim = monster.GetComponent<Animator>();
            if (anim == null) anim = monster.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (fallbackHumanoidController != null && (anim.isHuman || anim.runtimeAnimatorController == null))
                {
                    anim.runtimeAnimatorController = fallbackHumanoidController;
                }
            }

            // 4. Ensure CharacterController is on root for movement & gun hit detection
            CharacterController cc = monster.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = monster.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.36f;
                cc.center = new Vector3(0f, 0.92f, 0f);
                cc.stepOffset = 0.45f;
                cc.slopeLimit = 55f;
            }
            else
            {
                cc.stepOffset = Mathf.Max(cc.stepOffset, 0.45f);
                cc.slopeLimit = Mathf.Max(cc.slopeLimit, 55f);
            }

            // 5. Ensure ZombieAI is attached and set to Highly Aggressive mode
            ZombieAI ai = monster.GetComponent<ZombieAI>();
            if (ai == null)
            {
                ai = monster.AddComponent<ZombieAI>();
            }

            ai.MakeHighlyAggressive(_playerTransform);

            if (attachWeapon || isDemonHorror)
            {
                ai.AttachHorrorWeaponToHand(isDemonHorror);
            }
        }

        private static void EnsureMonsterURPMaterials(GameObject monster)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            Renderer[] renderers = monster.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    string sName = m.shader != null ? m.shader.name : "";
                    if (sName == "Standard" || sName.Contains("InternalErrorShader") || sName.Contains("Legacy"))
                    {
                        Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null);
                        Texture bumpTex = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                        Texture metalTex = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                        Texture occTex = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
                        Texture emisTex = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
                        Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

                        Material upgraded = new Material(urpLit);
                        upgraded.name = m.name + "_URP";
                        upgraded.SetColor("_BaseColor", col);
                        if (mainTex != null) upgraded.SetTexture("_BaseMap", mainTex);
                        if (bumpTex != null)
                        {
                            upgraded.SetTexture("_BumpMap", bumpTex);
                            upgraded.EnableKeyword("_NORMALMAP");
                        }
                        if (metalTex != null)
                        {
                            upgraded.SetTexture("_MetallicGlossMap", metalTex);
                            upgraded.EnableKeyword("_METALLICSPECGLOSSMAP");
                        }
                        if (occTex != null)
                        {
                            upgraded.SetTexture("_OcclusionMap", occTex);
                            upgraded.EnableKeyword("_OCCLUSIONMAP");
                        }
                        if (emisTex != null)
                        {
                            upgraded.SetTexture("_EmissionMap", emisTex);
                            upgraded.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f, 1f));
                            upgraded.EnableKeyword("_EMISSION");
                        }
                        upgraded.SetFloat("_Smoothness", 0.35f);
                        mats[i] = upgraded;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }
        }

        private static void NormalizeMonsterHeight(GameObject monster, float targetHeight)
        {
            SkinnedMeshRenderer[] smrs = monster.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0) return;

            // If child mesh object has an exaggerated localScale (e.g. MN D's 10x scale on child mesh), normalize child scale first
            foreach (var smr in smrs)
            {
                if (smr == null) continue;
                smr.updateWhenOffscreen = true;
                if (smr.transform != monster.transform && smr.transform.localScale.x > 4f)
                {
                    smr.transform.localScale = Vector3.one;
                }
            }

            Bounds combined = smrs[0].bounds;
            for (int i = 1; i < smrs.Length; i++)
            {
                if (smrs[i] != null) combined.Encapsulate(smrs[i].bounds);
            }

            float currentHeight = combined.size.y;
            if (currentHeight > 0.05f && (currentHeight > 2.6f || currentHeight < 1.25f))
            {
                float scaleFactor = Mathf.Clamp(targetHeight / currentHeight, 0.08f, 3.0f);
                monster.transform.localScale *= scaleFactor;
            }
        }

        private Vector3 FindDrySpawnPositionNear(Vector3 origin, float minRadius, float maxRadius)
        {
            float waterThreshold = 16.25f;
            int groundMask = ~((1 << 2) | (1 << 4) | (1 << 8));

            Vector3 bestSpot = Vector3.zero;
            int bestDryNeighbors = -1;

            // Search around origin between minRadius (17m) and maxRadius (25m) for an open dry clearing
            for (int attempt = 0; attempt < 36; attempt++)
            {
                float angle = (attempt / 36f) * Mathf.PI * 2f;
                float dist = Mathf.Lerp(minRadius, maxRadius, (attempt % 6) / 5f);
                Vector3 candidate = new Vector3(origin.x + Mathf.Cos(angle) * dist, origin.y, origin.z + Mathf.Sin(angle) * dist);

                float groundY = SampleDryGroundY(candidate, groundMask);
                if (groundY >= waterThreshold)
                {
                    // Score how many surrounding points (4m radius) are also dry so the horde can cluster here cleanly
                    int dryNeighbors = 0;
                    for (int n = 0; n < 6; n++)
                    {
                        float nAngle = (n / 6f) * Mathf.PI * 2f;
                        Vector3 neighbor = candidate + new Vector3(Mathf.Cos(nAngle) * 4f, 0f, Mathf.Sin(nAngle) * 4f);
                        if (SampleDryGroundY(neighbor, groundMask) >= waterThreshold)
                        {
                            dryNeighbors++;
                        }
                    }

                    if (dryNeighbors > bestDryNeighbors)
                    {
                        bestDryNeighbors = dryNeighbors;
                        bestSpot = new Vector3(candidate.x, groundY + 0.05f, candidate.z);
                        if (dryNeighbors >= 5)
                        {
                            return bestSpot;
                        }
                    }
                }
            }

            if (bestDryNeighbors >= 0)
            {
                return bestSpot;
            }

            Vector3 fallback = origin + transform.forward * 18f;
            float fbGroundY = SampleDryGroundY(fallback, groundMask);
            if (fbGroundY > -900f) fallback.y = fbGroundY + 0.05f;
            return fallback;
        }

        private Vector3 FindNonOverlappingDrySpawnNear(Vector3 spawnerCenter, Vector3 radioPos, System.Collections.Generic.List<Vector3> usedPositions, int index, int total)
        {
            int groundMask = ~((1 << 2) | (1 << 4) | (1 << 8));

            // Spawn directly at the Hedge Maze / Dirt Road / Church-Cabin clearing points shown in the user's screenshot
            if (HedgeMazeAmbushSpawnPoints != null && HedgeMazeAmbushSpawnPoints.Length > 0)
            {
                Vector3 preset = HedgeMazeAmbushSpawnPoints[index % HedgeMazeAmbushSpawnPoints.Length];
                if (index >= HedgeMazeAmbushSpawnPoints.Length)
                {
                    float extraAngle = index * 1.15f;
                    preset += new Vector3(Mathf.Cos(extraAngle) * 2.2f, 0f, Mathf.Sin(extraAngle) * 2.2f);
                }

                // Ensure we never spawn right on top of the player if the player is near the maze
                if (_playerTransform != null)
                {
                    Vector2 toPreset = new Vector2(preset.x - _playerTransform.position.x, preset.z - _playerTransform.position.z);
                    if (toPreset.magnitude < 12.0f)
                    {
                        Vector2 dir = toPreset.sqrMagnitude > 0.01f ? toPreset.normalized : Vector2.up;
                        preset.x = _playerTransform.position.x + dir.x * 14.0f;
                        preset.z = _playerTransform.position.z + dir.y * 14.0f;
                    }
                }

                float presetGroundY = SampleDryGroundY(preset, groundMask);
                preset.y = (presetGroundY > -900f ? presetGroundY : 16.80f) + 0.05f;
                return preset;
            }

            float waterThreshold = 16.25f;
            const float minDistanceFromPlayer = 14.0f;
            const float minDistanceFromRadio = 13.0f;

            // First try spawning around the dedicated spawner
            for (int attempt = 0; attempt < 64; attempt++)
            {
                Vector3 candidate;
                if (attempt < 36)
                {
                    float angle = ((float)index / Mathf.Max(1, total)) * Mathf.PI * 2f + attempt * 0.52f;
                    float clusterRadius = Mathf.Lerp(1.8f, 9.5f, (attempt % 9) / 8f);
                    candidate = new Vector3(
                        spawnerCenter.x + Mathf.Cos(angle) * clusterRadius,
                        spawnerCenter.y,
                        spawnerCenter.z + Mathf.Sin(angle) * clusterRadius);
                }
                else
                {
                    float angle = ((float)index / Mathf.Max(1, total)) * Mathf.PI * 2f + attempt * 0.37f;
                    float ringRadius = Random.Range(15.0f, 27.0f);
                    candidate = new Vector3(
                        radioPos.x + Mathf.Cos(angle) * ringRadius,
                        radioPos.y,
                        radioPos.z + Mathf.Sin(angle) * ringRadius);
                }

                // Never spawn on top of or close to the player: require at least 14m distance so they must run toward the player!
                if (_playerTransform != null)
                {
                    float dPlayer = Vector2.Distance(
                        new Vector2(candidate.x, candidate.z),
                        new Vector2(_playerTransform.position.x, _playerTransform.position.z));
                    if (dPlayer < minDistanceFromPlayer) continue;
                }

                // Also keep at least 13m away from the radio itself
                float dRadio = Vector2.Distance(
                    new Vector2(candidate.x, candidate.z),
                    new Vector2(radioPos.x, radioPos.z));
                if (dRadio < minDistanceFromRadio) continue;

                bool tooClose = false;
                for (int u = 0; u < usedPositions.Count; u++)
                {
                    if (Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(usedPositions[u].x, usedPositions[u].z)) < 1.85f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                float groundY = SampleDryGroundY(candidate, groundMask);
                if (groundY >= waterThreshold)
                {
                    return new Vector3(candidate.x, groundY + 0.05f, candidate.z);
                }
            }

            // Fallback: place around spawnerCenter (which is already a short distance away from the radio/player)
            float fbAngle = ((float)index / Mathf.Max(1, total)) * Mathf.PI * 2f;
            Vector3 fbPos = spawnerCenter + new Vector3(Mathf.Cos(fbAngle) * 4.5f, 0f, Mathf.Sin(fbAngle) * 4.5f);
            if (_playerTransform != null)
            {
                Vector2 toFb = new Vector2(fbPos.x - _playerTransform.position.x, fbPos.z - _playerTransform.position.z);
                if (toFb.magnitude < minDistanceFromPlayer)
                {
                    Vector2 dir = toFb.sqrMagnitude > 0.01f ? toFb.normalized : Vector2.up;
                    fbPos.x = _playerTransform.position.x + dir.x * (minDistanceFromPlayer + 3f);
                    fbPos.z = _playerTransform.position.z + dir.y * (minDistanceFromPlayer + 3f);
                }
            }
            float fbY = SampleDryGroundY(fbPos, groundMask);
            fbPos.y = (fbY > -900f ? fbY : spawnerCenter.y) + 0.05f;
            return fbPos;
        }

        private static float SampleDryGroundY(Vector3 pos, int groundMask)
        {
            float groundY = -999f;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 25f, pos.z), Vector3.down, out RaycastHit hit, 60f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y >= 0.5f)
                {
                    groundY = hit.point.y;
                }
            }

            if (Terrain.activeTerrains != null)
            {
                foreach (var terr in Terrain.activeTerrains)
                {
                    if (terr == null || terr.terrainData == null) continue;
                    Vector3 tPos = terr.transform.position;
                    Vector3 tSize = terr.terrainData.size;
                    if (pos.x >= tPos.x && pos.x <= tPos.x + tSize.x &&
                        pos.z >= tPos.z && pos.z <= tPos.z + tSize.z)
                    {
                        float tY = terr.SampleHeight(pos) + tPos.y;
                        if (tY > groundY) groundY = tY;
                    }
                }
            }

            return groundY;
        }

        private IEnumerator PlayRadioDialogueRoutine()
        {
            _isDialoguePlaying = true;
            _hasListenedToRadio = true;
            _lastListenedRadio = this;
            _advanceRequested = false;

            // Silence search static when tuning in to the transmission
            if (_cabinStaticSource != null && _cabinStaticSource.isPlaying)
            {
                _cabinStaticSource.Stop();
            }

            // Immediately spawn the large aggressive monster horde near the radio when E is pressed!
            TriggerRadioAmbushSpawner();

            // =========================================================================
            // STAGE 1: Radio Survivor Broadcast
            // =========================================================================
            _dialogueStage = 1;
            _currentSpeaker = "RADIO BROADCAST  [ SURVIVOR TRANSMISSION ]";
            _currentFullText = $"\"{radioDialogueText}\"";
            _visibleCharCount = 0;

            // Play radio tuning squelch + voice transmission sound
            if (_audioSource != null)
            {
                _audioSource.PlayOneShot(CreateRadioTransmissionClip(5.5f), 0.9f);
            }

            float charsPerSec = 36f;
            float elapsed = 0f;
            float totalTypeTime = _currentFullText.Length / charsPerSec;

            while (elapsed < totalTypeTime)
            {
                if (_advanceRequested)
                {
                    _advanceRequested = false;
                    _visibleCharCount = _currentFullText.Length;
                    break;
                }
                elapsed += Time.deltaTime;
                _visibleCharCount = Mathf.Clamp(Mathf.FloorToInt(elapsed * charsPerSec), 0, _currentFullText.Length);
                yield return null;
            }
            _visibleCharCount = _currentFullText.Length;

            // Hold so player can read the full message (or press E to continue)
            float holdTimer = 0f;
            while (holdTimer < 4.0f && !_advanceRequested)
            {
                holdTimer += Time.deltaTime;
                yield return null;
            }
            _advanceRequested = false;

            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            // =========================================================================
            // STAGE 2: Player Responds (optional — skipped for single-message broadcasts)
            // =========================================================================
            if (!string.IsNullOrEmpty(playerResponseText))
            {
                _dialogueStage = 2;
                _currentSpeaker = "YOU";
                _currentFullText = $"\"{playerResponseText}\"";
                _visibleCharCount = 0;

                if (_audioSource != null)
                {
                    _audioSource.PlayOneShot(CreatePlayerMicClickClip(), 0.85f);
                }

                elapsed = 0f;
                totalTypeTime = Mathf.Max(0.45f, _currentFullText.Length / 28f);
                while (elapsed < totalTypeTime)
                {
                    if (_advanceRequested)
                    {
                        _advanceRequested = false;
                        _visibleCharCount = _currentFullText.Length;
                        break;
                    }
                    elapsed += Time.deltaTime;
                    _visibleCharCount = Mathf.Clamp(Mathf.FloorToInt(elapsed * 28f), 0, _currentFullText.Length);
                    yield return null;
                }
                _visibleCharCount = _currentFullText.Length;

                holdTimer = 0f;
                while (holdTimer < 2.2f && !_advanceRequested)
                {
                    holdTimer += Time.deltaTime;
                    yield return null;
                }
                _advanceRequested = false;

                if (_audioSource != null)
                {
                    _audioSource.Stop();
                }
            }

            // =========================================================================
            // STAGE 3: Radio Replies (optional — skipped for single-message broadcasts)
            // =========================================================================
            if (!string.IsNullOrEmpty(radioReplyText))
            {
                _dialogueStage = 3;
                _currentSpeaker = "RADIO BROADCAST  [ EAST SURVIVOR CAMP ]";
                _currentFullText = $"\"{radioReplyText}\"";
                _visibleCharCount = 0;

                if (_audioSource != null)
                {
                    _audioSource.PlayOneShot(CreateRadioTransmissionClip(4.8f), 0.9f);
                }

                elapsed = 0f;
                totalTypeTime = Mathf.Max(0.8f, _currentFullText.Length / charsPerSec);
                while (elapsed < totalTypeTime)
                {
                    if (_advanceRequested)
                    {
                        _advanceRequested = false;
                        _visibleCharCount = _currentFullText.Length;
                        break;
                    }
                    elapsed += Time.deltaTime;
                    _visibleCharCount = Mathf.Clamp(Mathf.FloorToInt(elapsed * charsPerSec), 0, _currentFullText.Length);
                    yield return null;
                }
                _visibleCharCount = _currentFullText.Length;

                holdTimer = 0f;
                while (holdTimer < 3.2f && !_advanceRequested)
                {
                    holdTimer += Time.deltaTime;
                    yield return null;
                }
                _advanceRequested = false;

                if (_audioSource != null)
                {
                    _audioSource.Stop();
                }
            }

            // =========================================================================
            // STAGE 4: Radio Cuts Out & Turns OFF (Light & Power Off!)
            // =========================================================================
            TurnOffRadio();

            _dialogueStage = 4;
            _currentSpeaker = "RADIO";
            _currentFullText = "[ *STATIC* ... SIGNAL LOST — RADIO TURNED OFF ]";
            _visibleCharCount = _currentFullText.Length;

            yield return new WaitForSeconds(2.2f);

            _dialogueStage = 0;
            _isDialoguePlaying = false;
        }

        public void TurnOffRadio()
        {
            isTurnedOff = true;
            SetRadioPowerVisuals(false);

            if (_cabinStaticSource != null && _cabinStaticSource.isPlaying)
            {
                _cabinStaticSource.Stop();
            }

            if (_ambientStaticSource != null && _ambientStaticSource.isPlaying)
            {
                _ambientStaticSource.Stop();
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.PlayOneShot(CreateRadioPowerOffClip(), 1.0f);
            }
        }

        private void SetRadioPowerVisuals(bool poweredOn)
        {
            // Toggle all Light components on this radio
            if (radioLight != null) radioLight.enabled = poweredOn;
            if (dialSpotLight != null) dialSpotLight.enabled = poweredOn;

            Light[] allLights = GetComponentsInChildren<Light>(true);
            foreach (Light l in allLights)
            {
                if (l != null) l.enabled = poweredOn;
            }

            // Toggle LED indicator color
            if (_ledRenderer != null && _ledRenderer.material != null)
            {
                Color c = poweredOn ? new Color(0.3f, 1.0f, 0.4f) : new Color(0.08f, 0.08f, 0.08f);
                _ledRenderer.material.color = c;
                if (_ledRenderer.material.HasProperty("_BaseColor"))
                    _ledRenderer.material.SetColor("_BaseColor", c);
            }

            // Toggle Radio dial emission
            if (_radioRenderer != null && _radioRenderer.sharedMaterial != null)
            {
                _radioRenderer.GetPropertyBlock(_propBlock);
                Color emColor = poweredOn ? new Color(0.35f, 0.28f, 0.16f, 1f) : Color.black;
                _propBlock.SetColor("_EmissionColor", emColor);
                _radioRenderer.SetPropertyBlock(_propBlock);
            }
        }

        private void InitGuiTextures()
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1);
                _bgTex.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.08f, 0.88f));
                _bgTex.Apply();
            }

            if (_dividerTex == null)
            {
                _dividerTex = new Texture2D(1, 1);
                _dividerTex.SetPixel(0, 0, new Color(0.92f, 0.94f, 0.96f, 0.40f));
                _dividerTex.Apply();
            }

            if (_borderTex == null)
            {
                _borderTex = new Texture2D(1, 1);
                _borderTex.SetPixel(0, 0, new Color(0.95f, 0.75f, 0.25f, 0.95f));
                _borderTex.Apply();
            }

            if (_speakerTagTex == null)
            {
                _speakerTagTex = new Texture2D(1, 1);
                _speakerTagTex.SetPixel(0, 0, new Color(0.20f, 0.85f, 0.45f, 0.95f));
                _speakerTagTex.Apply();
            }

            if (_signalBarOnTex == null)
            {
                _signalBarOnTex = new Texture2D(1, 1);
                _signalBarOnTex.SetPixel(0, 0, new Color(1.0f, 0.80f, 0.25f, 0.95f));
                _signalBarOnTex.Apply();
            }

            if (_signalBarOffTex == null)
            {
                _signalBarOffTex = new Texture2D(1, 1);
                _signalBarOffTex.SetPixel(0, 0, new Color(0.35f, 0.38f, 0.42f, 0.40f));
                _signalBarOffTex.Apply();
            }
        }

        private void OnGUI()
        {
            if (StarterAssets.Boat4EscapeInteraction.Instance != null && StarterAssets.Boat4EscapeInteraction.Instance.isEscaping) return;

            if (_bgTex == null || _dividerTex == null)
            {
                InitGuiTextures();
            }

            Font regFont = dialogueFont != null ? dialogueFont : StarterAssets.DialogueFontProvider.Regular;
            Font boldFont = dialogueBoldFont != null ? dialogueBoldFont : StarterAssets.DialogueFontProvider.Bold;
            if (regFont != null) GUI.skin.font = regFont;

            // 0. Cabin radio static search prompt when inside Cabin1_DM
            if (_isPlayerInsideCabin && !isTurnedOff && !_isDialoguePlaying && !_hasListenedToRadio)
            {
                if (StarterAssets.Boat4EscapeInteraction.Instance == null || !StarterAssets.Boat4EscapeInteraction.Instance.isEscaping)
                {
                    if (StarterAssets.PlayerInventoryBag.Instance == null || !StarterAssets.PlayerInventoryBag.Instance.IsBagOpen)
                    {
                        DrawCabinRadioStaticPrompt();
                    }
                }
            }

            // 1. Draw [E] Interaction Prompt when player is near the active radio
            if (!isTurnedOff && !_isDialoguePlaying && _playerInRange)
            {
                float pw = 340f;
                float ph = 44f;
                float px = (Screen.width - pw) * 0.5f;
                float py = Screen.height * 0.56f;

                if (_bgTex != null)
                {
                    GUI.DrawTexture(new Rect(px - 2, py - 2, pw + 4, ph + 4), _bgTex);
                }
                if (_borderTex != null)
                {
                    GUI.DrawTexture(new Rect(px - 2, py - 2, pw + 4, 3f), _borderTex);
                }

                GUIStyle promptStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) promptStyle.font = boldFont;
                promptStyle.fontSize = 16;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.alignment = TextAnchor.MiddleCenter;
                promptStyle.richText = true;
                promptStyle.normal.textColor = Color.white;

                GUI.Label(new Rect(px, py, pw, ph), "PRESS <color=#FFD147>[E]</color> TO LISTEN TO <color=#55FF88>RADIO</color>", promptStyle);
            }

            // 2. Draw HUD badge if player has heard radio and panel is closed
            if (_hasListenedToRadio && (_lastListenedRadio == null || _lastListenedRadio == this) && !_isDialoguePlaying && !_isJournalOpen)
            {
                float hx = 45f;
                float hy = 42f;
                float hw = 140f;
                float hh = 26f;

                if (_bgTex != null)
                {
                    GUI.DrawTexture(new Rect(hx, hy, hw, hh), _bgTex);
                }
                if (_dividerTex != null)
                {
                    GUI.DrawTexture(new Rect(hx, hy + hh - 1.5f, hw, 1.5f), _dividerTex);
                }

                GUIStyle logBadgeStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) logBadgeStyle.font = boldFont;
                logBadgeStyle.fontSize = 12;
                logBadgeStyle.fontStyle = FontStyle.Bold;
                logBadgeStyle.alignment = TextAnchor.MiddleCenter;
                logBadgeStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.85f);
                logBadgeStyle.richText = true;

                GUI.Label(new Rect(hx, hy, hw, hh), "<color=#FFD147>[J]</color> QUEST LOG", logBadgeStyle);
            }

            // 3. Draw Radio Announcement / Quest Panel matching reference design
            bool isAnnounceActive = (_isDialoguePlaying && _dialogueStage > 0) || (_isJournalOpen && (_lastListenedRadio == null || _lastListenedRadio == this));
            if (isAnnounceActive)
            {
                float panelW = Mathf.Min(560f, Screen.width - 60f);
                float padX = 24f;
                float padY = 20f;
                float contentW = panelW - padX * 2f;

                float titleH = 24f;
                float divH = 1.5f;

                string titleStr = !string.IsNullOrEmpty(questName) ? questName.ToUpper() : "SURVIVE THE INFESTATION";
                if (_isDialoguePlaying && !string.IsNullOrEmpty(_currentSpeaker))
                {
                    titleStr = _currentSpeaker.ToUpper();
                }

                string descText = "";
                if (_isDialoguePlaying)
                {
                    descText = (_visibleCharCount >= _currentFullText.Length)
                        ? _currentFullText
                        : _currentFullText.Substring(0, Mathf.Clamp(_visibleCharCount, 0, _currentFullText.Length));
                }
                else
                {
                    // Journal / Log mode: complete mission lore
                    descText = !string.IsNullOrEmpty(radioReplyText)
                        ? $"\"{radioDialogueText}\"\n\nSurvivor Camp Reply:\n\"{radioReplyText}\""
                        : $"\"{radioDialogueText}\"";
                }

                // GUI Styles
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) titleStyle.font = boldFont;
                titleStyle.fontSize = 18;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.alignment = TextAnchor.MiddleLeft;
                titleStyle.normal.textColor = Color.white;

                GUIStyle descStyle = new GUIStyle(GUI.skin.label);
                if (regFont != null) descStyle.font = regFont;
                descStyle.fontSize = 14;
                descStyle.fontStyle = FontStyle.Normal;
                descStyle.wordWrap = true;
                descStyle.alignment = TextAnchor.UpperLeft;
                descStyle.normal.textColor = new Color(0.86f, 0.88f, 0.90f, 0.95f);

                GUIContent descContent = new GUIContent(descText);
                float descH = Mathf.Max(65f, descStyle.CalcHeight(descContent, contentW));

                GUIStyle hintsHeaderStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) hintsHeaderStyle.font = boldFont;
                hintsHeaderStyle.fontSize = 15;
                hintsHeaderStyle.fontStyle = FontStyle.Bold;
                hintsHeaderStyle.alignment = TextAnchor.MiddleLeft;
                hintsHeaderStyle.normal.textColor = Color.white;

                GUIStyle hintBulletStyle = new GUIStyle(GUI.skin.label);
                if (regFont != null) hintBulletStyle.font = regFont;
                hintBulletStyle.fontSize = 13;
                hintBulletStyle.fontStyle = FontStyle.Normal;
                hintBulletStyle.wordWrap = true;
                hintBulletStyle.alignment = TextAnchor.UpperLeft;
                hintBulletStyle.normal.textColor = new Color(0.80f, 0.83f, 0.86f, 0.95f);

                string[] hints = questHints;
                if (hints == null || hints.Length == 0)
                {
                    hints = new string[]
                    {
                        "Search abandoned villas & structures for weapons, ammo, and food",
                        "Locate a seaworthy boat to escape toward the East Survivor Camp",
                        "Find a safe place and wait for the next call"
                    };
                }

                float hintsTotalH = 0f;
                float[] hintHeights = new float[hints.Length];
                for (int i = 0; i < hints.Length; i++)
                {
                    GUIContent hc = new GUIContent("•  " + hints[i]);
                    hintHeights[i] = Mathf.Max(20f, hintBulletStyle.CalcHeight(hc, contentW));
                    hintsTotalH += hintHeights[i] + 4f;
                }

                float footerH = 22f;
                float panelH = padY + titleH + 8f + divH + 12f + descH + 16f + 20f + 6f + divH + 10f + hintsTotalH + 12f + footerH + padY;

                float panelX = 45f;
                float panelY = 48f;

                // Draw background panel
                if (_bgTex != null)
                {
                    GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _bgTex);
                }

                float curY = panelY + padY;

                // 1. Header Title (matching "QUEST NAME")
                GUI.Label(new Rect(panelX + padX, curY, contentW, titleH), titleStr, titleStyle);
                curY += titleH + 8f;

                // 2. Horizontal Divider Line
                if (_dividerTex != null)
                {
                    GUI.DrawTexture(new Rect(panelX + padX, curY, contentW, divH), _dividerTex);
                }
                curY += divH + 12f;

                // 3. Description Body Text
                GUI.Label(new Rect(panelX + padX, curY, contentW, descH), descText, descStyle);
                curY += descH + 16f;

                // 4. HINTS: Section Title
                GUI.Label(new Rect(panelX + padX, curY, contentW, 20f), "HINTS:", hintsHeaderStyle);
                curY += 20f + 6f;

                // 5. Horizontal Divider Line under HINTS:
                if (_dividerTex != null)
                {
                    GUI.DrawTexture(new Rect(panelX + padX, curY, Mathf.Min(320f, contentW), divH), _dividerTex);
                }
                curY += divH + 10f;

                // 6. Bullet Point Hints
                for (int i = 0; i < hints.Length; i++)
                {
                    GUI.Label(new Rect(panelX + padX, curY, contentW, hintHeights[i]), "•  " + hints[i], hintBulletStyle);
                    curY += hintHeights[i] + 4f;
                }
                curY += 12f;

                // 7. Footer continuation prompt
                GUIStyle footerStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) footerStyle.font = boldFont;
                footerStyle.fontSize = 11;
                footerStyle.fontStyle = FontStyle.Bold;
                footerStyle.alignment = TextAnchor.MiddleRight;
                footerStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f, 0.85f);

                string promptText = _isDialoguePlaying ? "[Press E to continue]" : "[Press J or ESC to close]";
                GUI.Label(new Rect(panelX + padX, curY, contentW, footerH), promptText, footerStyle);
            }
        }

        // =========================================================================
        // Procedural Radio Sound Effects (Zero external audio file dependencies)
        // =========================================================================
        private static AudioClip CreateStandbyHumClip()
        {
            int sampleRate = 22050;
            int totalSamples = sampleRate * 2;
            float[] data = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.15f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.08f;
                float crackle = (Random.value * 2f - 1f) * 0.18f;
                data[i] = (hum + crackle) * 0.35f;
            }
            AudioClip clip = AudioClip.Create("RadioStandbyHum", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateRadioTransmissionClip(float duration)
        {
            int sampleRate = 22050;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Initial tuning squelch (first 0.25s)
                if (t < 0.25f)
                {
                    float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1800f, 620f, t / 0.25f) * t);
                    float noise = (Random.value * 2f - 1f);
                    data[i] = (sweep * 0.4f + noise * 0.6f) * 0.35f;
                }
                else
                {
                    // Band-limited radio voice cadence + warm walkie-talkie static
                    float syllableEnv = Mathf.Max(0f, Mathf.Sin(t * 14f) * 0.6f + Mathf.Sin(t * 6.5f) * 0.5f);
                    float f0 = 145f + Mathf.Sin(t * 4.2f) * 25f;
                    float voice = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.45f
                                + Mathf.Sin(2f * Mathf.PI * f0 * 2f * t) * 0.30f
                                + Mathf.Sin(2f * Mathf.PI * f0 * 3f * t) * 0.15f;
                    float staticNoise = (Random.value * 2f - 1f) * 0.14f;
                    data[i] = (voice * syllableEnv * 0.32f + staticNoise);
                }
            }

            AudioClip clip = AudioClip.Create("RadioVoiceBroadcast", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePlayerMicClickClip()
        {
            int sampleRate = 22050;
            int totalSamples = (int)(sampleRate * 0.25f);
            float[] data = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float click = Mathf.Exp(-t * 45f) * Mathf.Sin(2f * Mathf.PI * 950f * t);
                data[i] = click * 0.5f;
            }
            AudioClip clip = AudioClip.Create("PlayerMicClick", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateRadioPowerOffClip()
        {
            int sampleRate = 22050;
            int totalSamples = (int)(sampleRate * 0.55f);
            float[] data = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Static burst followed by mechanical switch click and power-down pitch drop
                float staticBurst = (t < 0.18f) ? (Random.value * 2f - 1f) * 0.55f : 0f;
                float click = Mathf.Exp(-Mathf.Abs(t - 0.18f) * 80f) * 0.9f;
                float powerDrop = (t >= 0.18f)
                    ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(520f, 60f, (t - 0.18f) / 0.37f) * t) * Mathf.Exp(-(t - 0.18f) * 10f) * 0.45f
                    : 0f;
                data[i] = Mathf.Clamp(staticBurst + click + powerDrop, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("RadioPowerOff", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // =========================================================================
        // Cabin Static Sound & Search Prompt System
        // =========================================================================
        private void SetupCabinStaticAudio()
        {
            Transform child = transform.Find("CabinRadioStaticSource");
            if (child == null)
            {
                GameObject go = new GameObject("CabinRadioStaticSource");
                go.transform.SetParent(transform, false);
                _cabinStaticSource = go.AddComponent<AudioSource>();
            }
            else
            {
                _cabinStaticSource = child.GetComponent<AudioSource>();
            }

            _cabinStaticSource.spatialBlend = 0.45f;
            _cabinStaticSource.minDistance = 2.5f;
            _cabinStaticSource.maxDistance = 25.0f;
            _cabinStaticSource.rolloffMode = AudioRolloffMode.Linear;
            _cabinStaticSource.loop = true;
            _cabinStaticSource.volume = 0f;
            _cabinStaticSource.playOnAwake = false;
            _cabinStaticSource.clip = CreateCabinRadioStaticClip();
        }

        private void FindAssociatedCabin()
        {
            if (_hasCabinBounds) return;

            // 1. Direct search for Cabin1_DM if near this radio
            GameObject directCabin = GameObject.Find("Cabin1_DM");
            if (directCabin != null && Vector3.Distance(transform.position, directCabin.transform.position) < 25f)
            {
                _associatedCabin = directCabin;
                _cabinBounds = CalculateHierarchyBounds(directCabin);
                _hasCabinBounds = true;
                return;
            }

            // 2. Search all scene objects with Cabin1_DM or Cabin in their name
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>();
            GameObject closestCabin = null;
            float closestDist = float.MaxValue;

            foreach (GameObject go in allObjects)
            {
                if (go == null) continue;
                string lower = go.name.ToLowerInvariant();
                if (lower.Contains("cabin1_dm") || lower.Contains("cabin1") || (lower.StartsWith("cabin") && !lower.Contains("door")))
                {
                    float d = Vector3.Distance(transform.position, go.transform.position);
                    if (d < 22f && d < closestDist)
                    {
                        closestDist = d;
                        closestCabin = go;
                    }
                }
            }

            if (closestCabin != null)
            {
                _associatedCabin = closestCabin;
                _cabinBounds = CalculateHierarchyBounds(closestCabin);
                _hasCabinBounds = true;
                return;
            }

            // 3. Known fallback for Radio_1 in Cabin1_DM
            if (gameObject.name == "Radio_1" || Vector3.Distance(transform.position, new Vector3(555.9f, 19.1f, 268.9f)) < 8.0f)
            {
                _cabinBounds = new Bounds(new Vector3(559.2f, 19.5f, 274.67f), new Vector3(14.0f, 7.5f, 14.0f));
                _hasCabinBounds = true;
            }
        }

        private static Bounds CalculateHierarchyBounds(GameObject root)
        {
            Bounds b = new Bounds(root.transform.position, Vector3.zero);
            bool initial = false;

            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c == null || c.isTrigger) continue;
                if (!initial)
                {
                    b = c.bounds;
                    initial = true;
                }
                else
                {
                    b.Encapsulate(c.bounds);
                }
            }

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!initial)
                {
                    b = r.bounds;
                    initial = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (!initial)
            {
                b = new Bounds(root.transform.position + Vector3.up * 2.5f, new Vector3(13f, 7f, 13f));
            }

            return b;
        }

        private void UpdateCabinProximityAndStaticAudio()
        {
            if (isTurnedOff || _hasListenedToRadio || _isDialoguePlaying)
            {
                _isPlayerInsideCabin = false;
                if (_cabinStaticSource != null && _cabinStaticSource.isPlaying)
                {
                    _cabinStaticSource.volume = Mathf.MoveTowards(_cabinStaticSource.volume, 0f, Time.deltaTime * 3.5f);
                    if (_cabinStaticSource.volume <= 0.005f)
                    {
                        _cabinStaticSource.Stop();
                    }
                }
                return;
            }

            if (_playerTransform == null) return;

            if (!_hasCabinBounds)
            {
                _cabinSearchRetryTimer += Time.deltaTime;
                if (_cabinSearchRetryTimer >= 1.0f)
                {
                    _cabinSearchRetryTimer = 0f;
                    FindAssociatedCabin();
                }
            }

            // Check if player is inside the house
            bool inside = false;
            Vector3 playerPos = _playerTransform.position;

            if (_hasCabinBounds)
            {
                Bounds expanded = _cabinBounds;
                expanded.Expand(new Vector3(1.2f, 1.0f, 1.2f));
                inside = expanded.Contains(playerPos) || expanded.Contains(playerPos + Vector3.up * 1.0f);
            }

            if (!inside && _associatedCabin != null)
            {
                Vector3 cPos = _associatedCabin.transform.position;
                float dHoriz = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(cPos.x, cPos.z));
                float dVert = Mathf.Abs(playerPos.y - cPos.y);
                if (dHoriz <= 8.8f && dVert <= 5.0f)
                {
                    inside = true;
                }
            }

            if (!inside)
            {
                // Also check proximity to Radio_1 inside Cabin1_DM
                float dHorizRadio = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(transform.position.x, transform.position.z));
                float dVertRadio = Mathf.Abs(playerPos.y - transform.position.y);
                if (dHorizRadio <= 9.2f && dVertRadio <= 4.0f)
                {
                    inside = true;
                }
            }

            _isPlayerInsideCabin = inside;

            // Handle Static Audio Volume & Playback
            if (_cabinStaticSource != null)
            {
                if (_isPlayerInsideCabin)
                {
                    if (!_cabinStaticSource.isPlaying)
                    {
                        _cabinStaticSource.Play();
                    }

                    float distToRadio = Vector3.Distance(playerPos, transform.position);
                    float proximity = 1f - Mathf.Clamp01(distToRadio / 11f);
                    float targetVol = Mathf.Lerp(0.38f, 0.72f, proximity);

                    _cabinStaticSource.volume = Mathf.MoveTowards(_cabinStaticSource.volume, targetVol, Time.deltaTime * 2.2f);
                }
                else
                {
                    if (_cabinStaticSource.isPlaying)
                    {
                        _cabinStaticSource.volume = Mathf.MoveTowards(_cabinStaticSource.volume, 0f, Time.deltaTime * 2.5f);
                        if (_cabinStaticSource.volume <= 0.005f)
                        {
                            _cabinStaticSource.Stop();
                        }
                    }
                }
            }
        }

        private void DrawCabinRadioStaticPrompt()
        {
            float pw = Mathf.Min(490f, Screen.width - 40f);
            float ph = 64f;
            float px = (Screen.width - pw) * 0.5f;
            float py = Screen.height * 0.16f;

            // 1. Background dark slate panel
            if (_bgTex != null)
            {
                GUI.DrawTexture(new Rect(px - 2, py - 2, pw + 4, ph + 4), _bgTex);
            }

            // 2. Top pulsating amber alert bar
            float pulse = 0.75f + Mathf.PingPong(Time.time * 2.2f, 0.25f);
            Color barCol = new Color(1.0f, 0.76f, 0.22f, pulse);
            Color oldCol = GUI.color;
            GUI.color = barCol;
            if (_borderTex != null)
            {
                GUI.DrawTexture(new Rect(px - 2, py - 2, pw + 4, 3.5f), _borderTex);
            }
            GUI.color = oldCol;

            // 3. Subtle outer border outline
            if (_borderTex != null)
            {
                Color borderFaint = new Color(0.95f, 0.75f, 0.25f, 0.28f);
                GUI.color = borderFaint;
                GUI.DrawTexture(new Rect(px - 2, py - 2, 1.5f, ph + 4), _borderTex);
                GUI.DrawTexture(new Rect(px + pw + 0.5f, py - 2, 1.5f, ph + 4), _borderTex);
                GUI.DrawTexture(new Rect(px - 2, py + ph + 0.5f, pw + 4, 1.5f), _borderTex);
                GUI.color = oldCol;
            }

            Font regFont = dialogueFont != null ? dialogueFont : StarterAssets.DialogueFontProvider.Regular;
            Font boldFont = dialogueBoldFont != null ? dialogueBoldFont : StarterAssets.DialogueFontProvider.Bold;

            // 4. Header title with radio wave indicator
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            if (boldFont != null) headerStyle.font = boldFont;
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.richText = true;
            headerStyle.normal.textColor = new Color(1.0f, 0.82f, 0.30f);

            GUI.Label(new Rect(px, py + 8f, pw, 22f), "[ 📻 RADIO STATIC DETECTED ]", headerStyle);

            // 5. Signal strength indicator bars in upper-right corner
            float barStartX = px + pw - 48f;
            float barBaseY = py + 12f;
            float distToRadio = (_playerTransform != null) ? Vector3.Distance(_playerTransform.position, transform.position) : 99f;
            int activeBars = (distToRadio < 3.2f) ? 4 : (distToRadio < 5.2f) ? 3 : (distToRadio < 7.5f) ? 2 : 1;

            for (int b = 0; b < 4; b++)
            {
                float bH = 6f + b * 3.5f;
                float bX = barStartX + b * 8f;
                float bY = barBaseY + (16f - bH);
                Texture2D barTex = (b < activeBars) ? _signalBarOnTex : _signalBarOffTex;
                if (barTex != null)
                {
                    GUI.DrawTexture(new Rect(bX, bY, 5f, bH), barTex);
                }
            }

            // 6. Subtitle description
            GUIStyle subStyle = new GUIStyle(GUI.skin.label);
            if (regFont != null) subStyle.font = regFont;
            subStyle.fontSize = 12;
            subStyle.fontStyle = FontStyle.Normal;
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.richText = true;
            subStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f, 0.95f);

            string subMsg;
            if (_playerInRange)
            {
                subMsg = "Radio located! Press <color=#FFD147>[E]</color> to tune in to transmission.";
            }
            else
            {
                subMsg = "Faint static crackling nearby... Search the house for the radio.";
            }

            GUI.Label(new Rect(px, py + 32f, pw, 22f), subMsg, subStyle);
        }

        private static AudioClip CreateCabinRadioStaticClip()
        {
            int sampleRate = 22050;
            float duration = 3.5f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[totalSamples];

            float b0 = 0f, b1 = 0f, b2 = 0f, b3 = 0f, b4 = 0f, b5 = 0f, b6 = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                float white = (Random.value * 2f - 1f);

                // Filtered pink noise algorithm
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                b3 = 0.86650f * b3 + white * 0.3104856f;
                b4 = 0.55000f * b4 + white * 0.5329522f;
                b5 = -0.7616f * b5 - white * 0.0168980f;
                float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
                b6 = white * 0.115926f;
                pink *= 0.10f;

                // Analog tuning crackle & pops
                float crackle = 0f;
                if (Random.value < 0.0035f)
                {
                    crackle = (Random.value * 2f - 1f) * 0.35f;
                }

                // Faint 60Hz hum & heterodyne tuner whistle
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.04f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.025f;
                float whistle = Mathf.Sin(2f * Mathf.PI * (860f + Mathf.Sin(t * 1.5f) * 40f) * t) * 0.03f;

                // Loop smoothing near edges
                float edgeFade = 1f;
                float fadeDuration = 0.08f;
                if (t < fadeDuration) edgeFade = t / fadeDuration;
                else if (t > duration - fadeDuration) edgeFade = (duration - t) / fadeDuration;

                data[i] = Mathf.Clamp((pink * 0.70f + crackle + hum + whistle) * edgeFade, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("CabinRadioStaticNoise", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

