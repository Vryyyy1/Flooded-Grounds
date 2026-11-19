using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StarterAssets;

namespace IgnitedEditor
{
    [InitializeOnLoad]
    public static class SetupSurvivalToolsAndBag
    {
        private const string SessionKey = "SurvivalToolsAndBag_Setup_v2";

        static SetupSurvivalToolsAndBag()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(SessionKey, false)) return;
                SessionState.SetBool(SessionKey, true);
                ApplyAll(false);
            };
        }

        [MenuItem("Tools/Setup Survival Tools Colors & Inventory Bag")]
        public static void MenuSetup()
        {
            ApplyAll(true);
        }

        public static void ApplyAll(bool verbose)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit == null) return;

            // 1. Upgrade all Survival Tools Materials to URP Lit with full textures
            UpgradeSurvivalMaterial("Battery", "survivaltools2_Battery_AlbedoTransparency", "survivaltools2_Battery_Normal", "survivaltools2_Battery_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("CannedFood", "survivaltools2_CannedFood_AlbedoTransparency", "survivaltools2_CannedFood_Normal", "survivaltools2_CannedFood_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("FirstAid", "survivaltools2_FirstAidKit_AlbedoTransparency", "survivaltools2_FirstAidKit_Normal", "survivaltools2_FirstAidKit_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Flashlight", "survivaltools2_Flashlight_AlbedoTransparency", "survivaltools2_Flashlight_Normal", "survivaltools2_Flashlight_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("FlashlightGlass", "survivaltools2_Flashlight_AlbedoTransparency", "survivaltools2_Flashlight_Normal", "survivaltools2_Flashlight_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Matchbox", "survivaltools2_MatchBox_AlbedoTransparency", "survivaltools2_MatchBox_Normal", "survivaltools2_MatchBox_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Matches", "survivaltools2_Matches_AlbedoTransparency", null, "survivaltools2_Matches_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Pills", "survivaltools2_Pills_AlbedoTransparency", "survivaltools2_Pills_Normal", "survivaltools2_Pills_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Tape", "survivaltools2_Tape_AlbedoTransparency", "survivaltools2_Tape_Normal", "survivaltools2_Tape_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Walkie", "survivaltools2_Walkie_AlbedoTransparency", "survivaltools2_Walkie_Normal", "survivaltools2_Walkie_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("Walkieglass", "survivaltools2_Walkie_AlbedoTransparency", null, null, urpLit);
            UpgradeSurvivalMaterial("WalkieNumbers", "survivaltools2_Walkie_Numbers", null, null, urpLit);
            UpgradeSurvivalMaterial("WaterBottle", "survivaltools2_Water_AlbedoTransparency", "survivaltools2_Water_Normal", "survivaltools2_Water_MetallicSmoothness", urpLit);
            UpgradeSurvivalMaterial("WaterBottleCap", "survivaltools2_Water_AlbedoTransparency", "survivaltools2_Water_Normal", "survivaltools2_Water_MetallicSmoothness", urpLit);

            // 2. Remap all FBX models in Assets/Survival Tools/Meshes so Project thumbnails render in full color
            RemapFBXMaterials("battery.fbx", new[] { "Battery", "No Name" }, new[] { "Battery", "Battery" });
            RemapFBXMaterials("cannedfood.fbx", new[] { "CannedFood", "No Name" }, new[] { "CannedFood", "CannedFood" });
            RemapFBXMaterials("firstaid.fbx", new[] { "FirstAidKit", "No Name" }, new[] { "FirstAid", "FirstAid" });
            RemapFBXMaterials("flashlight.fbx", new[] { "Flashlight", "Flashlightglass", "No Name", "Mat.1", "Mat" }, new[] { "Flashlight", "FlashlightGlass", "Flashlight", "Flashlight", "Flashlight" });
            RemapFBXMaterials("matchbox.fbx", new[] { "MatchBox", "Matches", "box", "matches" }, new[] { "Matchbox", "Matches", "Matchbox", "Matches" });
            RemapFBXMaterials("pills.fbx", new[] { "Pills", "No Name" }, new[] { "Pills", "Pills" });
            RemapFBXMaterials("tape.fbx", new[] { "Tape", "No Name" }, new[] { "Tape", "Tape" });
            RemapFBXMaterials("walkie.fbx", new[] { "Walkie", "Walkieglass", "Mat" }, new[] { "Walkie", "Walkieglass", "Walkie" });
            RemapFBXMaterials("waterbottle.fbx", new[] { "Water", "Water bottle" }, new[] { "WaterBottle", "WaterBottleCap" });

            // 3. Configure Prefabs in Assets/Survival Tools/Prefabs
            ConfigureSurvivalPrefab("battery", SurvivalToolType.Battery, new[] { "Battery" });
            ConfigureSurvivalPrefab("cannedfood", SurvivalToolType.CannedFood, new[] { "CannedFood" });
            ConfigureSurvivalPrefab("firstaid", SurvivalToolType.FirstAid, new[] { "FirstAid" });
            ConfigureSurvivalPrefab("flashlight", SurvivalToolType.Flashlight, new[] { "Flashlight", "FlashlightGlass" });
            ConfigureSurvivalPrefab("matchbox", SurvivalToolType.Matchbox, new[] { "Matchbox", "Matches" });
            ConfigureSurvivalPrefab("pills", SurvivalToolType.Pills, new[] { "Pills" });
            ConfigureSurvivalPrefab("tape", SurvivalToolType.Tape, new[] { "Tape" });
            ConfigureSurvivalPrefab("walkie", SurvivalToolType.Walkie, new[] { "Walkie", "WalkieNumbers" });
            ConfigureSurvivalPrefab("waterbottle", SurvivalToolType.WaterBottle, new[] { "WaterBottle", "WaterBottleCap" });

            // 4. Attach PlayerInventoryBag (starting EMPTY) & configure all Scene Survival Tools
            var fpc = Object.FindFirstObjectByType<FirstPersonController>();
            GameObject playerObj = fpc != null ? fpc.gameObject : GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                PlayerInventoryBag bag = playerObj.GetComponent<PlayerInventoryBag>();
                if (bag == null)
                {
                    bag = playerObj.AddComponent<PlayerInventoryBag>();
                }
                bag.items.Clear();
                bag.Build3DCharacterInventoryBag();
                EditorUtility.SetDirty(playerObj);
                if (!EditorApplication.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(playerObj.scene);
                }
            }

            PlayerInventoryBag.SetupSceneSurvivalTools(playerObj != null ? playerObj.transform : null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (verbose)
            {
                Debug.Log("[SetupSurvivalToolsAndBag] All Survival Tools upgraded to URP Lit colors, FBX meshes remapped, and Character Inventory Bag attached!");
            }
        }

        private static void UpgradeSurvivalMaterial(string matFileName, string albedoFile, string normalFile, string metallicFile, Shader urpLit)
        {
            string matPath = $"Assets/Survival Tools/Materials/{matFileName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return;

            mat.shader = urpLit;

            Texture2D albedo = !string.IsNullOrEmpty(albedoFile)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Survival Tools/Textures/{albedoFile}.png")
                : null;
            Texture2D normal = !string.IsNullOrEmpty(normalFile)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Survival Tools/Textures/{normalFile}.png")
                : null;
            Texture2D metallic = !string.IsNullOrEmpty(metallicFile)
                ? AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Survival Tools/Textures/{metallicFile}.png")
                : null;

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

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
            }

            mat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(mat);

            // Also update mirrored Resources copy if present
            string resMatPath = $"Assets/Resources/SurvivalTools/Materials/{matFileName}.mat";
            Material resMat = AssetDatabase.LoadAssetAtPath<Material>(resMatPath);
            if (resMat != null)
            {
                EditorUtility.CopySerialized(mat, resMat);
                EditorUtility.SetDirty(resMat);
            }
        }

        private static void RemapFBXMaterials(string fbxFileName, string[] embeddedNames, string[] targetMatNames)
        {
            string fbxPath = $"Assets/Survival Tools/Meshes/{fbxFileName}";
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            for (int i = 0; i < embeddedNames.Length; i++)
            {
                Material targetMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Survival Tools/Materials/{targetMatNames[i]}.mat");
                if (targetMat != null)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedNames[i]);
                    importer.AddRemap(id, targetMat);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureSurvivalPrefab(string prefabFileName, SurvivalToolType type, string[] matNames)
        {
            string prefabPath = $"Assets/Survival Tools/Prefabs/{prefabFileName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            MeshRenderer mr = prefab.GetComponentInChildren<MeshRenderer>();
            if (mr != null && matNames != null && matNames.Length > 0)
            {
                Material[] mats = new Material[matNames.Length];
                for (int i = 0; i < matNames.Length; i++)
                {
                    mats[i] = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Survival Tools/Materials/{matNames[i]}.mat");
                }
                mr.sharedMaterials = mats;
            }

            SurvivalItemPickup pickup = prefab.GetComponent<SurvivalItemPickup>();
            if (pickup == null)
            {
                pickup = prefab.AddComponent<SurvivalItemPickup>();
            }
            pickup.itemType = type;
            pickup.AutoDetectItemType();

            EditorUtility.SetDirty(prefab);
        }
    }
}

