using UnityEngine;
using UnityEditor;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class InspectAndFixGrass
{

    [MenuItem("Tools/Inspect and Fix Grass Color")]
    public static void InspectAndFix()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("=== [GrassFix] Inspecting Grass ===");

        // 1. Fix NAT_Grass.mat
        Material grassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Flooded_Grounds/Content/Materials/NAT_Grass.mat");
        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Flooded_Grounds/Content/Textures/NAT_Grass1_AS.tif");

        if (grassMat != null)
        {
            Undo.RecordObject(grassMat, "Fix Grass Material");
            Debug.Log($"[GrassFix] NAT_Grass.mat current _BaseMap: {(grassMat.mainTexture != null ? grassMat.mainTexture.name : "NULL")}, _BaseColor: {grassMat.color}");

            if (grassTex != null)
            {
                grassMat.mainTexture = grassTex;
                grassMat.SetTexture("_BaseMap", grassTex);
                grassMat.SetTexture("_MainTex", grassTex);
                Debug.Log($"[GrassFix] Assigned NAT_Grass1_AS texture to NAT_Grass.mat");
            }

            // Set color to vibrant natural grass tint (white so texture shows true colors, or lush green)
            grassMat.color = Color.white;
            grassMat.SetColor("_BaseColor", Color.white);
            grassMat.SetFloat("_Cutoff", 0.35f); // Ensure clean alpha clipping for grass blades

            EditorUtility.SetDirty(grassMat);
            Debug.Log($"[GrassFix] Updated NAT_Grass.mat color to Color.white and cutoff to 0.35");
        }

        // 2. Check and fix Terrain Detail Prototypes (Grass)
        var terrains = Terrain.activeTerrains;
        Debug.Log($"[GrassFix] Found {terrains.Length} active terrains in scene.");

        foreach (var terrain in terrains)
        {
            if (terrain.terrainData == null) continue;

            var tData = terrain.terrainData;
            Undo.RecordObject(tData, "Fix Terrain Grass Colors");

            var details = tData.detailPrototypes;
            Debug.Log($"[GrassFix] Terrain '{terrain.name}' has {details.Length} detail prototypes.");

            bool detailsChanged = false;
            for (int i = 0; i < details.Length; i++)
            {
                var proto = details[i];
                Debug.Log($"[GrassFix] Detail {i}: Texture={proto.prototypeTexture?.name}, Prefab={proto.prototype?.name}, HealthyColor={proto.healthyColor}, DryColor={proto.dryColor}");

                // Check if colors are washed out / gray
                float hSat = GetSaturation(proto.healthyColor);
                float dSat = GetSaturation(proto.dryColor);

                // Healthy green: (60/255, 120/255, 40/255), Dry green/golden: (90/255, 125/255, 45/255)
                proto.healthyColor = new Color(0.32f, 0.55f, 0.22f, 1.0f);
                proto.dryColor = new Color(0.45f, 0.58f, 0.28f, 1.0f);

                if (proto.prototypeTexture == null && proto.prototype == null && grassTex != null)
                {
                    proto.prototypeTexture = grassTex;
                }

                details[i] = proto;
                detailsChanged = true;
            }

            if (detailsChanged)
            {
                tData.detailPrototypes = details;
                EditorUtility.SetDirty(tData);
                terrain.Flush();
                Debug.Log($"[GrassFix] Updated detail grass colors on '{terrain.name}' to lush green!");
            }
        }

        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        Debug.Log("=== [GrassFix] Complete ===");
    }

    private static float GetSaturation(Color c)
    {
        Color.RGBToHSV(c, out _, out float s, out _);
        return s;
    }
}
