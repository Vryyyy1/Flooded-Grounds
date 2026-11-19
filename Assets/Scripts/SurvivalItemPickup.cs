using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public enum SurvivalToolType
    {
        Battery,
        CannedFood,
        FirstAid,
        Flashlight,
        Matchbox,
        Pills,
        Tape,
        Walkie,
        WaterBottle,
        Gun_Pistol,
        Gun_SMG,
        Gun_Shotgun,
        Gun_AssaultRifle,
        Gun_SniperRifle,
        RustKey,
        Melee_Knife,
        Melee_Crowbar
    }

    /// <summary>
    /// Ensures Survival Tools render with their full PBR colors/textures in URP
    /// and allows the player to pick them up one-by-one ([E]) into their Character Inventory Bag.
    /// </summary>
    public class SurvivalItemPickup : MonoBehaviour
    {
        private static readonly List<SurvivalItemPickup> AllPickups = new List<SurvivalItemPickup>();
        public static SurvivalItemPickup ActiveTarget { get; private set; }
        private static int _lastTargetFrame = -1;
        private static int _lastPickupFrame = -1;

        [Header("Survival Item Settings")]
        public SurvivalToolType itemType = SurvivalToolType.FirstAid;
        public string displayName = "Bandage";
        public int amount = 1;
        public float interactRange = 3.4f;

        [Header("Visuals")]
        public bool gentleBob = false;
        public float bobSpeed = 2.0f;
        public float bobHeight = 0.03f;

        private Transform _playerTransform;
        private Camera _mainCam;
        private PlayerInventoryBag _playerBag;
        private Vector3 _basePosition;
        private Light _itemLight;

        private void Awake()
        {
            AutoDetectItemType();
            EnsureColoredURPMaterials(gameObject, itemType);
            EnsureCollider();
            EnsureSubtleItemLight();
        }

        private void OnEnable()
        {
            if (!AllPickups.Contains(this))
                AllPickups.Add(this);
        }

        private void OnDisable()
        {
            AllPickups.Remove(this);
            if (ActiveTarget == this)
                ActiveTarget = null;
        }

        private void Start()
        {
            _basePosition = transform.position;
            FindPlayer();
            if (_itemLight != null)
            {
                _itemLight.color = GetItemGlowColor(itemType);
            }
        }

        private void Update()
        {
            if (gentleBob)
            {
                float phase = _basePosition.x * 1.7f + _basePosition.z * 2.3f;
                float offset = Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
                transform.position = _basePosition + new Vector3(0f, Mathf.Max(0f, offset), 0f);
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            // Evaluate the single best targeted item once per frame so items on a table are picked up one by one
            if (_lastTargetFrame != Time.frameCount)
            {
                _lastTargetFrame = Time.frameCount;
                UpdateBestActiveTarget();
            }

            if (ActiveTarget == this && _lastPickupFrame != Time.frameCount && WasInteractPressed())
            {
                _lastPickupFrame = Time.frameCount;
                PickupIntoBag();
            }
        }

        private void UpdateBestActiveTarget()
        {
            ActiveTarget = null;
            if (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen)
                return;
            if (RadioInteraction.IsAnyRadioActiveForPlayer())
                return;

            if (_mainCam == null) _mainCam = Camera.main;

            float bestScore = float.MaxValue;
            SurvivalItemPickup bestCandidate = null;

            for (int i = AllPickups.Count - 1; i >= 0; i--)
            {
                SurvivalItemPickup p = AllPickups[i];
                if (p == null || !p.isActiveAndEnabled) continue;

                if (p._playerTransform == null) p.FindPlayer();
                if (p._playerTransform == null) continue;

                Vector3 itemCenter = p.GetItemWorldCenter();
                float distToPlayer = Vector3.Distance(itemCenter, p._playerTransform.position);
                float distToCam = _mainCam != null ? Vector3.Distance(itemCenter, _mainCam.transform.position) : distToPlayer;
                float dist = Mathf.Min(distToPlayer, distToCam);

                float effectiveRange = Mathf.Max(p.interactRange, 3.8f);
                if (dist > effectiveRange) continue;

                float anglePenalty = 0f;
                if (_mainCam != null)
                {
                    Vector3 toItem = (itemCenter - _mainCam.transform.position).normalized;
                    float angle = Vector3.Angle(_mainCam.transform.forward, toItem);
                    // Allow picking up items on the floor/table even when looking ahead if close,
                    // while preferring items closest to the center of the player's view.
                    if (angle > 115f && dist > 2.2f) continue;
                    anglePenalty = angle * 0.025f;
                }

                float score = dist + anglePenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = p;
                }
            }

            ActiveTarget = bestCandidate;
        }

        public Vector3 GetItemWorldCenter()
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) return r.bounds.center;
            return transform.position;
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

        private void PickupIntoBag()
        {
            if (_playerBag == null && PlayerInventoryBag.Instance != null)
            {
                _playerBag = PlayerInventoryBag.Instance;
            }

            if (_playerBag == null && _playerTransform != null)
            {
                _playerBag = _playerTransform.GetComponent<PlayerInventoryBag>();
                if (_playerBag == null)
                    _playerBag = _playerTransform.gameObject.AddComponent<PlayerInventoryBag>();
            }

            if (_playerBag != null)
            {
                bool added = _playerBag.AddItem(itemType, displayName, amount);
                if (added)
                {
                    if (ActiveTarget == this) ActiveTarget = null;
                    Destroy(gameObject);
                }
            }
        }

        private void FindPlayer()
        {
            if (PlayerInventoryBag.Instance != null)
            {
                _playerBag = PlayerInventoryBag.Instance;
                _playerTransform = _playerBag.transform;
                return;
            }

            var fpc = Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                _playerTransform = fpc.transform;
                _playerBag = fpc.GetComponent<PlayerInventoryBag>();
                return;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                _playerBag = playerObj.GetComponent<PlayerInventoryBag>();
            }
        }

        public void AutoDetectItemType()
        {
            string n = gameObject.name.ToLowerInvariant();
            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                n += "_" + mf.sharedMesh.name.ToLowerInvariant();
            }

            if (n.Contains("battery"))
            {
                itemType = SurvivalToolType.Battery;
                displayName = "Battery";
            }
            else if (n.Contains("canned") || n.Contains("can_food") || n.Contains("food"))
            {
                itemType = SurvivalToolType.CannedFood;
                displayName = "Canned Food";
            }
            else if (n.Contains("firstaid") || n.Contains("first_aid") || n.Contains("bandage") || n.Contains("med"))
            {
                itemType = SurvivalToolType.FirstAid;
                displayName = "Bandage (First Aid)";
            }
            else if (n.Contains("flashlight"))
            {
                itemType = SurvivalToolType.Flashlight;
                displayName = "Tactical Flashlight";
            }
            else if (n.Contains("match"))
            {
                itemType = SurvivalToolType.Matchbox;
                displayName = "Matchbox";
            }
            else if (n.Contains("pill"))
            {
                itemType = SurvivalToolType.Pills;
                displayName = "Painkiller Pills";
            }
            else if (n.Contains("tape"))
            {
                itemType = SurvivalToolType.Tape;
                displayName = "Duct Tape";
            }
            else if (n.Contains("walkie"))
            {
                itemType = SurvivalToolType.Walkie;
                displayName = "Walkie-Talkie";
            }
            else if (n.Contains("water") || n.Contains("bottle"))
            {
                itemType = SurvivalToolType.WaterBottle;
                displayName = "Water Bottle";
            }
            else if (n.Contains("rust_key") || n.Contains("rustkey") || n.Contains("key"))
            {
                itemType = SurvivalToolType.RustKey;
                displayName = "Rust Key (Boat 4 Key)";
            }
            else if (n.Contains("knife"))
            {
                itemType = SurvivalToolType.Melee_Knife;
                displayName = "Combat Knife";
            }
            else if (n.Contains("crowbar"))
            {
                itemType = SurvivalToolType.Melee_Crowbar;
                displayName = "Steel Crowbar";
            }
        }

        private void EnsureCollider()
        {
            Collider col = GetComponentInChildren<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                Renderer r = GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    box.center = transform.InverseTransformPoint(r.bounds.center);
                    Vector3 localSize = transform.InverseTransformVector(r.bounds.size);
                    box.size = new Vector3(
                        Mathf.Max(0.25f, Mathf.Abs(localSize.x)),
                        Mathf.Max(0.25f, Mathf.Abs(localSize.y)),
                        Mathf.Max(0.25f, Mathf.Abs(localSize.z)));
                }
                else
                {
                    box.size = new Vector3(0.35f, 0.35f, 0.35f);
                }
            }
        }

        public static Color GetItemGlowColor(SurvivalToolType type)
        {
            switch (type)
            {
                // Weapons including knife, crowbar, and firearms glow RED
                case SurvivalToolType.Melee_Knife:
                case SurvivalToolType.Melee_Crowbar:
                case SurvivalToolType.Gun_Pistol:
                case SurvivalToolType.Gun_SMG:
                case SurvivalToolType.Gun_Shotgun:
                case SurvivalToolType.Gun_AssaultRifle:
                case SurvivalToolType.Gun_SniperRifle:
                    return new Color(1.0f, 0.15f, 0.15f);

                // Survival tools glow GREEN
                case SurvivalToolType.Battery:
                case SurvivalToolType.CannedFood:
                case SurvivalToolType.FirstAid:
                case SurvivalToolType.Flashlight:
                case SurvivalToolType.Matchbox:
                case SurvivalToolType.Pills:
                case SurvivalToolType.Tape:
                case SurvivalToolType.Walkie:
                case SurvivalToolType.WaterBottle:
                case SurvivalToolType.RustKey:
                default:
                    return new Color(0.20f, 1.0f, 0.30f);
            }
        }

        private void EnsureSubtleItemLight()
        {
            Color glowColor = GetItemGlowColor(itemType);

            Transform existing = transform.Find("ItemGlow");
            if (existing != null)
            {
                _itemLight = existing.GetComponent<Light>();
                if (_itemLight != null)
                {
                    _itemLight.color = glowColor;
                    _itemLight.intensity = 0.25f;
                    _itemLight.range = 0.85f;
                }
                return;
            }

            GameObject lightObj = new GameObject("ItemGlow");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);

            _itemLight = lightObj.AddComponent<Light>();
            _itemLight.type = LightType.Point;
            _itemLight.color = glowColor;
            _itemLight.intensity = 0.25f;
            _itemLight.range = 0.85f;
            _itemLight.shadows = LightShadows.None;
        }

        /// <summary>
        /// Forces any Survival Tool mesh/prefab to render with its full-color URP Lit textures
        /// even if instantiated directly from the raw FBX mesh.
        /// </summary>
        public static void EnsureColoredURPMaterials(GameObject target, SurvivalToolType type)
        {
            if (target == null) return;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit == null) return;

            if (type == SurvivalToolType.RustKey)
            {
                Renderer[] keyRenderers = target.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in keyRenderers)
                {
                    if (r == null) continue;
                    Material m = r.sharedMaterial;
                    if (m != null && m.shader != urpLit)
                    {
                        Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                        Texture bumpTex = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                        Texture msTex = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                        m.shader = urpLit;
                        if (mainTex != null) { m.SetTexture("_BaseMap", mainTex); m.SetTexture("_MainTex", mainTex); }
                        if (bumpTex != null) { m.SetTexture("_BumpMap", bumpTex); m.EnableKeyword("_NORMALMAP"); }
                        if (msTex != null) { m.SetTexture("_MetallicGlossMap", msTex); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                        m.SetColor("_BaseColor", Color.white);
                        m.SetColor("_Color", Color.white);
                    }
                }
                return;
            }

            if (type == SurvivalToolType.Melee_Knife || type == SurvivalToolType.Melee_Crowbar)
            {
                Renderer[] meleeRenderers = target.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in meleeRenderers)
                {
                    if (r == null) continue;
                    Material m = r.sharedMaterial;
                    if (m != null)
                    {
                        Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null);
                        Texture bumpTex = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                        Texture msTex = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                        m.shader = urpLit;
                        if (mainTex != null) { m.SetTexture("_BaseMap", mainTex); m.SetTexture("_MainTex", mainTex); }
                        if (bumpTex != null) { m.SetTexture("_BumpMap", bumpTex); m.EnableKeyword("_NORMALMAP"); }
                        if (msTex != null) { m.SetTexture("_MetallicGlossMap", msTex); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                        m.SetColor("_BaseColor", Color.white);
                        m.SetColor("_Color", Color.white);
                        m.SetFloat("_Smoothness", 0.50f);
                    }
                }
                return;
            }

            GetTextureSetNames(type, out string matName, out string albedoTexName, out string normalTexName, out string metallicTexName, out Color fallbackTint);

            Material loadedMat = Resources.Load<Material>("SurvivalTools/Materials/" + matName);
            Texture2D albedoTex = Resources.Load<Texture2D>("SurvivalTools/Textures/" + albedoTexName);
            Texture2D normalTex = !string.IsNullOrEmpty(normalTexName) ? Resources.Load<Texture2D>("SurvivalTools/Textures/" + normalTexName) : null;
            Texture2D metallicTex = !string.IsNullOrEmpty(metallicTexName) ? Resources.Load<Texture2D>("SurvivalTools/Textures/" + metallicTexName) : null;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                Material[] updated = new Material[mats.Length];

                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    bool needsRebuild = (m == null || m.shader == null ||
                                         !m.shader.name.Contains("Universal Render Pipeline") ||
                                         (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null));

                    if (!needsRebuild && m != null)
                    {
                        updated[i] = m;
                        continue;
                    }

                    string slotName = (m != null ? m.name.ToLowerInvariant() : "");
                    if (type == SurvivalToolType.Matchbox && (slotName.Contains("matches") || r.gameObject.name.ToLowerInvariant().Contains("1_2")))
                    {
                        Texture2D matchesAlbedo = Resources.Load<Texture2D>("SurvivalTools/Textures/survivaltools2_Matches_AlbedoTransparency");
                        Material matchesMat = new Material(urpLit) { name = "Matches_URP" };
                        ConfigureURPMaterial(matchesMat, matchesAlbedo != null ? matchesAlbedo : albedoTex, null, null, Color.white);
                        updated[i] = matchesMat;
                        continue;
                    }

                    if (loadedMat != null && loadedMat.shader != null && loadedMat.shader.name.Contains("Universal Render Pipeline"))
                    {
                        if (albedoTex != null && loadedMat.GetTexture("_BaseMap") == null)
                        {
                            loadedMat.SetTexture("_BaseMap", albedoTex);
                            loadedMat.SetTexture("_MainTex", albedoTex);
                        }
                        updated[i] = loadedMat;
                    }
                    else
                    {
                        Material runtimeMat = new Material(urpLit) { name = matName + "_URP" };
                        ConfigureURPMaterial(runtimeMat, albedoTex, normalTex, metallicTex, fallbackTint);
                        updated[i] = runtimeMat;
                    }
                }

                r.sharedMaterials = updated;
            }
        }

        private static void ConfigureURPMaterial(Material mat, Texture2D albedo, Texture2D normal, Texture2D metallic, Color tint)
        {
            if (mat == null) return;
            Color finalColor = albedo != null ? Color.white : tint;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", finalColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", finalColor);

            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
        }

        public static void GetTextureSetNames(
            SurvivalToolType type,
            out string matName,
            out string albedoTex,
            out string normalTex,
            out string metallicTex,
            out Color fallbackTint)
        {
            switch (type)
            {
                case SurvivalToolType.Battery:
                    matName = "Battery";
                    albedoTex = "survivaltools2_Battery_AlbedoTransparency";
                    normalTex = "survivaltools2_Battery_Normal";
                    metallicTex = "survivaltools2_Battery_MetallicSmoothness";
                    fallbackTint = new Color(0.85f, 0.55f, 0.15f);
                    break;
                case SurvivalToolType.CannedFood:
                    matName = "CannedFood";
                    albedoTex = "survivaltools2_CannedFood_AlbedoTransparency";
                    normalTex = "survivaltools2_CannedFood_Normal";
                    metallicTex = "survivaltools2_CannedFood_MetallicSmoothness";
                    fallbackTint = new Color(0.75f, 0.32f, 0.22f);
                    break;
                case SurvivalToolType.FirstAid:
                    matName = "FirstAid";
                    albedoTex = "survivaltools2_FirstAidKit_AlbedoTransparency";
                    normalTex = "survivaltools2_FirstAidKit_Normal";
                    metallicTex = "survivaltools2_FirstAidKit_MetallicSmoothness";
                    fallbackTint = new Color(0.85f, 0.18f, 0.18f);
                    break;
                case SurvivalToolType.Flashlight:
                    matName = "Flashlight";
                    albedoTex = "survivaltools2_Flashlight_AlbedoTransparency";
                    normalTex = "survivaltools2_Flashlight_Normal";
                    metallicTex = "survivaltools2_Flashlight_MetallicSmoothness";
                    fallbackTint = new Color(0.25f, 0.30f, 0.35f);
                    break;
                case SurvivalToolType.Matchbox:
                    matName = "Matchbox";
                    albedoTex = "survivaltools2_MatchBox_AlbedoTransparency";
                    normalTex = "survivaltools2_MatchBox_Normal";
                    metallicTex = "survivaltools2_MatchBox_MetallicSmoothness";
                    fallbackTint = new Color(0.82f, 0.28f, 0.18f);
                    break;
                case SurvivalToolType.Pills:
                    matName = "Pills";
                    albedoTex = "survivaltools2_Pills_AlbedoTransparency";
                    normalTex = "survivaltools2_Pills_Normal";
                    metallicTex = "survivaltools2_Pills_MetallicSmoothness";
                    fallbackTint = new Color(0.92f, 0.65f, 0.2f);
                    break;
                case SurvivalToolType.Tape:
                    matName = "Tape";
                    albedoTex = "survivaltools2_Tape_AlbedoTransparency";
                    normalTex = "survivaltools2_Tape_Normal";
                    metallicTex = "survivaltools2_Tape_MetallicSmoothness";
                    fallbackTint = new Color(0.55f, 0.58f, 0.62f);
                    break;
                case SurvivalToolType.Walkie:
                    matName = "Walkie";
                    albedoTex = "survivaltools2_Walkie_AlbedoTransparency";
                    normalTex = "survivaltools2_Walkie_Normal";
                    metallicTex = "survivaltools2_Walkie_MetallicSmoothness";
                    fallbackTint = new Color(0.25f, 0.32f, 0.25f);
                    break;
                case SurvivalToolType.WaterBottle:
                default:
                    matName = "WaterBottle";
                    albedoTex = "survivaltools2_Water_AlbedoTransparency";
                    normalTex = "survivaltools2_Water_Normal";
                    metallicTex = "survivaltools2_Water_MetallicSmoothness";
                    fallbackTint = new Color(0.25f, 0.65f, 0.92f);
                    break;
            }
        }

        private void OnGUI()
        {
            if (StarterAssets.Boat4EscapeInteraction.Instance != null && StarterAssets.Boat4EscapeInteraction.Instance.isEscaping) return;

            if (ActiveTarget != this) return;
            if (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen) return;

            bool gunPromptVisible = PlayerWeaponManager.Instance != null && PlayerWeaponManager.Instance.HasNearbyLootableGun;
            float width = 460f;
            float height = 38f;
            float promptY = gunPromptVisible ? Screen.height * 0.70f : Screen.height * 0.64f;
            Rect promptRect = new Rect((Screen.width - width) * 0.5f, promptY, width, height);

            GUI.color = new Color(0.05f, 0.08f, 0.06f, 0.88f);
            GUI.DrawTexture(promptRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = StarterAssets.DialogueFontProvider.Bold,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.45f, 1.0f, 0.55f);

            string typeSuffix = (itemType == SurvivalToolType.Melee_Knife || itemType == SurvivalToolType.Melee_Crowbar)
                ? " (MELEE WEAPON)"
                : " (STORE IN BACKPACK)";

            GUI.Label(promptRect, $"PRESS [E] TO LOOT {displayName.ToUpper()}{typeSuffix}", style);
        }
    }
}
