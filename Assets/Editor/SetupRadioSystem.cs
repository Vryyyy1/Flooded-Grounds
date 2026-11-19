using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StarterAssets;

[InitializeOnLoad]
public static class SetupRadioSystem
{
    private const string SessionKey = "RadioSystemConfigured_v2";
    private const string RadioPrefabPath = "Assets/AK STUDIO ART/Radio/Prefabs/Radio.prefab";
    private const string ResourcesRadioPrefabPath = "Assets/Resources/Radio.prefab";

    static SetupRadioSystem()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            Execute();
        };
    }

    [MenuItem("Tools/Setup Radio (Light & E-Key Dialogue)")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

        // 1. Configure Radio Prefabs
        ConfigureRadioPrefabAsset(RadioPrefabPath);
        ConfigureRadioPrefabAsset(ResourcesRadioPrefabPath);

        // 2. Configure any Radio objects in the currently open scene (with reduced subtle light)
        int count = 0;
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            if (RadioInteraction.IsRadioObject(t.gameObject))
            {
                RadioInteraction ri = t.GetComponentInParent<RadioInteraction>();
                if (ri == null)
                {
                    ri = t.gameObject.AddComponent<RadioInteraction>();
                }
                ri.activeLightIntensity = 0.45f;
                ri.activeLightRange = 1.6f;
                ri.SetupRadioLight();
                EditorUtility.SetDirty(t.gameObject);
                count++;
            }
        }

        // 3. Reduce light intensity on all GunPickup instances in the open scene
        GunPickup[] allGuns = Object.FindObjectsByType<GunPickup>(FindObjectsSortMode.None);
        foreach (GunPickup gp in allGuns)
        {
            if (gp == null) continue;
            gp.glowIntensity = 0.25f;
            gp.glowRange = 0.9f;
            Light l = gp.lootGlow != null ? gp.lootGlow : gp.GetComponentInChildren<Light>();
            if (l != null)
            {
                l.intensity = 0.25f;
                l.range = 0.9f;
                EditorUtility.SetDirty(l);
            }
            EditorUtility.SetDirty(gp);
        }

        // 4. Boost Flashlight intensity & range on Player in the open scene
        FlashlightController[] flashlights = Object.FindObjectsByType<FlashlightController>(FindObjectsSortMode.None);
        foreach (FlashlightController fc in flashlights)
        {
            if (fc == null) continue;
            fc.beamIntensity = 32.0f;
            fc.beamRange = 60.0f;
            fc.outerSpotAngle = 68.0f;
            fc.innerSpotAngle = 44.0f;
            fc.ApplyBeamSettings();
            if (fc.flashlightLight != null) EditorUtility.SetDirty(fc.flashlightLight);
            EditorUtility.SetDirty(fc);
        }

        // 5. If no Radio exists in the open scene yet, place one near the player so it's immediately ready
        if (count == 0)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadioPrefabPath);
            if (prefab != null)
            {
                Vector3 spawnPos = new Vector3(484.5f, 19.4f, 155.2f);
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null) player = GameObject.Find("PlayerCapsule");
                if (player != null)
                {
                    spawnPos = player.transform.position + player.transform.forward * 2.5f + Vector3.up * 0.5f;
                    if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~((1 << 2) | (1 << 8))))
                    {
                        spawnPos = hit.point;
                    }
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance != null)
                {
                    instance.name = "Radio";
                    instance.transform.position = spawnPos;
                    RadioInteraction ri = instance.GetComponent<RadioInteraction>();
                    if (ri == null) ri = instance.AddComponent<RadioInteraction>();
                    ri.activeLightIntensity = 0.45f;
                    ri.activeLightRange = 1.6f;
                    ri.SetupRadioLight();
                    EditorUtility.SetDirty(instance);
                    count++;
                }
            }
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupRadioSystem] Updated Radio light (subtle), Gun lights (reduced), and Flashlight (boosted).");
    }

    private static void ConfigureRadioPrefabAsset(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        RadioInteraction ri = prefab.GetComponent<RadioInteraction>();
        if (ri == null)
        {
            ri = prefab.AddComponent<RadioInteraction>();
        }
        ri.activeLightIntensity = 0.45f;
        ri.activeLightRange = 1.6f;
        EditorUtility.SetDirty(prefab);
    }
}
