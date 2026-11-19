using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StarterAssets;

[InitializeOnLoad]
public static class SetupHouseGuns
{
    private const string GunsPrefabDir = "Assets/Prefabs/Guns";

    static SetupHouseGuns()
    {
        EditorApplication.delayCall += Execute;
    }

    [MenuItem("Tools/Setup Guns In Houses (URP, Prefabs & Spawning)")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("====================================================");
        Debug.Log("=== [SetupHouseGuns] CONFIGURING WEAPONS & HOUSES ===");
        Debug.Log("====================================================");

        // 1. Convert all Low Poly Guns materials from Standard/Built-in to URP Lit
        ConvertAllGunMaterialsToURP();

        // 2. Create Gun Prefabs with GunPickup, visuals, and stats
        var prefabs = CreateOrUpdateGunPrefabs();

        // 3. Configure PlayerCapsule with PlayerWeaponManager & weapon hold point
        ConfigurePlayerInScene();

        // 4. Setup HouseGunSpawner in active scene and place initial random guns in houses
        SetupSceneHouseSpawner(prefabs);

        // 5. Save all assets and scene
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupHouseGuns] Scene and assets saved successfully!");
        }

        Debug.Log("====================================================");
        Debug.Log("=== [SetupHouseGuns] SETUP COMPLETE! READY TO PLAY ===");
        Debug.Log("====================================================");
    }

    public static void ConvertAllGunMaterialsToURP()
    {
        var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            Debug.LogWarning("[SetupHouseGuns] 'Universal Render Pipeline/Lit' shader not found!");
            return;
        }

        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Low Poly Guns" });
        int fixedCount = 0;

        foreach (var guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string dir = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);

            mat.shader = urpLitShader;

            // Find diffuse texture
            string diffusePath = Path.Combine(dir, $"{baseName}_diffuse.png").Replace("\\", "/");
            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuseTex != null)
            {
                mat.SetTexture("_BaseMap", diffuseTex);
                mat.SetTexture("_MainTex", diffuseTex);
                mat.SetColor("_BaseColor", Color.white);
            }

            // Find normal map texture
            string normalPath = Path.Combine(dir, $"{baseName}_normal.png").Replace("\\", "/");
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Tactical gun metallic finish
            mat.SetFloat("_Metallic", 0.65f);
            mat.SetFloat("_Smoothness", 0.45f);

            EditorUtility.SetDirty(mat);
            fixedCount++;
        }

        Debug.Log($"[SetupHouseGuns] Converted {fixedCount} gun materials to URP Lit shader.");
    }

    public static Dictionary<GunType, GameObject> CreateOrUpdateGunPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(GunsPrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Guns");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Guns"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Guns");
        }

        var results = new Dictionary<GunType, GameObject>();

        // 1. Pistol (pistol1) - Realistic Person Scale ~22cm
        results[GunType.Pistol] = BuildGunPrefab(
            "Pistol_Pickup",
            "Tactical Pistol",
            GunType.Pistol,
            "Assets/Low Poly Guns/Models/Guns/pistol1/pistol1.fbx",
            damage: 35,
            fireRate: 0.22f,
            isAuto: false,
            mag: 15,
            reserve: 60,
            reloadTime: 1.5f,
            range: 65f,
            pellets: 1,
            spread: 0.4f,
            modelScale: 0.15f
        );

        // 2. Assault Rifle (assault1) - Realistic Person Scale ~80cm
        results[GunType.AssaultRifle] = BuildGunPrefab(
            "AssaultRifle_Pickup",
            "Assault Rifle",
            GunType.AssaultRifle,
            "Assets/Low Poly Guns/Models/Guns/assault1/assault1.fbx",
            damage: 45,
            fireRate: 0.11f,
            isAuto: true,
            mag: 30,
            reserve: 90,
            reloadTime: 2.0f,
            range: 85f,
            pellets: 1,
            spread: 0.6f,
            modelScale: 0.16f
        );

        // 3. Shotgun (shotgun1) - Realistic Person Scale ~90cm
        results[GunType.Shotgun] = BuildGunPrefab(
            "Shotgun_Pickup",
            "Combat Shotgun",
            GunType.Shotgun,
            "Assets/Low Poly Guns/Models/Guns/shotgun1/shotgun1.fbx",
            damage: 22, // 22 * 8 = 176 max dmg
            fireRate: 0.65f,
            isAuto: false,
            mag: 8,
            reserve: 32,
            reloadTime: 2.5f,
            range: 45f,
            pellets: 8,
            spread: 2.8f,
            modelScale: 0.15f
        );

        // 4. SMG (smg1) - Realistic Person Scale ~48cm
        results[GunType.SMG] = BuildGunPrefab(
            "SMG_Pickup",
            "Rapid SMG",
            GunType.SMG,
            "Assets/Low Poly Guns/Models/Guns/smg1/smg1.fbx",
            damage: 28,
            fireRate: 0.08f,
            isAuto: true,
            mag: 40,
            reserve: 120,
            reloadTime: 1.6f,
            range: 55f,
            pellets: 1,
            spread: 0.9f,
            modelScale: 0.16f
        );

        // 5. Sniper Rifle (sniper1) - Realistic Person Scale ~115cm
        results[GunType.SniperRifle] = BuildGunPrefab(
            "SniperRifle_Pickup",
            "Heavy Sniper Rifle",
            GunType.SniperRifle,
            "Assets/Low Poly Guns/Models/Guns/sniper1/sniper1.fbx",
            damage: 140, // One-shot kill on 60hp zombie!
            fireRate: 1.15f,
            isAuto: false,
            mag: 5,
            reserve: 20,
            reloadTime: 2.8f,
            range: 150f,
            pellets: 1,
            spread: 0.1f,
            modelScale: 0.13f
        );

        return results;
    }

    private static GameObject BuildGunPrefab(
        string prefabName,
        string gunName,
        GunType type,
        string fbxPath,
        int damage,
        float fireRate,
        bool isAuto,
        int mag,
        int reserve,
        float reloadTime,
        float range,
        int pellets,
        float spread,
        float modelScale)
    {
        string savePath = $"{GunsPrefabDir}/{prefabName}.prefab";
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError($"[SetupHouseGuns] FBX model not found at {fbxPath}!");
            return null;
        }

        // Root pickup GameObject
        GameObject root = new GameObject(prefabName);

        // Child model instance - lays flat horizontally on the ground/table
        GameObject model = UnityEngine.Object.Instantiate(fbxAsset, root.transform);
        model.name = "Model";
        model.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // Laid flat on side
        model.transform.localScale = Vector3.one * modelScale;

        // BoxCollider trigger for interaction
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.1f, 0.5f, 1.1f);
        box.center = new Vector3(0f, 0.2f, 0f);

        // Ground-level subtle illumination
        GameObject lightGo = new GameObject("LootLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.06f;
        var pLight = lightGo.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.range = 0.9f;
        pLight.intensity = 0.25f;
        pLight.color = GunPickup.GetGunColor(type);

        // GunPickup component (no floating, no bobbing - rests directly on ground)
        var pickup = root.AddComponent<GunPickup>();
        pickup.gunName = gunName;
        pickup.gunType = type;
        pickup.damage = damage;
        pickup.fireRate = fireRate;
        pickup.isAutomatic = isAuto;
        pickup.magazineCapacity = mag;
        pickup.currentAmmo = mag;
        pickup.reserveAmmo = reserve;
        pickup.reloadTime = reloadTime;
        pickup.range = range;
        pickup.pelletsPerShot = pellets;
        pickup.spreadAngle = spread;
        pickup.rotationSpeed = 0f;
        pickup.bobHeight = 0f;
        pickup.bobFrequency = 0f;
        pickup.lootGlow = pLight;
        pickup.weaponModelPrefab = fbxAsset;

        // Save as Prefab in Prefabs and Resources
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        string resPath = $"Assets/Resources/Guns/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, resPath);
        UnityEngine.Object.DestroyImmediate(root);

        Debug.Log($"[SetupHouseGuns] Created gun prefab '{prefabName}' at {savePath} and {resPath}");
        return savedPrefab;
    }

    public static void ConfigurePlayerInScene()
    {
        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogWarning("[SetupHouseGuns] PlayerCapsule not found in scene!");
            return;
        }

        Undo.RecordObject(player, "Configure Player Weapon Manager");

        // Set Player layer
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            player.layer = playerLayer;
        }

        var weaponMgr = player.GetComponent<PlayerWeaponManager>();
        if (weaponMgr == null)
        {
            weaponMgr = Undo.AddComponent<PlayerWeaponManager>(player);
            Debug.Log("[SetupHouseGuns] Added PlayerWeaponManager to PlayerCapsule.");
        }

        // Auto-locate weapon hold point on Right_Hand
        weaponMgr.LocateWeaponHoldPoint();
        if (weaponMgr.weaponHoldPoint != null)
        {
            Debug.Log($"[SetupHouseGuns] WeaponHoldPoint linked to: {weaponMgr.weaponHoldPoint.name}");
        }
        else
        {
            Debug.LogWarning("[SetupHouseGuns] WeaponHoldPoint could not be resolved automatically!");
        }
    }

    public static void SetupSceneHouseSpawner(Dictionary<GunType, GameObject> prefabs)
    {
        var spawnerGo = GameObject.Find("HouseGunSpawner");
        if (spawnerGo == null)
        {
            spawnerGo = new GameObject("HouseGunSpawner");
            Undo.RegisterCreatedObjectUndo(spawnerGo, "Create HouseGunSpawner");
        }

        var spawner = spawnerGo.GetComponent<HouseGunSpawner>();
        if (spawner == null) spawner = Undo.AddComponent<HouseGunSpawner>(spawnerGo);

        spawner.targetGunCount = 14;
        spawner.minDistanceBetweenGuns = 9.0f;
        spawner.respawnCheckInterval = 30f;
        spawner.autoRespawn = true;

        // Configure available guns with rarity weights
        spawner.availableGuns = new List<HouseGunSpawner.GunSpawnData>()
        {
            new HouseGunSpawner.GunSpawnData { gunName = "Tactical Pistol", gunType = GunType.Pistol, gunPrefab = prefabs.ContainsKey(GunType.Pistol) ? prefabs[GunType.Pistol] : null, spawnWeight = 35 },
            new HouseGunSpawner.GunSpawnData { gunName = "Rapid SMG", gunType = GunType.SMG, gunPrefab = prefabs.ContainsKey(GunType.SMG) ? prefabs[GunType.SMG] : null, spawnWeight = 25 },
            new HouseGunSpawner.GunSpawnData { gunName = "Combat Shotgun", gunType = GunType.Shotgun, gunPrefab = prefabs.ContainsKey(GunType.Shotgun) ? prefabs[GunType.Shotgun] : null, spawnWeight = 20 },
            new HouseGunSpawner.GunSpawnData { gunName = "Assault Rifle", gunType = GunType.AssaultRifle, gunPrefab = prefabs.ContainsKey(GunType.AssaultRifle) ? prefabs[GunType.AssaultRifle] : null, spawnWeight = 15 },
            new HouseGunSpawner.GunSpawnData { gunName = "Heavy Sniper Rifle", gunType = GunType.SniperRifle, gunPrefab = prefabs.ContainsKey(GunType.SniperRifle) ? prefabs[GunType.SniperRifle] : null, spawnWeight = 5 },
        };

        // Initialize default and discovered house points
        spawner.InitDefaultHousePoints();
        spawner.ScanSceneForHouseLocations();

        // Clear existing spawned guns in scene before re-populating
        var existingGuns = UnityEngine.Object.FindObjectsByType<GunPickup>(FindObjectsSortMode.None);
        foreach (var eg in existingGuns)
        {
            Undo.DestroyObjectImmediate(eg.gameObject);
        }

        // Pre-place guns across houses
        List<GunType> spawnSequence = new List<GunType>()
        {
            GunType.AssaultRifle,
            GunType.Pistol,
            GunType.Shotgun,
            GunType.SMG,
            GunType.SniperRifle,
            GunType.Pistol,
            GunType.AssaultRifle,
            GunType.Shotgun,
            GunType.SMG,
            GunType.Pistol,
            GunType.AssaultRifle,
            GunType.Shotgun,
            GunType.SniperRifle,
            GunType.SMG
        };

        int placed = 0;
        int ptIndex = 0;

        foreach (var gType in spawnSequence)
        {
            if (ptIndex >= spawner.houseInteriorPoints.Count) break;
            if (!prefabs.ContainsKey(gType) || prefabs[gType] == null) continue;

            Vector3 pt = spawner.houseInteriorPoints[ptIndex++];

            // Raycast down to find ground/table surface, ignoring loot triggers & other items
            Vector3 rayStart = pt + Vector3.up * 1.5f;
            int mask = ~((1 << 2) | (1 << 5) | (1 << 8));
            Vector3 spawnPos = pt;
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
                    spawnPos = h.point + Vector3.up * 0.02f;
                    floorFound = true;
                    break;
                }
            }
            if (!floorFound)
            {
                spawnPos += Vector3.up * 0.02f;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[gType]);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Gun");
            instance.name = $"Lootable_{gType}_{placed + 1}";
            instance.transform.position = spawnPos;
            instance.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            placed++;
            Debug.Log($"[SetupHouseGuns] Pre-placed {instance.name} in house at {spawnPos}");
        }

        Debug.Log($"[SetupHouseGuns] Pre-placed {placed} guns in houses and configured HouseGunSpawner!");
    }
}
