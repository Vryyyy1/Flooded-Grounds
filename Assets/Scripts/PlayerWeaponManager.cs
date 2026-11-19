using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

namespace StarterAssets
{
    /// <summary>
    /// Manages weapon looting (starts unarmed at first spawn so the player must loot a gun first),
    /// mounts the exact 3D gun model that was looted (Pistol, SMG, Shotgun, Assault Rifle, Sniper Rifle)
    /// into the Easy FPS character hands (GunAnimator.controller: Character_GUnTakeout, Player_Idle,
    /// Player_Walk, Player_Run, Player_Reload, Character_Malee), spawns bullet holes on surfaces,
    /// and drives responsive firing animation and zombie combat.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PlayerWeaponManager : MonoBehaviour
    {
        [Header("Weapon Placement")]
        [Tooltip("Transform where the gun is mounted. Defaults to FP_WeaponHolder under PlayerCameraRoot.")]
        public Transform weaponHoldPoint;

        [Header("Equipped Weapon")]
        public string equippedGunName = "";
        public GunType equippedGunType = GunType.AssaultRifle;
        public int currentAmmo = 0;
        public int reserveAmmo = 0;
        public int magazineCapacity = 0;
        public int damagePerShot = 35;
        public float fireRate = 0.14f;
        public bool isAutomatic = true;
        public float reloadDuration = 2.2f;
        public float range = 80f;
        public int pellets = 1;
        public float spreadAngle = 0.5f;

        [Header("State")]
        public bool hasWeapon = false;
        public bool isReloading = false;
        public bool isMeleeAttacking = false;
        public bool IsMeleeEquipped => hasWeapon && (equippedGunType == GunType.Knife || equippedGunType == GunType.Crowbar);
        [Tooltip("False by default: player spawns unarmed and must loot a gun first.")]
        public bool startWithRifle = false;

        [Header("Easy FPS Viewmodel & ADS Positioning")]
        [Tooltip("Local position in camera space for the looted weapon when resting at hip.")]
        public Vector3 easyFpsRestLocalPos = new Vector3(0.18f, -0.18f, 0.45f);

        [Tooltip("Local rotation in camera space for the weapon when resting at hip.")]
        public Quaternion easyFpsRestLocalRot = Quaternion.identity;

        [Tooltip("Target sight position in camera view when aiming (X=0 for dead center alignment with crosshair).")]
        public Vector3 targetSightPosition = new Vector3(0.00f, -0.085f, 0.35f);

        [Tooltip("Extra fine-tuning offset for ADS aiming (e.g., adjust Y to micro-adjust scope height).")]
        public Vector3 adsLocalOffset = Vector3.zero;

        [Tooltip("Transform anchor marking the iron sight alignment point. Moves to screen center when aiming.")]
        public Transform ironSight;

        [Tooltip("Whether to mount the iconic Easy FPS black IronSight addon model onto the gun (seen in Easy FPS 1.1 preview).")]
        public bool attachEasyFpsIronSightAddon = true;

        public float gunAimTime = 0.08f;
        public float normalFov = 60f;
        public float adsFov = 40f;
        public float adsSpeed = 14f;
        [Range(0f, 1f)] public float adsRecoilMultiplier = 0.6f;
        public bool isAiming = false;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip pistolShotClip;
        public AudioClip rifleShotClip;
        public AudioClip shotgunShotClip;
        public AudioClip reloadClip;
        public AudioClip emptyClickClip;
        public AudioClip lootPickupClip;

        [Header("Shooting Effects")]
        public bool ejectShellCasings = true;

        // Easy FPS runtime references
        private GameObject _heldWeaponInstance;
        private GameObject _lootedGunVisualPrefab;
        private Animator _easyFpsAnimator;
        private Transform _easyFpsMuzzleSpawn;
        private GameObject[] _easyFpsMuzzleFlashes;
        private GameObject _easyFpsBulletHolePrefab;
        private AudioSource _easyFpsShootAudio;
        private AudioSource _easyFpsReloadAudio;
        private AudioSource _easyFpsHitMarkerAudio;

        // Firing animation state (position kickback + rotational muzzle climb)
        private Vector3 _currentEasyFpsPos;
        private Vector3 _easyFpsPosVel;
        private Vector3 _easyFpsRecoilOffset;
        private Vector3 _easyFpsRecoilVel;
        private Quaternion _currentRecoilRot = Quaternion.identity;
        private Quaternion _currentSwayRot = Quaternion.identity;
        private Vector3 _reloadAnimOffset = Vector3.zero;
        private Quaternion _reloadAnimRot = Quaternion.identity;

        // Procedural Melee Attack (Slash & Smash) Animation State
        private Vector3 _meleeAnimOffset = Vector3.zero;
        private Quaternion _meleeAnimRot = Quaternion.identity;
        private int _meleeComboIndex = 0;
        private Coroutine _meleeAttackCoroutine;
        private AudioClip _knifeSwingWhoosh;
        private AudioClip _crowbarSmashWhoosh;
        private AudioClip _knifeFleshHit;
        private AudioClip _crowbarBluntHit;
        private float _sprintWeight = 0f;
        private float _fovVel;

        // Fallback muzzle light & cycling index
        private Light _muzzleFlashLight;
        private int _lastMuzzleFlashIndex = -1;
        [Tooltip("Size multiplier for muzzle fire flash")]
        [SerializeField] private float muzzleFlashScale = 0.82f;

        private float _nextFireTime;
        private float _reloadEndTime;
        private Camera _mainCam;
        private GunPickup _nearestLootable;

        // Camera kick / dip runtime (applied on PlayerCameraRoot after controller rotation)
        private Transform _camRoot;
        private Quaternion _fxApplied = Quaternion.identity;
        private Quaternion _fxLastWritten = Quaternion.identity;
        private bool _fxActive;
        private float _kickTimer, _kickDuration = 0.1f, _kickPitch, _kickJitter;
        private float _dipTimer, _dipDuration = 0.4f, _dipAngle;

        private StarterAssetsInputs _starterInputs;
        private FirstPersonController _fpController;
        private CharacterController _characterController;
        private Animator _playerAnimator;

        // Loot Banner Feedback
        private float _pickupBannerTimer = 0f;
        private string _pickupBannerName = "";
        private string _pickupBannerAmmo = "";
        private Color _pickupBannerColor = Color.white;

        // Textures for HUD & Bullet Holes
        private Texture2D _crosshairTex;
        private Texture2D _hudBgTex;
        private Texture2D _hudFillTex;
        private Texture2D _bulletHoleDecalTex;
        private Texture2D _sniperScopeTex;

        public bool HasGunEquipped => hasWeapon;
        public bool HasNearbyLootableGun => false;

        // Singleton Instance & Kill Banner
        public static PlayerWeaponManager Instance { get; private set; }
        private float _killBannerTimer = 0f;

        public void ShowTargetEliminated()
        {
            _killBannerTimer = 3.0f;
        }

        public void AddReserveAmmo(int amount)
        {
            reserveAmmo += amount;
            SyncEquippedGunToBag();
            ShowPickupBanner("AMMO REWARD", amount, reserveAmmo, new Color(1.0f, 0.85f, 0.2f));
        }

        private void Awake()
        {
            Instance = this;
            _mainCam = Camera.main;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            // Ensure player starts unarmed at first spawn and must loot a gun first
            startWithRifle = false;
            hasWeapon = false;
            isReloading = false;
            isMeleeAttacking = false;

            // Sanitize ads offset to keep iron sight centered at X = 0
            if (Mathf.Abs(adsLocalOffset.x) > 0.05f) adsLocalOffset.x = 0f;

            // Ensure gun is lowered during ADS so sight sits in lower center of view
            if (targetSightPosition.y > -0.05f) targetSightPosition = new Vector3(0.00f, -0.085f, 0.35f);

            InitProceduralGunAudio();
            InitHudTextures();
            InitBulletHoleDecalTexture();
            LoadEasyFpsMuzzleFlashes();
        }

        private void OnValidate()
        {
            if (Mathf.Abs(adsLocalOffset.x) > 0.05f) adsLocalOffset.x = 0f;
            if (targetSightPosition.y > -0.05f) targetSightPosition = new Vector3(0.00f, -0.085f, 0.35f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _starterInputs = GetComponent<StarterAssetsInputs>();
            if (_starterInputs == null) _starterInputs = GetComponentInParent<StarterAssetsInputs>();
            _fpController = GetComponent<FirstPersonController>();
            _characterController = GetComponent<CharacterController>();

            LocateWeaponHoldPoint();
            LocatePlayerAnimator();
            ConfigureFirstPersonCameraClippingAndBody();

            // Ensure PlayerCrawl component is active for prone and grass hiding mechanics
            if (GetComponent<PlayerCrawl>() == null)
            {
                gameObject.AddComponent<PlayerCrawl>();
            }

            // Clear any pre-existing weapon objects on first spawn so the player must loot a gun first
            ClearHeldWeaponAtSpawn();
        }

        private void ClearHeldWeaponAtSpawn()
        {
            if (_meleeAttackCoroutine != null)
            {
                StopCoroutine(_meleeAttackCoroutine);
                _meleeAttackCoroutine = null;
            }
            _meleeAnimOffset = Vector3.zero;
            _meleeAnimRot = Quaternion.identity;

            hasWeapon = false;
            isReloading = false;
            isMeleeAttacking = false;
            equippedGunName = "";
            currentAmmo = 0;
            reserveAmmo = 0;

            if (_heldWeaponInstance != null)
            {
                Destroy(_heldWeaponInstance);
                _heldWeaponInstance = null;
            }

            if (weaponHoldPoint != null)
            {
                for (int i = weaponHoldPoint.childCount - 1; i >= 0; i--)
                {
                    if (weaponHoldPoint.GetChild(i) != null)
                    {
                        Destroy(weaponHoldPoint.GetChild(i).gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the camera near clip plane to 0.01f (matching Easy FPS) so the hands and weapon
        /// never clip, and sets the third-person body mesh to ShadowsOnly so it never blocks the camera.
        /// </summary>
        private void ConfigureFirstPersonCameraClippingAndBody()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam != null)
            {
                _mainCam.nearClipPlane = 0.01f;
            }

            var vcam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                var lens = vcam.Lens;
                lens.NearClipPlane = 0.01f;
                vcam.Lens = lens;
            }

            Transform camRoot = weaponHoldPoint != null ? weaponHoldPoint.parent : transform.Find("PlayerCameraRoot");
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (camRoot != null && r.transform.IsChildOf(camRoot)) continue;
                if (r is SkinnedMeshRenderer || r.transform.name.Contains("Mesh") || r.transform.name.Contains("Body") || r.transform.name.Contains("Skeleton"))
                {
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        /// <summary>
        /// Instantiates and mounts ONLY the looted 3D weapon model (Low Poly Guns: Pistol, SMG, Shotgun, Assault Rifle, Sniper)
        /// directly into the player's weapon hold point. NewGun_auto from Easy FPS is completely eliminated.
        /// </summary>
        private void SpawnLootedGunViewModel(GameObject customLootedGunModel = null)
        {
            LocateWeaponHoldPoint();
            ConfigureFirstPersonCameraClippingAndBody();

            if (_heldWeaponInstance != null)
            {
                Destroy(_heldWeaponInstance);
                _heldWeaponInstance = null;
            }

            if (weaponHoldPoint != null)
            {
                for (int i = weaponHoldPoint.childCount - 1; i >= 0; i--)
                {
                    if (weaponHoldPoint.GetChild(i) != null)
                    {
                        Destroy(weaponHoldPoint.GetChild(i).gameObject);
                    }
                }
            }

            Transform parentHold = weaponHoldPoint != null ? weaponHoldPoint : transform;

            // Resolve the 3D model prefab for the looted weapon type
            GameObject sourceModel = customLootedGunModel;
            if (sourceModel == null)
            {
                if (equippedGunType == GunType.Knife)
                {
                    sourceModel = Resources.Load<GameObject>("SurvivalTools/Prefabs/knife");
#if UNITY_EDITOR
                    if (sourceModel == null)
                        sourceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/iDos Games/Stylized Knife/Prefabs/Knife.prefab");
#endif
                }
                else if (equippedGunType == GunType.Crowbar)
                {
                    sourceModel = Resources.Load<GameObject>("SurvivalTools/Prefabs/crowbar");
#if UNITY_EDITOR
                    if (sourceModel == null)
                        sourceModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowbar/Prefab/Crowbar.prefab");
#endif
                }
                else
                {
                    string resPath = GetResourcePathForGunType(equippedGunType);
                    GameObject resPickup = Resources.Load<GameObject>(resPath);
                    if (resPickup != null)
                    {
                        var gp = resPickup.GetComponent<GunPickup>();
                        if (gp != null && gp.weaponModelPrefab != null)
                        {
                            sourceModel = gp.weaponModelPrefab;
                        }
                        else
                        {
                            var modelChild = resPickup.transform.Find("Model");
                            sourceModel = modelChild != null ? modelChild.gameObject : resPickup;
                        }
                    }
                }
            }

            if (sourceModel == null)
            {
                Debug.LogError($"[PlayerWeapon] Cannot resolve visual model for gun: {equippedGunName} ({equippedGunType})");
                return;
            }

            // Instantiate the looted weapon directly under the weapon hold point
            _heldWeaponInstance = Instantiate(sourceModel, parentHold);
            _heldWeaponInstance.name = $"Held_{equippedGunType}_Weapon";

            // Strip any pickup scripts, colliders, or world lights
            var oldPickup = _heldWeaponInstance.GetComponent<GunPickup>();
            if (oldPickup != null) Destroy(oldPickup);
            var oldSurvival = _heldWeaponInstance.GetComponent<SurvivalItemPickup>();
            if (oldSurvival != null) Destroy(oldSurvival);
            foreach (var col in _heldWeaponInstance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }
            foreach (var l in _heldWeaponInstance.GetComponentsInChildren<Light>(true))
            {
                Destroy(l.gameObject);
            }

            // Set all children to Default layer (0) so MainCamera renders the looted weapon cleanly
            foreach (var t in _heldWeaponInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t != null) t.gameObject.layer = 0;
            }

            // Configure scale, resting, and muzzle position based on looted gun type
            float gunScale = 0.18f;
            easyFpsRestLocalPos = new Vector3(0.18f, -0.18f, 0.45f);
            easyFpsRestLocalRot = Quaternion.identity;
            Vector3 muzzleOffset = new Vector3(0f, 0.08f, 0.65f);

            switch (equippedGunType)
            {
                case GunType.Pistol:
                    gunScale = 0.22f;
                    easyFpsRestLocalPos = new Vector3(0.14f, -0.17f, 0.40f);
                    easyFpsRestLocalRot = Quaternion.identity;
                    muzzleOffset = new Vector3(0f, 0.06f, 0.42f);
                    break;
                case GunType.SMG:
                    gunScale = 0.19f;
                    easyFpsRestLocalPos = new Vector3(0.16f, -0.18f, 0.42f);
                    easyFpsRestLocalRot = Quaternion.identity;
                    muzzleOffset = new Vector3(0f, 0.07f, 0.52f);
                    break;
                case GunType.Shotgun:
                    gunScale = 0.19f;
                    easyFpsRestLocalPos = new Vector3(0.18f, -0.19f, 0.46f);
                    easyFpsRestLocalRot = Quaternion.identity;
                    muzzleOffset = new Vector3(0f, 0.08f, 0.70f);
                    break;
                case GunType.AssaultRifle:
                    gunScale = 0.18f;
                    easyFpsRestLocalPos = new Vector3(0.18f, -0.18f, 0.45f);
                    easyFpsRestLocalRot = Quaternion.identity;
                    muzzleOffset = new Vector3(0f, 0.08f, 0.70f);
                    break;
                case GunType.SniperRifle:
                    gunScale = 0.17f;
                    easyFpsRestLocalPos = new Vector3(0.18f, -0.19f, 0.48f);
                    easyFpsRestLocalRot = Quaternion.identity;
                    muzzleOffset = new Vector3(0f, 0.08f, 0.82f);
                    break;
                case GunType.Knife:
                    gunScale = 1.15f;
                    // First-person ready combat posture: Grip held at bottom right, blade extends diagonally up-forward towards center
                    easyFpsRestLocalPos = new Vector3(0.24f, -0.22f, 0.42f);
                    easyFpsRestLocalRot = Quaternion.Euler(30f, 142f, 38f);
                    muzzleOffset = new Vector3(0f, 0.06f, 0.40f);
                    break;
                case GunType.Crowbar:
                    gunScale = 0.40f;
                    // First-person ready combat posture: Shaft held at bottom right, prying hook head extends diagonally up-forward towards center
                    easyFpsRestLocalPos = new Vector3(0.25f, -0.24f, 0.44f);
                    easyFpsRestLocalRot = Quaternion.Euler(26f, 144f, 34f);
                    muzzleOffset = new Vector3(0f, 0.08f, 0.50f);
                    break;
            }

            if (equippedGunType == GunType.Knife)
            {
                SurvivalItemPickup.EnsureColoredURPMaterials(_heldWeaponInstance, SurvivalToolType.Melee_Knife);
            }
            else if (equippedGunType == GunType.Crowbar)
            {
                SurvivalItemPickup.EnsureColoredURPMaterials(_heldWeaponInstance, SurvivalToolType.Melee_Crowbar);
            }

            _heldWeaponInstance.transform.localScale = Vector3.one * gunScale;
            _heldWeaponInstance.transform.localPosition = easyFpsRestLocalPos;
            _heldWeaponInstance.transform.localRotation = easyFpsRestLocalRot;

            _currentEasyFpsPos = easyFpsRestLocalPos;
            _easyFpsPosVel = Vector3.zero;
            _easyFpsRecoilOffset = Vector3.zero;
            _easyFpsRecoilVel = Vector3.zero;
            _currentRecoilRot = Quaternion.identity;
            _currentSwayRot = Quaternion.identity;

            // Strictly disable iron sights and ADS for melee weapons
            if (IsMeleeEquipped)
            {
                ironSight = null;
                isAiming = false;
                foreach (var t in _heldWeaponInstance.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && (t.name == "ironSight" || t.name == "IronSight" || t.name == "IronSight_addon" || t.name.Contains("Iron Sight")))
                    {
                        Destroy(t.gameObject);
                    }
                }
            }
            else
            {
                // Setup or locate ironSight transform anchor under the gun
                SetupIronSightAnchor();
            }

            // Locate or create muzzle point at the very front tip of the gun barrel
            _easyFpsMuzzleSpawn = null;
            foreach (var t in _heldWeaponInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t != _heldWeaponInstance.transform)
                {
                    string n = t.name.ToLower();
                    if (n == "muzzlepoint" || n == "muzzle" || n == "muzzelspawn" || n == "bulletspawn")
                    {
                        _easyFpsMuzzleSpawn = t;
                        break;
                    }
                }
            }
            if (_easyFpsMuzzleSpawn == null)
            {
                GameObject muzzleObj = new GameObject("MuzzlePoint");
                muzzleObj.transform.SetParent(_heldWeaponInstance.transform, false);

                // Auto-detect front-most tip of the gun barrel using MeshFilter bounds
                MeshFilter mf = _heldWeaponInstance.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    Vector3 tipInMeshSpace = new Vector3(b.center.x, b.center.y + b.extents.y * 0.12f, b.max.z);
                    Vector3 worldTip = mf.transform.TransformPoint(tipInMeshSpace);
                    muzzleObj.transform.position = worldTip;
                }
                else
                {
                    muzzleObj.transform.localPosition = muzzleOffset;
                }
                _easyFpsMuzzleSpawn = muzzleObj.transform;
            }

            // Create dynamic muzzle flash point light
            GameObject flashGo = new GameObject("MuzzleFlashLight");
            flashGo.transform.SetParent(_easyFpsMuzzleSpawn, false);
            flashGo.transform.localPosition = Vector3.zero;
            _muzzleFlashLight = flashGo.AddComponent<Light>();
            _muzzleFlashLight.type = LightType.Point;
            _muzzleFlashLight.range = 3.5f;
            _muzzleFlashLight.intensity = 0f;
            _muzzleFlashLight.color = new Color(1.0f, 0.85f, 0.4f);
            _muzzleFlashLight.enabled = false;

            // Upgrade gun materials to URP Lit
            UpgradeEasyFpsMaterials(_heldWeaponInstance);
            LoadEasyFpsMuzzleFlashes();

            Debug.Log($"[PlayerWeapon] Successfully spawned looted gun {equippedGunName} ({equippedGunType}) with centered IronSight alignment.");
        }

        private void LoadEasyFpsMuzzleFlashes()
        {
            if (_easyFpsMuzzleFlashes != null && _easyFpsMuzzleFlashes.Length > 0) return;

            var list = new System.Collections.Generic.List<GameObject>();
            var all = Resources.LoadAll<GameObject>("MuzzelFlash");
            if (all != null && all.Length > 0)
            {
                foreach (var go in all)
                {
                    if (go != null && go.name.ToLower().Contains("muzzel"))
                    {
                        list.Add(go);
                    }
                }
            }

            if (list.Count == 0)
            {
                for (int i = 1; i <= 5; i++)
                {
                    var p = Resources.Load<GameObject>($"MuzzelFlash/muzzelFlash 0{i}");
                    if (p != null) list.Add(p);
                }
            }

            _easyFpsMuzzleFlashes = list.ToArray();
            Debug.Log($"[PlayerWeapon] Loaded {_easyFpsMuzzleFlashes.Length} Easy FPS Muzzle Flash prefabs.");
        }

        /// <summary>
        /// Locates or creates the ironSight anchor transform under the gun.
        /// This is the exact anchor that aligns with the crosshair line of sight when aiming down sights.
        /// Can also mount the Easy FPS IronSight 3D addon model (seen in Easy FPS 1.1 preview).
        /// </summary>
        private void SetupIronSightAnchor()
        {
            if (_heldWeaponInstance == null || IsMeleeEquipped) return;

            Transform isAnchor = null;
            foreach (var t in _heldWeaponInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && (t.name == "ironSight" || t.name == "IronSight"))
                {
                    isAnchor = t;
                    break;
                }
            }

            if (isAnchor == null)
            {
                GameObject isGo = new GameObject("ironSight");
                isGo.transform.SetParent(_heldWeaponInstance.transform, false);
                isAnchor = isGo.transform;
                isAnchor.localPosition = GetDefaultIronSightLocalPos(equippedGunType);
                isAnchor.localRotation = Quaternion.identity;
                isAnchor.localScale = Vector3.one;
            }

            ironSight = isAnchor;

            // Attach Easy FPS Iron Sight 3D model if requested and not already present
            if (attachEasyFpsIronSightAddon && ironSight.Find("IronSight_addon") == null)
            {
                GameObject sightPrefab = Resources.Load<GameObject>("Iron Sight_Low");
                if (sightPrefab != null)
                {
                    GameObject sightInstance = Instantiate(sightPrefab, ironSight);
                    sightInstance.name = "IronSight_addon";
                    sightInstance.transform.localPosition = Vector3.zero;
                    sightInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    sightInstance.transform.localScale = Vector3.one * 1.5f;

                    // Strip any colliders and upgrade materials to URP Lit
                    foreach (var c in sightInstance.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    UpgradeEasyFpsMaterials(sightInstance);
                }
            }
        }

        public Vector3 GetDefaultIronSightLocalPos(GunType type)
        {
            switch (type)
            {
                case GunType.Pistol:
                    return new Vector3(0.00f, 0.24f, 0.40f);
                case GunType.SMG:
                    return new Vector3(0.00f, 0.30f, 0.50f);
                case GunType.Shotgun:
                    return new Vector3(0.00f, 0.34f, 0.60f);
                case GunType.AssaultRifle:
                    return new Vector3(0.00f, 0.38f, 0.70f);
                case GunType.SniperRifle:
                    return new Vector3(0.00f, 0.42f, 0.80f);
                default:
                    return new Vector3(0.00f, 0.35f, 0.60f);
            }
        }

        private string GetResourcePathForGunType(GunType type)
        {
            switch (type)
            {
                case GunType.Pistol: return "Guns/Pistol_Pickup";
                case GunType.SMG: return "Guns/SMG_Pickup";
                case GunType.Shotgun: return "Guns/Shotgun_Pickup";
                case GunType.AssaultRifle: return "Guns/AssaultRifle_Pickup";
                case GunType.SniperRifle: return "Guns/SniperRifle_Pickup";
                default: return "Guns/AssaultRifle_Pickup";
            }
        }

        /// <summary>
        /// Permanently strips and destroys Easy FPS's default gun meshes (MainCharacterMesh, Magazine, Iron Sights)
        /// so that ONLY the character hands ("Body") remain to hold the real looted weapon.
        /// </summary>
        private void StripEasyFpsDefaultGunMeshes(GameObject root)
        {
            if (root == null) return;

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                string n = smr.gameObject.name;
                if (n == "MainCharacterMesh" || n == "Magazine" || n.Contains("Iron Sight") || (n != "Body" && !n.StartsWith("Mounted_")))
                {
                    smr.sharedMesh = null;
                    smr.enabled = false;
                    Destroy(smr.gameObject);
                }
            }

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null) continue;
                string n = mf.gameObject.name;
                if (n == "MainCharacterMesh" || n == "Magazine" || n.Contains("Iron Sight") || (n != "Body" && !n.StartsWith("Mounted_")))
                {
                    mf.sharedMesh = null;
                    Destroy(mf.gameObject);
                }
            }
        }

        private void UpgradeEasyFpsMaterials(GameObject root)
        {
            if (root == null) return;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = ShadowCastingMode.On;
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    if (urpLit != null && (m.shader == null || !m.shader.isSupported || m.shader.name == "Standard" || m.shader.name.Contains("InternalError")))
                    {
                        Texture mainTex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        if (mainTex == null && m.HasProperty("_MainTex")) mainTex = m.GetTexture("_MainTex");
                        Texture bumpTex = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                        Texture metalTex = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                        Texture occTex = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;

                        m.shader = urpLit;
                        if (mainTex != null)
                        {
                            m.SetTexture("_BaseMap", mainTex);
                            m.SetTexture("_MainTex", mainTex);
                        }
                        if (bumpTex != null)
                        {
                            m.SetTexture("_BumpMap", bumpTex);
                            m.EnableKeyword("_NORMALMAP");
                        }
                        if (metalTex != null)
                        {
                            m.SetTexture("_MetallicGlossMap", metalTex);
                            m.EnableKeyword("_METALLICSPECGLOSSMAP");
                        }
                        if (occTex != null)
                        {
                            m.SetTexture("_OcclusionMap", occTex);
                        }
                        m.SetColor("_BaseColor", Color.white);
                        changed = true;
                    }
                }
                if (changed) r.materials = mats;
            }
        }

        public void LocatePlayerAnimator()
        {
            foreach (var anim in GetComponentsInChildren<Animator>(true))
            {
                if (_heldWeaponInstance != null && anim.transform.IsChildOf(_heldWeaponInstance.transform)) continue;
                if (weaponHoldPoint != null && anim.transform.IsChildOf(weaponHoldPoint)) continue;
                _playerAnimator = anim;
                break;
            }
            if (_playerAnimator != null)
            {
                _playerAnimator.applyRootMotion = false;
            }
        }

        public void LocateWeaponHoldPoint()
        {
            Transform camRoot = transform.Find("PlayerCameraRoot");
            if (camRoot == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "PlayerCameraRoot" || t.CompareTag("CinemachineTarget"))
                    {
                        camRoot = t;
                        break;
                    }
                }
            }

            if (camRoot != null)
            {
                _camRoot = camRoot;
                var fpHolder = camRoot.Find("FP_WeaponHolder");
                if (fpHolder == null)
                {
                    GameObject holderGo = new GameObject("FP_WeaponHolder");
                    holderGo.transform.SetParent(camRoot, false);
                    fpHolder = holderGo.transform;
                }
                fpHolder.localPosition = Vector3.zero;
                fpHolder.localRotation = Quaternion.identity;
                fpHolder.localScale = Vector3.one;
                weaponHoldPoint = fpHolder;
                return;
            }

            if (weaponHoldPoint == null)
            {
                weaponHoldPoint = transform;
            }
        }

        private void Update()
        {
            var health = GetComponent<PlayerHealth>();
            if (health != null && health.isDead) return;

            // Pause combat input while inventory bag UI, Game Menu, or cinematic intro is active
            if ((PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen) ||
                (GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuOpen) ||
                CinematicWakeUpIntro.IsIntroActive)
            {
                UpdateEasyFpsAnimatorParameters(false);
                return;
            }

            // 1. Detect nearby lootable guns in houses
            FindNearbyLootable();

            // 2. Press E or F to loot gun
            if (_nearestLootable != null && WasLootPressed())
            {
                LootGun(_nearestLootable);
            }

            if (!hasWeapon) return;
            if (_heldWeaponInstance == null) SpawnLootedGunViewModel(_lootedGunVisualPrefab);

            // 3. Aim Down Sights (Right-click) - Firearms only, completely disabled for Melee weapons
            isAiming = !IsMeleeEquipped && IsAimingTriggered();

            // 4. Update Easy FPS character movement and aiming animations (Player_Idle, Player_Walk, Player_Run, Player_AImpose)
            bool fireHeld = IsFireTriggered(true);
            UpdateEasyFpsAnimatorParameters(fireHeld);

            // 4. Melee attack with Q or Left-Click (if Melee weapon is equipped)
            if (WasMeleePressed() && !isMeleeAttacking && !isReloading)
            {
                int meleeDmg = (equippedGunType == GunType.Knife) ? 60 : ((equippedGunType == GunType.Crowbar) ? 85 : 50);
                float meleeRng = (equippedGunType == GunType.Knife) ? 2.4f : ((equippedGunType == GunType.Crowbar) ? 2.6f : 2.4f);
                if (_meleeAttackCoroutine != null) StopCoroutine(_meleeAttackCoroutine);
                _meleeAttackCoroutine = StartCoroutine(ExecuteMeleeAttackRoutine(meleeDmg, meleeRng));
                return;
            }

            // 5. If Melee Weapon Equipped, handle LMB attack and prevent firearm reloading
            if (IsMeleeEquipped)
            {
                bool swingInput = IsFireTriggered(false);
                if (swingInput && Time.time >= _nextFireTime && !isMeleeAttacking)
                {
                    _nextFireTime = Time.time + (equippedGunType == GunType.Knife ? 0.32f : 0.46f);
                    int meleeDmg = (equippedGunType == GunType.Knife) ? 60 : 85;
                    float meleeRng = (equippedGunType == GunType.Knife) ? 2.4f : 2.6f;
                    if (_meleeAttackCoroutine != null) StopCoroutine(_meleeAttackCoroutine);
                    _meleeAttackCoroutine = StartCoroutine(ExecuteMeleeAttackRoutine(meleeDmg, meleeRng));
                }
                return;
            }

            // 6. Handle Reloading
            if (isReloading)
            {
                if (Time.time >= _reloadEndTime)
                {
                    FinishReload();
                }
                return;
            }

            if (WasReloadPressed() && currentAmmo < magazineCapacity && reserveAmmo > 0 && !isMeleeAttacking)
            {
                StartReload();
                return;
            }

            // 7. Handle Shooting & Firing Animation
            bool fireInput = IsFireTriggered(isAutomatic);
            if (fireInput && Time.time >= _nextFireTime && !isMeleeAttacking)
            {
                if (currentAmmo > 0)
                {
                    Fire();
                }
                else
                {
                    _nextFireTime = Time.time + 0.25f;
                    PlaySound(emptyClickClip, 0.7f);

                    if (reserveAmmo > 0)
                    {
                        StartReload();
                    }
                }
            }
        }

        /// <summary>
        /// Drives the Easy FPS GunAnimator.controller character animations:
        /// - walkSpeed (float): 0 when idle (Player_Idle), 3 when walking (Player_Walk), 5 when running
        /// - maxSpeed (int): 5 when sprinting with LeftShift (Player_Run), 3 otherwise
        /// </summary>
        private void UpdateEasyFpsAnimatorParameters(bool isFiring)
        {
            if (_easyFpsAnimator == null || !_easyFpsAnimator.isActiveAndEnabled || _easyFpsAnimator.runtimeAnimatorController == null) return;

            Vector2 moveInput = Vector2.zero;
            bool sprintInput = false;

            if (_starterInputs != null)
            {
                moveInput = _starterInputs.move;
                sprintInput = _starterInputs.sprint;
            }
            else if (Keyboard.current != null)
            {
                float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
                float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
                moveInput = new Vector2(x, y);
                sprintInput = Keyboard.current.leftShiftKey.isPressed;
            }

            bool isGrounded = _characterController == null || _characterController.isGrounded;
            bool isMoving = isGrounded && moveInput.sqrMagnitude > 0.04f;

            bool isRunning = isMoving && moveInput.y > 0.1f && sprintInput && !isReloading && !isMeleeAttacking && !isFiring;

            float animWalkSpeed = isMoving ? (isRunning ? 5f : 3f) : 0f;
            int animMaxSpeed = isRunning ? 5 : 3;

            _easyFpsAnimator.SetFloat("walkSpeed", animWalkSpeed);
            _easyFpsAnimator.SetInteger("maxSpeed", animMaxSpeed);
            _easyFpsAnimator.SetBool("aiming", isAiming);
        }

        private IEnumerator ExecuteMeleeAttackRoutine(int damage, float range)
        {
            isMeleeAttacking = true;
            int combo = _meleeComboIndex++;

            if (_easyFpsAnimator != null)
            {
                _easyFpsAnimator.SetBool("meeleAttack", true);
            }

            if (equippedGunType == GunType.Knife)
            {
                yield return StartCoroutine(AnimateKnifeSwing(combo, damage, range));
            }
            else if (equippedGunType == GunType.Crowbar)
            {
                yield return StartCoroutine(AnimateCrowbarSmash(combo, damage, range));
            }
            else
            {
                yield return StartCoroutine(AnimateRifleMeleeStrike(damage, range));
            }

            if (_easyFpsAnimator != null)
            {
                _easyFpsAnimator.SetBool("meeleAttack", false);
            }

            _meleeAnimOffset = Vector3.zero;
            _meleeAnimRot = Quaternion.identity;
            isMeleeAttacking = false;
        }

        private IEnumerator AnimateKnifeSwing(int combo, int damage, float range)
        {
            float duration = 0.30f;
            bool hitPerformed = false;
            if (_knifeSwingWhoosh != null) PlaySound(_knifeSwingWhoosh, 0.95f);

            bool isBackhand = (combo % 2 == 1);

            // Phase 1: Windup (0% to 22%)
            // Phase 2: Rapid Slash Across (22% to 52%)
            // Phase 3: Smooth Recovery back to Rest (52% to 100%)
            Vector3 windupPos = isBackhand ? new Vector3(-0.07f, 0.04f, -0.06f) : new Vector3(0.06f, 0.05f, -0.07f);
            Quaternion windupRot = isBackhand ? Quaternion.Euler(-14f, -22f, 16f) : Quaternion.Euler(-16f, 20f, -14f);

            Vector3 slashPos = isBackhand ? new Vector3(0.18f, -0.03f, 0.16f) : new Vector3(-0.25f, -0.05f, 0.18f);
            Quaternion slashRot = isBackhand ? Quaternion.Euler(18f, 36f, -30f) : Quaternion.Euler(24f, -48f, 40f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                if (p < 0.22f)
                {
                    float subP = Mathf.SmoothStep(0f, 1f, p / 0.22f);
                    _meleeAnimOffset = Vector3.Lerp(Vector3.zero, windupPos, subP);
                    _meleeAnimRot = Quaternion.Slerp(Quaternion.identity, windupRot, subP);
                }
                else if (p < 0.52f)
                {
                    float subP = (p - 0.22f) / 0.30f;
                    float smoothSlash = Mathf.SmoothStep(0f, 1f, subP);
                    _meleeAnimOffset = Vector3.Lerp(windupPos, slashPos, smoothSlash);
                    _meleeAnimRot = Quaternion.Slerp(windupRot, slashRot, smoothSlash);

                    if (!hitPerformed && subP >= 0.35f)
                    {
                        hitPerformed = true;
                        PerformMeleeHitDetection(damage, range, true);
                    }
                }
                else
                {
                    float subP = (p - 0.52f) / 0.48f;
                    float smoothRecover = Mathf.SmoothStep(0f, 1f, subP);
                    _meleeAnimOffset = Vector3.Lerp(slashPos, Vector3.zero, smoothRecover);
                    _meleeAnimRot = Quaternion.Slerp(slashRot, Quaternion.identity, smoothRecover);
                }

                yield return null;
            }

            if (!hitPerformed)
            {
                PerformMeleeHitDetection(damage, range, true);
            }

            _meleeAnimOffset = Vector3.zero;
            _meleeAnimRot = Quaternion.identity;
        }

        private IEnumerator AnimateCrowbarSmash(int combo, int damage, float range)
        {
            float duration = 0.44f;
            bool hitPerformed = false;
            if (_crowbarSmashWhoosh != null) PlaySound(_crowbarSmashWhoosh, 1.05f);

            bool isHorizontalSmash = (combo % 2 == 1);

            // Combo 0: Heavy Overhead Downward Smash (Crush downward)
            // Combo 1: Brutal Horizontal Side Smash (Pry/shatter sideways)
            Vector3 windupPos = isHorizontalSmash ? new Vector3(0.18f, 0.08f, -0.12f) : new Vector3(0.08f, 0.24f, -0.14f);
            Quaternion windupRot = isHorizontalSmash ? Quaternion.Euler(-18f, 52f, -34f) : Quaternion.Euler(-48f, 30f, -34f);

            Vector3 smashPos = isHorizontalSmash ? new Vector3(-0.28f, -0.06f, 0.22f) : new Vector3(-0.06f, -0.26f, 0.26f);
            Quaternion smashRot = isHorizontalSmash ? Quaternion.Euler(18f, -54f, 44f) : Quaternion.Euler(55f, -24f, 26f);

            Vector3 bouncePos = isHorizontalSmash ? new Vector3(-0.20f, -0.03f, 0.14f) : new Vector3(-0.03f, -0.14f, 0.14f);
            Quaternion bounceRot = isHorizontalSmash ? Quaternion.Euler(12f, -38f, 30f) : Quaternion.Euler(30f, -10f, 15f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                if (p < 0.28f)
                {
                    float subP = Mathf.SmoothStep(0f, 1f, p / 0.28f);
                    _meleeAnimOffset = Vector3.Lerp(Vector3.zero, windupPos, subP);
                    _meleeAnimRot = Quaternion.Slerp(Quaternion.identity, windupRot, subP);
                }
                else if (p < 0.52f)
                {
                    float subP = (p - 0.28f) / 0.24f;
                    float smashCurve = subP * subP;
                    _meleeAnimOffset = Vector3.Lerp(windupPos, smashPos, smashCurve);
                    _meleeAnimRot = Quaternion.Slerp(windupRot, smashRot, smashCurve);

                    if (!hitPerformed && subP >= 0.65f)
                    {
                        hitPerformed = true;
                        PerformMeleeHitDetection(damage, range, false);
                        StartCameraDip(isHorizontalSmash ? 4.5f : 6.5f, 0.18f);
                    }
                }
                else if (p < 0.64f)
                {
                    float subP = (p - 0.52f) / 0.12f;
                    float smoothBounce = Mathf.SmoothStep(0f, 1f, subP);
                    _meleeAnimOffset = Vector3.Lerp(smashPos, bouncePos, smoothBounce);
                    _meleeAnimRot = Quaternion.Slerp(smashRot, bounceRot, smoothBounce);
                }
                else
                {
                    float subP = (p - 0.64f) / 0.36f;
                    float smoothRecover = Mathf.SmoothStep(0f, 1f, subP);
                    _meleeAnimOffset = Vector3.Lerp(bouncePos, Vector3.zero, smoothRecover);
                    _meleeAnimRot = Quaternion.Slerp(bounceRot, Quaternion.identity, smoothRecover);
                }

                yield return null;
            }

            if (!hitPerformed)
            {
                PerformMeleeHitDetection(damage, range, false);
            }

            _meleeAnimOffset = Vector3.zero;
            _meleeAnimRot = Quaternion.identity;
        }

        private IEnumerator AnimateRifleMeleeStrike(int damage, float range)
        {
            float duration = 0.35f;
            bool hitPerformed = false;
            if (_crowbarSmashWhoosh != null) PlaySound(_crowbarSmashWhoosh, 0.85f);

            Vector3 buttWindup = new Vector3(-0.06f, 0.06f, -0.08f);
            Quaternion buttWindupRot = Quaternion.Euler(-14f, 20f, -18f);

            Vector3 buttStrike = new Vector3(0.04f, -0.08f, 0.22f);
            Quaternion buttStrikeRot = Quaternion.Euler(22f, -28f, 24f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                if (p < 0.25f)
                {
                    float subP = Mathf.SmoothStep(0f, 1f, p / 0.25f);
                    _meleeAnimOffset = Vector3.Lerp(Vector3.zero, buttWindup, subP);
                    _meleeAnimRot = Quaternion.Slerp(Quaternion.identity, buttWindupRot, subP);
                }
                else if (p < 0.50f)
                {
                    float subP = (p - 0.25f) / 0.25f;
                    float strikeP = Mathf.SmoothStep(0f, 1f, subP);
                    _meleeAnimOffset = Vector3.Lerp(buttWindup, buttStrike, strikeP);
                    _meleeAnimRot = Quaternion.Slerp(buttWindupRot, buttStrikeRot, strikeP);

                    if (!hitPerformed && subP >= 0.5f)
                    {
                        hitPerformed = true;
                        PerformMeleeHitDetection(damage, range, false);
                        StartCameraDip(4.0f, 0.15f);
                    }
                }
                else
                {
                    float subP = (p - 0.50f) / 0.50f;
                    float recoverP = Mathf.SmoothStep(0f, 1f, subP);
                    _meleeAnimOffset = Vector3.Lerp(buttStrike, Vector3.zero, recoverP);
                    _meleeAnimRot = Quaternion.Slerp(buttStrikeRot, Quaternion.identity, recoverP);
                }

                yield return null;
            }

            if (!hitPerformed)
            {
                PerformMeleeHitDetection(damage, range, false);
            }

            _meleeAnimOffset = Vector3.zero;
            _meleeAnimRot = Quaternion.identity;
        }

        private void PerformMeleeHitDetection(int damage, float range, bool isKnife)
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            Ray ray = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float radius = isKnife ? 0.45f : 0.55f;
            RaycastHit[] hits = Physics.SphereCastAll(ray, radius, range, ~((1 << 2) | (1 << 8)), QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider.gameObject == gameObject) continue;
                var zombie = h.collider.GetComponentInParent<ZombieAI>();
                if (zombie != null && !zombie.isDead)
                {
                    zombie.TakeDamage(damage, h.point, ray.direction);
                    CreateBloodImpact(h.point, h.normal);
                    if (_easyFpsHitMarkerAudio != null) _easyFpsHitMarkerAudio.Play();

                    if (isKnife && _knifeFleshHit != null)
                    {
                        PlaySound(_knifeFleshHit, 1.0f);
                    }
                    else if (!isKnife && _crowbarBluntHit != null)
                    {
                        PlaySound(_crowbarBluntHit, 1.15f);
                    }
                    break;
                }
            }
        }

        private IEnumerator EasyFpsMeleeRoutine()
        {
            return ExecuteMeleeAttackRoutine(50, 2.4f);
        }

        private bool WasMeleePressed()
        {
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) return true;
            try
            {
                if (Input.GetKeyDown(KeyCode.Q)) return true;
            }
            catch { }
            return false;
        }

        private bool WasLootPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame) return true;
            }
            try
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)) return true;
            }
            catch { }
            return false;
        }

        private bool WasReloadPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame) return true;
            }
            try
            {
                if (Input.GetKeyDown(KeyCode.R)) return true;
            }
            catch { }
            return false;
        }

        private bool IsFireTriggered(bool auto)
        {
            if (Mouse.current != null)
            {
                if (auto ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame) return true;
            }
            try
            {
                if (auto ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0)) return true;
            }
            catch { }
            return false;
        }

        private bool IsAimingTriggered()
        {
            if (IsMeleeEquipped) return false;
            if (!hasWeapon || isMeleeAttacking || isReloading) return false;
            if (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen) return false;

            if (Mouse.current != null)
            {
                if (Mouse.current.rightButton.isPressed) return true;
            }
            try
            {
                if (Input.GetMouseButton(1) || Input.GetAxis("Fire2") > 0.1f) return true;
            }
            catch { }
            return false;
        }

        public void LootGun(GunPickup pickup)
        {
            if (pickup == null) return;

            // Save currently equipped gun's ammo back to its slot in the backpack before switching/storing
            SyncEquippedGunToBag();

            var bag = PlayerInventoryBag.Instance != null ? PlayerInventoryBag.Instance : GetComponent<PlayerInventoryBag>();
            if (bag == null)
            {
                bag = gameObject.AddComponent<PlayerInventoryBag>();
            }

            bool stored = bag.AddGunToBag(pickup);
            if (!stored)
            {
                return;
            }

            var gunSlot = bag.GetGunSlot(pickup.gunType);

            equippedGunName = pickup.gunName;
            equippedGunType = pickup.gunType;
            damagePerShot = pickup.damage;
            fireRate = pickup.fireRate;
            isAutomatic = pickup.isAutomatic;
            magazineCapacity = pickup.magazineCapacity;
            currentAmmo = gunSlot != null ? gunSlot.currentAmmo : pickup.currentAmmo;
            reserveAmmo = gunSlot != null ? gunSlot.reserveAmmo : pickup.reserveAmmo;
            reloadDuration = Mathf.Max(2.1f, pickup.reloadTime);
            range = pickup.range;
            pellets = pickup.pelletsPerShot;
            spreadAngle = pickup.spreadAngle;
            hasWeapon = true;
            isReloading = false;
            isMeleeAttacking = false;

            _lootedGunVisualPrefab = pickup.weaponModelPrefab;
            GameObject visualSource = _lootedGunVisualPrefab;
            if (visualSource == null)
            {
                var modelChild = pickup.transform.Find("Model");
                visualSource = modelChild != null ? modelChild.gameObject : pickup.gameObject;
            }

            // Spawn ONLY the exact gun that was looted (zero NewGun_auto)
            SpawnLootedGunViewModel(visualSource);

            // Reach-down camera dip
            StartCameraDip(6f, 0.35f);

            // Show on-screen pickup banner
            ShowPickupBanner(equippedGunName, currentAmmo, reserveAmmo, GunPickup.GetGunColor(equippedGunType));

            Debug.Log($"[PlayerWeapon] Looted {equippedGunName} ({equippedGunType}) into Backpack! Ammo: {currentAmmo}/{reserveAmmo}");

            Destroy(pickup.gameObject);
            _nearestLootable = null;
        }

        public void EquipGunFromBagSlot(PlayerInventoryBag.BagSlot slot)
        {
            if (slot == null || !slot.isGun) return;

            SyncEquippedGunToBag();

            equippedGunName = slot.displayName;
            equippedGunType = slot.gunType;
            damagePerShot = slot.damage > 0 ? slot.damage : 35;
            fireRate = slot.fireRate > 0.01f ? slot.fireRate : 0.14f;
            isAutomatic = slot.isAutomatic;
            magazineCapacity = slot.magazineCapacity > 0 ? slot.magazineCapacity : 30;
            currentAmmo = slot.currentAmmo;
            reserveAmmo = slot.reserveAmmo;
            reloadDuration = Mathf.Max(2.1f, slot.reloadTime);
            range = slot.range > 1f ? slot.range : 80f;
            pellets = Mathf.Max(1, slot.pelletsPerShot);
            spreadAngle = slot.spreadAngle;
            hasWeapon = true;
            isReloading = false;
            isMeleeAttacking = false;

            _lootedGunVisualPrefab = slot.weaponModelPrefab;
            SpawnLootedGunViewModel(_lootedGunVisualPrefab);
            StartCameraDip(5f, 0.28f);
            ShowPickupBanner(equippedGunName, currentAmmo, reserveAmmo, GunPickup.GetGunColor(equippedGunType));
        }

        public void UnequipCurrentWeaponAndTryEquipNext()
        {
            ClearHeldWeaponAtSpawn();

            if (PlayerInventoryBag.Instance != null)
            {
                foreach (var s in PlayerInventoryBag.Instance.items)
                {
                    if (s.isGun)
                    {
                        EquipGunFromBagSlot(s);
                        return;
                    }
                }
            }
        }

        public void SyncEquippedGunToBag()
        {
            if (!hasWeapon) return;
            if (PlayerInventoryBag.Instance != null)
            {
                PlayerInventoryBag.Instance.SyncEquippedGunAmmo(equippedGunType, currentAmmo, reserveAmmo);
            }
        }

        private void ShowPickupBanner(string gunName, int ammo, int reserve, Color color)
        {
            // Prompt disabled per user request
            _pickupBannerTimer = 0f;
        }

        /// <summary>
        /// Drives the responsive firing animation on every shot:
        /// kicks the weapon and hands backward along Z, pops the barrel upward with pitch/roll recoil,
        /// and smoothly recovers back to the rest pose in LateUpdate.
        /// </summary>
        private void ApplyWeaponRecoil(GunType type)
        {
            float kickZ = 0.055f;
            float kickY = 0.014f;
            float pitchKick = 6.5f;
            float yawKick = 1.6f;

            switch (type)
            {
                case GunType.Pistol:
                    kickZ = 0.045f; kickY = 0.012f; pitchKick = 6.0f; yawKick = 1.2f;
                    break;
                case GunType.AssaultRifle:
                case GunType.SMG:
                    kickZ = 0.055f; kickY = 0.014f; pitchKick = 6.5f; yawKick = 1.6f;
                    break;
                case GunType.Shotgun:
                    kickZ = 0.085f; kickY = 0.022f; pitchKick = 11.0f; yawKick = 2.5f;
                    break;
                case GunType.SniperRifle:
                    kickZ = 0.095f; kickY = 0.025f; pitchKick = 12.5f; yawKick = 2.0f;
                    break;
            }

            _easyFpsRecoilOffset.z = Mathf.Max(_easyFpsRecoilOffset.z - kickZ, -0.11f);
            _easyFpsRecoilOffset.y = Mathf.Min(_easyFpsRecoilOffset.y + kickY, 0.04f);
            _easyFpsRecoilOffset.x += (Random.value - 0.5f) * 0.018f;

            Quaternion shotRotKick = Quaternion.Euler(
                -pitchKick,
                Random.Range(-yawKick, yawKick),
                Random.Range(-yawKick * 1.2f, yawKick * 1.2f)
            );
            _currentRecoilRot = Quaternion.Slerp(_currentRecoilRot * shotRotKick, shotRotKick, 0.65f);
        }

        private void LateUpdate()
        {
            if (_pickupBannerTimer > 0f)
            {
                _pickupBannerTimer -= Time.deltaTime;
            }

            UpdateEasyFpsViewModelTransform();
            ApplyCameraFx();
        }

        private void UpdateEasyFpsViewModelTransform()
        {
            if (!hasWeapon || _heldWeaponInstance == null) return;

            // Hide 3D sniper rifle model while aiming through the optic scope so it never clips into the camera
            bool hideSniperMesh = (hasWeapon && equippedGunType == GunType.SniperRifle && isAiming);
            Renderer[] heldRends = _heldWeaponInstance.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < heldRends.Length; r++)
            {
                if (heldRends[r] != null && heldRends[r].enabled == hideSniperMesh)
                {
                    heldRends[r].enabled = !hideSniperMesh;
                }
            }

            bool bagOpen = PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen;
            bool isCrawling = PlayerCrawl.Instance != null && PlayerCrawl.Instance.isCrawling;

            Vector3 targetPos;
            if (isAiming)
            {
                // ADS Alignment via ironSight anchor transform (Easy FPS template standard)
                Vector3 sightOffset = (ironSight != null) ? ironSight.localPosition : GetDefaultIronSightLocalPos(equippedGunType);
                Vector3 scaledSightOffset = Vector3.Scale(_heldWeaponInstance.transform.localScale, sightOffset);
                targetPos = targetSightPosition - scaledSightOffset + adsLocalOffset;
            }
            else
            {
                targetPos = easyFpsRestLocalPos;
            }

            // Prone / crawl weapon resting stance
            if (isCrawling && !isAiming)
            {
                targetPos = new Vector3(easyFpsRestLocalPos.x + 0.02f, easyFpsRestLocalPos.y - 0.08f, easyFpsRestLocalPos.z - 0.04f);
            }

            // Subtle rotational lag when turning camera (reduced during ADS for precision)
            float mouseX = 0f;
            float mouseY = 0f;
            if (_starterInputs != null && !bagOpen)
            {
                mouseX = Mathf.Clamp(_starterInputs.look.x, -5f, 5f);
                mouseY = Mathf.Clamp(_starterInputs.look.y, -5f, 5f);
            }
            float swayAmount = isAiming ? 0.06f : 0.35f;
            Quaternion targetSwayRot = Quaternion.Euler(-mouseY * swayAmount, mouseX * swayAmount, 0f);

            // Procedural First-Person Crawling Animation
            if (isCrawling)
            {
                float hSpeed = 0f;
                var cc = GetComponent<CharacterController>();
                if (cc != null)
                {
                    hSpeed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
                }
                if (hSpeed > 0.15f)
                {
                    // Alternating elbow creep crawl cadence
                    float crawlTime = Time.time * 5.0f;
                    float bobX = Mathf.Sin(crawlTime) * (isAiming ? 0.004f : 0.032f);
                    float bobY = -Mathf.Abs(Mathf.Sin(crawlTime)) * (isAiming ? 0.004f : 0.028f);
                    float rollZ = Mathf.Cos(crawlTime) * (isAiming ? 1.0f : 4.5f);
                    float pitchX = Mathf.Sin(crawlTime) * (isAiming ? 0.8f : 3.0f);

                    targetPos += new Vector3(bobX, bobY, 0f);
                    targetSwayRot *= Quaternion.Euler(pitchX, 0f, rollZ);
                }
                else if (!isAiming)
                {
                    // Gentle breathing while prone
                    float breath = Mathf.Sin(Time.time * 1.6f) * 0.004f;
                    targetPos += new Vector3(0f, breath, 0f);
                }
            }
            else
            {
                // Easy FPS Walk/Sprint/Idle procedural weapon animation
                float hSpeed = 0f;
                var cc = GetComponent<CharacterController>();
                if (cc != null)
                {
                    hSpeed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;
                }
                else if (_starterInputs != null)
                {
                    hSpeed = _starterInputs.move.magnitude * (_starterInputs.sprint ? 5f : 3f);
                }

                bool sprintInput = (_starterInputs != null && _starterInputs.sprint) ||
                                   (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
                bool isMoving = hSpeed > 0.4f;
                bool isSprinting = isMoving && sprintInput && !isAiming && !isReloading && !isMeleeAttacking;

                // Smoothly blend sprint weight in and out
                _sprintWeight = Mathf.MoveTowards(_sprintWeight, isSprinting ? 1f : 0f, Time.deltaTime * 7.0f);

                if (_sprintWeight > 0.001f)
                {
                    if (IsMeleeEquipped)
                    {
                        // Melee sprint posture: lowers weapon slightly and bobs naturally along the right flank
                        float sprintCadence = Time.time * 10.0f;
                        float bobX = Mathf.Cos(sprintCadence * 0.5f) * 0.012f;
                        float bobY = -Mathf.Abs(Mathf.Sin(sprintCadence)) * 0.018f;
                        Vector3 meleeSprintPos = new Vector3(0.03f, -0.05f, -0.04f) + new Vector3(bobX, bobY, 0f);
                        Quaternion meleeSprintRot = Quaternion.Euler(10f, 12f, -8f);

                        targetPos += Vector3.Lerp(Vector3.zero, meleeSprintPos, _sprintWeight);
                        targetSwayRot = Quaternion.Slerp(targetSwayRot, targetSwayRot * meleeSprintRot, _sprintWeight);
                    }
                    else
                    {
                        // Easy FPS 1.0 Sprint Animation: gun in front of camera, horizontal across bottom of screen, side facing player POV
                        float sprintCadence = Time.time * 11.0f;
                        float sprintBobX = Mathf.Cos(sprintCadence * 0.5f) * 0.016f;
                        float sprintBobY = -Mathf.Abs(Mathf.Sin(sprintCadence)) * 0.022f;
                        float sprintRollZ = Mathf.Sin(sprintCadence * 0.5f) * 3.5f;
                        float sprintPitchX = Mathf.Cos(sprintCadence) * 2.5f;

                        // Shifts weapon from hip (X: 0.18) to front-center of camera (X: 0.02) lowered along the bottom of the screen
                        Vector3 sprintPosePos = new Vector3(-0.16f, -0.055f, -0.08f) + new Vector3(sprintBobX, sprintBobY, 0f);
                        // Rotates weapon sideways across chest (-62° yaw) so the side profile is the player's POV
                        Quaternion sprintPoseRot = Quaternion.Euler(10f + sprintPitchX, -62f, 20f + sprintRollZ);

                        targetPos += Vector3.Lerp(Vector3.zero, sprintPosePos, _sprintWeight);
                        targetSwayRot = Quaternion.Slerp(targetSwayRot, targetSwayRot * sprintPoseRot, _sprintWeight);
                    }
                }

                if (isMoving && _sprintWeight < 0.95f)
                {
                    // Easy FPS Walk bobbing (when not fully sprinting)
                    float walkCadence = Time.time * 6.5f;
                    float walkWeight = (1f - _sprintWeight) * (isAiming ? 0.15f : 1.0f);

                    float bobX = Mathf.Cos(walkCadence * 0.5f) * 0.009f * walkWeight;
                    float bobY = -Mathf.Abs(Mathf.Sin(walkCadence)) * 0.007f * walkWeight;
                    float rollZ = Mathf.Sin(walkCadence * 0.5f) * 1.2f * walkWeight;

                    targetPos += new Vector3(bobX, bobY, 0f);
                    targetSwayRot *= Quaternion.Euler(0f, 0f, rollZ);
                }
                else if (!isMoving && !isAiming)
                {
                    // Gentle breathing while idle
                    float breathTime = Time.time * 1.8f;
                    float breathY = Mathf.Sin(breathTime) * 0.0025f;
                    float breathX = Mathf.Cos(breathTime * 0.5f) * 0.0015f;
                    targetPos += new Vector3(breathX, breathY, 0f);
                }
            }

            // Apply procedural reload animation offset (magazine drop, tilt, and bolt rack)
            targetPos += _reloadAnimOffset;

            _currentEasyFpsPos = Vector3.SmoothDamp(_currentEasyFpsPos, targetPos, ref _easyFpsPosVel, gunAimTime);

            // Smoothly recover firing recoil position & rotation back to rest
            _easyFpsRecoilOffset.x = Mathf.SmoothDamp(_easyFpsRecoilOffset.x, 0f, ref _easyFpsRecoilVel.x, 0.07f);
            _easyFpsRecoilOffset.y = Mathf.SmoothDamp(_easyFpsRecoilOffset.y, 0f, ref _easyFpsRecoilVel.y, 0.07f);
            _easyFpsRecoilOffset.z = Mathf.SmoothDamp(_easyFpsRecoilOffset.z, 0f, ref _easyFpsRecoilVel.z, 0.07f);
            _currentRecoilRot = Quaternion.Slerp(_currentRecoilRot, Quaternion.identity, Time.deltaTime * 18f);

            _currentSwayRot = Quaternion.Slerp(_currentSwayRot, targetSwayRot, Time.deltaTime * 12f);

            _heldWeaponInstance.transform.localPosition = _currentEasyFpsPos + _easyFpsRecoilOffset + _meleeAnimOffset;
            _heldWeaponInstance.transform.localRotation = _currentRecoilRot * _currentSwayRot * _reloadAnimRot * _meleeAnimRot * easyFpsRestLocalRot;
        }

        private void Fire()
        {
            _nextFireTime = Time.time + fireRate;
            currentAmmo--;
            SyncEquippedGunToBag();

            // Spawn Easy FPS MuzzleFlash prefab at muzzelSpawn + light burst
            StartCoroutine(MuzzleFlashRoutine());
            if (ejectShellCasings) EjectShellCasing();
            PlayGunshotAudio(equippedGunType);

            // Play firing recoil animation on Easy FPS viewmodel
            ApplyWeaponRecoil(equippedGunType);

            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            int pelletCount = Mathf.Max(1, pellets);
            bool hitAnyZombie = false;

            for (int i = 0; i < pelletCount; i++)
            {
                Ray ray = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

                if (spreadAngle > 0.01f || pelletCount > 1)
                {
                    Vector3 spreadDir = ray.direction + _mainCam.transform.right * Random.Range(-spreadAngle, spreadAngle) * 0.02f
                                                      + _mainCam.transform.up * Random.Range(-spreadAngle, spreadAngle) * 0.02f;
                    ray.direction = spreadDir.normalized;
                }

                int hitMask = ~((1 << 2) | (1 << 8));

                RaycastHit[] hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform) || h.collider.gameObject == gameObject) continue;

                    var zombie = h.collider.GetComponentInParent<ZombieAI>();
                    if (zombie == null) zombie = h.collider.GetComponent<ZombieAI>();
                    if (zombie == null) zombie = h.collider.GetComponentInChildren<ZombieAI>();

                    if (zombie != null && !zombie.isDead)
                    {
                        zombie.TakeDamage(damagePerShot, h.point, ray.direction);
                        CreateBloodImpact(h.point, h.normal);
                        hitAnyZombie = true;
                    }
                    else
                    {
                        CreateBulletHoleAndSpark(h.point, h.normal, h.collider.transform);
                    }
                    break;
                }
            }

            if (hitAnyZombie && _easyFpsHitMarkerAudio != null)
            {
                _easyFpsHitMarkerAudio.Play();
            }

            AddCameraKick(equippedGunType);
        }

        // ---------------- Camera kick & loot dip ----------------

        private void AddCameraKick(GunType type)
        {
            float pitch = 0.75f;
            float duration = 0.10f;
            switch (type)
            {
                case GunType.Pistol: pitch = 0.85f; break;
                case GunType.AssaultRifle: pitch = 0.75f; break;
                case GunType.SMG: pitch = 0.6f; duration = 0.08f; break;
                case GunType.Shotgun: pitch = 2.0f; duration = 0.18f; break;
                case GunType.SniperRifle: pitch = 2.4f; duration = 0.20f; break;
            }

            _kickPitch = pitch;
            _kickJitter = 0.20f;
            _kickDuration = duration;
            _kickTimer = duration;
        }

        private void StartCameraDip(float angle, float duration)
        {
            _dipAngle = angle;
            _dipDuration = Mathf.Max(0.05f, duration);
            _dipTimer = _dipDuration;
        }

        private void ApplyCameraFx()
        {
            // Smooth Camera Field of View (FOV) Zoom during Aim Down Sights (ADS)
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam != null)
            {
                float targetFov = (hasWeapon && isAiming)
                    ? (equippedGunType == GunType.SniperRifle ? 14f : adsFov)
                    : normalFov;
                _mainCam.fieldOfView = Mathf.SmoothDamp(_mainCam.fieldOfView, targetFov, ref _fovVel, gunAimTime);
            }

            float pitch = 0f;
            float jitter = 0f;

            if (_kickTimer > 0f)
            {
                _kickTimer -= Time.deltaTime;
                float f = Mathf.Clamp01(_kickTimer / _kickDuration);
                f *= f;
                pitch -= _kickPitch * f;
                jitter = _kickJitter * f;
            }

            if (_dipTimer > 0f)
            {
                _dipTimer -= Time.deltaTime;
                float p = 1f - Mathf.Clamp01(_dipTimer / _dipDuration);
                pitch += Mathf.Sin(p * Mathf.PI) * _dipAngle;
            }

            bool wantFx = Mathf.Abs(pitch) > 0.0001f || jitter > 0.0001f;
            if (!wantFx && !_fxActive) return;

            if (_camRoot == null)
            {
                var go = GameObject.Find("PlayerCameraRoot");
                if (go == null) return;
                _camRoot = go.transform;
            }

            Quaternion baseRot = _camRoot.localRotation;
            if (_fxActive && Quaternion.Angle(baseRot, _fxLastWritten) < 0.0001f)
            {
                baseRot = baseRot * Quaternion.Inverse(_fxApplied);
            }

            if (wantFx)
            {
                _fxApplied = Quaternion.Euler(pitch, Random.Range(-jitter, jitter), Random.Range(-jitter, jitter) * 0.5f);
                _camRoot.localRotation = baseRot * _fxApplied;
                _fxLastWritten = _camRoot.localRotation;
                _fxActive = true;
            }
            else
            {
                _camRoot.localRotation = baseRot;
                _fxApplied = Quaternion.identity;
                _fxActive = false;
            }
        }

        // ---------------- Shell casings & Easy FPS Muzzle Flash ----------------

        public void EjectShellCasing()
        {
            if (_heldWeaponInstance == null) return;
            if (_mainCam == null) _mainCam = Camera.main;
            Transform camT = _mainCam != null ? _mainCam.transform : transform;

            bool isShotgun = equippedGunType == GunType.Shotgun;

            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "ShellCasing";
            shell.layer = 2;
            shell.transform.position = _heldWeaponInstance.transform.position + camT.right * 0.06f + camT.up * 0.02f + camT.forward * 0.15f;
            shell.transform.rotation = Random.rotation;
            shell.transform.localScale = isShotgun ? new Vector3(0.016f, 0.024f, 0.016f) : new Vector3(0.008f, 0.012f, 0.008f);

            var r = shell.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = isShotgun ? new Color(0.8f, 0.15f, 0.12f) : new Color(0.85f, 0.65f, 0.2f);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            var col = shell.GetComponent<Collider>();
            var cc = GetComponent<CharacterController>();
            if (col != null && cc != null) Physics.IgnoreCollision(col, cc);

            var rb = shell.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.linearVelocity = camT.right * Random.Range(2.0f, 3.4f)
                              + camT.up * Random.Range(1.2f, 2.2f)
                              - camT.forward * Random.Range(0f, 0.5f);
            rb.angularVelocity = Random.insideUnitSphere * 25f;

            Destroy(shell, 3.5f);
        }

        private IEnumerator MuzzleFlashRoutine()
        {
            if (_easyFpsMuzzleFlashes == null || _easyFpsMuzzleFlashes.Length == 0)
            {
                LoadEasyFpsMuzzleFlashes();
            }

            // Spawn Easy FPS MuzzleFlash prefab directly at the front tip of the barrel
            if (_easyFpsMuzzleFlashes != null && _easyFpsMuzzleFlashes.Length > 0 && _easyFpsMuzzleSpawn != null)
            {
                // Guarantee each consecutive shot uses a DIFFERENT muzzle flash variation
                int idx = Random.Range(0, _easyFpsMuzzleFlashes.Length);
                if (_easyFpsMuzzleFlashes.Length > 1 && idx == _lastMuzzleFlashIndex)
                {
                    idx = (idx + 1) % _easyFpsMuzzleFlashes.Length;
                }
                _lastMuzzleFlashIndex = idx;

                GameObject flashPrefab = _easyFpsMuzzleFlashes[idx];
                if (flashPrefab != null)
                {
                    GameObject holdFlash = Instantiate(
                        flashPrefab,
                        _easyFpsMuzzleSpawn.position,
                        _easyFpsMuzzleSpawn.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                        _easyFpsMuzzleSpawn
                    );

                    // Strictly lock to (0, 0, 0) of the muzzle tip to prevent any offset
                    holdFlash.transform.localPosition = Vector3.zero;
                    holdFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                    float flashScale = (muzzleFlashScale > 0.05f) ? muzzleFlashScale : 0.82f;
                    holdFlash.transform.localScale = new Vector3(flashScale, flashScale, 0.01f);

                    foreach (var r in holdFlash.GetComponentsInChildren<Renderer>(true))
                    {
                        r.gameObject.layer = 0;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        if (r.sharedMaterial != null)
                        {
                            r.material = GetAdditiveMuzzleMaterial(r.sharedMaterial);
                        }
                    }
                    Destroy(holdFlash, 0.045f);
                }
            }

            if (_muzzleFlashLight != null)
            {
                _muzzleFlashLight.enabled = true;
                _muzzleFlashLight.intensity = 2.5f;
            }

            yield return new WaitForSeconds(0.04f);

            if (_muzzleFlashLight != null)
            {
                _muzzleFlashLight.enabled = false;
            }
        }

        // Cache of additive muzzle materials (keyed by source material) so we don't leak a new material every shot
        private readonly System.Collections.Generic.Dictionary<Material, Material> _additiveMuzzleMats =
            new System.Collections.Generic.Dictionary<Material, Material>();

        /// <summary>
        /// Returns an ADDITIVE version of the Easy FPS muzzle flash material.
        /// The muzzle textures have a black background, so alpha blending shows a black square;
        /// additive blending makes black fully invisible and only the bright flash remains.
        /// </summary>
        private Material GetAdditiveMuzzleMaterial(Material source)
        {
            if (source == null) return null;
            if (_additiveMuzzleMats.TryGetValue(source, out Material cached) && cached != null) return cached;

            Texture tex = null;
            if (source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null) tex = source.GetTexture("_BaseMap");
            if (tex == null && source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null) tex = source.GetTexture("_MainTex");
            if (tex == null && source.mainTexture != null) tex = source.mainTexture;

            Material mat = null;
            Shader customAdditive = Shader.Find("Custom/URP_MuzzleFlash_Additive");
            if (customAdditive != null)
            {
                mat = new Material(customAdditive) { name = source.name + "_Additive" };
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                }
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_TintColor", Color.white);
                mat.SetFloat("_Intensity", 1.4f);
                mat.renderQueue = (int)RenderQueue.Transparent + 500;
            }
            else
            {
                Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (urpParticles != null)
                {
                    mat = new Material(urpParticles) { name = source.name + "_Additive" };
                    mat.SetFloat("_Surface", 1f);            // Transparent
                    mat.SetFloat("_Blend", 2f);              // Additive
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.One);
                    if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.SetFloat("_Cull", 0f);               // Double sided
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetShaderPassEnabled("ShadowCaster", false);
                    mat.SetShaderPassEnabled("DepthOnly", false);
                    mat.renderQueue = (int)RenderQueue.Transparent + 500;
                    if (tex != null) mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", Color.white);
                }
                else
                {
                    Shader legacyAdd = Shader.Find("Legacy Shaders/Particles/Additive");
                    mat = new Material(legacyAdd != null ? legacyAdd : source.shader) { name = source.name + "_Additive" };
                    if (tex != null) mat.mainTexture = tex;
                    if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
                }
            }

            _additiveMuzzleMats[source] = mat;
            return mat;
        }

        private void StartReload()
        {
            if (isReloading || reserveAmmo <= 0 || currentAmmo >= magazineCapacity) return;

            isReloading = true;
            _reloadEndTime = Time.time + reloadDuration;

            StartCoroutine(EasyFpsReloadAnimRoutine());
            Debug.Log($"[PlayerWeapon] Reloading {equippedGunName} with Easy FPS Character_Reload animation...");
        }

        private IEnumerator EasyFpsReloadAnimRoutine()
        {
            if (_easyFpsReloadAudio != null)
            {
                _easyFpsReloadAudio.Play();
            }
            else
            {
                PlaySound(reloadClip, 1.0f);
            }

            // Procedural Easy FPS reload motion: dip weapon, tilt to insert magazine, and recover
            float reloadTime = reloadDuration;
            float t = 0f;
            Vector3 reloadDipPos = new Vector3(0.015f, -0.10f, -0.05f);
            Quaternion reloadDipRot = Quaternion.Euler(-18f, 12f, -8f);

            // Dip weapon down to insert mag
            float dipDuration = reloadTime * 0.40f;
            while (t < dipDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dipDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);
                _reloadAnimOffset = Vector3.Lerp(Vector3.zero, reloadDipPos, smoothP);
                _reloadAnimRot = Quaternion.Slerp(Quaternion.identity, reloadDipRot, smoothP);
                yield return null;
            }

            // Mag inserted: quick tactile upward click
            _reloadAnimOffset += new Vector3(0f, 0.02f, 0.01f);
            yield return new WaitForSeconds(0.12f);

            // Recover weapon back to rest pose
            float recoverDuration = reloadTime * 0.45f;
            t = 0f;
            Vector3 startRecoverPos = _reloadAnimOffset;
            Quaternion startRecoverRot = _reloadAnimRot;
            while (t < recoverDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / recoverDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);
                _reloadAnimOffset = Vector3.Lerp(startRecoverPos, Vector3.zero, smoothP);
                _reloadAnimRot = Quaternion.Slerp(startRecoverRot, Quaternion.identity, smoothP);
                yield return null;
            }

            _reloadAnimOffset = Vector3.zero;
            _reloadAnimRot = Quaternion.identity;
        }

        private void FinishReload()
        {
            isReloading = false;
            int needed = magazineCapacity - currentAmmo;
            int toLoad = Mathf.Min(needed, reserveAmmo);

            currentAmmo += toLoad;
            reserveAmmo -= toLoad;
            SyncEquippedGunToBag();

            Debug.Log($"[PlayerWeapon] Reloaded! Ammo: {currentAmmo}/{reserveAmmo}");
        }

        private void FindNearbyLootable()
        {
            _nearestLootable = null;
            float closestDist = 3.5f;

            Collider[] hits = Physics.OverlapSphere(transform.position, 3.8f);
            foreach (var col in hits)
            {
                var pickup = col.GetComponent<GunPickup>();
                if (pickup == null) pickup = col.GetComponentInParent<GunPickup>();
                if (pickup != null)
                {
                    float dist = Vector3.Distance(transform.position, pickup.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        _nearestLootable = pickup;
                    }
                }
            }
        }

        private static GameObject[] _cachedBloodVfx;

        private void CreateBloodImpact(Vector3 pos, Vector3 normal)
        {
            if (_cachedBloodVfx == null || _cachedBloodVfx.Length == 0)
            {
                var list = new System.Collections.Generic.List<GameObject>();
                var all = Resources.LoadAll<GameObject>("BloodVFX");
                if (all != null && all.Length > 0)
                {
                    foreach (var go in all)
                    {
                        if (go != null && !go.name.Contains("Bunch"))
                        {
                            list.Add(go);
                        }
                    }
                    if (list.Count == 0) list.AddRange(all);
                }
                _cachedBloodVfx = list.ToArray();
            }

            if (_cachedBloodVfx != null && _cachedBloodVfx.Length > 0)
            {
                GameObject prefab = _cachedBloodVfx[Random.Range(0, _cachedBloodVfx.Length)];
                if (prefab != null)
                {
                    Vector3 spawnPos = pos + (normal.sqrMagnitude > 0.001f ? normal * 0.03f : Vector3.zero);
                    GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);
                    vfx.name = "FreeBloodVFX_BulletHit";

                    // Configure the Vefects particle system as a compact 3D camera-facing wound splash
                    // (instead of a horizontal floor decal floating in mid-air)
                    foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = ps.main;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        main.startLifetime = 0.45f;
                        main.startSpeed = Random.Range(0.25f, 0.65f);
                        main.gravityModifier = 0.75f;
                        main.startSize3D = false;
                        main.startSize = Random.Range(0.32f, 0.46f);
                        main.startRotation3D = true;
                        main.startRotationX = 0f;
                        main.startRotationY = 0f;
                        main.startRotationZ = Random.Range(0f, Mathf.PI * 2f);

                        var sizeOverLife = ps.sizeOverLifetime;
                        sizeOverLife.enabled = false;

                        var psr = ps.GetComponent<ParticleSystemRenderer>();
                        if (psr != null && psr.enabled)
                        {
                            psr.alignment = ParticleSystemRenderSpace.View;
                            psr.pivot = Vector3.zero;
                        }
                    }

                    foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Play(true);
                    }

                    Destroy(vfx, 1.0f);
                    return;
                }
            }
        }

        /// <summary>
        /// Spawns a persistent bullet hole decal (using Easy FPS's bulletHole.prefab or a high-detail
        /// radial crater decal) flushed against the hit surface, plus a brief impact spark.
        /// </summary>
        private void CreateBulletHoleAndSpark(Vector3 pos, Vector3 normal, Transform hitSurface)
        {
            Quaternion decalRot = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Vector3 decalPos = pos + normal * 0.012f;
            Shader spritesShader = Shader.Find("Sprites/Default");

            if (_easyFpsBulletHolePrefab != null)
            {
                GameObject hole = Instantiate(_easyFpsBulletHolePrefab, decalPos, decalRot);
                hole.name = "BulletHoleDecal";
                hole.layer = 2;
                hole.transform.localScale = new Vector3(0.18f, 0.18f, 0.002f);

                var col = hole.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var r = hole.GetComponent<Renderer>();
                if (r != null && r.material != null && spritesShader != null)
                {
                    Texture tex = r.material.GetTexture("_MainTex");
                    if (tex == null) tex = _bulletHoleDecalTex;
                    r.material.shader = spritesShader;
                    r.material.mainTexture = tex;
                    r.material.color = Color.white;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (hitSurface != null)
                {
                    hole.transform.SetParent(hitSurface, true);
                }
                Destroy(hole, 12f);
            }
            else
            {
                GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
                hole.name = "BulletHoleDecal";
                hole.layer = 2;
                Destroy(hole.GetComponent<Collider>());

                // Quad faces -Z by default, so rotate 180 deg around Y to face outward along normal
                hole.transform.position = decalPos;
                hole.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                hole.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

                var r = hole.GetComponent<Renderer>();
                if (r != null && spritesShader != null)
                {
                    r.material = new Material(spritesShader);
                    r.material.mainTexture = _bulletHoleDecalTex;
                    r.material.color = Color.white;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                if (hitSurface != null)
                {
                    hole.transform.SetParent(hitSurface, true);
                }
                Destroy(hole, 12f);
            }

            // Brief impact spark
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spark.name = "BulletSpark";
            spark.layer = 2;
            spark.transform.position = pos + normal * 0.025f;
            spark.transform.localScale = Vector3.one * 0.05f;
            var sr = spark.GetComponent<Renderer>();
            if (sr != null)
            {
                sr.material.color = new Color(1.0f, 0.85f, 0.3f);
                sr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Destroy(spark.GetComponent<Collider>());
            Destroy(spark, 0.18f);
        }

        private void InitBulletHoleDecalTexture()
        {
            int size = 64;
            _bulletHoleDecalTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // Jagged radial cracks
                    float crack = Mathf.Abs(Mathf.Sin(angle * 7f) * Mathf.Cos(angle * 5f));
                    float maxR = 0.48f + crack * 0.35f;

                    if (r < 0.22f)
                    {
                        // Deep dark bullet hole core
                        _bulletHoleDecalTex.SetPixel(x, y, new Color(0.04f, 0.04f, 0.05f, 0.96f));
                    }
                    else if (r < 0.42f)
                    {
                        // Charred crater rim
                        float t = (r - 0.22f) / 0.20f;
                        float a = Mathf.Lerp(0.90f, 0.55f, t);
                        _bulletHoleDecalTex.SetPixel(x, y, new Color(0.14f, 0.13f, 0.12f, a));
                    }
                    else if (r < maxR && crack > 0.55f)
                    {
                        // Outer radial crack lines
                        float a = Mathf.Clamp01((maxR - r) * 3.0f) * 0.65f;
                        _bulletHoleDecalTex.SetPixel(x, y, new Color(0.10f, 0.10f, 0.10f, a));
                    }
                    else
                    {
                        _bulletHoleDecalTex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            _bulletHoleDecalTex.Apply();
        }

        private void PlayGunshotAudio(GunType type)
        {
            if (_easyFpsShootAudio != null && _easyFpsShootAudio.clip != null)
            {
                _easyFpsShootAudio.pitch = Random.Range(0.95f, 1.05f);
                _easyFpsShootAudio.Play();
                return;
            }

            AudioClip clip = rifleShotClip;
            float pitch = Random.Range(0.92f, 1.08f);

            switch (type)
            {
                case GunType.Pistol:
                    clip = pistolShotClip;
                    break;
                case GunType.Shotgun:
                    clip = shotgunShotClip;
                    pitch = Random.Range(0.85f, 1.0f);
                    break;
                case GunType.AssaultRifle:
                case GunType.SMG:
                    clip = rifleShotClip;
                    break;
                case GunType.SniperRifle:
                    clip = rifleShotClip;
                    pitch = 0.75f;
                    break;
            }

            if (clip != null && audioSource != null)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, 0.95f);
            }
        }

        private void PlaySound(AudioClip clip, float vol)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(clip, vol);
            }
        }

        private void InitProceduralGunAudio()
        {
            if (pistolShotClip == null) pistolShotClip = CreateSynthGunshot(320f, 0.28f, 0.8f);
            if (rifleShotClip == null) rifleShotClip = CreateSynthGunshot(220f, 0.35f, 1.2f);
            if (shotgunShotClip == null) shotgunShotClip = CreateSynthShotgun(130f, 0.5f);
            if (reloadClip == null) reloadClip = CreateSynthReload(0.7f);
            if (emptyClickClip == null) emptyClickClip = CreateSynthClick(0.08f);
            if (lootPickupClip == null) lootPickupClip = CreateSynthReload(0.4f);
            if (_knifeSwingWhoosh == null) _knifeSwingWhoosh = CreateSynthWhoosh(460f, 0.22f, 0.90f);
            if (_crowbarSmashWhoosh == null) _crowbarSmashWhoosh = CreateSynthWhoosh(240f, 0.32f, 1.15f);
            if (_knifeFleshHit == null) _knifeFleshHit = CreateSynthSliceHit(0.24f);
            if (_crowbarBluntHit == null) _crowbarBluntHit = CreateSynthBluntHit(0.30f);
        }

        private static AudioClip CreateSynthWhoosh(float baseFreq, float duration, float strength)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * duration);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;
                float env = Mathf.Sin(p * Mathf.PI);
                env = env * env;
                float freq = baseFreq * (1.4f - p * 0.8f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (Random.value * 2f - 1f);
                data[i] = (tone * 0.35f + noise * 0.65f) * env * strength;
            }

            AudioClip clip = AudioClip.Create("MeleeWhoosh", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthSliceHit(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);
                float squish = Mathf.Sin(2f * Mathf.PI * 340f * t * (1f - t * 2f));
                float noise = (Random.value * 2f - 1f);
                data[i] = (squish * 0.45f + noise * 0.55f) * env * 1.1f;
            }

            AudioClip clip = AudioClip.Create("SliceHit", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthBluntHit(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 26f);
                float sub = Mathf.Sin(2f * Mathf.PI * 95f * t);
                float clank = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 40f);
                float noise = (Random.value * 2f - 1f) * 0.6f;
                data[i] = (sub * 0.65f + clank * 0.35f + noise * 0.35f) * env * 1.3f;
            }

            AudioClip clip = AudioClip.Create("BluntHit", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthGunshot(float freq, float length, float punch)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Pow(1f - (t / length), 4f);
                float thump = Mathf.Sin(2f * Mathf.PI * freq * (1f - t * 1.5f) * t);
                float noise = (Random.value * 2f - 1f);
                data[i] = (thump * 0.5f + noise * 0.65f) * env * punch;
            }

            AudioClip clip = AudioClip.Create("Gunshot", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthShotgun(float freq, float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Pow(1f - (t / length), 2.5f);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (Random.value * 2f - 1f);
                data[i] = (sub * 0.6f + noise * 0.8f) * env * 1.3f;
            }

            AudioClip clip = AudioClip.Create("ShotgunShot", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthReload(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float click1 = Mathf.Exp(-Mathf.Abs(t - 0.15f) * 60f) * Mathf.Sin(2f * Mathf.PI * 650f * t);
                float click2 = Mathf.Exp(-Mathf.Abs(t - 0.45f) * 60f) * Mathf.Sin(2f * Mathf.PI * 850f * t);
                data[i] = (click1 + click2) * 0.8f;
            }

            AudioClip clip = AudioClip.Create("ReloadCock", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthClick(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 50f) * 0.7f;
            }

            AudioClip clip = AudioClip.Create("EmptyClick", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void InitHudTextures()
        {
            _crosshairTex = new Texture2D(1, 1);
            _crosshairTex.SetPixel(0, 0, Color.white);
            _crosshairTex.Apply();

            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.12f, 0.85f));
            _hudBgTex.Apply();

            _hudFillTex = new Texture2D(1, 1);
            _hudFillTex.SetPixel(0, 0, new Color(1.0f, 0.65f, 0.15f, 0.9f));
            _hudFillTex.Apply();
        }

        private void OnGUI()
        {
            if (Boat4EscapeInteraction.Instance != null && Boat4EscapeInteraction.Instance.isEscaping) return;
            if (GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuOpen) return;
            if (CinematicWakeUpIntro.IsIntroActive) return;

            var health = GetComponent<PlayerHealth>();
            if (health != null && health.isDead) return;

            // ================= 1. LOOT INTERACTION PROMPT (REMOVED) =================

            // ================= 2. CROSSHAIR & SCOPE OVERLAY =================
            if (hasWeapon && !isAiming)
            {
                float cx = Screen.width * 0.5f;
                float cy = Screen.height * 0.5f;
                float chSize = 10f;
                float chThickness = 2f;
                float chGap = isReloading ? 14f : 5f;

                Color chColor = Color.white;
                if (_mainCam != null)
                {
                    Ray aimRay = _mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    RaycastHit[] hits = Physics.RaycastAll(aimRay, range, ~((1 << 2) | (1 << 8)), QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits)
                    {
                        if (h.collider.transform.IsChildOf(transform) || h.collider.gameObject == gameObject) continue;
                        if (h.collider.GetComponentInParent<ZombieAI>() != null || h.collider.GetComponentInChildren<ZombieAI>() != null)
                        {
                            chColor = Color.red;
                        }
                        break;
                    }
                }

                Color prevGuiColor = GUI.color;
                GUI.color = chColor;

                if (_crosshairTex != null)
                {
                    GUI.DrawTexture(new Rect(cx - chThickness * 0.5f, cy - chGap - chSize, chThickness, chSize), _crosshairTex);
                    GUI.DrawTexture(new Rect(cx - chThickness * 0.5f, cy + chGap, chThickness, chSize), _crosshairTex);
                    GUI.DrawTexture(new Rect(cx - chGap - chSize, cy - chThickness * 0.5f, chSize, chThickness), _crosshairTex);
                    GUI.DrawTexture(new Rect(cx + chGap, cy - chThickness * 0.5f, chSize, chThickness), _crosshairTex);
                    GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), _crosshairTex);
                }

                GUI.color = prevGuiColor;
            }
            else if (hasWeapon && isAiming && equippedGunType == GunType.SniperRifle)
            {
                DrawSniperScopeOverlay();
            }

            if (hasWeapon)
            {
                // ================= 3. WEAPON & AMMO HUD (Bottom-Right) =================
                float hudW = 210f;
                float hudH = 58f;
                float hx = Screen.width - hudW - 28f;
                float hy = Screen.height - hudH - 28f;

                if (_hudBgTex != null)
                {
                    GUI.DrawTexture(new Rect(hx - 2, hy - 2, hudW + 4, hudH + 4), _hudBgTex);
                }

                GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
                nameStyle.font = StarterAssets.DialogueFontProvider.Bold;
                nameStyle.fontSize = 13;
                nameStyle.fontStyle = FontStyle.Bold;
                nameStyle.normal.textColor = GunPickup.GetGunColor(equippedGunType);
                GUI.Label(new Rect(hx + 10, hy + 5, hudW - 20, 20), equippedGunName.ToUpper(), nameStyle);

                GUIStyle ammoStyle = new GUIStyle(GUI.skin.label);
                ammoStyle.font = StarterAssets.DialogueFontProvider.Bold;
                ammoStyle.fontSize = 22;
                ammoStyle.fontStyle = FontStyle.Bold;
                ammoStyle.normal.textColor = isReloading ? Color.yellow : (currentAmmo == 0 ? Color.red : Color.white);

                string ammoString = IsMeleeEquipped ? "MELEE" : (isReloading ? "RELOADING..." : $"{currentAmmo}  /  {reserveAmmo}");
                GUI.Label(new Rect(hx + 10, hy + 24, hudW - 20, 28), ammoString, ammoStyle);
            }

            // ================= 4. WEAPON PICKUP NOTIFICATION BANNER (REMOVED) =================

            // ================= 5. TARGET ELIMINATED BANNER =================
            if (_killBannerTimer > 0f)
            {
                _killBannerTimer -= Time.deltaTime;
                float bannerW = 360f;
                float bannerH = 38f;
                float bx = (Screen.width - bannerW) * 0.5f;
                float by = Screen.height - 75f;

                float alpha = Mathf.Clamp01(_killBannerTimer / 0.5f);
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                if (_hudBgTex != null)
                {
                    GUI.DrawTexture(new Rect(bx - 2, by - 2, bannerW + 4, bannerH + 4), _hudBgTex);
                }

                if (_hudFillTex != null)
                {
                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1.0f, 0.22f, 0.18f, 0.95f);
                    GUI.DrawTexture(new Rect(bx - 2, by - 2, bannerW + 4, 3), _hudFillTex);
                    GUI.backgroundColor = prevBg;
                }

                GUIStyle killStyle = new GUIStyle(GUI.skin.label);
                killStyle.font = StarterAssets.DialogueFontProvider.Bold;
                killStyle.fontSize = 17;
                killStyle.fontStyle = FontStyle.Bold;
                killStyle.alignment = TextAnchor.MiddleCenter;
                killStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1.0f);

                GUI.Label(new Rect(bx, by, bannerW, bannerH), "[ TARGET ELIMINATED ]", killStyle);
                GUI.color = prevColor;
            }
        }

        private void DrawSniperScopeOverlay()
        {
            if (_sniperScopeTex == null)
            {
                _sniperScopeTex = Resources.Load<Texture2D>("Textures/SniperScope_PSO1");
                if (_sniperScopeTex == null)
                {
                    string diskPath = System.IO.Path.Combine(Application.dataPath, "Resources/Textures/SniperScope_PSO1.png");
                    if (System.IO.File.Exists(diskPath))
                    {
                        byte[] fileBytes = System.IO.File.ReadAllBytes(diskPath);
                        _sniperScopeTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        _sniperScopeTex.LoadImage(fileBytes);
                    }
                }
            }

            // Scope circular size: scaled to comfortably cover the full height of the display
            float scopeSize = Screen.height * 1.04f;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float halfSize = scopeSize * 0.5f;
            Rect scopeRect = new Rect(cx - halfSize, cy - halfSize, scopeSize, scopeSize);

            Color prev = GUI.color;

            // 1. Solid opaque black masks covering all screen borders beyond the scope
            GUI.color = Color.black;
            if (scopeRect.x > 0f)
            {
                GUI.DrawTexture(new Rect(0, 0, scopeRect.x + 2f, Screen.height), Texture2D.whiteTexture);
            }
            if (scopeRect.xMax < Screen.width)
            {
                GUI.DrawTexture(new Rect(scopeRect.xMax - 2f, 0, Screen.width - (scopeRect.xMax - 2f), Screen.height), Texture2D.whiteTexture);
            }
            if (scopeRect.y > 0f)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, scopeRect.y + 2f), Texture2D.whiteTexture);
            }
            if (scopeRect.yMax < Screen.height)
            {
                GUI.DrawTexture(new Rect(0, scopeRect.yMax - 2f, Screen.width, Screen.height - (scopeRect.yMax - 2f)), Texture2D.whiteTexture);
            }

            // 2. Russian PSO-1 Sniper Scope Reticle matching reference design
            GUI.color = Color.white;
            if (_sniperScopeTex != null)
            {
                GUI.DrawTexture(scopeRect, _sniperScopeTex, ScaleMode.StretchToFill, true);
            }

            GUI.color = prev;
        }
    }
}

