using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StarterAssets;

namespace IgnitedEditor
{
    [InitializeOnLoad]
    public static class SetupMeleeWeaponsInWorld
    {
        private const string SessionKey = "MeleeWeapons_Setup_v1";

        static SetupMeleeWeaponsInWorld()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(SessionKey, false)) return;
                SessionState.SetBool(SessionKey, true);
                ExecuteSetup(false);
            };
        }

        [MenuItem("Tools/Setup Melee Weapons In World (Knife & Crowbar)")]
        public static void MenuExecute()
        {
            ExecuteSetup(true);
        }

        public static void ExecuteSetup(bool verbose)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit == null) return;

            // 1. Upgrade Materials
            UpgradeKnifeMaterial(urpLit);
            UpgradeCrowbarMaterial(urpLit);

            // 2. Generate UI Icons
            GenerateMeleeIcons();

            // 3. Create or Update Prefabs in Assets/Resources/SurvivalTools/Prefabs/
            GameObject knifePrefab = CreateOrUpdateKnifePrefab(urpLit);
            GameObject crowbarPrefab = CreateOrUpdateCrowbarPrefab(urpLit);

            // 4. Scatter Knives and Crowbars in the Active Scene
            ScatterMeleeInScene(knifePrefab, crowbarPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (verbose)
            {
                Debug.Log("[SetupMeleeWeaponsInWorld] Successfully scattered Knife & Crowbar across the world, configured URP materials, UI icons, and pickable melee behaviors!");
            }
        }

        private static void UpgradeKnifeMaterial(Shader urpLit)
        {
            string matPath = "Assets/iDos Games/Stylized Knife/Materials/Knife.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return;

            mat.shader = urpLit;
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/iDos Games/Stylized Knife/Textures/Knife_BaseColor.png");
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Smoothness", 0.50f);
            mat.SetFloat("_Metallic", 0.25f);
            EditorUtility.SetDirty(mat);
        }

        private static void UpgradeCrowbarMaterial(Shader urpLit)
        {
            string matPath = "Assets/Crowbar/Materials/M_Crowbar.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return;

            mat.shader = urpLit;
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowbar/Textures/T_Crowbar_Albedo.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowbar/Textures/T_Crowbar_Normal.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowbar/Textures/T_Crowbar_Metallic.png");
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crowbar/Textures/T_Crowbar_AO.png");

            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(mat);
        }

        private static void GenerateMeleeIcons()
        {
            string iconDir = "Assets/Resources/SurvivalTools/Textures/Icons";
            if (!Directory.Exists(iconDir)) Directory.CreateDirectory(iconDir);

            // Icon_Knife.png
            string knifeIconPath = $"{iconDir}/Icon_Knife.png";
            if (!File.Exists(knifeIconPath))
            {
                Texture2D knifeTex = CreateIconTexture(SurvivalToolType.Melee_Knife);
                File.WriteAllBytes(knifeIconPath, knifeTex.EncodeToPNG());
                AssetDatabase.ImportAsset(knifeIconPath, ImportAssetOptions.ForceUpdate);
            }

            // Icon_Crowbar.png
            string crowbarIconPath = $"{iconDir}/Icon_Crowbar.png";
            if (!File.Exists(crowbarIconPath))
            {
                Texture2D crowbarTex = CreateIconTexture(SurvivalToolType.Melee_Crowbar);
                File.WriteAllBytes(crowbarIconPath, crowbarTex.EncodeToPNG());
                AssetDatabase.ImportAsset(crowbarIconPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static Texture2D CreateIconTexture(SurvivalToolType type)
        {
            int w = 128;
            int h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            void Fill(int rx, int ry, int rw, int rh, Color col)
            {
                int x0 = Mathf.Clamp(rx, 0, w - 1);
                int y0 = Mathf.Clamp(ry, 0, h - 1);
                int x1 = Mathf.Clamp(rx + rw, 0, w);
                int y1 = Mathf.Clamp(ry + rh, 0, h);
                for (int py = y0; py < y1; py++)
                    for (int px = x0; px < x1; px++)
                        pixels[py * w + px] = col;
            }

            if (type == SurvivalToolType.Melee_Knife)
            {
                Color steel = new Color(0.92f, 0.94f, 0.97f, 1f);
                Color darkSteel = new Color(0.48f, 0.52f, 0.58f, 1f);
                Color handle = new Color(0.18f, 0.20f, 0.22f, 1f);
                Color guard = new Color(0.65f, 0.68f, 0.72f, 1f);

                // Blade
                for (int i = 0; i < 58; i++)
                {
                    int x = 48 + i;
                    int y = 58 + (i / 4);
                    int hgt = 14 - (i / 5);
                    Fill(x, y, 1, Mathf.Max(2, hgt), steel);
                    Fill(x, y, 1, 3, darkSteel);
                }
                // Guard
                Fill(44, 50, 5, 28, guard);
                // Handle
                Fill(16, 56, 28, 14, handle);
                Fill(22, 54, 4, 18, guard);
                Fill(34, 54, 4, 18, guard);
            }
            else // Crowbar
            {
                Color ironBlue = new Color(0.45f, 0.72f, 0.95f, 1f);
                Color darkIron = new Color(0.24f, 0.36f, 0.48f, 1f);

                for (int i = 0; i < 72; i++)
                {
                    int x = 24 + i;
                    int y = 30 + (i * 62 / 72);
                    Fill(x, y, 6, 6, ironBlue);
                    Fill(x + 1, y, 3, 3, darkIron);
                }
                // Curved hook head
                Fill(90, 92, 16, 8, ironBlue);
                Fill(98, 80, 10, 14, ironBlue);
                Fill(100, 72, 12, 10, darkIron);
                // Prying chisel tip (bottom)
                Fill(16, 24, 10, 8, ironBlue);
                Fill(14, 20, 8, 6, darkIron);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static GameObject CreateOrUpdateKnifePrefab(Shader urpLit)
        {
            string prefabDir = "Assets/Resources/SurvivalTools/Prefabs";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);
            string prefabPath = $"{prefabDir}/knife.prefab";

            GameObject sourceFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/iDos Games/Stylized Knife/Models/Knife.fbx");
            if (sourceFBX == null)
            {
                sourceFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/iDos Games/Stylized Knife/Prefabs/Knife.prefab");
            }
            if (sourceFBX == null) return null;

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(sourceFBX);
            temp.name = "knife";

            // Ensure trigger collider
            BoxCollider col = temp.GetComponent<BoxCollider>();
            if (col == null) col = temp.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.02f, 0.10f);
            col.size = new Vector3(0.12f, 0.10f, 0.45f);

            // Ensure SurvivalItemPickup
            SurvivalItemPickup pickup = temp.GetComponent<SurvivalItemPickup>();
            if (pickup == null) pickup = temp.AddComponent<SurvivalItemPickup>();
            pickup.itemType = SurvivalToolType.Melee_Knife;
            pickup.displayName = "Combat Knife";
            pickup.amount = 1;
            pickup.interactRange = 3.2f;

            // Ensure loot light (Red glow for melee weapon)
            Transform oldLight = temp.transform.Find("ItemGlow");
            if (oldLight != null) Object.DestroyImmediate(oldLight.gameObject);
            GameObject lightObj = new GameObject("ItemGlow");
            lightObj.transform.SetParent(temp.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.15f, 0.15f); // Red for knife
            l.intensity = 0.25f;
            l.range = 0.90f;
            l.shadows = LightShadows.None;

            SurvivalItemPickup.EnsureColoredURPMaterials(temp, SurvivalToolType.Melee_Knife);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            return savedPrefab;
        }

        private static GameObject CreateOrUpdateCrowbarPrefab(Shader urpLit)
        {
            string prefabDir = "Assets/Resources/SurvivalTools/Prefabs";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);
            string prefabPath = $"{prefabDir}/crowbar.prefab";

            GameObject sourceFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowbar/Mesh/Crowbar.fbx");
            if (sourceFBX == null)
            {
                sourceFBX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crowbar/Prefab/Crowbar.prefab");
            }
            if (sourceFBX == null) return null;

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(sourceFBX);
            temp.name = "crowbar";

            // Ensure trigger collider
            BoxCollider col = temp.GetComponent<BoxCollider>();
            if (col == null) col = temp.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.02f, 0.05f);
            col.size = new Vector3(0.15f, 0.12f, 0.75f);

            // Ensure SurvivalItemPickup
            SurvivalItemPickup pickup = temp.GetComponent<SurvivalItemPickup>();
            if (pickup == null) pickup = temp.AddComponent<SurvivalItemPickup>();
            pickup.itemType = SurvivalToolType.Melee_Crowbar;
            pickup.displayName = "Steel Crowbar";
            pickup.amount = 1;
            pickup.interactRange = 3.4f;

            // Ensure loot light (Red glow for melee weapon)
            Transform oldLight = temp.transform.Find("ItemGlow");
            if (oldLight != null) Object.DestroyImmediate(oldLight.gameObject);
            GameObject lightObj = new GameObject("ItemGlow");
            lightObj.transform.SetParent(temp.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.15f, 0.15f); // Red for crowbar
            l.intensity = 0.25f;
            l.range = 0.95f;
            l.shadows = LightShadows.None;

            SurvivalItemPickup.EnsureColoredURPMaterials(temp, SurvivalToolType.Melee_Crowbar);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            return savedPrefab;
        }

        private static void ScatterMeleeInScene(GameObject knifePrefab, GameObject crowbarPrefab)
        {
            if (knifePrefab == null || crowbarPrefab == null) return;

            // Container in Scene
            string containerName = "World_Melee_Weapons";
            GameObject container = GameObject.Find(containerName);
            if (container != null)
            {
                Object.DestroyImmediate(container);
            }
            container = new GameObject(containerName);

            // Curated scattering spots matching houses, shacks, tables, and exploration hubs
            (Vector3 pos, bool isKnife, string label)[] spawnSpots = new[]
            {
                // 1. Starting area / covered bridge table
                (new Vector3(484.5f, 19.25f, 155.2f), true, "Knife_BridgeTable"),
                // 2. Starting area / guard hut side porch
                (new Vector3(471.2f, 19.35f, 141.6f), false, "Crowbar_GuardHut"),
                // 3. Barn 2 interior workbench
                (new Vector3(564.2f, 29.55f, 92.5f), false, "Crowbar_BarnWorkbench"),
                // 4. Barn 2 rear loft
                (new Vector3(568.0f, 29.55f, 94.0f), true, "Knife_BarnLoft"),
                // 5. Riverside Cabin 1 porch table
                (new Vector3(561.5f, 17.55f, 271.0f), true, "Knife_Cabin1Porch"),
                // 6. Riverside Cabin 1 bedroom table
                (new Vector3(563.8f, 18.25f, 276.5f), false, "Crowbar_Cabin1Bedroom"),
                // 7. Riverside Cabin 2 interior
                (new Vector3(400.0f, 17.55f, 293.0f), true, "Knife_Cabin2Interior"),
                // 8. Riverside Cabin 2 backroom
                (new Vector3(385.0f, 17.55f, 293.0f), false, "Crowbar_Cabin2Back"),
                // 9. Cabin small table
                (new Vector3(415.0f, 17.25f, 478.0f), true, "Knife_CabinSmallTable"),
                // 10. Cabin 2 doorway table
                (new Vector3(445.0f, 17.85f, 514.0f), false, "Crowbar_CabinDoorway"),
                // 11. BrickHouse living room
                (new Vector3(371.5f, 17.65f, 265.0f), true, "Knife_BrickHouse1"),
                // 12. BrickHouse upper floor
                (new Vector3(453.0f, 24.55f, 439.0f), false, "Crowbar_BrickHouseUpper"),
                // 13. Grand Villa 2 bedroom
                (new Vector3(516.5f, 26.85f, 533.5f), true, "Knife_Villa2Bedroom"),
                // 14. Grand Villa 2 main hall
                (new Vector3(524.0f, 21.05f, 523.5f), false, "Crowbar_Villa2Hall"),
                // 15. Grand Villa 1 floor
                (new Vector3(556.0f, 25.05f, 442.0f), true, "Knife_Villa1Floor"),
                // 16. Outhouse / storage shack
                (new Vector3(531.3f, 17.25f, 173.1f), false, "Crowbar_StorageShack"),
                // 17. Near Boat 4 escape dock
                (new Vector3(590.2f, 14.85f, 195.0f), false, "Crowbar_Boat4Dock")
            };

            int mask = ~((1 << 2) | (1 << 5) | (1 << 8));
            int placedCount = 0;

            foreach (var spot in spawnSpots)
            {
                Vector3 p = spot.pos;
                RaycastHit[] hits = Physics.RaycastAll(new Ray(p + Vector3.up * 1.5f, Vector3.down), 6.0f, mask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool snapped = false;
                foreach (var h in hits)
                {
                    if (h.collider.isTrigger) continue;
                    if (h.collider.GetComponentInParent<GunPickup>() != null) continue;
                    if (h.collider.GetComponentInParent<SurvivalItemPickup>() != null) continue;
                    if (Vector3.Dot(h.normal, Vector3.up) > 0.40f)
                    {
                        p = h.point + Vector3.up * 0.03f;
                        snapped = true;
                        break;
                    }
                }
                if (!snapped)
                {
                    p += Vector3.up * 0.03f;
                }

                GameObject prefabToSpawn = spot.isKnife ? knifePrefab : crowbarPrefab;
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), spot.isKnife ? 0f : 8f);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn);
                instance.name = spot.label;
                instance.transform.position = p;
                instance.transform.rotation = rot;
                instance.transform.SetParent(container.transform);

                SurvivalItemPickup pickup = instance.GetComponent<SurvivalItemPickup>();
                if (pickup != null)
                {
                    pickup.itemType = spot.isKnife ? SurvivalToolType.Melee_Knife : SurvivalToolType.Melee_Crowbar;
                    pickup.displayName = spot.isKnife ? "Combat Knife" : "Steel Crowbar";
                }

                SurvivalItemPickup.EnsureColoredURPMaterials(instance, spot.isKnife ? SurvivalToolType.Melee_Knife : SurvivalToolType.Melee_Crowbar);
                placedCount++;
            }

            if (!Application.isPlaying)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[SetupMeleeWeaponsInWorld] Scattered {placedCount} pickable Melee Weapons (Knives & Crowbars) across the Flooded Grounds world!");
        }
    }
}

