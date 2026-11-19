using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Spawns random weapons inside houses, cabins, villas, and on furniture (tables, beds, desks).
    /// Manages world loot density, respawns collected weapons, and provides visual loot beacons.
    /// </summary>
    public class HouseGunSpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct GunSpawnData
        {
            public string gunName;
            public GunType gunType;
            public GameObject gunPrefab;
            [Range(1, 100)]
            public int spawnWeight; // Higher = more common
        }

        [Header("Weapon Prefabs & Rarity")]
        public List<GunSpawnData> availableGuns = new List<GunSpawnData>();

        [Header("Spawn Settings")]
        [Tooltip("Target number of guns present in houses across the world")]
        public int targetGunCount = 14;
        [Tooltip("Minimum distance between spawned guns")]
        public float minDistanceBetweenGuns = 8.0f;
        [Tooltip("Respawn check interval in seconds")]
        public float respawnCheckInterval = 30f;
        [Tooltip("Whether to automatically respawn guns when looted")]
        public bool autoRespawn = true;

        [Header("Curated House Interior Locations")]
        [Tooltip("Specific house/cabin/villa interior coordinates across Flooded Grounds")]
        public List<Vector3> houseInteriorPoints = new List<Vector3>();

        // Runtime Tracking
        private readonly List<GunPickup> _activePickups = new List<GunPickup>();
        private float _nextCheckTime;

        private void Awake()
        {
            EnsurePlayerWeaponManager();

            // Seed curated house interior locations if empty
            if (houseInteriorPoints == null || houseInteriorPoints.Count == 0)
            {
                InitDefaultHousePoints();
            }

            // Load gun prefabs from Resources if list is empty
            if (availableGuns == null || availableGuns.Count == 0)
            {
                LoadGunsFromResources();
            }
        }

        public void EnsurePlayerWeaponManager()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("PlayerCapsule");
            if (player != null)
            {
                int pLayer = LayerMask.NameToLayer("Player");
                if (pLayer >= 0) player.layer = pLayer;

                var pwm = player.GetComponent<PlayerWeaponManager>();
                if (pwm == null)
                {
                    pwm = player.AddComponent<PlayerWeaponManager>();
                    pwm.LocateWeaponHoldPoint();
                    Debug.Log("[HouseGunSpawner] Attached PlayerWeaponManager to PlayerCapsule.");
                }
            }
        }

        public void LoadGunsFromResources()
        {
            availableGuns = new List<GunSpawnData>();

            LoadOrRegisterGun("Tactical Pistol", GunType.Pistol, "Guns/Pistol_Pickup", 35);
            LoadOrRegisterGun("Rapid SMG", GunType.SMG, "Guns/SMG_Pickup", 25);
            LoadOrRegisterGun("Combat Shotgun", GunType.Shotgun, "Guns/Shotgun_Pickup", 20);
            LoadOrRegisterGun("Assault Rifle", GunType.AssaultRifle, "Guns/AssaultRifle_Pickup", 15);
            LoadOrRegisterGun("Heavy Sniper Rifle", GunType.SniperRifle, "Guns/SniperRifle_Pickup", 5);

            Debug.Log($"[HouseGunSpawner] Loaded {availableGuns.Count} gun types from Resources/Guns.");
        }

        private void LoadOrRegisterGun(string name, GunType type, string resPath, int weight)
        {
            GameObject prefab = Resources.Load<GameObject>(resPath);
            if (prefab != null)
            {
                availableGuns.Add(new GunSpawnData { gunName = name, gunType = type, gunPrefab = prefab, spawnWeight = weight });
            }
        }

        private void Start()
        {
            // Auto-detect furniture and house floors in the scene to augment spawn points
            ScanSceneForHouseLocations();

            // Initial population if not already pre-spawned
            CleanupActiveList();
            if (_activePickups.Count < targetGunCount)
            {
                PopulateWorld();
            }

            PopulateHouseMedsAndFoods();
            PopulateHouseMeleeWeapons();

            _nextCheckTime = Time.time + respawnCheckInterval;
        }

        private void PopulateHouseMeleeWeapons()
        {
            if (houseInteriorPoints == null || houseInteriorPoints.Count == 0) return;

            SurvivalToolType[] meleePool =
            {
                SurvivalToolType.Melee_Knife,
                SurvivalToolType.Melee_Crowbar
            };

            int mask = ~((1 << 2) | (1 << 8));
            int spawnedMelee = 0;

            for (int i = 0; i < houseInteriorPoints.Count; i++)
            {
                // Scatter knives and crowbars on tables, counters, and rooms across houses
                if (i % 3 != 0) continue;

                SurvivalToolType chosenMelee = meleePool[(i / 3) % meleePool.Length];
                Vector3 offset = new Vector3(
                    Mathf.Sin(i * 2.3f) * 0.70f,
                    0f,
                    Mathf.Cos(i * 2.3f) * 0.70f);
                Vector3 spawnPos = houseInteriorPoints[i] + offset;

                Vector3 rayOrigin = spawnPos + Vector3.up * 1.5f;
                RaycastHit[] hits = Physics.RaycastAll(new Ray(rayOrigin, Vector3.down), 5.0f, mask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool snapped = false;
                foreach (var h in hits)
                {
                    if (h.collider.isTrigger) continue;
                    if (h.collider.GetComponentInParent<GunPickup>() != null) continue;
                    if (h.collider.GetComponentInParent<SurvivalItemPickup>() != null) continue;
                    if (Vector3.Dot(h.normal, Vector3.up) > 0.40f)
                    {
                        spawnPos = h.point + Vector3.up * 0.03f;
                        snapped = true;
                        break;
                    }
                }
                if (!snapped)
                {
                    spawnPos += Vector3.up * 0.03f;
                }

                Quaternion rot = Quaternion.Euler(0f, (i * 73f) % 360f, (chosenMelee == SurvivalToolType.Melee_Knife ? 0f : 10f));
                PlayerInventoryBag.SpawnSurvivalToolInWorld(chosenMelee, spawnPos, rot);
                spawnedMelee++;
            }

            Debug.Log($"[HouseGunSpawner] Populated houses with {spawnedMelee} Melee Weapons (Combat Knives & Steel Crowbars) alongside Guns & Supplies.");
        }

        private void PopulateHouseMedsAndFoods()
        {
            if (houseInteriorPoints == null || houseInteriorPoints.Count == 0) return;

            SurvivalToolType[] supplyPool =
            {
                SurvivalToolType.FirstAid,
                SurvivalToolType.Pills,
                SurvivalToolType.CannedFood,
                SurvivalToolType.WaterBottle
            };

            int mask = ~((1 << 2) | (1 << 8));
            int spawnedSupplies = 0;

            for (int i = 0; i < houseInteriorPoints.Count; i++)
            {
                // Spawn a med or food item near every second house interior point
                if (i % 2 != 0 && i > 3) continue;

                SurvivalToolType chosenType = supplyPool[i % supplyPool.Length];
                Vector3 offset = new Vector3(
                    Mathf.Cos(i * 1.9f) * 0.65f,
                    0f,
                    Mathf.Sin(i * 1.9f) * 0.65f);
                Vector3 spawnPos = houseInteriorPoints[i] + offset;

                Vector3 rayOrigin = spawnPos + Vector3.up * 1.5f;
                RaycastHit[] hits = Physics.RaycastAll(new Ray(rayOrigin, Vector3.down), 5.0f, mask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool snapped = false;
                foreach (var h in hits)
                {
                    if (h.collider.isTrigger) continue;
                    if (h.collider.GetComponentInParent<GunPickup>() != null) continue;
                    if (h.collider.GetComponentInParent<SurvivalItemPickup>() != null) continue;
                    if (Vector3.Dot(h.normal, Vector3.up) > 0.40f)
                    {
                        spawnPos = h.point + Vector3.up * 0.04f;
                        snapped = true;
                        break;
                    }
                }
                if (!snapped)
                {
                    spawnPos += Vector3.up * 0.04f;
                }

                PlayerInventoryBag.SpawnSurvivalToolInWorld(chosenType, spawnPos, Quaternion.Euler(0f, (i * 53f) % 360f, 0f));
                spawnedSupplies++;
            }

            Debug.Log($"[HouseGunSpawner] Populated houses with {spawnedSupplies} Meds & Foods alongside Guns.");
        }

        private void Update()
        {
            if (!autoRespawn) return;

            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + respawnCheckInterval;
                CleanupActiveList();

                if (_activePickups.Count < targetGunCount)
                {
                    int needed = targetGunCount - _activePickups.Count;
                    for (int i = 0; i < needed; i++)
                    {
                        SpawnRandomGunInRandomHouse();
                    }
                }
            }
        }

        public void PopulateWorld()
        {
            CleanupActiveList();
            int needed = targetGunCount - _activePickups.Count;
            int attempts = 0;

            while (needed > 0 && attempts < targetGunCount * 3)
            {
                attempts++;
                if (SpawnRandomGunInRandomHouse())
                {
                    needed--;
                }
            }

            Debug.Log($"[HouseGunSpawner] Populated world with {_activePickups.Count} guns inside houses.");
        }

        public bool SpawnRandomGunInRandomHouse()
        {
            if (houseInteriorPoints == null || houseInteriorPoints.Count == 0) return false;
            if (availableGuns == null || availableGuns.Count == 0) return false;

            // Pick a candidate house point that is not too close to existing guns
            List<Vector3> candidatePoints = new List<Vector3>(houseInteriorPoints);
            // Shuffle
            for (int i = candidatePoints.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                var temp = candidatePoints[i];
                candidatePoints[i] = candidatePoints[r];
                candidatePoints[r] = temp;
            }

            Vector3 chosenPoint = Vector3.zero;
            bool foundValid = false;

            foreach (var pt in candidatePoints)
            {
                bool tooClose = false;
                foreach (var active in _activePickups)
                {
                    if (active != null && Vector3.Distance(pt, active.transform.position) < minDistanceBetweenGuns)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    chosenPoint = pt;
                    foundValid = true;
                    break;
                }
            }

            // Fallback: pick any random house point
            if (!foundValid && candidatePoints.Count > 0)
            {
                chosenPoint = candidatePoints[Random.Range(0, candidatePoints.Count)];
            }

            // Raycast down to find exact table/bed/floor height ignoring other loot items
            Vector3 rayStart = chosenPoint + Vector3.up * 1.5f;
            int mask = ~((1 << 2) | (1 << 5) | (1 << 8)); // Ignore Raycast, UI, Player
            RaycastHit[] hits = Physics.RaycastAll(new Ray(rayStart, Vector3.down), 6.0f, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool floorFound = false;
            foreach (var h in hits)
            {
                if (h.collider.isTrigger) continue;
                if (h.collider.GetComponentInParent<GunPickup>() != null) continue;
                if (h.collider.GetComponentInParent<SurvivalItemPickup>() != null) continue;
                if (Vector3.Dot(h.normal, Vector3.up) > 0.40f)
                {
                    chosenPoint = h.point + Vector3.up * 0.02f;
                    floorFound = true;
                    break;
                }
            }
            if (!floorFound)
            {
                chosenPoint += Vector3.up * 0.02f;
            }

            // Select random gun by weight
            GunSpawnData chosenGun = SelectGunByWeight();
            if (chosenGun.gunPrefab == null) return false;

            // Instantiate gun
            GameObject spawnedGo = Instantiate(chosenGun.gunPrefab, chosenPoint, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            spawnedGo.name = $"Gun_{chosenGun.gunName}_{Random.Range(100, 999)}";

            var pickup = spawnedGo.GetComponent<GunPickup>();
            if (pickup != null)
            {
                _activePickups.Add(pickup);
            }

            return true;
        }

        private GunSpawnData SelectGunByWeight()
        {
            int totalWeight = 0;
            foreach (var g in availableGuns)
            {
                totalWeight += Mathf.Max(1, g.spawnWeight);
            }

            int roll = Random.Range(0, totalWeight);
            int current = 0;
            foreach (var g in availableGuns)
            {
                current += Mathf.Max(1, g.spawnWeight);
                if (roll < current)
                {
                    return g;
                }
            }

            return availableGuns[0];
        }

        private void CleanupActiveList()
        {
            _activePickups.RemoveAll(item => item == null);
        }

        public void ScanSceneForHouseLocations()
        {
            // Find all tables, beds, cabinets, and house floor centers in the scene
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int addedCount = 0;

            foreach (var t in allTransforms)
            {
                string name = t.name;

                // Priority: Indoor Furniture (Tables, Beds, Sofas, Cabinets)
                if (name.Contains("Prop_SmallTable") || name.Contains("Prop_LargeTable") ||
                    name.Contains("Prop_Bed") || name.Contains("Prop_Cabinet") || name.Contains("Prop_Sofa"))
                {
                    Vector3 pos = t.position + Vector3.up * 0.45f;
                    if (!IsPositionAlreadyTracked(pos, 3.0f))
                    {
                        houseInteriorPoints.Add(pos);
                        addedCount++;
                    }
                }
                // Secondary: House/Cabin/Villa/Brickhouse floor centers
                else if (name.Contains("Villa") && name.Contains("Floor") && !name.Contains("DM"))
                {
                    Vector3 pos = t.position + Vector3.up * 0.4f;
                    if (!IsPositionAlreadyTracked(pos, 5.0f))
                    {
                        houseInteriorPoints.Add(pos);
                        addedCount++;
                    }
                }
                else if (name.Contains("Cabin") && (name.Contains("Mid") || name.Contains("End")))
                {
                    Vector3 pos = t.position + Vector3.up * 0.4f;
                    if (!IsPositionAlreadyTracked(pos, 6.0f))
                    {
                        houseInteriorPoints.Add(pos);
                        addedCount++;
                    }
                }
                else if (name.Contains("BrickHouse") && (name.Contains("Mid") || name.Contains("End")))
                {
                    Vector3 pos = t.position + Vector3.up * 0.4f;
                    if (!IsPositionAlreadyTracked(pos, 6.0f))
                    {
                        houseInteriorPoints.Add(pos);
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                Debug.Log($"[HouseGunSpawner] Auto-discovered {addedCount} house & furniture locations in the scene! Total house points: {houseInteriorPoints.Count}");
            }
        }

        private bool IsPositionAlreadyTracked(Vector3 pos, float threshold)
        {
            foreach (var p in houseInteriorPoints)
            {
                if (Vector3.Distance(pos, p) < threshold) return true;
            }
            return false;
        }

        public void InitDefaultHousePoints()
        {
            houseInteriorPoints = new List<Vector3>()
            {
                // 1. Starting Outpost / Shacks near Player (479, 19, 148)
                new Vector3(484.5f, 19.25f, 155.2f),  // Covered bridge shelter floor
                new Vector3(472.4f, 19.36f, 142.5f),  // Beside wooden guard hut porch floor
                new Vector3(531.3f, 17.2f, 173.1f),  // Outhouse / storage shack

                // 2. Barn Compound (East of bridge)
                new Vector3(559.4f, 29.5f, 90.8f),   // Barn 2 interior ground floor
                new Vector3(564.2f, 29.5f, 92.5f),   // Barn 2 interior workbench
                new Vector3(568.0f, 29.5f, 94.0f),   // Barn 2 rear loft

                // 3. Riverside Cabins
                new Vector3(561.5f, 17.5f, 271.0f),  // Cabin 1 front porch table
                new Vector3(563.8f, 18.2f, 276.5f),  // Cabin 1 bedroom
                new Vector3(500.5f, 21.5f, 365.0f),  // Cabin 1 table room
                new Vector3(400.0f, 17.5f, 293.0f),  // Cabin 2 interior
                new Vector3(385.0f, 17.5f, 293.0f),  // Cabin 2 back room
                new Vector3(415.0f, 17.2f, 478.0f),  // Cabin small table (Prop_SmallTable_B)
                new Vector3(445.0f, 17.8f, 514.0f),  // Cabin 2 doorway table

                // 4. Brick Houses
                new Vector3(371.5f, 17.6f, 265.0f),  // BrickHouse ground floor
                new Vector3(371.5f, 17.6f, 271.5f),  // BrickHouse corner
                new Vector3(450.5f, 19.5f, 435.0f),  // BrickHouse 2 living room
                new Vector3(453.0f, 24.5f, 439.0f),  // BrickHouse 2 upper floor

                // 5. Grand Villas (Villa 1 & Villa 2)
                new Vector3(516.5f, 26.8f, 533.5f),  // Villa 2 master bedroom (Prop_Bed_A)
                new Vector3(524.0f, 21.0f, 523.5f),  // Villa 2 main hall floor
                new Vector3(556.0f, 25.0f, 442.0f),  // Villa 1 floor mid
                new Vector3(558.5f, 25.0f, 445.0f),  // Villa 1 side room
                new Vector3(416.1f, 18.0f, 477.8f),  // House Small Table (Prop_SmallTable_B 2)

                // 6. Industrial Depot / LightHouse
                new Vector3(250.0f, 26.0f, 430.0f),  // Industrial building 1 office floor
                new Vector3(255.0f, 26.0f, 440.0f),  // Industrial building 1 storage
                new Vector3(245.0f, 36.0f, 435.0f),  // Industrial building 1 upper catwalk
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.7f);
            if (houseInteriorPoints != null)
            {
                foreach (var pt in houseInteriorPoints)
                {
                    Gizmos.DrawWireSphere(pt, 0.6f);
                    Gizmos.DrawLine(pt, pt + Vector3.up * 1.5f);
                }
            }
        }
    }
}
