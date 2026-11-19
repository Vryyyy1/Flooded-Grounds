using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class FixFloodedGroundsMaterials
{
    static FixFloodedGroundsMaterials()
    {
        EditorApplication.delayCall += AutoFixBoatsOnLoad;
        EditorApplication.hierarchyChanged += FixBoatsInActiveScene;
    }

    private static void AutoFixBoatsOnLoad()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool alreadyImportedFbx = SessionState.GetBool("BoatsAndRustKeyURPFixed_v3", false);
        FixBoatsPackMaterials(reimportFbxModels: !alreadyImportedFbx);
        SessionState.SetBool("BoatsAndRustKeyURPFixed_v3", true);
    }

    [MenuItem("Tools/Fix Boats Pack & Rust Key Pink Materials (URP)")]
    public static void MenuFixBoatsPack()
    {
        FixBoatsPackMaterials(reimportFbxModels: true);
    }

    [MenuItem("Tools/Darken Houses & Bridges (Match Environment)")]
    public static void MenuFixAndDarken()
    {
        FixAllMaterials();
    }

    [MenuItem("Tools/Fix Flooded Grounds Pink Materials (URP)")]
    public static void FixAllMaterials()
    {
        int fixedCount = 0;
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Flooded_Grounds" });

        var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        var urpParticlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var waterShader = Shader.Find("Flooded_Grounds/PBR_Water");
        var topBlendShader = Shader.Find("Flooded_Grounds/PBR_TopBlend");
        var triplanarShader = Shader.Find("Flooded_Grounds/Triplanar_BumpSpec");
        var treeLeavesShader = Shader.Find("Nature/Tree Creator Leaves");
        var treeBarkShader = Shader.Find("Nature/Tree Creator Bark");
        var skyShader = Shader.Find("Flooded_Grounds/Skybox_Rotating");

        // Environment-matched darker color palette for swamp/flooded grounds
        // Slightly darker with subtle olive/slate damp tone to match misty environment
        Color houseColor = new Color(0.58f, 0.61f, 0.57f, 1.0f);
        Color villa2Color = new Color(0.55f, 0.58f, 0.54f, 1.0f);
        Color bridgeColor = new Color(0.58f, 0.61f, 0.57f, 1.0f);
        Color propColor = new Color(0.64f, 0.66f, 0.63f, 1.0f);

        Debug.Log($"[FixFloodedGrounds] Processing {matGuids.Length} materials in Flooded_Grounds...");

        foreach (var guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string name = mat.name;
            string currentShader = mat.shader != null ? mat.shader.name : "None";
            bool isUnsupported = mat.shader == null || !mat.shader.isSupported || currentShader.Contains("Error");
            bool is2DShader = currentShader.Contains("2D");

            bool modified = false;

            // 1. Water
            if (name.Equals("BGR_Water", StringComparison.OrdinalIgnoreCase))
            {
                if (waterShader != null && mat.shader != waterShader)
                {
                    mat.shader = waterShader;
                    modified = true;
                }
            }
            // 2. Sky
            else if (name.Equals("BGR_Sky1", StringComparison.OrdinalIgnoreCase))
            {
                if (skyShader != null && mat.shader != skyShader)
                {
                    mat.shader = skyShader;
                    modified = true;
                }
            }
            // 3. Particles
            else if (name.StartsWith("ATM_", StringComparison.OrdinalIgnoreCase))
            {
                if (urpParticlesUnlit != null && (isUnsupported || !currentShader.Contains("Universal Render Pipeline")))
                {
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    mat.shader = urpParticlesUnlit;
                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0); // Alpha or Additive
                    modified = true;
                }
            }
            // 4. Tree leaves in SrcMaterials
            else if (name.StartsWith("Branch", StringComparison.OrdinalIgnoreCase) || name.Equals("BushFlower", StringComparison.OrdinalIgnoreCase))
            {
                if (treeLeavesShader != null && (isUnsupported || !currentShader.Contains("Nature")))
                {
                    mat.shader = treeLeavesShader;
                    modified = true;
                }
            }
            // 5. Tree trunk in SrcMaterials
            else if (name.StartsWith("Trunk", StringComparison.OrdinalIgnoreCase))
            {
                if (treeBarkShader != null && (isUnsupported || !currentShader.Contains("Nature")))
                {
                    mat.shader = treeBarkShader;
                    modified = true;
                }
            }
            // 6. Bushes
            else if (name.StartsWith("NAT_Bush", StringComparison.OrdinalIgnoreCase))
            {
                if (triplanarShader != null && (isUnsupported || !currentShader.Contains("Flooded_Grounds")))
                {
                    mat.shader = triplanarShader;
                    modified = true;
                }
            }
            // 7. TopBlend materials (Mossy objects, rocks, cars)
            else if (name.Contains("Mossy") || name.Contains("Rusty") || name.StartsWith("NAT_Rocks", StringComparison.OrdinalIgnoreCase)
                     || name.StartsWith("NAT_CobbleRocks", StringComparison.OrdinalIgnoreCase)
                     || name.Equals("PROP_Car1", StringComparison.OrdinalIgnoreCase)
                     || name.Equals("PROP_Ship1", StringComparison.OrdinalIgnoreCase))
            {
                if (topBlendShader != null && (isUnsupported || !currentShader.Contains("Flooded_Grounds")))
                {
                    mat.shader = topBlendShader;
                    modified = true;
                }
            }
            // 8. Bridges (BLD_Bridge1, BLD_Bridge1_LOD)
            else if (name.StartsWith("BLD_Bridge", StringComparison.OrdinalIgnoreCase))
            {
                SetupBuildingMaterial(mat, "BLD_Bridge1", bridgeColor, smoothness: 0.06f, glossScale: 0.30f);
                modified = true;
            }
            // 9. Window glass
            else if (name.Equals("BLD_WindowGlass1", StringComparison.OrdinalIgnoreCase))
            {
                if (urpLitShader != null && mat.shader != urpLitShader)
                {
                    mat.shader = urpLitShader;
                }
                var albedo = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Flooded_Grounds/Content/Textures/BLD_WindowGlass1_A.tif");
                var normal = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Flooded_Grounds/Content/Textures/BLD_WindowGlass1_N.tif");
                var ms = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Flooded_Grounds/Content/Textures/BLD_WindowGlass1_MS.tif");
                if (albedo != null) { mat.SetTexture("_BaseMap", albedo); mat.SetTexture("_MainTex", albedo); }
                if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
                if (ms != null) { mat.SetTexture("_MetallicGlossMap", ms); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                mat.SetFloat("_Surface", 1.0f); // Transparent
                mat.SetFloat("_Blend", 0.0f); // Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", new Color(0.42f, 0.46f, 0.44f, 0.45f));
                mat.SetFloat("_Smoothness", 0.65f);
                modified = true;
            }
            // 10. Houses and Buildings (Villa1, Villa2, Cabins, Brickhouse, Church, Doors, IndBuilding, Structures, Rubble)
            else if (name.StartsWith("BLD_", StringComparison.OrdinalIgnoreCase))
            {
                string baseTexName = name;
                if (baseTexName.EndsWith("_LOD", StringComparison.OrdinalIgnoreCase))
                {
                    baseTexName = baseTexName.Substring(0, baseTexName.Length - 4);
                }
                if (baseTexName.Equals("BLD_Cabins", StringComparison.OrdinalIgnoreCase))
                {
                    baseTexName = "BLD_Cabins1";
                }

                Color targetColor = name.Equals("BLD_Villa2", StringComparison.OrdinalIgnoreCase) ? villa2Color : houseColor;
                SetupBuildingMaterial(mat, baseTexName, targetColor, smoothness: 0.08f, glossScale: 0.30f);
                modified = true;
            }
            // 11. Grass & Ground Moss
            else if (name.Equals("NAT_Grass", StringComparison.OrdinalIgnoreCase))
            {
                if (urpLitShader != null)
                {
                    mat.shader = urpLitShader;
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    mat.SetColor("_BaseColor", new Color(0.58f, 0.62f, 0.54f, 1.0f));
                    mat.SetFloat("_AlphaClip", 1.0f);
                    mat.SetFloat("_Cutoff", 0.5f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Smoothness", 0.05f);
                    mat.SetFloat("_SpecularHighlights", 0.0f);
                    modified = true;
                }
            }
            else if (name.Equals("NAT_GroundMoss", StringComparison.OrdinalIgnoreCase))
            {
                if (urpLitShader != null)
                {
                    mat.shader = urpLitShader;
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    mat.SetColor("_BaseColor", new Color(0.58f, 0.62f, 0.54f, 1.0f));
                    mat.SetFloat("_Smoothness", 0.05f);
                    modified = true;
                }
            }
            // 12. General unconverted / 2D / unsupported materials (Props, etc.)
            else if (isUnsupported || is2DShader || currentShader.Equals("Standard") || currentShader.Equals("Standard (Specular setup)"))
            {
                if (urpLitShader != null)
                {
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    Texture msMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                    Texture aoMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;

                    mat.shader = urpLitShader;
                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    if (bumpMap != null)
                    {
                        mat.SetTexture("_BumpMap", bumpMap);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    if (msMap != null)
                    {
                        mat.SetTexture("_MetallicGlossMap", msMap);
                        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    if (aoMap != null)
                    {
                        mat.SetTexture("_OcclusionMap", aoMap);
                        mat.EnableKeyword("_OCCLUSIONMAP");
                        mat.SetFloat("_OcclusionStrength", 1.0f);
                    }

                    mat.SetColor("_BaseColor", propColor);
                    mat.SetFloat("_Smoothness", 0.12f);
                    mat.SetFloat("_GlossMapScale", 0.35f);
                    mat.SetFloat("_Metallic", 0.0f);
                    modified = true;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(mat);
                fixedCount++;
                Debug.Log("[FixFloodedGrounds] Updated material: " + name + " -> Shader: " + mat.shader.name);
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FixFloodedGrounds] Successfully updated {fixedCount} materials to match environment!");
        }
        else
        {
            Debug.Log("[FixFloodedGrounds] All materials are already up-to-date and supported.");
        }

        FixBoatsPackMaterials(reimportFbxModels: false);
    }

    public static void FixBoatsPackMaterials(bool reimportFbxModels = false)
    {
        bool anyAssetChanged = false;

        for (int i = 1; i <= 5; i++)
        {
            string matPath = $"Assets/Boats/Materials/Boat_{i}_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                if (UpgradeSingleBoatMaterial(mat, i))
                {
                    anyAssetChanged = true;
                }
            }

            string prefabPath = $"Assets/Boats/Prefabs/Boat_{i}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null && mat != null)
            {
                MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer mr in renderers)
                {
                    if (mr != null && mr.sharedMaterial != mat)
                    {
                        mr.sharedMaterial = mat;
                        EditorUtility.SetDirty(mr);
                        EditorUtility.SetDirty(prefab);
                        anyAssetChanged = true;
                    }
                }
            }
        }

        // Also upgrade Rust Key materials and prefab
        string[] rustKeyMatPaths =
        {
            "Assets/Rust Key/Materials/rust_key_autodesk.mat",
            "Assets/Rust Key/Materials/rust_key_standard.mat"
        };
        Material primaryRustKeyMat = null;
        foreach (string rkMatPath in rustKeyMatPaths)
        {
            Material rkMat = AssetDatabase.LoadAssetAtPath<Material>(rkMatPath);
            if (rkMat != null)
            {
                if (primaryRustKeyMat == null) primaryRustKeyMat = rkMat;
                if (UpgradeRustKeyMaterial(rkMat))
                {
                    anyAssetChanged = true;
                }
            }
        }

        GameObject rustKeyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rust Key/Prefabs/rust_key.prefab");
        if (rustKeyPrefab != null && primaryRustKeyMat != null)
        {
            MeshRenderer[] rkRenderers = rustKeyPrefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer mr in rkRenderers)
            {
                if (mr != null && mr.sharedMaterial != primaryRustKeyMat)
                {
                    mr.sharedMaterial = primaryRustKeyMat;
                    EditorUtility.SetDirty(mr);
                    EditorUtility.SetDirty(rustKeyPrefab);
                    anyAssetChanged = true;
                }
            }
        }

        if (anyAssetChanged)
        {
            AssetDatabase.SaveAssets();
        }

        if (reimportFbxModels)
        {
            for (int i = 1; i <= 5; i++)
            {
                string fbxPath = $"Assets/Boats/Models/Boat_{i}.fbx";
                if (File.Exists(fbxPath))
                {
                    AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
                }
            }

            const string rustKeyFbxPath = "Assets/Rust Key/Mesh/rust_key.FBX";
            if (File.Exists(rustKeyFbxPath))
            {
                AssetDatabase.ImportAsset(rustKeyFbxPath, ImportAssetOptions.ForceUpdate);
            }
        }

        FixBoatsInActiveScene();
    }

    public static bool UpgradeRustKeyMaterial(Material mat)
    {
        if (mat == null) return false;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return false;

        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Rust Key/Textures/rust_key_Albedo.png");
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Rust Key/Textures/rust_key_Normal.png");
        Texture metalness = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Rust Key/Textures/rust_key_Metalness.png");

        bool changed = false;

        if (mat.shader != urpLit)
        {
            mat.shader = urpLit;
            changed = true;
        }

        if (albedo != null && mat.GetTexture("_BaseMap") != albedo)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
            changed = true;
        }

        if (normal != null && mat.GetTexture("_BumpMap") != normal)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.28f);
            changed = true;
        }

        if (metalness != null && mat.GetTexture("_MetallicGlossMap") != metalness)
        {
            mat.SetTexture("_MetallicGlossMap", metalness);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            changed = true;
        }

        if (mat.HasProperty("_BaseColor") && mat.GetColor("_BaseColor") != Color.white)
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            changed = true;
        }

        mat.SetFloat("_Metallic", 0.45f);
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_WorkflowMode", 1.0f);

        if (changed)
        {
            EditorUtility.SetDirty(mat);
        }

        return changed;
    }

    public static bool UpgradeSingleBoatMaterial(Material mat, int boatIndex)
    {
        if (mat == null) return false;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return false;

        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>($"Assets/Boats/Textures/Boat_{boatIndex}_Color.png");
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture>($"Assets/Boats/Textures/Boat_{boatIndex}_Normal.png");
        Texture ms = AssetDatabase.LoadAssetAtPath<Texture>($"Assets/Boats/Textures/Boat_{boatIndex}_MetallicSmoothness.png");

        bool changed = false;

        if (mat.shader != urpLit)
        {
            mat.shader = urpLit;
            changed = true;
        }

        if (albedo != null && mat.GetTexture("_BaseMap") != albedo)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
            changed = true;
        }

        if (normal != null && mat.GetTexture("_BumpMap") != normal)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.0f);
            changed = true;
        }

        if (ms != null && mat.GetTexture("_MetallicGlossMap") != ms)
        {
            mat.SetTexture("_MetallicGlossMap", ms);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            changed = true;
        }

        if (mat.HasProperty("_BaseColor") && mat.GetColor("_BaseColor") != Color.white)
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            changed = true;
        }

        mat.SetFloat("_Smoothness", 0.45f);
        mat.SetFloat("_WorkflowMode", 1.0f);

        if (changed)
        {
            EditorUtility.SetDirty(mat);
        }

        return changed;
    }

    public static void FixBoatsInActiveScene()
    {
        if (Application.isPlaying) return;

        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (MeshRenderer mr in renderers)
        {
            if (mr == null || mr.gameObject == null) continue;

            int boatIdx = DetectBoatIndex(mr);
            if (boatIdx >= 1 && boatIdx <= 5)
            {
                Material boatMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Boats/Materials/Boat_{boatIdx}_Mat.mat");
                if (boatMat != null)
                {
                    UpgradeSingleBoatMaterial(boatMat, boatIdx);
                    if (mr.sharedMaterial != boatMat)
                    {
                        mr.sharedMaterial = boatMat;
                        EditorUtility.SetDirty(mr);
                    }
                }
            }
            else if (IsRustKeyRenderer(mr))
            {
                Material rkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rust Key/Materials/rust_key_autodesk.mat");
                if (rkMat != null)
                {
                    UpgradeRustKeyMaterial(rkMat);
                    if (mr.sharedMaterial != rkMat)
                    {
                        mr.sharedMaterial = rkMat;
                        EditorUtility.SetDirty(mr);
                    }
                }
            }
        }
    }

    private static bool IsRustKeyRenderer(MeshRenderer mr)
    {
        if (mr == null) return false;

        MeshFilter mf = mr.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            string meshAssetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(meshAssetPath) && meshAssetPath.IndexOf("rust_key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        string objName = mr.gameObject.name;
        string parentName = mr.transform.parent != null ? mr.transform.parent.name : "";
        return objName.IndexOf("rust_key", StringComparison.OrdinalIgnoreCase) >= 0 ||
               parentName.IndexOf("rust_key", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int DetectBoatIndex(MeshRenderer mr)
    {
        if (mr == null) return 0;

        MeshFilter mf = mr.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            string meshAssetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(meshAssetPath) && meshAssetPath.StartsWith("Assets/Boats/", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i <= 5; i++)
                {
                    if (meshAssetPath.IndexOf($"Boat_{i}", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }
        }

        string objName = mr.gameObject.name;
        string parentName = mr.transform.parent != null ? mr.transform.parent.name : "";
        for (int i = 1; i <= 5; i++)
        {
            string token = $"Boat_{i}";
            if (objName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }

        return 0;
    }

    private static void SetupBuildingMaterial(Material mat, string baseTextureName, Color envColor, float smoothness, float glossScale)
    {
        var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader != null && mat.shader != urpLitShader)
        {
            mat.shader = urpLitShader;
        }

        string texDir = "Assets/Flooded_Grounds/Content/Textures/";
        var albedo = AssetDatabase.LoadAssetAtPath<Texture>(texDir + baseTextureName + "_A.tif");
        var normal = AssetDatabase.LoadAssetAtPath<Texture>(texDir + baseTextureName + "_N.tif");
        var ms = AssetDatabase.LoadAssetAtPath<Texture>(texDir + baseTextureName + "_MS.tif");
        var ao = AssetDatabase.LoadAssetAtPath<Texture>(texDir + baseTextureName + "_AO.tif");

        // Albedo / BaseMap
        if (albedo != null)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
        }
        else if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
        {
            mat.SetTexture("_BaseMap", mat.GetTexture("_MainTex"));
        }

        // Normal Map
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.0f);
        }
        else if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
        {
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.0f);
        }

        // Metallic / Smoothness Map
        if (ms != null)
        {
            mat.SetTexture("_MetallicGlossMap", ms);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null)
        {
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        // Ambient Occlusion Map
        if (ao != null)
        {
            mat.SetTexture("_OcclusionMap", ao);
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetFloat("_OcclusionStrength", 1.0f);
        }
        else if (mat.HasProperty("_OcclusionMap") && mat.GetTexture("_OcclusionMap") != null)
        {
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetFloat("_OcclusionStrength", 1.0f);
        }

        // Environment-matched darker color and reduced light/smoothness
        mat.SetColor("_BaseColor", envColor);
        mat.SetColor("_Color", envColor);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_GlossMapScale", glossScale);
        mat.SetFloat("_Metallic", 0.0f);
        mat.SetFloat("_SpecularHighlights", 1.0f);
        mat.SetFloat("_EnvironmentReflections", 1.0f);
    }

    private static void AutoFix()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        FixAllMaterials();
    }
}

public class BoatsModelPostprocessor : AssetPostprocessor
{
    private Material OnAssignMaterialModel(Material material, Renderer renderer)
    {
        if (!string.IsNullOrEmpty(assetPath))
        {
            if (assetPath.StartsWith("Assets/Boats/Models/Boat_", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i <= 5; i++)
                {
                    if (assetPath.IndexOf($"Boat_{i}", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Material boatMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Boats/Materials/Boat_{i}_Mat.mat");
                        if (boatMat != null)
                        {
                            FixFloodedGroundsMaterials.UpgradeSingleBoatMaterial(boatMat, i);
                            return boatMat;
                        }
                    }
                }
            }
            else if (assetPath.IndexOf("rust_key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Material rkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rust Key/Materials/rust_key_autodesk.mat");
                if (rkMat != null)
                {
                    FixFloodedGroundsMaterials.UpgradeRustKeyMaterial(rkMat);
                    return rkMat;
                }
            }
        }
        return null;
    }
}
