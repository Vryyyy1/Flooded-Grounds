using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Implements The Long Dark inspired survival Backpack UI,
    /// complete with category rail, 5x4 slot grid, item inspection panel,
    /// weight management, condition tracking, and hotkey actions.
    /// </summary>
    public class PlayerInventoryBag : MonoBehaviour
    {
        public static PlayerInventoryBag Instance { get; private set; }

        [Serializable]
        public class BagSlot
        {
            public SurvivalToolType itemType;
            public string displayName;
            public int count;
            public string description;
            public Color badgeColor;
            public float unitWeight = 0.25f;
            public int condition = 100;
            public string category = "TOOLS";

            // Gun-specific inventory properties
            public bool isGun;
            public GunType gunType;
            public int currentAmmo;
            public int reserveAmmo;
            public int magazineCapacity;
            public int damage;
            public float fireRate;
            public bool isAutomatic;
            public float reloadTime;
            public float range;
            public float spreadAngle;
            public int pelletsPerShot;
            public GameObject weaponModelPrefab;
        }

        [Serializable]
        public class QuickSlot
        {
            public bool isAssigned;
            public bool isGun;
            public GunType gunType;
            public SurvivalToolType itemType;
            public string assignedName = "";
        }

        [Header("Inventory Bag Settings")]
        public int maxCapacity = 20;
        public float maxWeightKg = 35.00f;
        public float bonusWeightKg = 5.00f;
        public List<BagSlot> items = new List<BagSlot>();

        [Header("3-Slot Hotbar / Quick Access")]
        public QuickSlot[] quickSlots = new QuickSlot[3]
        {
            new QuickSlot(),
            new QuickSlot(),
            new QuickSlot()
        };

        [Header("3D Character Bag Visual")]
        public GameObject characterBagModel;

        private bool _isBagOpen;
        public bool IsBagOpen => _isBagOpen;

        private int _selectedSlotIndex = 0;
        private int _categoryFilter = 0; // 0: ALL, 1: MEDS, 2: FOOD
        private int _sortMode = 0; // 0: A-Z, 1: COND, 2: WEIGHT

        private PlayerHealth _playerHealth;
        private StarterAssetsInputs _inputs;
        private AudioSource _audioSource;

        private string _toastMessage = "";
        private float _toastTimer = 0f;

        private Light _matchFlareLight;
        private float _matchFlareTimer = 0f;

        // UI Procedural Textures
        private Texture2D _vignetteTex;
        private Texture2D _slotBgTex;
        private Texture2D _slotSelectedTex;
        private Texture2D _slotHoverTex;
        private Texture2D _cardBgTex;
        private Texture2D _btnNormalTex;
        private Texture2D _btnHoverTex;
        private Texture2D _accentTex;
        private Texture2D _badgeBgTex;

        // Loaded survival tool icon textures from Resources
        private readonly Dictionary<SurvivalToolType, Texture2D> _itemIcons = new Dictionary<SurvivalToolType, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachInventoryBag()
        {
            var fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            GameObject playerObj = fpc != null ? fpc.gameObject : GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
            {
                if (playerObj.GetComponent<PlayerInventoryBag>() == null)
                {
                    playerObj.AddComponent<PlayerInventoryBag>();
                }
            }

            SetupSceneSurvivalTools(playerObj != null ? playerObj.transform : null);
        }

        private void Awake()
        {
            Instance = this;
            _playerHealth = GetComponent<PlayerHealth>();
            if (_playerHealth != null)
            {
                // Disable passive auto-regen so player health stays damaged until using a Bandage / survival item
                _playerHealth.enableRegen = false;
            }

            _inputs = GetComponent<StarterAssetsInputs>();

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f;
            }

            // Start the Inventory Bag completely empty — player must pick up survival tools in the world
            items.Clear();
            if (maxCapacity < 20) maxCapacity = 20;

            if (quickSlots == null || quickSlots.Length != 3)
            {
                quickSlots = new QuickSlot[3] { new QuickSlot(), new QuickSlot(), new QuickSlot() };
            }
            for (int i = 0; i < 3; i++)
            {
                if (quickSlots[i] == null) quickSlots[i] = new QuickSlot();
            }

            InitUITextures();
            PreloadItemIcons();
            Build3DCharacterInventoryBag();
        }

        private void Start()
        {
            RefreshQuickSlotsFromItems();
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
            }

            if (_matchFlareTimer > 0f)
            {
                _matchFlareTimer -= Time.deltaTime;
                if (_matchFlareLight != null)
                {
                    _matchFlareLight.intensity = 1.6f + Mathf.Sin(Time.time * 14f) * 0.2f;
                    if (_matchFlareTimer <= 0f)
                    {
                        _matchFlareLight.enabled = false;
                    }
                }
            }

            if (WasToggleBagPressed())
            {
                ToggleBag(!_isBagOpen);
            }

            if (WasQuickHealPressed() && !_isBagOpen)
            {
                QuickHealFromBag();
            }

            if (_isBagOpen)
            {
                HandleInventoryHotkeys();
            }
            else
            {
                HandleQuickSlotGameplayHotkeys();
            }
        }

        private bool WasToggleBagPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.bKey.wasPressedThisFrame ||
                    Keyboard.current.iKey.wasPressedThisFrame ||
                    Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
                    return true;
            }
            catch { }

            return false;
        }

        private bool WasQuickHealPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.H)) return true;
            }
            catch { }

            return false;
        }

        private void HandleInventoryHotkeys()
        {
            // Close bag on Escape
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    ToggleBag(false);
                    return;
                }
                if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    UseSelectedItem();
                    return;
                }
                if (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)
                {
                    DropSelectedItem();
                    return;
                }
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ToggleBag(false);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
                {
                    UseSelectedItem();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Delete))
                {
                    DropSelectedItem();
                    return;
                }
            }
            catch { }

            // Number hotkeys 1, 2, 3: Put currently selected item into Quick Slot 1, 2, or 3
            List<BagSlot> filtered = GetFilteredAndSortedItems();
            if (filtered.Count > 0 && _selectedSlotIndex >= 0 && _selectedSlotIndex < filtered.Count)
            {
                BagSlot selected = filtered[_selectedSlotIndex];
                bool assign1 = false, assign2 = false, assign3 = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) assign1 = true;
                    if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) assign2 = true;
                    if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) assign3 = true;
                }
#endif
                try
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) assign1 = true;
                    if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) assign2 = true;
                    if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) assign3 = true;
                }
                catch { }

                if (assign1) { AssignItemToQuickSlot(0, selected); return; }
                if (assign2) { AssignItemToQuickSlot(1, selected); return; }
                if (assign3) { AssignItemToQuickSlot(2, selected); return; }
            }
        }

        private void HandleQuickSlotGameplayHotkeys()
        {
            bool s1 = false, s2 = false, s3 = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) s1 = true;
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) s2 = true;
                if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) s3 = true;
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) s1 = true;
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) s2 = true;
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) s3 = true;
            }
            catch { }

            if (s1) UseQuickSlot(0);
            else if (s2) UseQuickSlot(1);
            else if (s3) UseQuickSlot(2);
        }

        public void ToggleBag(bool open)
        {
            if (open && PlayerWeaponManager.Instance != null)
            {
                PlayerWeaponManager.Instance.SyncEquippedGunToBag();
            }

            _isBagOpen = open;
            StarterAssets.StarterAssetsInputs.InputBlocked = _isBagOpen;

            if (_inputs != null)
            {
                _inputs.cursorInputForLook = !_isBagOpen;
                _inputs.look = Vector2.zero;
            }

            Cursor.visible = _isBagOpen;
            Cursor.lockState = _isBagOpen ? CursorLockMode.None : CursorLockMode.Locked;
            PlayBagZipperSound(_isBagOpen ? 520f : 380f);

            if (_categoryFilter > 3)
            {
                _categoryFilter = 0;
            }

            if (_isBagOpen && _selectedSlotIndex >= items.Count)
            {
                _selectedSlotIndex = Mathf.Max(0, items.Count - 1);
            }
        }

        public int GetTotalItemCount()
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
                total += items[i].count;
            return total;
        }

        public float GetTotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < items.Count; i++)
                total += items[i].unitWeight * items[i].count;
            return total;
        }

        public bool HasPlayerDamage()
        {
            if (_playerHealth == null)
                _playerHealth = GetComponent<PlayerHealth>();

            if (_playerHealth == null) return false;
            return _playerHealth.currentHealth < _playerHealth.maxHealth - 0.5f;
        }

        public static bool IsHealingTool(SurvivalToolType type)
        {
            return type == SurvivalToolType.FirstAid ||
                   type == SurvivalToolType.Pills ||
                   type == SurvivalToolType.CannedFood ||
                   type == SurvivalToolType.WaterBottle;
        }

        public static bool IsGunTool(SurvivalToolType type)
        {
            return type == SurvivalToolType.Gun_Pistol ||
                   type == SurvivalToolType.Gun_SMG ||
                   type == SurvivalToolType.Gun_Shotgun ||
                   type == SurvivalToolType.Gun_AssaultRifle ||
                   type == SurvivalToolType.Gun_SniperRifle;
        }

        public static bool IsMeleeTool(SurvivalToolType type)
        {
            return type == SurvivalToolType.Melee_Knife ||
                   type == SurvivalToolType.Melee_Crowbar;
        }

        public static SurvivalToolType GetToolTypeForGun(GunType gunType)
        {
            switch (gunType)
            {
                case GunType.Pistol: return SurvivalToolType.Gun_Pistol;
                case GunType.SMG: return SurvivalToolType.Gun_SMG;
                case GunType.Shotgun: return SurvivalToolType.Gun_Shotgun;
                case GunType.AssaultRifle: return SurvivalToolType.Gun_AssaultRifle;
                case GunType.SniperRifle: return SurvivalToolType.Gun_SniperRifle;
                case GunType.Knife: return SurvivalToolType.Melee_Knife;
                case GunType.Crowbar: return SurvivalToolType.Melee_Crowbar;
                default: return SurvivalToolType.Gun_Pistol;
            }
        }

        public bool AddGunToBag(GunPickup pickup)
        {
            if (pickup == null) return false;

            SurvivalToolType toolType = GetToolTypeForGun(pickup.gunType);

            // Check if player already has this gun type in their backpack
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].isGun && items[i].gunType == pickup.gunType)
                {
                    int addedAmmo = Mathf.Max(0, pickup.currentAmmo + pickup.reserveAmmo);
                    items[i].reserveAmmo += addedAmmo;
                    items[i].condition = 100;
                    PlayBagZipperSound(680f);
                    ShowToast($"STORED {pickup.gunName.ToUpper()} (+{addedAmmo} AMMO) IN BACKPACK [Press TAB]");
                    return true;
                }
            }

            if (GetTotalItemCount() + 1 > maxCapacity)
            {
                ShowToast($"BACKPACK IS FULL! (MAX {maxCapacity} ITEMS)");
                return false;
            }

            GetItemMetadata(toolType, out string defaultName, out string desc, out Color badge, out float weight, out int cond, out string category);

            BagSlot gunSlot = new BagSlot
            {
                itemType = toolType,
                displayName = !string.IsNullOrEmpty(pickup.gunName) ? pickup.gunName : defaultName,
                count = 1,
                description = desc,
                badgeColor = badge,
                unitWeight = weight,
                condition = cond,
                category = "GUNS",
                isGun = true,
                gunType = pickup.gunType,
                currentAmmo = pickup.currentAmmo,
                reserveAmmo = pickup.reserveAmmo,
                magazineCapacity = pickup.magazineCapacity,
                damage = pickup.damage,
                fireRate = pickup.fireRate,
                isAutomatic = pickup.isAutomatic,
                reloadTime = pickup.reloadTime,
                range = pickup.range,
                spreadAngle = pickup.spreadAngle,
                pelletsPerShot = pickup.pelletsPerShot,
                weaponModelPrefab = pickup.weaponModelPrefab
            };

            items.Add(gunSlot);
            AutoAssignIfQuickSlotsEmpty(gunSlot);
            PlayBagZipperSound(680f);
            ShowToast($"STORED {gunSlot.displayName.ToUpper()} IN BACKPACK [Press TAB]");
            return true;
        }

        public BagSlot GetGunSlot(GunType gunType)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].isGun && items[i].gunType == gunType)
                    return items[i];
            }
            return null;
        }

        public void SyncEquippedGunAmmo(GunType gunType, int currentAmmo, int reserveAmmo)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].isGun && items[i].gunType == gunType)
                {
                    items[i].currentAmmo = currentAmmo;
                    items[i].reserveAmmo = reserveAmmo;
                    return;
                }
            }
        }

        public bool AddItem(SurvivalToolType type, string name, int amount = 1)
        {
            if (GetTotalItemCount() + amount > maxCapacity)
            {
                ShowToast("BACKPACK IS FULL! (MAX 20 ITEMS)");
                return false;
            }

            AddItemSilent(type, name, amount);
            PlayBagZipperSound(660f);
            ShowToast($"STORED +{amount} {name.ToUpper()} IN BACKPACK [Press TAB]");
            return true;
        }

        public bool HasItem(SurvivalToolType type)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemType == type && items[i].count > 0)
                    return true;
            }
            return false;
        }

        public bool ConsumeItem(SurvivalToolType type, int amount = 1)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemType == type && items[i].count > 0)
                {
                    items[i].count -= amount;
                    if (items[i].count <= 0)
                    {
                        items.RemoveAt(i);
                        if (_selectedSlotIndex >= items.Count)
                        {
                            _selectedSlotIndex = Mathf.Max(0, items.Count - 1);
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        private void AddItemSilent(SurvivalToolType type, string name, int amount)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemType == type)
                {
                    items[i].count += amount;
                    return;
                }
            }

            GetItemMetadata(type, out string defaultName, out string desc, out Color badge, out float weight, out int cond, out string category);
            BagSlot newSlot = new BagSlot
            {
                itemType = type,
                displayName = string.IsNullOrEmpty(name) ? defaultName : name,
                count = amount,
                description = desc,
                badgeColor = badge,
                unitWeight = weight,
                condition = cond,
                category = category
            };

            if (type == SurvivalToolType.Melee_Knife)
            {
                newSlot.isGun = true;
                newSlot.gunType = GunType.Knife;
                newSlot.damage = 60;
                newSlot.fireRate = 0.35f;
                newSlot.range = 2.4f;
                newSlot.category = "MELEE";
            }
            else if (type == SurvivalToolType.Melee_Crowbar)
            {
                newSlot.isGun = true;
                newSlot.gunType = GunType.Crowbar;
                newSlot.damage = 85;
                newSlot.fireRate = 0.55f;
                newSlot.range = 2.6f;
                newSlot.category = "MELEE";
            }

            items.Add(newSlot);
            AutoAssignIfQuickSlotsEmpty(newSlot);
        }

        #region 3-Slot Hotbar / Quick Access System

        public BagSlot GetItemForQuickSlot(int slotIndex)
        {
            if (quickSlots == null || slotIndex < 0 || slotIndex >= quickSlots.Length) return null;
            var qs = quickSlots[slotIndex];
            if (!qs.isAssigned) return null;

            if (qs.isGun)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if ((items[i].isGun || IsGunTool(items[i].itemType)) && items[i].gunType == qs.gunType)
                    {
                        return items[i];
                    }
                }
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (!items[i].isGun && items[i].itemType == qs.itemType)
                    {
                        return items[i];
                    }
                }
            }

            return null;
        }

        public void AssignItemToQuickSlot(int slotIndex, BagSlot bagItem)
        {
            if (quickSlots == null || slotIndex < 0 || slotIndex >= quickSlots.Length || bagItem == null) return;

            quickSlots[slotIndex].isAssigned = true;
            quickSlots[slotIndex].isGun = bagItem.isGun || IsGunTool(bagItem.itemType);
            quickSlots[slotIndex].gunType = bagItem.gunType;
            quickSlots[slotIndex].itemType = bagItem.itemType;
            quickSlots[slotIndex].assignedName = bagItem.displayName;

            PlayBagZipperSound(600f + slotIndex * 90f);
            ShowToast($"ASSIGNED {bagItem.displayName.ToUpper()} TO QUICK SLOT {slotIndex + 1}!");
        }

        public void ClearQuickSlot(int slotIndex)
        {
            if (quickSlots == null || slotIndex < 0 || slotIndex >= quickSlots.Length) return;
            quickSlots[slotIndex].isAssigned = false;
            quickSlots[slotIndex].assignedName = "";
            PlayBagZipperSound(320f);
            ShowToast($"CLEARED QUICK SLOT {slotIndex + 1}");
        }

        public void UseQuickSlot(int slotIndex)
        {
            if (quickSlots == null || slotIndex < 0 || slotIndex >= quickSlots.Length) return;
            var qs = quickSlots[slotIndex];
            if (!qs.isAssigned)
            {
                ShowToast($"QUICK SLOT {slotIndex + 1} IS EMPTY — OPEN BAG [TAB] TO ASSIGN AN ITEM");
                PlayBagZipperSound(240f);
                return;
            }

            BagSlot bagItem = GetItemForQuickSlot(slotIndex);

            // Gun handling
            if (qs.isGun)
            {
                var pwm = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance : GetComponent<PlayerWeaponManager>();
                if (pwm != null && pwm.HasGunEquipped && pwm.equippedGunType == qs.gunType)
                {
                    ShowToast($"{qs.gunType} IS ALREADY EQUIPPED IN YOUR HANDS");
                    PlayBagZipperSound(500f);
                    return;
                }

                if (bagItem != null)
                {
                    int idx = items.IndexOf(bagItem);
                    if (idx >= 0)
                    {
                        UseItemAt(idx);
                        return;
                    }
                }
                else
                {
                    ShowToast($"NO {qs.gunType} FOUND IN BACKPACK!");
                    PlayBagZipperSound(240f);
                    return;
                }
            }

            if (bagItem == null || bagItem.count <= 0)
            {
                string itemName = !string.IsNullOrEmpty(qs.assignedName) ? qs.assignedName : qs.itemType.ToString();
                ShowToast($"NO {itemName.ToUpper()} LEFT IN BACKPACK!");
                PlayBagZipperSound(240f);
                return;
            }

            int actualIndex = items.IndexOf(bagItem);
            if (actualIndex >= 0)
            {
                UseItemAt(actualIndex);
            }
        }

        public void AutoAssignIfQuickSlotsEmpty(BagSlot newSlot)
        {
            if (newSlot == null || quickSlots == null) return;

            // Check if already assigned
            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] != null && quickSlots[i].isAssigned)
                {
                    if (newSlot.isGun && quickSlots[i].isGun && quickSlots[i].gunType == newSlot.gunType) return;
                    if (!newSlot.isGun && !quickSlots[i].isGun && quickSlots[i].itemType == newSlot.itemType) return;
                }
            }

            // Find first free slot
            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] == null || !quickSlots[i].isAssigned)
                {
                    AssignItemToQuickSlot(i, newSlot);
                    break;
                }
            }
        }

        public void RefreshQuickSlotsFromItems()
        {
            if (items == null || quickSlots == null) return;
            for (int i = 0; i < items.Count && i < quickSlots.Length; i++)
            {
                if (quickSlots[i] == null || !quickSlots[i].isAssigned)
                {
                    AssignItemToQuickSlot(i, items[i]);
                }
            }
        }

        private static string GetShortSlotName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string upper = name.ToUpper().Trim();
            if (upper.Contains("ASSAULT") || upper.Contains("RIFLE")) return "RIFLE";
            if (upper.Contains("SHOTGUN")) return "SHOTGUN";
            if (upper.Contains("SNIPER")) return "SNIPER";
            if (upper.Contains("PISTOL")) return "PISTOL";
            if (upper.Contains("SMG")) return "SMG";
            if (upper.Contains("BANDAGE")) return "BANDAGE";
            if (upper.Contains("MEDKIT")) return "MEDKIT";
            if (upper.Contains("PILLS") || upper.Contains("ANTIBIOTICS")) return "PILLS";
            if (upper.Contains("CANNED") || upper.Contains("FOOD") || upper.Contains("RATIONS")) return "FOOD";
            if (upper.Contains("WATER")) return "WATER";
            if (upper.Contains("MATCH")) return "MATCHES";
            if (upper.Contains("BATTERY")) return "BATTERY";
            if (upper.Contains("FLASHLIGHT")) return "TORCH";
            if (upper.Contains("TAPE")) return "TAPE";
            if (upper.Contains("RADIO") || upper.Contains("WALKIE")) return "RADIO";
            if (upper.Contains("KEY") || upper.Contains("RUST")) return "RUST KEY";
            if (upper.Contains("KNIFE")) return "KNIFE";
            if (upper.Contains("CROWBAR")) return "CROWBAR";

            if (upper.Length > 9) return upper.Substring(0, 8) + ".";
            return upper;
        }

        #endregion

        public void UseSelectedItem()
        {
            List<BagSlot> filtered = GetFilteredAndSortedItems();
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < filtered.Count)
            {
                int actualIndex = items.IndexOf(filtered[_selectedSlotIndex]);
                if (actualIndex >= 0)
                {
                    UseItemAt(actualIndex);
                }
            }
        }

        public void DropSelectedItem()
        {
            List<BagSlot> filtered = GetFilteredAndSortedItems();
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < filtered.Count)
            {
                int actualIndex = items.IndexOf(filtered[_selectedSlotIndex]);
                if (actualIndex >= 0)
                {
                    DropItemAt(actualIndex);
                }
            }
        }

        public void UseItemAt(int index)
        {
            if (index < 0 || index >= items.Count) return;

            BagSlot slot = items[index];
            if (slot.count <= 0) return;

            // Equip Gun or Melee Weapon from Backpack
            if (slot.isGun || IsGunTool(slot.itemType) || IsMeleeTool(slot.itemType))
            {
                var pwm = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance : GetComponent<PlayerWeaponManager>();
                if (pwm == null) pwm = gameObject.AddComponent<PlayerWeaponManager>();

                pwm.EquipGunFromBagSlot(slot);
                PlayBagZipperSound(760f);
                string ammoInfo = (slot.gunType == GunType.Knife || slot.gunType == GunType.Crowbar) ? "MELEE" : $"{slot.currentAmmo}/{slot.reserveAmmo} AMMO";
                ShowToast($"EQUIPPED {slot.displayName.ToUpper()} ({ammoInfo})");
                return;
            }

            if (_playerHealth == null)
                _playerHealth = GetComponent<PlayerHealth>();

            // Bandages & healing items can only be used if the character has low life or has taken damage
            if (IsHealingTool(slot.itemType) && !HasPlayerDamage())
            {
                ShowToast($"CANNOT USE {slot.displayName.ToUpper()}: YOUR HP IS FULL ({_playerHealth.currentHealth:F0}/{_playerHealth.maxHealth:F0})!");
                PlayBagZipperSound(260f);
                return;
            }

            switch (slot.itemType)
            {
                case SurvivalToolType.FirstAid:
                    if (_playerHealth != null) _playerHealth.Heal(55f);
                    ShowToast($"USED BANDAGE (+55 HP) — HP: {(_playerHealth != null ? _playerHealth.currentHealth : 100f):F0}/{(_playerHealth != null ? _playerHealth.maxHealth : 100f):F0}");
                    break;

                case SurvivalToolType.Pills:
                    if (_playerHealth != null) _playerHealth.Heal(35f);
                    ShowToast($"USED ANTIBIOTICS (+35 HP) — HP: {(_playerHealth != null ? _playerHealth.currentHealth : 100f):F0}/{(_playerHealth != null ? _playerHealth.maxHealth : 100f):F0}");
                    break;

                case SurvivalToolType.CannedFood:
                    if (_playerHealth != null) _playerHealth.Heal(25f);
                    ShowToast($"CONSUMED RATIONS (+25 HP) — HP: {(_playerHealth != null ? _playerHealth.currentHealth : 100f):F0}/{(_playerHealth != null ? _playerHealth.maxHealth : 100f):F0}");
                    break;

                case SurvivalToolType.WaterBottle:
                    if (_playerHealth != null) _playerHealth.Heal(20f);
                    ShowToast($"DRANK FRESHWATER (+20 HP) — HP: {(_playerHealth != null ? _playerHealth.currentHealth : 100f):F0}/{(_playerHealth != null ? _playerHealth.maxHealth : 100f):F0}");
                    break;

                case SurvivalToolType.Battery:
                    var fl = GetComponentInChildren<FlashlightController>();
                    if (fl != null)
                    {
                        fl.beamIntensity = 42f;
                        fl.beamRange = 75f;
                        fl.ApplyBeamSettings();
                        fl.TurnOn();
                    }
                    ShowToast("BATTERY INSTALLED: TACTICAL FLASHLIGHT SUPERCHARGED!");
                    break;

                case SurvivalToolType.Flashlight:
                    var flCtrl = GetComponentInChildren<FlashlightController>();
                    if (flCtrl != null)
                    {
                        flCtrl.Toggle();
                    }
                    ShowToast("TACTICAL FLASHLIGHT TOGGLED [F]");
                    PlayBagZipperSound(740f);
                    return;

                case SurvivalToolType.Matchbox:
                    IgniteMatchFlare();
                    ShowToast("STRUCK STORM MATCH: WARM FLARE ACTIVE (45s)");
                    break;

                case SurvivalToolType.Tape:
                    if (!HasPlayerDamage())
                    {
                        ShowToast("HP IS FULL — SAVE DUCT TAPE FOR WHEN YOU HAVE DAMAGE!");
                        PlayBagZipperSound(260f);
                        return;
                    }
                    if (_playerHealth != null) _playerHealth.Heal(15f);
                    ShowToast("USED DUCT TAPE: PATCHED WOUNDS (+15 HP)");
                    break;

                case SurvivalToolType.Walkie:
                    ShowToast("RADIO: \"Beware people who's been infected are mutated, dont get infected and find a key, use the boat and escape\"");
                    PlayBagZipperSound(820f);
                    return;

                case SurvivalToolType.RustKey:
                    if (Boat4EscapeInteraction.Instance != null &&
                        Boat4EscapeInteraction.Instance.GetDistanceToBoatHull(transform.position) <= 6.5f)
                    {
                        Boat4EscapeInteraction.Instance.TryInteractWithBoat4();
                        return;
                    }
                    ShowToast("RUST KEY READY — BRING IT TO BOAT 4 AND PRESS [E] TO UNLOCK & ESCAPE!");
                    PlayBagZipperSound(840f);
                    return;
            }

            PlayBagZipperSound(780f);
            slot.count--;
            if (slot.count <= 0)
            {
                items.RemoveAt(index);
                if (_selectedSlotIndex >= items.Count)
                {
                    _selectedSlotIndex = Mathf.Max(0, items.Count - 1);
                }
            }
        }

        public void DropItemAt(int index)
        {
            if (index < 0 || index >= items.Count) return;

            BagSlot slot = items[index];
            Vector3 dropPos = transform.position + transform.forward * 1.35f + Vector3.up * 0.45f;
            if (Physics.Raycast(dropPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                dropPos = hit.point + Vector3.up * 0.08f;
            }

            // Dropping Melee Weapon
            if (IsMeleeTool(slot.itemType) || slot.gunType == GunType.Knife || slot.gunType == GunType.Crowbar)
            {
                SpawnSurvivalToolInWorld(slot.itemType, dropPos, Quaternion.identity);
                ShowToast($"DROPPED {slot.displayName.ToUpper()}");
                PlayBagZipperSound(340f);

                GunType droppedType = slot.gunType;
                items.RemoveAt(index);
                if (_selectedSlotIndex >= items.Count)
                {
                    _selectedSlotIndex = Mathf.Max(0, items.Count - 1);
                }

                if (PlayerWeaponManager.Instance != null &&
                    PlayerWeaponManager.Instance.HasGunEquipped &&
                    PlayerWeaponManager.Instance.equippedGunType == droppedType)
                {
                    PlayerWeaponManager.Instance.UnequipCurrentWeaponAndTryEquipNext();
                }
                return;
            }

            if (slot.isGun || IsGunTool(slot.itemType))
            {
                if (PlayerWeaponManager.Instance != null)
                {
                    PlayerWeaponManager.Instance.SyncEquippedGunToBag();
                }

                SpawnGunInWorld(slot, dropPos);
                ShowToast($"DROPPED {slot.displayName.ToUpper()}");
                PlayBagZipperSound(340f);

                GunType droppedGunType = slot.gunType;
                items.RemoveAt(index);
                if (_selectedSlotIndex >= items.Count)
                {
                    _selectedSlotIndex = Mathf.Max(0, items.Count - 1);
                }

                if (PlayerWeaponManager.Instance != null &&
                    PlayerWeaponManager.Instance.HasGunEquipped &&
                    PlayerWeaponManager.Instance.equippedGunType == droppedGunType)
                {
                    PlayerWeaponManager.Instance.UnequipCurrentWeaponAndTryEquipNext();
                }
                return;
            }

            SpawnSurvivalToolInWorld(slot.itemType, dropPos, Quaternion.identity);
            ShowToast($"DROPPED {slot.displayName.ToUpper()}");
            PlayBagZipperSound(340f);

            slot.count--;
            if (slot.count <= 0)
            {
                items.RemoveAt(index);
                if (_selectedSlotIndex >= items.Count)
                {
                    _selectedSlotIndex = Mathf.Max(0, items.Count - 1);
                }
            }
        }

        public static GameObject SpawnGunInWorld(BagSlot slot, Vector3 position)
        {
            string resPath = slot.gunType switch
            {
                GunType.Pistol => "Guns/Pistol_Pickup",
                GunType.SMG => "Guns/SMG_Pickup",
                GunType.Shotgun => "Guns/Shotgun_Pickup",
                GunType.AssaultRifle => "Guns/AssaultRifle_Pickup",
                GunType.SniperRifle => "Guns/SniperRifle_Pickup",
                _ => "Guns/Pistol_Pickup"
            };

            GameObject prefab = Resources.Load<GameObject>(resPath);
            if (prefab != null)
            {
                GameObject spawned = Instantiate(prefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                spawned.name = $"Dropped_{slot.displayName}";
                var gp = spawned.GetComponent<GunPickup>();
                if (gp != null)
                {
                    gp.currentAmmo = slot.currentAmmo;
                    gp.reserveAmmo = slot.reserveAmmo;
                }
                return spawned;
            }
            return null;
        }

        private void QuickHealFromBag()
        {
            if (!HasPlayerDamage())
            {
                ShowToast("YOUR HP IS FULL (100/100) — BANDAGE NOT NEEDED YET!");
                return;
            }

            SurvivalToolType[] healPriority =
            {
                SurvivalToolType.FirstAid,
                SurvivalToolType.Pills,
                SurvivalToolType.CannedFood,
                SurvivalToolType.WaterBottle
            };

            foreach (var hType in healPriority)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].itemType == hType)
                    {
                        UseItemAt(i);
                        return;
                    }
                }
            }

            ShowToast("NO HEALING ITEMS (BANDAGES / PILLS) IN BACKPACK!");
        }

        private void IgniteMatchFlare()
        {
            if (_matchFlareLight == null)
            {
                GameObject flareObj = new GameObject("PlayerMatchFlareLight");
                flareObj.transform.SetParent(transform, false);
                flareObj.transform.localPosition = new Vector3(0.22f, 1.15f, 0.45f);
                _matchFlareLight = flareObj.AddComponent<Light>();
                _matchFlareLight.type = LightType.Point;
                _matchFlareLight.color = new Color(1.0f, 0.58f, 0.22f);
                _matchFlareLight.range = 14f;
                _matchFlareLight.shadows = LightShadows.None;
            }

            _matchFlareLight.enabled = true;
            _matchFlareTimer = 45f;
        }

        public void ShowToast(string msg)
        {
            _toastMessage = msg;
            _toastTimer = 3.5f;
        }

        /// <summary>
        /// Builds a 3D Tactical Survival Backpack & Utility Pouch attached directly to the player character (`PlayerCapsule`).
        /// </summary>
        public void Build3DCharacterInventoryBag()
        {
            Transform existing = transform.Find("PlayerInventoryBag_3D");
            if (existing != null)
            {
                characterBagModel = existing.gameObject;
                return;
            }

            GameObject bagRoot = new GameObject("PlayerInventoryBag_3D");
            bagRoot.transform.SetParent(transform, false);
            bagRoot.transform.localPosition = new Vector3(0f, 1.05f, -0.28f);
            bagRoot.transform.localRotation = Quaternion.identity;
            characterBagModel = bagRoot;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material canvasMat = new Material(urpLit) { name = "BagCanvas_URP" };
            SetMatColor(canvasMat, new Color(0.26f, 0.31f, 0.22f));

            Material strapMat = new Material(urpLit) { name = "BagStrap_URP" };
            SetMatColor(strapMat, new Color(0.16f, 0.12f, 0.09f));

            Material pouchMat = new Material(urpLit) { name = "BagMedPouch_URP" };
            SetMatColor(pouchMat, new Color(0.72f, 0.16f, 0.14f));

            Material buckleMat = new Material(urpLit) { name = "BagBuckle_URP" };
            SetMatColor(buckleMat, new Color(0.78f, 0.65f, 0.28f));

            CreateBagPart("MainPack", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(0f, 0f, 0f), new Vector3(0.38f, 0.48f, 0.22f), canvasMat);

            GameObject topRoll = CreateBagPart("TopBedroll", PrimitiveType.Cylinder, bagRoot.transform,
                new Vector3(0f, 0.27f, 0f), new Vector3(0.14f, 0.20f, 0.14f), strapMat);
            topRoll.transform.localEulerAngles = new Vector3(0f, 0f, 90f);

            CreateBagPart("RearPocket", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(0f, -0.05f, -0.12f), new Vector3(0.28f, 0.26f, 0.08f), canvasMat);

            CreateBagPart("SideMedPouch", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(0.22f, -0.04f, 0f), new Vector3(0.08f, 0.22f, 0.14f), pouchMat);

            CreateBagPart("LeftStrap", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(-0.13f, 0.04f, 0.18f), new Vector3(0.05f, 0.44f, 0.26f), strapMat);
            CreateBagPart("RightStrap", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(0.13f, 0.04f, 0.18f), new Vector3(0.05f, 0.44f, 0.26f), strapMat);

            CreateBagPart("WaistBeltPouch", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(-0.22f, -0.22f, 0.34f), new Vector3(0.12f, 0.14f, 0.10f), canvasMat);

            CreateBagPart("LeftBuckle", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(-0.08f, -0.02f, -0.165f), new Vector3(0.03f, 0.06f, 0.02f), buckleMat);
            CreateBagPart("RightBuckle", PrimitiveType.Cube, bagRoot.transform,
                new Vector3(0.08f, -0.02f, -0.165f), new Vector3(0.03f, 0.06f, 0.02f), buckleMat);
        }

        private static GameObject CreateBagPart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;

            Renderer r = part.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return part;
        }

        private static void SetMatColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>
        /// Finds all Survival Tools placed in the scene, attaches SurvivalItemPickup to their root GameObject,
        /// and applies full-color URP materials so every survival tool is pickable into the bag.
        /// </summary>
        public static void SetupSceneSurvivalTools(Transform playerTransform)
        {
            GameObject oldCache = GameObject.Find("SurvivalTools_SupplyCache");
            if (oldCache != null)
            {
                Destroy(oldCache);
            }

            string[] toolKeywords = { "battery", "cannedfood", "firstaid", "flashlight", "matchbox", "pills", "tape", "walkie", "waterbottle", "rust_key", "rustkey", "knife", "crowbar" };
            MeshRenderer[] allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            HashSet<GameObject> configuredRoots = new HashSet<GameObject>();

            foreach (MeshRenderer mr in allRenderers)
            {
                if (mr == null || mr.gameObject == null) continue;
                if (playerTransform != null && mr.transform.IsChildOf(playerTransform)) continue;
                if (mr.GetComponentInParent<GunPickup>() != null || mr.GetComponentInParent<RadioInteraction>() != null) continue;

                string objName = mr.gameObject.name.ToLowerInvariant();
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                string meshName = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.name.ToLowerInvariant() : "";
                string parentName = mr.transform.parent != null ? mr.transform.parent.name.ToLowerInvariant() : "";

                EnsureBoatURPMaterialIfNeeded(mr, objName, parentName);

                foreach (string kw in toolKeywords)
                {
                    if (objName.Contains(kw) || meshName.Contains(kw) || parentName.Contains(kw))
                    {
                        GameObject rootObj = ResolveSurvivalToolRoot(mr.transform, kw);
                        if (configuredRoots.Contains(rootObj)) break;
                        configuredRoots.Add(rootObj);

                        SurvivalItemPickup[] childPickups = rootObj.GetComponentsInChildren<SurvivalItemPickup>(true);
                        foreach (var cp in childPickups)
                        {
                            if (cp != null && cp.gameObject != rootObj)
                            {
                                Destroy(cp);
                            }
                        }

                        SurvivalItemPickup pickup = rootObj.GetComponent<SurvivalItemPickup>();
                        if (pickup == null)
                        {
                            pickup = rootObj.AddComponent<SurvivalItemPickup>();
                        }
                        pickup.AutoDetectItemType();
                        SurvivalItemPickup.EnsureColoredURPMaterials(rootObj, pickup.itemType);
                        break;
                    }
                }
            }
        }

        private static void EnsureBoatURPMaterialIfNeeded(MeshRenderer mr, string objNameLower, string parentNameLower)
        {
            if (mr == null) return;

            int boatIdx = 0;
            for (int i = 1; i <= 5; i++)
            {
                string token = $"boat_{i}";
                if (objNameLower.Contains(token) || parentNameLower.Contains(token))
                {
                    boatIdx = i;
                    break;
                }
            }

            if (boatIdx == 0 && mr.sharedMaterial != null && mr.sharedMaterial.name != null)
            {
                string matNameLower = mr.sharedMaterial.name.ToLowerInvariant();
                for (int i = 1; i <= 5; i++)
                {
                    if (matNameLower.Contains($"boat_{i}"))
                    {
                        boatIdx = i;
                        break;
                    }
                }
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            bool isRustKey = objNameLower.Contains("rust_key") || parentNameLower.Contains("rust_key") ||
                             (mr.sharedMaterial != null && mr.sharedMaterial.name != null && mr.sharedMaterial.name.ToLowerInvariant().Contains("rust_key"));

            if (isRustKey)
            {
#if UNITY_EDITOR
                Material rkMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Rust Key/Materials/rust_key_autodesk.mat");
                Texture rkAlbedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Rust Key/Textures/rust_key_Albedo.png");
                Texture rkNormal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Rust Key/Textures/rust_key_Normal.png");
                Texture rkMetal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/Rust Key/Textures/rust_key_Metalness.png");

                if (rkMat != null)
                {
                    if (rkMat.shader != urpLit) rkMat.shader = urpLit;
                    if (rkAlbedo != null)
                    {
                        rkMat.SetTexture("_BaseMap", rkAlbedo);
                        rkMat.SetTexture("_MainTex", rkAlbedo);
                    }
                    if (rkNormal != null)
                    {
                        rkMat.SetTexture("_BumpMap", rkNormal);
                        rkMat.EnableKeyword("_NORMALMAP");
                    }
                    if (rkMetal != null)
                    {
                        rkMat.SetTexture("_MetallicGlossMap", rkMetal);
                        rkMat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    rkMat.SetColor("_BaseColor", Color.white);
                    rkMat.SetColor("_Color", Color.white);
                    mr.sharedMaterial = rkMat;
                    return;
                }
#endif
                Material curMat = mr.sharedMaterial;
                if (curMat != null && curMat.shader != urpLit)
                {
                    Texture mTex = curMat.HasProperty("_MainTex") ? curMat.GetTexture("_MainTex") : null;
                    Texture bTex = curMat.HasProperty("_BumpMap") ? curMat.GetTexture("_BumpMap") : null;
                    Texture msTex = curMat.HasProperty("_MetallicGlossMap") ? curMat.GetTexture("_MetallicGlossMap") : null;
                    curMat.shader = urpLit;
                    if (mTex != null) { curMat.SetTexture("_BaseMap", mTex); curMat.SetTexture("_MainTex", mTex); }
                    if (bTex != null) { curMat.SetTexture("_BumpMap", bTex); curMat.EnableKeyword("_NORMALMAP"); }
                    if (msTex != null) { curMat.SetTexture("_MetallicGlossMap", msTex); curMat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                    curMat.SetColor("_BaseColor", Color.white);
                    curMat.SetColor("_Color", Color.white);
                }
                return;
            }

            if (boatIdx < 1 || boatIdx > 5) return;

            if (boatIdx == 4)
            {
                GameObject boatRoot = mr.transform.parent != null && mr.transform.parent.name.ToLowerInvariant().Contains("boat_4")
                    ? mr.transform.parent.gameObject
                    : mr.gameObject;
                if (boatRoot.GetComponent<Boat4EscapeInteraction>() == null)
                {
                    boatRoot.AddComponent<Boat4EscapeInteraction>();
                }
            }

#if UNITY_EDITOR
            Material boatMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Boats/Materials/Boat_{boatIdx}_Mat.mat");
            Texture albedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>($"Assets/Boats/Textures/Boat_{boatIdx}_Color.png");
            Texture normal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>($"Assets/Boats/Textures/Boat_{boatIdx}_Normal.png");
            Texture ms = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>($"Assets/Boats/Textures/Boat_{boatIdx}_MetallicSmoothness.png");

            if (boatMat != null)
            {
                if (boatMat.shader != urpLit) boatMat.shader = urpLit;
                if (albedo != null)
                {
                    boatMat.SetTexture("_BaseMap", albedo);
                    boatMat.SetTexture("_MainTex", albedo);
                }
                if (normal != null)
                {
                    boatMat.SetTexture("_BumpMap", normal);
                    boatMat.EnableKeyword("_NORMALMAP");
                }
                if (ms != null)
                {
                    boatMat.SetTexture("_MetallicGlossMap", ms);
                    boatMat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                boatMat.SetColor("_BaseColor", Color.white);
                boatMat.SetColor("_Color", Color.white);
                mr.sharedMaterial = boatMat;
                return;
            }
#endif
            Material currentMat = mr.sharedMaterial;
            if (currentMat != null && currentMat.shader != urpLit)
            {
                Texture mainTex = currentMat.HasProperty("_MainTex") ? currentMat.GetTexture("_MainTex") : null;
                Texture bumpTex = currentMat.HasProperty("_BumpMap") ? currentMat.GetTexture("_BumpMap") : null;
                Texture msTex = currentMat.HasProperty("_MetallicGlossMap") ? currentMat.GetTexture("_MetallicGlossMap") : null;

                currentMat.shader = urpLit;
                if (mainTex != null)
                {
                    currentMat.SetTexture("_BaseMap", mainTex);
                    currentMat.SetTexture("_MainTex", mainTex);
                }
                if (bumpTex != null)
                {
                    currentMat.SetTexture("_BumpMap", bumpTex);
                    currentMat.EnableKeyword("_NORMALMAP");
                }
                if (msTex != null)
                {
                    currentMat.SetTexture("_MetallicGlossMap", msTex);
                    currentMat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                currentMat.SetColor("_BaseColor", Color.white);
                currentMat.SetColor("_Color", Color.white);
            }
        }

        private static GameObject ResolveSurvivalToolRoot(Transform t, string keyword)
        {
            Transform current = t;
            while (current.parent != null)
            {
                string pName = current.parent.name.ToLowerInvariant();
                if (pName.Contains(keyword) || pName.Contains("matchbox") || pName.Contains("firstaid") ||
                    pName.Contains("cannedfood") || pName.Contains("waterbottle") || pName.Contains("walkie") ||
                    pName.Contains("flashlight") || pName.Contains("battery") || pName.Contains("pills") || pName.Contains("tape") ||
                    pName.Contains("rust_key") || pName.Contains("rustkey"))
                {
                    current = current.parent;
                }
                else
                {
                    break;
                }
            }
            return current.gameObject;
        }

        public static GameObject SpawnSurvivalToolInWorld(SurvivalToolType type, Vector3 position, Quaternion rotation)
        {
            string prefabName = GetPrefabFileName(type);
            GameObject prefab = null;

#if UNITY_EDITOR
            if (type == SurvivalToolType.RustKey)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rust Key/Prefabs/rust_key.prefab");
            }
            else if (type == SurvivalToolType.Melee_Knife)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SurvivalTools/Prefabs/knife.prefab");
                if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/iDos Games/Stylized Knife/Prefabs/Knife.prefab");
            }
            else if (type == SurvivalToolType.Melee_Crowbar)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SurvivalTools/Prefabs/crowbar.prefab");
                if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowbar/Prefab/Crowbar.prefab");
            }
#endif
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("SurvivalTools/Prefabs/" + prefabName);
            }
            GameObject instance;

            if (prefab != null)
            {
                instance = Instantiate(prefab, position, prefab.transform.rotation);
                instance.name = prefabName;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = prefabName;
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            }

            SurvivalItemPickup pickup = instance.GetComponent<SurvivalItemPickup>();
            if (pickup == null)
                pickup = instance.AddComponent<SurvivalItemPickup>();

            pickup.itemType = type;
            pickup.AutoDetectItemType();
            SurvivalItemPickup.EnsureColoredURPMaterials(instance, type);
            return instance;
        }

        public static string GetPrefabFileName(SurvivalToolType type)
        {
            switch (type)
            {
                case SurvivalToolType.Battery: return "battery";
                case SurvivalToolType.CannedFood: return "cannedfood";
                case SurvivalToolType.FirstAid: return "firstaid";
                case SurvivalToolType.Flashlight: return "flashlight";
                case SurvivalToolType.Matchbox: return "matchbox";
                case SurvivalToolType.Pills: return "pills";
                case SurvivalToolType.Tape: return "tape";
                case SurvivalToolType.Walkie: return "walkie";
                case SurvivalToolType.RustKey: return "rust_key";
                case SurvivalToolType.Melee_Knife: return "knife";
                case SurvivalToolType.Melee_Crowbar: return "crowbar";
                case SurvivalToolType.WaterBottle:
                default: return "waterbottle";
            }
        }

        public static void GetItemMetadata(SurvivalToolType type, out string defaultName, out string description, out Color badgeColor, out float unitWeight, out int condition, out string category)
        {
            switch (type)
            {
                case SurvivalToolType.FirstAid:
                    defaultName = "Bandage";
                    description = "A sterile emergency pressure bandage roll. Halts bleeding and restores extensive health when injured.";
                    badgeColor = new Color(0.85f, 0.20f, 0.20f);
                    unitWeight = 0.50f;
                    condition = 100;
                    category = "MEDS";
                    break;
                case SurvivalToolType.Pills:
                    defaultName = "Antibiotics";
                    description = "A bottle of Antibiotics. Take it to treat wounds, soothe trauma, and stop infection from spreading.";
                    badgeColor = new Color(0.70f, 0.40f, 0.85f);
                    unitWeight = 0.10f;
                    condition = 95;
                    category = "MEDS";
                    break;
                case SurvivalToolType.CannedFood:
                    defaultName = "Canned Food";
                    description = "Sealed preserved canned rations. High in energy, restores health and sustains vitality in the wasteland.";
                    badgeColor = new Color(0.85f, 0.45f, 0.15f);
                    unitWeight = 0.45f;
                    condition = 85;
                    category = "FOOD";
                    break;
                case SurvivalToolType.WaterBottle:
                    defaultName = "Freshwater Bottle";
                    description = "Purified freshwater bottle. Critical for hydration and sustaining physical endurance in the flooded wasteland.";
                    badgeColor = new Color(0.20f, 0.65f, 0.95f);
                    unitWeight = 0.60f;
                    condition = 100;
                    category = "FOOD";
                    break;
                case SurvivalToolType.Battery:
                    defaultName = "Heavy Battery";
                    description = "High-output industrial lithium battery cell. Supercharges tactical flashlight beam intensity and distance.";
                    badgeColor = new Color(0.92f, 0.78f, 0.18f);
                    unitWeight = 0.20f;
                    condition = 100;
                    category = "GEAR";
                    break;
                case SurvivalToolType.Flashlight:
                    defaultName = "Tactical Flashlight";
                    description = "Heavy-duty waterproof torch. Pierces through the murky darkness of the flooded wetlands. Press [F] to toggle.";
                    badgeColor = new Color(0.40f, 0.50f, 0.62f);
                    unitWeight = 0.35f;
                    condition = 90;
                    category = "GEAR";
                    break;
                case SurvivalToolType.Matchbox:
                    defaultName = "Storm Matches";
                    description = "Waterproof strike-anywhere wooden matches. Creates an immediate warm flare illuminating the surroundings for 45 seconds.";
                    badgeColor = new Color(0.92f, 0.40f, 0.16f);
                    unitWeight = 0.05f;
                    condition = 100;
                    category = "FIRE";
                    break;
                case SurvivalToolType.Tape:
                    defaultName = "Duct Tape";
                    description = "Reinforced waterproof tape. Used for emergency wound binding and gear repair when taking damage.";
                    badgeColor = new Color(0.60f, 0.65f, 0.70f);
                    unitWeight = 0.15f;
                    condition = 100;
                    category = "MEDS";
                    break;
                case SurvivalToolType.Gun_Pistol:
                    defaultName = "Tactical Pistol";
                    description = "Reliable semi-automatic 9mm sidearm. Lightweight, fast reload, and accurate at close-to-medium range.";
                    badgeColor = new Color(0.25f, 0.75f, 1.0f);
                    unitWeight = 1.15f;
                    condition = 100;
                    category = "GUNS";
                    break;
                case SurvivalToolType.Gun_SMG:
                    defaultName = "Rapid SMG";
                    description = "Compact submachine gun with high rate of fire. Ideal for clearing tight house corridors and swarming infected.";
                    badgeColor = new Color(1.0f, 0.85f, 0.20f);
                    unitWeight = 2.30f;
                    condition = 100;
                    category = "GUNS";
                    break;
                case SurvivalToolType.Gun_Shotgun:
                    defaultName = "Combat Shotgun";
                    description = "Heavy 12-gauge pump-action shotgun. Fires a devastating buckshot spread at close range.";
                    badgeColor = new Color(1.0f, 0.42f, 0.15f);
                    unitWeight = 3.40f;
                    condition = 100;
                    category = "GUNS";
                    break;
                case SurvivalToolType.Gun_AssaultRifle:
                    defaultName = "Assault Rifle";
                    description = "Military-grade automatic carbine. Balanced stopping power, range, and sustained automatic fire.";
                    badgeColor = new Color(0.30f, 0.95f, 0.45f);
                    unitWeight = 3.60f;
                    condition = 100;
                    category = "GUNS";
                    break;
                case SurvivalToolType.Gun_SniperRifle:
                    defaultName = "Heavy Sniper Rifle";
                    description = "High-caliber long-range precision rifle. Extreme stopping power capable of eliminating threats from afar.";
                    badgeColor = new Color(0.85f, 0.30f, 1.0f);
                    unitWeight = 4.50f;
                    condition = 100;
                    category = "GUNS";
                    break;
                case SurvivalToolType.RustKey:
                    defaultName = "Rust Key (Boat 4 Key)";
                    description = "An old rusted ignition & padlock key. Bring this key to Boat 4 and press [E] to unlock the boat and escape the infected zone.";
                    badgeColor = new Color(0.92f, 0.62f, 0.22f);
                    unitWeight = 0.10f;
                    condition = 100;
                    category = "KEY";
                    break;
                case SurvivalToolType.Melee_Knife:
                    defaultName = "Combat Knife";
                    description = "A razor-sharp tactical combat knife. Silent, lethal, and fast. Devastating for close-quarters infected encounters. Equip via [1/2/3] and strike with [LMB/Q].";
                    badgeColor = new Color(1.0f, 0.40f, 0.30f);
                    unitWeight = 0.35f;
                    condition = 100;
                    category = "MELEE";
                    break;
                case SurvivalToolType.Melee_Crowbar:
                    defaultName = "Steel Crowbar";
                    description = "A heavy forged-steel wrecking bar. Delivers bone-crushing blunt force trauma to infected at close range. Equip via [1/2/3] and smash with [LMB/Q].";
                    badgeColor = new Color(0.35f, 0.75f, 1.0f);
                    unitWeight = 1.60f;
                    condition = 100;
                    category = "MELEE";
                    break;
                case SurvivalToolType.Walkie:
                default:
                    defaultName = "Two-Way Radio";
                    description = "Emergency survivor transceiver. Tuned to the East Boat Dock survivor evacuation broadcast.";
                    badgeColor = new Color(0.30f, 0.75f, 0.45f);
                    unitWeight = 0.40f;
                    condition = 80;
                    category = "MEDS";
                    break;
            }
        }

        private static Texture2D LoadCustomIcon(string resourceSubPath, string relativeFilePath)
        {
            // 1. Try Resources.Load
            Texture2D tex = Resources.Load<Texture2D>(resourceSubPath);
            if (tex != null) return tex;

            // 2. Direct fallback from file on disk
            try
            {
                string fullPath = System.IO.Path.Combine(Application.dataPath, "Resources", relativeFilePath);
                if (System.IO.File.Exists(fullPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                    Texture2D directTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (directTex.LoadImage(bytes))
                    {
                        directTex.name = System.IO.Path.GetFileNameWithoutExtension(relativeFilePath);
                        return directTex;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerInventoryBag] Failed fallback load for icon {relativeFilePath}: {ex.Message}");
            }
            return null;
        }

        private void PreloadItemIcons()
        {
            var types = (SurvivalToolType[])Enum.GetValues(typeof(SurvivalToolType));
            foreach (var t in types)
            {
                // Custom high-res UI icons for weapons
                if (t == SurvivalToolType.Gun_AssaultRifle)
                {
                    Texture2D rifleIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_AssaultRifle", "SurvivalTools/Textures/Icons/Icon_AssaultRifle.png");
                    _itemIcons[t] = rifleIcon != null ? rifleIcon : CreateProceduralGunIcon(t);
                    continue;
                }
                if (t == SurvivalToolType.Gun_Shotgun)
                {
                    Texture2D shotgunIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_Shotgun", "SurvivalTools/Textures/Icons/Icon_Shotgun.png");
                    _itemIcons[t] = shotgunIcon != null ? shotgunIcon : CreateProceduralGunIcon(t);
                    continue;
                }
                if (t == SurvivalToolType.Gun_SniperRifle)
                {
                    Texture2D sniperIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_SniperRifle", "SurvivalTools/Textures/Icons/Icon_SniperRifle.png");
                    _itemIcons[t] = sniperIcon != null ? sniperIcon : CreateProceduralGunIcon(t);
                    continue;
                }
                if (t == SurvivalToolType.Gun_SMG)
                {
                    Texture2D smgIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_SMG", "SurvivalTools/Textures/Icons/Icon_SMG.png");
                    _itemIcons[t] = smgIcon != null ? smgIcon : CreateProceduralGunIcon(t);
                    continue;
                }
                if (t == SurvivalToolType.Gun_Pistol)
                {
                    Texture2D pistolIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_Pistol", "SurvivalTools/Textures/Icons/Icon_Pistol.png");
                    _itemIcons[t] = pistolIcon != null ? pistolIcon : CreateProceduralGunIcon(t);
                    continue;
                }

                // Custom high-res UI icons for survival items
                if (t == SurvivalToolType.FirstAid)
                {
                    Texture2D bandageIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_Bandage", "SurvivalTools/Textures/Icons/Icon_Bandage.png");
                    if (bandageIcon != null)
                    {
                        _itemIcons[t] = bandageIcon;
                        continue;
                    }
                }
                if (t == SurvivalToolType.CannedFood)
                {
                    Texture2D cannedIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_CannedFood", "SurvivalTools/Textures/Icons/Icon_CannedFood.png");
                    if (cannedIcon != null)
                    {
                        _itemIcons[t] = cannedIcon;
                        continue;
                    }
                }

                if (IsGunTool(t))
                {
                    _itemIcons[t] = CreateProceduralGunIcon(t);
                    continue;
                }
                if (t == SurvivalToolType.RustKey)
                {
                    _itemIcons[t] = CreateProceduralKeyIcon();
                    continue;
                }
                if (t == SurvivalToolType.Melee_Knife)
                {
                    Texture2D knifeIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_Knife", "SurvivalTools/Textures/Icons/Icon_Knife.png");
                    _itemIcons[t] = knifeIcon != null ? knifeIcon : CreateProceduralMeleeIcon(t);
                    continue;
                }
                if (t == SurvivalToolType.Melee_Crowbar)
                {
                    Texture2D crowbarIcon = LoadCustomIcon("SurvivalTools/Textures/Icons/Icon_Crowbar", "SurvivalTools/Textures/Icons/Icon_Crowbar.png");
                    _itemIcons[t] = crowbarIcon != null ? crowbarIcon : CreateProceduralMeleeIcon(t);
                    continue;
                }

                string texName = t switch
                {
                    SurvivalToolType.Battery => "survivaltools2_Battery_AlbedoTransparency",
                    SurvivalToolType.CannedFood => "survivaltools2_CannedFood_AlbedoTransparency",
                    SurvivalToolType.FirstAid => "survivaltools2_FirstAidKit_AlbedoTransparency",
                    SurvivalToolType.Flashlight => "survivaltools2_Flashlight_AlbedoTransparency",
                    SurvivalToolType.Matchbox => "survivaltools2_MatchBox_AlbedoTransparency",
                    SurvivalToolType.Pills => "survivaltools2_Pills_AlbedoTransparency",
                    SurvivalToolType.Tape => "survivaltools2_Tape_AlbedoTransparency",
                    SurvivalToolType.Walkie => "survivaltools2_Walkie_AlbedoTransparency",
                    SurvivalToolType.WaterBottle => "survivaltools2_Water_AlbedoTransparency",
                    _ => null
                };

                if (!string.IsNullOrEmpty(texName))
                {
                    Texture2D tex = Resources.Load<Texture2D>("SurvivalTools/Textures/" + texName);
                    if (tex != null)
                    {
                        _itemIcons[t] = tex;
                    }
                }
            }
        }

        private static Texture2D CreateProceduralGunIcon(SurvivalToolType gunToolType)
        {
            int w = 96;
            int h = 96;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            void FillRect(int rx, int ry, int rw, int rh, Color col)
            {
                int x0 = Mathf.Clamp(rx, 0, w - 1);
                int y0 = Mathf.Clamp(ry, 0, h - 1);
                int x1 = Mathf.Clamp(rx + rw, 0, w);
                int y1 = Mathf.Clamp(ry + rh, 0, h);
                for (int py = y0; py < y1; py++)
                {
                    for (int px = x0; px < x1; px++)
                    {
                        pixels[py * w + px] = col;
                    }
                }
            }

            Color bodyCol = new Color(0.82f, 0.86f, 0.90f, 1f);
            Color darkMetal = new Color(0.42f, 0.47f, 0.54f, 1f);
            Color accent;

            switch (gunToolType)
            {
                case SurvivalToolType.Gun_Pistol:
                    accent = new Color(0.25f, 0.75f, 1.0f, 1f);
                    FillRect(24, 52, 46, 13, bodyCol);   // Slide / barrel
                    FillRect(26, 56, 38, 4, accent);     // Slide accent stripe
                    FillRect(24, 30, 15, 23, darkMetal); // Grip
                    FillRect(39, 44, 10, 8, darkMetal);  // Trigger guard
                    FillRect(66, 65, 3, 4, bodyCol);     // Front sight
                    break;

                case SurvivalToolType.Gun_SMG:
                    accent = new Color(1.0f, 0.85f, 0.20f, 1f);
                    FillRect(16, 50, 58, 12, bodyCol);   // Upper receiver
                    FillRect(74, 53, 12, 5, darkMetal);  // Barrel / suppressor tip
                    FillRect(24, 30, 12, 20, darkMetal); // Rear grip
                    FillRect(46, 26, 9, 24, accent);     // Extended SMG magazine
                    FillRect(10, 48, 8, 14, darkMetal);  // Compact stock
                    break;

                case SurvivalToolType.Gun_Shotgun:
                    accent = new Color(1.0f, 0.45f, 0.18f, 1f);
                    FillRect(8, 44, 18, 11, accent);     // Wooden/tactical stock
                    FillRect(26, 48, 30, 11, bodyCol);   // Receiver
                    FillRect(56, 53, 32, 6, bodyCol);    // Long barrel
                    FillRect(52, 46, 22, 6, accent);     // Pump forend
                    FillRect(24, 34, 10, 14, darkMetal); // Grip
                    break;

                case SurvivalToolType.Gun_SniperRifle:
                    accent = new Color(0.85f, 0.35f, 1.0f, 1f);
                    FillRect(6, 44, 18, 12, darkMetal);  // Precision stock
                    FillRect(24, 48, 34, 10, bodyCol);   // Receiver
                    FillRect(58, 51, 34, 5, bodyCol);    // Long heavy barrel
                    FillRect(30, 62, 26, 7, accent);     // Telescopic scope
                    FillRect(36, 58, 4, 4, darkMetal);   // Scope mount L
                    FillRect(48, 58, 4, 4, darkMetal);   // Scope mount R
                    FillRect(38, 36, 12, 12, darkMetal); // Mag
                    break;

                case SurvivalToolType.Gun_AssaultRifle:
                default:
                    accent = new Color(0.35f, 0.95f, 0.48f, 1f);
                    FillRect(8, 44, 16, 13, darkMetal);  // Buttstock
                    FillRect(24, 48, 38, 12, bodyCol);   // Receiver & handguard
                    FillRect(28, 53, 28, 4, accent);     // Tactical rail stripe
                    FillRect(62, 52, 24, 5, bodyCol);    // Barrel
                    FillRect(26, 30, 11, 18, darkMetal); // Pistol grip
                    FillRect(44, 28, 11, 20, accent);    // Curved rifle magazine
                    FillRect(34, 60, 14, 5, darkMetal);  // Optic sight
                    break;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateProceduralKeyIcon()
        {
            int w = 96;
            int h = 96;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            void FillRect(int rx, int ry, int rw, int rh, Color col)
            {
                int x0 = Mathf.Clamp(rx, 0, w - 1);
                int y0 = Mathf.Clamp(ry, 0, h - 1);
                int x1 = Mathf.Clamp(rx + rw, 0, w);
                int y1 = Mathf.Clamp(ry + rh, 0, h);
                for (int py = y0; py < y1; py++)
                    for (int px = x0; px < x1; px++)
                        pixels[py * w + px] = col;
            }

            Color rustGold = new Color(0.88f, 0.58f, 0.22f, 1f);
            Color rustDark = new Color(0.56f, 0.34f, 0.14f, 1f);

            // Key bow (handle ring)
            FillRect(14, 34, 28, 28, rustGold);
            FillRect(20, 40, 16, 16, Color.clear); // Key ring hole
            // Key shaft
            FillRect(42, 44, 40, 8, rustGold);
            FillRect(42, 44, 40, 3, rustDark);
            // Key teeth
            FillRect(64, 32, 6, 12, rustGold);
            FillRect(74, 28, 6, 16, rustGold);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateProceduralMeleeIcon(SurvivalToolType meleeType)
        {
            int w = 96;
            int h = 96;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            void FillRect(int rx, int ry, int rw, int rh, Color col)
            {
                int x0 = Mathf.Clamp(rx, 0, w - 1);
                int y0 = Mathf.Clamp(ry, 0, h - 1);
                int x1 = Mathf.Clamp(rx + rw, 0, w);
                int y1 = Mathf.Clamp(ry + rh, 0, h);
                for (int py = y0; py < y1; py++)
                    for (int px = x0; px < x1; px++)
                        pixels[py * w + px] = col;
            }

            if (meleeType == SurvivalToolType.Melee_Knife)
            {
                Color steel = new Color(0.85f, 0.90f, 0.95f, 1f);
                Color darkSteel = new Color(0.40f, 0.45f, 0.50f, 1f);
                Color handle = new Color(0.20f, 0.22f, 0.24f, 1f);
                Color guard = new Color(0.60f, 0.62f, 0.65f, 1f);

                // Knife Blade (tapered diagonal appearance)
                for (int i = 0; i < 44; i++)
                {
                    int x = 38 + i;
                    int y = 44 + (i / 4);
                    int hgt = 10 - (i / 5);
                    FillRect(x, y, 1, Mathf.Max(2, hgt), steel);
                    FillRect(x, y, 1, 2, darkSteel);
                }
                // Guard
                FillRect(34, 38, 4, 20, guard);
                // Handle
                FillRect(14, 43, 20, 10, handle);
                FillRect(18, 42, 3, 12, guard);
                FillRect(26, 42, 3, 12, guard);
            }
            else // Crowbar
            {
                Color ironBlue = new Color(0.40f, 0.65f, 0.90f, 1f);
                Color darkIron = new Color(0.25f, 0.35f, 0.45f, 1f);

                // Main bar shaft (angled)
                for (int i = 0; i < 55; i++)
                {
                    int x = 20 + i;
                    int y = 25 + (i * 45 / 55);
                    FillRect(x, y, 5, 5, ironBlue);
                    FillRect(x + 1, y, 2, 2, darkIron);
                }
                // Curved hook head
                FillRect(70, 70, 12, 6, ironBlue);
                FillRect(76, 62, 7, 10, ironBlue);
                FillRect(78, 56, 8, 8, darkIron);
                // Prying chisel tip (bottom)
                FillRect(14, 20, 8, 6, ironBlue);
                FillRect(12, 17, 6, 4, darkIron);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }



        public Texture2D GetGunIcon(GunType gunType)
        {
            SurvivalToolType toolType = GetToolTypeForGun(gunType);
            return GetItemIcon(toolType);
        }

        public Texture2D GetItemIcon(SurvivalToolType type)
        {
            if (_itemIcons.TryGetValue(type, out Texture2D tex) && tex != null)
                return tex;
            return null;
        }

        private void InitUITextures()
        {
            _vignetteTex = MakeTex(new Color(0.04f, 0.05f, 0.07f, 0.94f));
            _slotBgTex = MakeTex(new Color(0.08f, 0.09f, 0.11f, 0.88f));
            _slotSelectedTex = MakeTex(new Color(0.18f, 0.22f, 0.27f, 0.96f));
            _slotHoverTex = MakeTex(new Color(0.12f, 0.15f, 0.18f, 0.92f));
            _cardBgTex = MakeTex(new Color(0.06f, 0.07f, 0.09f, 0.90f));
            _btnNormalTex = MakeTex(new Color(0.11f, 0.13f, 0.16f, 0.95f));
            _btnHoverTex = MakeTex(new Color(0.20f, 0.25f, 0.30f, 1.0f));
            _badgeBgTex = MakeTex(new Color(0.08f, 0.10f, 0.12f, 0.88f));
            _accentTex = MakeTex(new Color(0.85f, 0.25f, 0.22f, 1.0f));
        }

        private static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static void DrawBorder(Rect r, Color c, float thickness = 1f)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void PlayBagZipperSound(float baseFreq)
        {
            if (_audioSource == null) return;
            int sampleRate = 22050;
            int samples = sampleRate / 9;
            AudioClip clip = AudioClip.Create("BagSound", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (float)i / samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * (baseFreq + i * 0.12f) * t) * env * 0.18f;
            }
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 0.55f);
        }

        private List<BagSlot> GetFilteredAndSortedItems()
        {
            List<BagSlot> list = new List<BagSlot>();
            for (int i = 0; i < items.Count; i++)
            {
                BagSlot s = items[i];
                if (_categoryFilter == 0) list.Add(s);
                else if (_categoryFilter == 1 && s.category == "MEDS") list.Add(s);
                else if (_categoryFilter == 2 && s.category == "FOOD") list.Add(s);
                else if (_categoryFilter == 3 && (s.category == "GUNS" || s.isGun || IsGunTool(s.itemType))) list.Add(s);
            }

            if (_sortMode == 0) // A-Z
            {
                list.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            }
            else if (_sortMode == 1) // Condition
            {
                list.Sort((a, b) => b.condition.CompareTo(a.condition));
            }
            else if (_sortMode == 2) // Weight
            {
                list.Sort((a, b) => (b.unitWeight * b.count).CompareTo(a.unitWeight * a.count));
            }

            return list;
        }

        private void OnGUI()
        {
            if (Boat4EscapeInteraction.Instance != null && Boat4EscapeInteraction.Instance.isEscaping) return;
            if (GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuOpen) return;

            if (_playerHealth != null && _playerHealth.isDead) return;

            Font regFont = DialogueFontProvider.Regular;
            if (regFont != null) GUI.skin.font = regFont;

            DrawHUDInventoryBadge();
            DrawToastBanner();

            if (_isBagOpen)
            {
                DrawLongDarkBackpackUI();
                DrawQuickSlotsHUD(true);
            }
            else
            {
                DrawQuickSlotsHUD(false);
            }
        }

        private void DrawHUDInventoryBadge()
        {
            float x = 28f;
            float y = Screen.height - 108f;
            float width = 275f;
            float height = 36f;

            if (_badgeBgTex != null)
                GUI.DrawTexture(new Rect(x, y, width, height), _badgeBgTex);

            DrawBorder(new Rect(x, y, width, height), new Color(0.24f, 0.28f, 0.32f, 0.75f), 1f);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = Color.white;

            int total = GetTotalItemCount();
            float totalWt = GetTotalWeight();
            GUI.Label(new Rect(x + 12f, y + 2f, width - 16f, 18f), $"BACKPACK: {total}/{maxCapacity} ({totalWt:F2} KG)", labelStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = HasPlayerDamage() ? new Color(1f, 0.72f, 0.35f) : new Color(0.72f, 0.78f, 0.82f);
            string subText = HasPlayerDamage()
                ? "DAMAGED: Press [H] to Quick-Heal"
                : "Press Tab to open";
            GUI.Label(new Rect(x + 12f, y + 18f, width - 16f, 15f), subText, subStyle);
        }

        private void DrawToastBanner()
        {
            if (_toastTimer <= 0f || string.IsNullOrEmpty(_toastMessage)) return;

            float width = 560f;
            float height = 36f;
            Rect r = new Rect((Screen.width - width) * 0.5f, 48f, width, height);

            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawBorder(r, new Color(0.35f, 0.40f, 0.46f, 0.90f), 1f);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.92f, 0.95f, 0.98f);
            GUI.Label(r, _toastMessage, style);
        }

        /// <summary>
        /// Renders the complete The Long Dark inspired survival Backpack UI.
        /// </summary>
        private void DrawLongDarkBackpackUI()
        {
            // Fullscreen dark moody atmosphere backdrop
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float frameW = Mathf.Min(1180f, Screen.width - 32f);
            float frameH = Mathf.Min(680f, Screen.height - 32f);
            float originX = (Screen.width - frameW) * 0.5f;
            float originY = (Screen.height - frameH) * 0.5f;

            // 1. Top Header Bar
            DrawTopHeaderBar(originX, originY, frameW);

            // 2. Left Category Rail (ALL, MEDS, FOOD, GEAR, FIRE)
            float railX = originX + 16f;
            float railY = originY + 82f;
            float railW = 44f;
            DrawCategoryRail(railX, railY, railW);

            // 3. Center 5x4 Slots Grid
            float gridX = originX + 76f;
            float gridY = originY + 82f;
            float gridW = 560f;
            List<BagSlot> filtered = GetFilteredAndSortedItems();
            DrawItemGrid(gridX, gridY, gridW, filtered);

            // 4. Right Item Detail & Inspection Panel
            float panelX = originX + 665f;
            float panelY = originY + 82f;
            float panelW = frameW - 685f;
            DrawItemDetailPanel(panelX, panelY, panelW, filtered);
        }

        private void DrawTopHeaderBar(float x, float y, float w)
        {
            // Title: BACKPACK
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 20f, y + 12f, 220f, 28f), "BACKPACK", titleStyle);

            // Weight Line: 14.20 / 35.00 (+5.00) KG
            float totalWt = GetTotalWeight();
            bool overWeight = totalWt > maxWeightKg;
            string wtColor = overWeight ? "#e74c3c" : "#ffffff";
            GUIStyle weightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            weightStyle.normal.textColor = overWeight ? new Color(0.95f, 0.35f, 0.30f) : new Color(0.85f, 0.88f, 0.92f);
            GUI.Label(new Rect(x + 20f, y + 42f, 320f, 18f),
                $"{totalWt:F2} / {maxWeightKg:F2} (+{bonusWeightKg:F2}) KG", weightStyle);

            // Top Header Tab (BACKPACK only — removed STATUS, CLOTHING, FIRE, JOURNAL, MAP)
            float tw = 110f;
            float tx = x + (w - tw) * 0.5f;
            GUIStyle tabStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            tabStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(tx, y + 20f, tw, 22f), "BACKPACK", tabStyle);

            Color prevC = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(tx + 8f, y + 46f, tw - 16f, 2f), Texture2D.whiteTexture);
            GUI.color = prevC;

            // Close hint in top-right
            GUIStyle closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight
            };
            closeStyle.normal.textColor = new Color(0.65f, 0.70f, 0.75f);
            GUI.Label(new Rect(x + w - 160f, y + 20f, 140f, 24f), "[B / TAB / ESC] CLOSE", closeStyle);

            // Subtle divider line
            Color prev = GUI.color;
            GUI.color = new Color(0.20f, 0.24f, 0.28f, 0.60f);
            GUI.DrawTexture(new Rect(x + 16f, y + 68f, w - 32f, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void DrawCategoryRail(float x, float y, float w)
        {
            string[] catLabels = { "ALL", "MED", "FOOD", "GUNS" };
            float btnH = 46f;
            float gap = 8f;

            for (int i = 0; i < catLabels.Length; i++)
            {
                Rect r = new Rect(x, y + i * (btnH + gap), w, btnH);
                bool isActive = (_categoryFilter == i);
                bool isHover = r.Contains(Event.current.mousePosition);

                GUI.color = isActive ? new Color(0.22f, 0.26f, 0.32f, 0.95f) : (isHover ? new Color(0.14f, 0.17f, 0.20f, 0.90f) : new Color(0.08f, 0.09f, 0.11f, 0.85f));
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;

                DrawBorder(r, isActive ? Color.white : new Color(0.24f, 0.28f, 0.32f, 0.75f), isActive ? 2f : 1f);

                GUIStyle lblStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                lblStyle.normal.textColor = isActive ? Color.white : new Color(0.65f, 0.70f, 0.75f);
                GUI.Label(r, catLabels[i], lblStyle);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    _categoryFilter = i;
                    _selectedSlotIndex = 0;
                    PlayBagZipperSound(440f);
                }
            }
        }

        private void DrawItemGrid(float x, float y, float w, List<BagSlot> filtered)
        {
            // Header above grid: Category label & category weight
            string[] catNames = { "ALL", "FIRST AID", "FOOD & DRINK", "WEAPONS & GUNS" };
            string activeCatName = catNames[Mathf.Clamp(_categoryFilter, 0, catNames.Length - 1)];

            GUIStyle catTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            catTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y - 24f, 240f, 22f), activeCatName, catTitleStyle);

            float catWeight = 0f;
            for (int i = 0; i < filtered.Count; i++) catWeight += filtered[i].unitWeight * filtered[i].count;

            GUIStyle catWeightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight
            };
            catWeightStyle.normal.textColor = new Color(0.70f, 0.75f, 0.80f);
            GUI.Label(new Rect(x + w - 160f, y - 24f, 160f, 22f), $"{catWeight:F2} KG", catWeightStyle);

            // 5 Columns x 4 Rows = 20 Slots Grid
            int cols = 5;
            int rows = 4;
            float slotW = (w - (cols - 1) * 8f) / cols;
            float slotH = slotW; // Square slots

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int index = row * cols + col;
                    Rect slotRect = new Rect(x + col * (slotW + 8f), y + row * (slotH + 8f), slotW, slotH);

                    if (index < filtered.Count)
                    {
                        // Occupied Slot
                        BagSlot slot = filtered[index];
                        bool isSelected = (index == _selectedSlotIndex);
                        bool isHover = slotRect.Contains(Event.current.mousePosition);

                        // Background
                        GUI.color = isSelected
                            ? new Color(0.18f, 0.22f, 0.27f, 0.95f)
                            : (isHover ? new Color(0.12f, 0.15f, 0.18f, 0.90f) : new Color(0.08f, 0.09f, 0.11f, 0.85f));
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;

                        // Border: 2px glowing white border if selected, subtle grey if normal
                        DrawBorder(slotRect, isSelected ? Color.white : new Color(0.24f, 0.28f, 0.32f, 0.80f), isSelected ? 2f : 1f);

                        bool isGunSlot = slot.isGun || IsGunTool(slot.itemType);
                        bool isEquippedGun = isGunSlot && PlayerWeaponManager.Instance != null &&
                                             PlayerWeaponManager.Instance.HasGunEquipped &&
                                             PlayerWeaponManager.Instance.equippedGunType == slot.gunType;

                        // Top-left: quantity badge if > 1, or EQUIPPED badge for guns
                        if (isEquippedGun)
                        {
                            GUIStyle eqStyle = new GUIStyle(GUI.skin.label)
                            {
                                fontSize = 9,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.UpperLeft
                            };
                            eqStyle.normal.textColor = new Color(0.35f, 1.0f, 0.55f);
                            GUI.Label(new Rect(slotRect.x + 6f, slotRect.y + 4f, 65f, 16f), "EQUIPPED", eqStyle);
                        }
                        else if (slot.count > 1)
                        {
                            GUIStyle countStyle = new GUIStyle(GUI.skin.label)
                            {
                                fontSize = 11,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.UpperLeft
                            };
                            countStyle.normal.textColor = Color.white;
                            GUI.Label(new Rect(slotRect.x + 6f, slotRect.y + 4f, 40f, 16f), $"x{slot.count}", countStyle);
                        }

                        // Center: Thumbnail of item
                        Texture2D icon = GetItemIcon(slot.itemType);
                        Rect iconRect = new Rect(slotRect.x + (slotW - 54f) * 0.5f, slotRect.y + (slotH - 54f) * 0.5f - 4f, 54f, 54f);
                        if (icon != null)
                        {
                            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                        }
                        else
                        {
                            // Fallback colored badge
                            Color prev = GUI.color;
                            GUI.color = slot.badgeColor;
                            GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
                            GUI.color = prev;
                        }

                        // Bottom-left: Ammo count for guns, or Condition rating (★ 100%)
                        GUIStyle condStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 9,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.LowerLeft
                        };
                        condStyle.normal.textColor = isGunSlot ? new Color(1.0f, 0.85f, 0.35f) : new Color(0.75f, 0.80f, 0.85f);
                        string bottomLeftText = isGunSlot ? $"{slot.currentAmmo}/{slot.reserveAmmo}" : $"★{slot.condition}%";
                        GUI.Label(new Rect(slotRect.x + 6f, slotRect.yMax - 18f, 52f, 15f), bottomLeftText, condStyle);

                        // Bottom-right: Slot weight
                        GUIStyle wtStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 9,
                            alignment = TextAnchor.LowerRight
                        };
                        wtStyle.normal.textColor = new Color(0.65f, 0.70f, 0.75f);
                        GUI.Label(new Rect(slotRect.xMax - 52f, slotRect.yMax - 18f, 46f, 15f), $"{(slot.unitWeight * slot.count):F2}k", wtStyle);

                        // Click to select
                        if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none))
                        {
                            _selectedSlotIndex = index;
                            PlayBagZipperSound(500f);
                        }
                    }
                    else
                    {
                        // Empty Slot (faint dark placeholder frame)
                        GUI.color = new Color(0.06f, 0.07f, 0.08f, 0.60f);
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        DrawBorder(slotRect, new Color(0.16f, 0.18f, 0.20f, 0.50f), 1f);
                    }
                }
            }

            // Bottom Sorting Controls
            float sortY = y + rows * (slotH + 8f) + 6f;
            GUIStyle sortLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            sortLabelStyle.normal.textColor = new Color(0.55f, 0.60f, 0.65f);
            GUI.Label(new Rect(x, sortY, 50f, 22f), "SORT:", sortLabelStyle);

            string[] sortLabels = { "A-Z", "★ COND", "KG WT" };
            float sx = x + 50f;
            for (int s = 0; s < sortLabels.Length; s++)
            {
                Rect sr = new Rect(sx + s * 72f, sortY, 66f, 22f);
                bool isSortActive = (_sortMode == s);

                GUI.color = isSortActive ? new Color(0.20f, 0.24f, 0.28f, 0.90f) : new Color(0.09f, 0.10f, 0.12f, 0.80f);
                GUI.DrawTexture(sr, Texture2D.whiteTexture);
                GUI.color = Color.white;
                DrawBorder(sr, isSortActive ? Color.white : new Color(0.22f, 0.26f, 0.30f, 0.70f), 1f);

                GUIStyle sStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                sStyle.normal.textColor = isSortActive ? Color.white : new Color(0.65f, 0.70f, 0.75f);
                GUI.Label(sr, sortLabels[s], sStyle);

                if (GUI.Button(sr, GUIContent.none, GUIStyle.none))
                {
                    _sortMode = s;
                    PlayBagZipperSound(480f);
                }
            }
        }

        private void DrawItemDetailPanel(float x, float y, float w, List<BagSlot> filtered)
        {
            if (filtered.Count == 0)
            {
                // Empty Bag state
                GUIStyle emptyTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
                emptyTitleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y + 20f, w, 28f), "BACKPACK IS EMPTY", emptyTitleStyle);

                GUIStyle emptyDescStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                emptyDescStyle.normal.textColor = new Color(0.70f, 0.75f, 0.80f);
                GUI.Label(new Rect(x, y + 60f, w - 20f, 120f),
                    "You have not scavenged any items in this category yet.\n\nExplore abandoned houses and docks, walk up to Meds, Foods, and Guns in the world, and press [E] to loot them into your backpack.", emptyDescStyle);

                // Back button at bottom right
                DrawBackButton(x + w - 110f, y + 420f);
                return;
            }

            int selIdx = Mathf.Clamp(_selectedSlotIndex, 0, filtered.Count - 1);
            BagSlot slot = filtered[selIdx];

            // 1. Item Name Title (Bold clean white capital letters)
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            nameStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, w, 28f), slot.displayName.ToUpper(), nameStyle);

            // 2. Item Description
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            descStyle.normal.textColor = new Color(0.78f, 0.83f, 0.88f);
            GUI.Label(new Rect(x, y + 34f, w - 16f, 48f), slot.description, descStyle);

            // 3. Large Center Preview Card
            float cardY = y + 86f;
            float cardH = 210f;
            Rect cardRect = new Rect(x, cardY, w - 16f, cardH);

            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.90f);
            GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawBorder(cardRect, new Color(0.20f, 0.24f, 0.28f, 0.85f), 1f);

            // Large Item Render / Texture
            Texture2D largeIcon = GetItemIcon(slot.itemType);
            float padX = 14f;
            float padY = 14f;
            Rect largeIconRect = new Rect(cardRect.x + padX, cardRect.y + padY, cardRect.width - (padX * 2f), cardH - (padY * 2f));
            if (largeIcon != null)
            {
                GUI.DrawTexture(largeIconRect, largeIcon, ScaleMode.ScaleToFit);
            }
            else
            {
                Color prev = GUI.color;
                GUI.color = slot.badgeColor;
                GUI.DrawTexture(largeIconRect, Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Category badge pill on card
            GUIStyle pillStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            pillStyle.normal.textColor = new Color(0.65f, 0.70f, 0.75f);
            GUI.Label(new Rect(cardRect.xMax - 110f, cardRect.y + 8f, 100f, 18f), slot.category, pillStyle);

            // 4. Stats Strip (Count/Ammo, Condition/Damage, Weight)
            float statsY = cardY + cardH + 12f;
            Rect statsRect = new Rect(x, statsY, w - 16f, 36f);

            GUI.color = new Color(0.08f, 0.10f, 0.12f, 0.88f);
            GUI.DrawTexture(statsRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawBorder(statsRect, new Color(0.22f, 0.26f, 0.30f, 0.75f), 1f);

            GUIStyle statColStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            statColStyle.normal.textColor = Color.white;

            bool isGunSlot = slot.isGun || IsGunTool(slot.itemType);
            bool isEquippedGun = isGunSlot && PlayerWeaponManager.Instance != null &&
                                 PlayerWeaponManager.Instance.HasGunEquipped &&
                                 PlayerWeaponManager.Instance.equippedGunType == slot.gunType;

            float colW = (w - 16f) / 3f;
            if (isGunSlot)
            {
                GUI.Label(new Rect(x, statsY + 8f, colW, 20f), $"AMMO: {slot.currentAmmo}/{slot.reserveAmmo}", statColStyle);
                GUI.Label(new Rect(x + colW, statsY + 8f, colW, 20f), $"DMG: {slot.damage}  (★{slot.condition}%)", statColStyle);
                GUI.Label(new Rect(x + colW * 2f, statsY + 8f, colW, 20f), $"{slot.unitWeight:F2} KG", statColStyle);
            }
            else
            {
                GUI.Label(new Rect(x, statsY + 8f, colW, 20f), $"● {slot.count} IN BAG", statColStyle);
                GUI.Label(new Rect(x + colW, statsY + 8f, colW, 20f), $"★ {slot.condition}% COND", statColStyle);
                GUI.Label(new Rect(x + colW * 2f, statsY + 8f, colW, 20f), $"{slot.unitWeight:F2} KG (TOT: {(slot.unitWeight * slot.count):F2})", statColStyle);
            }

            // 5. Treatment / Usage Status Indicator
            float statusY = statsY + 44f;
            bool isHealing = IsHealingTool(slot.itemType) || slot.itemType == SurvivalToolType.Tape;
            bool damaged = HasPlayerDamage();

            string statusText;
            Color statusColor;
            if (isGunSlot)
            {
                if (isEquippedGun)
                {
                    statusText = "● CURRENTLY EQUIPPED IN HANDS — Ready for combat";
                    statusColor = new Color(0.25f, 0.90f, 0.48f);
                }
                else
                {
                    statusText = "● STORED IN BACKPACK — Press [E] to Equip Weapon";
                    statusColor = new Color(0.40f, 0.78f, 1.0f);
                }
            }
            else if (isHealing)
            {
                if (damaged)
                {
                    float hp = (_playerHealth != null) ? _playerHealth.currentHealth : 100f;
                    float maxHp = (_playerHealth != null) ? _playerHealth.maxHealth : 100f;
                    statusText = $"● READY TO HEAL (Current HP: {hp:F0}/{maxHp:F0})";
                    statusColor = new Color(0.25f, 0.85f, 0.45f);
                }
                else
                {
                    statusText = "● FULL HEALTH (100/100) — Bandage not needed yet";
                    statusColor = new Color(0.95f, 0.65f, 0.25f);
                }
            }
            else if (slot.itemType == SurvivalToolType.RustKey)
            {
                statusText = "● BOAT 4 ESCAPE KEY — Walk up to Boat 4 and press [E] to Escape";
                statusColor = new Color(0.95f, 0.72f, 0.28f);
            }
            else
            {
                statusText = "● READY TO ACTIVATE / TOGGLE";
                statusColor = new Color(0.40f, 0.75f, 0.95f);
            }

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            statusStyle.normal.textColor = statusColor;
            GUI.Label(new Rect(x, statusY, w - 16f, 20f), statusText, statusStyle);

            // 6. Action Buttons: [ USE / EQUIP ] and [ DROP ]
            float btnY = statusY + 28f;
            float btnW = (w - 32f) * 0.5f;
            float btnH = 42f;

            // USE / EQUIP Button
            Rect useRect = new Rect(x, btnY, btnW, btnH);
            bool useDisabled = !isGunSlot && isHealing && !damaged;
            bool useHover = useRect.Contains(Event.current.mousePosition) && !useDisabled;

            GUI.color = useDisabled
                ? new Color(0.12f, 0.13f, 0.15f, 0.70f)
                : (useHover ? new Color(0.22f, 0.32f, 0.26f, 0.95f) : new Color(0.12f, 0.18f, 0.15f, 0.90f));
            GUI.DrawTexture(useRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawBorder(useRect, useDisabled ? new Color(0.22f, 0.24f, 0.26f, 0.50f) : (useHover ? Color.white : new Color(0.30f, 0.60f, 0.40f, 0.85f)), 1f);

            string useLabel = isGunSlot
                ? (isEquippedGun ? "EQUIPPED [E]" : "EQUIP [E]")
                : (useDisabled ? "FULL HP" : "USE [E]");
            GUIStyle useStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            useStyle.normal.textColor = useDisabled ? new Color(0.50f, 0.54f, 0.58f) : Color.white;
            GUI.Label(useRect, useLabel, useStyle);

            if (GUI.Button(useRect, GUIContent.none, GUIStyle.none) && !useDisabled)
            {
                UseSelectedItem();
            }

            // DROP Button
            Rect dropRect = new Rect(x + btnW + 16f, btnY, btnW, btnH);
            bool dropHover = dropRect.Contains(Event.current.mousePosition);

            GUI.color = dropHover ? new Color(0.24f, 0.20f, 0.20f, 0.95f) : new Color(0.14f, 0.12f, 0.12f, 0.90f);
            GUI.DrawTexture(dropRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawBorder(dropRect, dropHover ? Color.white : new Color(0.40f, 0.28f, 0.28f, 0.85f), 1f);

            GUIStyle dropStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            dropStyle.normal.textColor = Color.white;
            GUI.Label(dropRect, "DROP [Q]", dropStyle);

            if (GUI.Button(dropRect, GUIContent.none, GUIStyle.none))
            {
                DropSelectedItem();
            }

            // 7. Quick Slots Assignment Row (Hotbar [1], [2], [3])
            float quickY = btnY + btnH + 12f;
            float qSlotW = (w - 32f - 16f) / 3f;
            float qSlotH = 32f;

            GUIStyle qHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            qHeaderStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            GUI.Label(new Rect(x, quickY - 18f, w - 16f, 18f), "PUT IN QUICK SLOT (HOTBAR):", qHeaderStyle);

            for (int q = 0; q < 3; q++)
            {
                Rect qRect = new Rect(x + q * (qSlotW + 8f), quickY, qSlotW, qSlotH);
                bool isHover = qRect.Contains(Event.current.mousePosition);
                bool alreadyInSlot = quickSlots != null && q < quickSlots.Length && quickSlots[q] != null && quickSlots[q].isAssigned &&
                    (slot.isGun ? (quickSlots[q].isGun && quickSlots[q].gunType == slot.gunType) : (!quickSlots[q].isGun && quickSlots[q].itemType == slot.itemType));

                GUI.color = alreadyInSlot
                    ? new Color(0.16f, 0.28f, 0.20f, 0.95f)
                    : (isHover ? new Color(0.20f, 0.25f, 0.30f, 0.95f) : new Color(0.10f, 0.12f, 0.15f, 0.88f));
                GUI.DrawTexture(qRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                DrawBorder(qRect, alreadyInSlot ? new Color(0.35f, 1.0f, 0.55f, 0.90f) : (isHover ? Color.white : new Color(0.25f, 0.30f, 0.36f, 0.75f)), 1f);

                GUIStyle qBtnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                qBtnStyle.normal.textColor = alreadyInSlot ? new Color(0.35f, 1.0f, 0.55f) : (isHover ? Color.white : new Color(0.75f, 0.80f, 0.85f));
                string qBtnLabel = alreadyInSlot ? $"SLOT {q + 1}  ✓" : $"+ SLOT {q + 1}  [{q + 1}]";
                GUI.Label(qRect, qBtnLabel, qBtnStyle);

                if (GUI.Button(qRect, GUIContent.none, GUIStyle.none))
                {
                    AssignItemToQuickSlot(q, slot);
                }
            }

            // Back button placed right below
            DrawBackButton(x + w - 110f, quickY + qSlotH + 10f);
        }

        private void DrawBackButton(float bx, float by)
        {
            Rect r = new Rect(bx, by, 94f, 30f);
            bool isHover = r.Contains(Event.current.mousePosition);

            GUI.color = isHover ? new Color(0.20f, 0.24f, 0.28f, 0.95f) : new Color(0.10f, 0.12f, 0.14f, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawBorder(r, isHover ? Color.white : new Color(0.24f, 0.28f, 0.32f, 0.70f), 1f);

            GUIStyle s = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            s.normal.textColor = isHover ? Color.white : new Color(0.70f, 0.75f, 0.80f);
            GUI.Label(r, "BACK", s);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                ToggleBag(false);
            }
        }

        private void DrawQuickSlotsHUD(bool isInsideBagView)
        {
            // Exact styling matching the reference image:
            // 3 square slots sitting side-by-side right at the bottom of the screen,
            // with thick dark charcoal/bronze frames, subtle inner bevels, and translucent dark glass centers.
            float slotSize = 68f;
            float gap = 5f;
            float totalW = 3 * slotSize + 2 * gap; // 214f
            float startX = (Screen.width - totalW) * 0.5f;
            float startY = Screen.height - slotSize - 14f;

            if (isInsideBagView)
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = new Color(0.80f, 0.85f, 0.90f);
                GUI.Label(new Rect(startX - 120f, startY - 24f, totalW + 240f, 20f),
                    "QUICK SLOTS  [1]  [2]  [3]  —  Click slot or press [1]-[3] to assign", titleStyle);
            }

            for (int i = 0; i < 3; i++)
            {
                Rect slotRect = new Rect(startX + i * (slotSize + gap), startY, slotSize, slotSize);
                QuickSlot qs = quickSlots != null && i < quickSlots.Length ? quickSlots[i] : null;
                BagSlot item = GetItemForQuickSlot(i);

                bool isAssigned = qs != null && qs.isAssigned;
                bool isEquippedGun = isAssigned && qs.isGun && PlayerWeaponManager.Instance != null &&
                                     PlayerWeaponManager.Instance.HasGunEquipped &&
                                     PlayerWeaponManager.Instance.equippedGunType == qs.gunType;
                bool isHover = slotRect.Contains(Event.current.mousePosition);

                // Draw the reference slot frame (translucent glass center + thick dark beveled frame)
                DrawReferenceSlotFrame(slotRect, isEquippedGun, isHover);

                // Slot number badge in top-left "1", "2", "3"
                GUIStyle numStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
                numStyle.normal.textColor = isEquippedGun
                    ? new Color(0.45f, 1.0f, 0.65f, 0.95f)
                    : (isAssigned ? new Color(1.0f, 0.88f, 0.45f, 0.85f) : new Color(0.65f, 0.70f, 0.75f, 0.45f));
                GUI.Label(new Rect(slotRect.x + 5f, slotRect.y + 4f, 20f, 16f), $"{i + 1}", numStyle);

                if (isAssigned)
                {
                    // Item Icon / Thumbnail
                    SurvivalToolType toolType = (item != null) ? item.itemType : qs.itemType;
                    Texture2D icon = GetItemIcon(toolType);
                    Rect iconRect = new Rect(slotRect.x + 10f, slotRect.y + 10f, slotSize - 20f, slotSize - 20f);

                    if (icon != null)
                    {
                        if (item == null && !isEquippedGun)
                        {
                            Color prevC = GUI.color;
                            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                            GUI.color = prevC;
                        }
                        else
                        {
                            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                        }
                    }
                    else
                    {
                        Color prevC = GUI.color;
                        GUI.color = (item != null) ? item.badgeColor : new Color(0.35f, 0.45f, 0.55f, 0.75f);
                        GUI.DrawTexture(new Rect(slotRect.x + 16f, slotRect.y + 16f, slotSize - 32f, slotSize - 32f), Texture2D.whiteTexture);
                        GUI.color = prevC;
                    }

                    // Bottom-Right / Status Badge
                    if (isEquippedGun)
                    {
                        GUIStyle eqStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 8,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.LowerRight
                        };
                        eqStyle.normal.textColor = new Color(0.35f, 1.0f, 0.55f);
                        GUI.Label(new Rect(slotRect.xMax - 48f, slotRect.yMax - 18f, 44f, 15f), "HELD", eqStyle);
                    }
                    else if (item != null && item.count > 1)
                    {
                        GUIStyle countStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 10,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.LowerRight
                        };
                        countStyle.normal.textColor = Color.white;
                        // Drop shadow
                        GUIStyle shadow = new GUIStyle(countStyle);
                        shadow.normal.textColor = Color.black;
                        GUI.Label(new Rect(slotRect.xMax - 40f + 1f, slotRect.yMax - 18f + 1f, 36f, 15f), $"x{item.count}", shadow);
                        GUI.Label(new Rect(slotRect.xMax - 40f, slotRect.yMax - 18f, 36f, 15f), $"x{item.count}", countStyle);
                    }
                    else if (item != null && (item.isGun || IsGunTool(item.itemType)))
                    {
                        GUIStyle ammoStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 8,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.LowerRight
                        };
                        ammoStyle.normal.textColor = new Color(1.0f, 0.85f, 0.35f);
                        GUIStyle shadow = new GUIStyle(ammoStyle);
                        shadow.normal.textColor = Color.black;
                        GUI.Label(new Rect(slotRect.xMax - 48f + 1f, slotRect.yMax - 17f + 1f, 44f, 14f), $"{item.currentAmmo}/{item.reserveAmmo}", shadow);
                        GUI.Label(new Rect(slotRect.xMax - 48f, slotRect.yMax - 17f, 44f, 14f), $"{item.currentAmmo}/{item.reserveAmmo}", ammoStyle);
                    }
                    else if (item == null && !isEquippedGun)
                    {
                        GUIStyle emptyCntStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 8,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.LowerCenter
                        };
                        emptyCntStyle.normal.textColor = new Color(0.85f, 0.35f, 0.35f, 0.85f);
                        GUI.Label(new Rect(slotRect.x, slotRect.yMax - 16f, slotSize, 14f), "0", emptyCntStyle);
                    }

                    // Clear button "×" when inside bag view
                    if (isInsideBagView)
                    {
                        Rect clearRect = new Rect(slotRect.xMax - 16f, slotRect.y + 3f, 13f, 13f);
                        bool isClearHover = clearRect.Contains(Event.current.mousePosition);
                        GUIStyle clearStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 11,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        };
                        clearStyle.normal.textColor = isClearHover ? Color.red : new Color(0.75f, 0.75f, 0.75f, 0.85f);
                        GUI.Label(clearRect, "×", clearStyle);

                        if (Event.current.type == EventType.MouseDown && clearRect.Contains(Event.current.mousePosition))
                        {
                            ClearQuickSlot(i);
                            Event.current.Use();
                        }
                    }
                }

                // Click slot handling
                if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none))
                {
                    if (isInsideBagView)
                    {
                        List<BagSlot> filtered = GetFilteredAndSortedItems();
                        if (filtered.Count > 0 && _selectedSlotIndex >= 0 && _selectedSlotIndex < filtered.Count)
                        {
                            AssignItemToQuickSlot(i, filtered[_selectedSlotIndex]);
                        }
                        else
                        {
                            ShowToast("SELECT AN ITEM IN YOUR BACKPACK FIRST TO ASSIGN IT HERE!");
                        }
                    }
                    else
                    {
                        UseQuickSlot(i);
                    }
                }
            }
        }

        private static void DrawReferenceSlotFrame(Rect r, bool isEquipped, bool isHover)
        {
            // 1. Translucent glass fill (transparent so the 3D world is visible behind it, exactly like the reference image)
            Color fillCol = isEquipped
                ? new Color(0.10f, 0.22f, 0.15f, 0.65f)
                : (isHover ? new Color(0.14f, 0.15f, 0.17f, 0.65f) : new Color(0.05f, 0.06f, 0.07f, 0.48f));

            GUI.color = fillCol;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 2. Thick dark outer border (3px) matching the reference image's heavy frame
            Color outerBorder = isEquipped
                ? new Color(0.35f, 0.95f, 0.55f, 0.95f)
                : (isHover ? new Color(0.48f, 0.50f, 0.54f, 0.95f) : new Color(0.12f, 0.12f, 0.13f, 0.96f));
            DrawBorder(r, outerBorder, 3f);

            // 3. Subtle inner bevel (1px inset)
            Rect inner = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
            Color topHighlight = isEquipped
                ? new Color(0.55f, 1.0f, 0.70f, 0.60f)
                : (isHover ? new Color(0.35f, 0.38f, 0.42f, 0.75f) : new Color(0.24f, 0.25f, 0.27f, 0.70f));
            Color bottomShadow = new Color(0.04f, 0.04f, 0.05f, 0.85f);

            // Inner top & left
            GUI.color = topHighlight;
            GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(inner.x, inner.y, 1f, inner.height), Texture2D.whiteTexture);

            // Inner bottom & right
            GUI.color = bottomShadow;
            GUI.DrawTexture(new Rect(inner.x, inner.yMax - 1f, inner.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(inner.xMax - 1f, inner.y, 1f, inner.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}
