using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Regional Zombie Spawner:
    /// - Placed across houses, forests, towns, docks, and wilderness areas of the map.
    /// - Maintains an active horde of at least 15 zombies in its designated zone.
    /// - Strictly enforces the Player Spawn Safe Zone (never spawns zombies where the player starts).
    /// - Respawns defeated zombies over time to keep every sector dangerous and alive.
    /// </summary>
    public class ZombieSpawner : MonoBehaviour
    {
        [Header("Zone Identity")]
        [Tooltip("Descriptive name of the house, forest, or landmark location")]
        public string zoneName = "Map Zone";

        [Header("Zombie Prefab")]
        public GameObject zombiePrefab;

        [Header("Player & Safety Settings")]
        [Tooltip("Target player transform (auto-detected if null)")]
        public Transform targetPlayer;

        [Tooltip("The designated player spawn coordinate")]
        public Vector3 playerSpawnPoint = new Vector3(479f, 19f, 148f);

        [Tooltip("Radius around player spawn point where NO zombies are ever allowed to spawn")]
        public float playerSpawnSafeRadius = 25f;

        [Tooltip("Minimum real-time distance from the player to prevent pop-in ambushes")]
        public float minPlayerDistance = 10f;

        [Tooltip("Minimum distance between zombies to prevent spawning overlapping")]
        public float minDistanceBetweenZombies = 3.5f;

        [Header("Horde & Spawn Settings")]
        [Tooltip("Target number of zombies maintained by this spawner (at least 15)")]
        public int targetZombies = 15;

        [Tooltip("Initial number of zombies to spawn at start (at least 15)")]
        public int initialZombies = 15;

        [Tooltip("Radius around this spawner to distribute zombies")]
        public float spawnRadius = 38f;

        [Tooltip("Minimum ground Y to prevent spawning in water / flooded basins (WaterSurfaceY = 16.0f)")]
        public float minGroundHeight = 16.12f;

        [Tooltip("Seconds between spawn checks to replenish fallen zombies")]
        public float spawnCheckInterval = 5f;

        [Header("Runtime Info")]
        [SerializeField] private int currentZombieCount = 0;

        private readonly List<GameObject> _activeZombies = new List<GameObject>();
        private float _nextSpawnCheckTime;

        private void Start()
        {
            if (playerSpawnSafeRadius > 30f) playerSpawnSafeRadius = 25f;
            if (minPlayerDistance > 12f) minPlayerDistance = 10f;
            // Enforce strict dry-land spawn threshold above the 16.0f water surface
            if (minGroundHeight < 16.12f) minGroundHeight = 16.12f;

            // Auto-detect player if unassigned
            if (targetPlayer == null)
            {
                var pGo = GameObject.FindWithTag("Player");
                if (pGo == null) pGo = GameObject.Find("PlayerCapsule");
                if (pGo != null)
                {
                    targetPlayer = pGo.transform;
                }
            }

            // Safety Check: if this spawner itself is accidentally positioned inside the safe zone, warn & disable
            float distToSpawn = Vector3.Distance(transform.position, playerSpawnPoint);
            if (distToSpawn < playerSpawnSafeRadius)
            {
                Debug.LogWarning($"[ZombieSpawner '{zoneName}'] Disabled because it is located inside the Player Spawn Safe Zone ({distToSpawn:F1}m < {playerSpawnSafeRadius}m)!");
                enabled = false;
                return;
            }

            // Spawn initial horde of zombies (at least 15)
            int needed = initialZombies - _activeZombies.Count;
            for (int i = 0; i < needed; i++)
            {
                SpawnZombieInZone();
            }

            currentZombieCount = _activeZombies.Count;
            _nextSpawnCheckTime = Time.time + spawnCheckInterval;
        }

        private void Update()
        {
            // Clean up destroyed / null zombie references
            _activeZombies.RemoveAll(z => z == null);

            // Clean up any zombies that fell into the void
            for (int i = _activeZombies.Count - 1; i >= 0; i--)
            {
                var z = _activeZombies[i];
                if (z != null && z.transform.position.y < -15f)
                {
                    Destroy(z);
                    _activeZombies.RemoveAt(i);
                }
            }

            currentZombieCount = _activeZombies.Count;

            // Replenish fallen zombies to maintain at least targetZombies (15)
            if (Time.time >= _nextSpawnCheckTime)
            {
                _nextSpawnCheckTime = Time.time + spawnCheckInterval;

                if (currentZombieCount < targetZombies)
                {
                    SpawnZombieInZone();
                }
            }
        }

        /// <summary>
        /// Spawns a zombie at a valid ground location within this spawner's house/forest radius.
        /// Strictly obeys Player Spawn Safe Zone and minimum player distance.
        /// </summary>
        public GameObject SpawnZombieInZone()
        {
            if (zombiePrefab == null)
            {
                zombiePrefab = Resources.Load<GameObject>("Prefabs/Zombie_Enemy");
                if (zombiePrefab == null)
                {
                    zombiePrefab = Resources.Load<GameObject>("Zombie_Enemy");
                }
#if UNITY_EDITOR
                if (zombiePrefab == null)
                {
                    zombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieMale_AAB/Prefabs/URP/Zombie_Enemy.prefab");
                }
#endif
                if (zombiePrefab == null)
                {
                    Debug.LogError($"[ZombieSpawner '{zoneName}'] Unable to locate Zombie_Enemy prefab!");
                    return null;
                }
            }

            Vector3 playerPos = targetPlayer != null ? targetPlayer.position : playerSpawnPoint;

            // Up to 24 attempts to find a solid dry ground position outside water
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 randCircle = Random.insideUnitCircle * spawnRadius;
                Vector3 candidateXZ = new Vector3(transform.position.x + randCircle.x, 0, transform.position.z + randCircle.y);

                // 1. STRICT CHECK: Must be outside the player's initial spawn safe zone!
                float distToSpawnPoint = Vector2.Distance(new Vector2(candidateXZ.x, candidateXZ.z),
                                                         new Vector2(playerSpawnPoint.x, playerSpawnPoint.z));
                if (distToSpawnPoint < playerSpawnSafeRadius)
                {
                    continue; // Skip: inside Player Spawn Safe Zone
                }

                // 2. REAL-TIME CHECK: Must not pop-in right in the player's face
                float distToPlayer = Vector2.Distance(new Vector2(candidateXZ.x, candidateXZ.z),
                                                     new Vector2(playerPos.x, playerPos.z));
                if (distToPlayer < minPlayerDistance)
                {
                    continue; // Skip: too close to current player location
                }

                // 3. SPACING CHECK: Don't spawn right on top of another active zombie
                bool tooClose = false;
                foreach (var active in _activeZombies)
                {
                    if (active != null)
                    {
                        float dist = Vector2.Distance(new Vector2(candidateXZ.x, candidateXZ.z),
                                                      new Vector2(active.transform.position.x, active.transform.position.z));
                        if (dist < minDistanceBetweenZombies)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
                if (tooClose) continue;

                // 4. RAYCAST DOWN: Find solid ground, bridge, pier or roadway surface (ignoring Water layer 4)
                Vector3 rayOrigin = new Vector3(candidateXZ.x, 200f, candidateXZ.z);
                int groundMask = ~((1 << 2) | (1 << 4) | (1 << 8)); // Ignore Raycast (2), Water (4) & Player (8)

                float spawnGroundY = -999f;
                float normalY = 1.0f;

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 300f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    spawnGroundY = hit.point.y;
                    normalY = hit.normal.y;
                }

                if (Terrain.activeTerrains != null)
                {
                    foreach (var terr in Terrain.activeTerrains)
                    {
                        if (terr == null || terr.terrainData == null) continue;
                        Vector3 tPos = terr.transform.position;
                        Vector3 tSize = terr.terrainData.size;
                        if (candidateXZ.x >= tPos.x && candidateXZ.x <= tPos.x + tSize.x &&
                            candidateXZ.z >= tPos.z && candidateXZ.z <= tPos.z + tSize.z)
                        {
                            float tY = terr.SampleHeight(candidateXZ) + tPos.y;
                            if (tY > spawnGroundY)
                            {
                                spawnGroundY = tY;
                                normalY = 1.0f;
                            }
                        }
                    }
                }

                // Must be above water level and walkable slope
                if (spawnGroundY >= minGroundHeight && normalY >= 0.5f)
                {
                    Vector3 groundPos = new Vector3(candidateXZ.x, spawnGroundY + 0.05f, candidateXZ.z);

                    // Ambient yaw facing outward or random
                    float randomYaw = Random.Range(0f, 360f);
                    Quaternion spawnRot = Quaternion.Euler(0f, randomYaw, 0f);

                    GameObject zombie = Instantiate(zombiePrefab, groundPos, spawnRot);
                    zombie.name = $"Zombie_{zoneName}_{_activeZombies.Count + 1}";

                    var ai = zombie.GetComponent<ZombieAI>();
                    if (ai != null && targetPlayer != null)
                    {
                        ai.targetPlayer = targetPlayer;
                    }

                    _activeZombies.Add(zombie);
                    currentZombieCount = _activeZombies.Count;

                    return zombie;
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw this spawner's zone radius in Cyan / Magenta
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 10f);

            // Draw Player Spawn Safe Zone in Bright Green
            Gizmos.color = new Color(0.1f, 1.0f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(playerSpawnPoint, playerSpawnSafeRadius);
        }

        private void OnDrawGizmos()
        {
            // Subtle icon marker in editor
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
            Gizmos.DrawSphere(transform.position, 1.2f);
        }
    }
}
