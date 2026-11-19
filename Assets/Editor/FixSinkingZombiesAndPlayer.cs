using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StarterAssets;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class FixSinkingZombiesAndPlayer
{

    [MenuItem("Tools/Fix Sinking Glitch (Zombies & Player)")]
    public static void ExecuteFix()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("=== [AntiSinkFix] Running Anti-Sinking & Combat Glitch Fix ===");

        int groundMask = ~((1 << 2) | (1 << 8));

        // 1. Clean and snap all zombies in the active scene
        var zombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        int fixedCount = 0;

        foreach (var z in zombies)
        {
            Undo.RecordObject(z.gameObject, "Fix Zombie Sinking");
            Undo.RecordObject(z.transform, "Fix Zombie Sinking Transform");

            // Remove rogue ArmatureActionTrigger
            var triggers = z.GetComponentsInChildren<ArmatureActionTrigger>(true);
            foreach (var t in triggers)
            {
                Undo.DestroyObjectImmediate(t);
                Debug.Log($"[AntiSinkFix] Removed rogue ArmatureActionTrigger from '{z.gameObject.name}'.");
            }

            // Find ground height
            float groundY = -999f;
            if (Physics.Raycast(z.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
            if (Terrain.activeTerrains != null)
            {
                foreach (var terr in Terrain.activeTerrains)
                {
                    if (terr == null || terr.terrainData == null) continue;
                    Vector3 tPos = terr.transform.position;
                    Vector3 tSize = terr.terrainData.size;
                    Vector3 zPos = z.transform.position;
                    if (zPos.x >= tPos.x && zPos.x <= tPos.x + tSize.x &&
                        zPos.z >= tPos.z && zPos.z <= tPos.z + tSize.z)
                    {
                        float tY = terr.SampleHeight(zPos) + tPos.y;
                        if (tY > groundY) groundY = tY;
                    }
                }
            }

            if (groundY > -900f && z.transform.position.y < groundY + 0.02f)
            {
                Vector3 oldPos = z.transform.position;
                z.transform.position = new Vector3(oldPos.x, groundY, oldPos.z);
                fixedCount++;
                Debug.Log($"[AntiSinkFix] Snapped sunken zombie '{z.gameObject.name}' from Y={oldPos.y:F2} up to ground Y={groundY:F2}");
            }
        }

        // 2. Check Player
        var player = GameObject.Find("PlayerCapsule");
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Fix Player Grounding");
            float playerGroundY = -999f;
            if (Physics.Raycast(player.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit pHit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            {
                playerGroundY = pHit.point.y;
            }
            if (Terrain.activeTerrain != null)
            {
                float ptY = Terrain.activeTerrain.SampleHeight(player.transform.position) + Terrain.activeTerrain.transform.position.y;
                if (ptY > playerGroundY) playerGroundY = ptY;
            }

            if (playerGroundY > -900f && player.transform.position.y < playerGroundY)
            {
                Vector3 pOld = player.transform.position;
                player.transform.position = new Vector3(pOld.x, playerGroundY, pOld.z);
                Debug.Log($"[AntiSinkFix] Snapped sunken player from Y={pOld.y:F2} up to ground Y={playerGroundY:F2}");
            }
        }

        // 3. Clean up Zombie_Enemy.prefab if it has ArmatureActionTrigger
        string prefabPath = "Assets/ZombieMale_AAB/Prefabs/URP/Zombie_Enemy.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            var pTriggers = prefab.GetComponentsInChildren<ArmatureActionTrigger>(true);
            if (pTriggers.Length > 0)
            {
                foreach (var pt in pTriggers)
                {
                    Object.DestroyImmediate(pt, true);
                }
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AntiSinkFix] Removed ArmatureActionTrigger from '{prefabPath}'.");
            }
        }

        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"=== [AntiSinkFix] Complete! Fixed {fixedCount} zombies, verified player grounding and cleaned triggers. ===");
    }
}
