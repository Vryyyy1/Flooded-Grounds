using UnityEngine;
using UnityEditor;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class CheckBridgeFloor
{

    [MenuItem("Tools/Check Bridge Floor and Zombies")]
    public static void Check()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("==================== ZOMBIE AND FLOOR DIAGNOSTICS ====================");

        // Check active scene zombies
        var zombies = Object.FindObjectsByType<StarterAssets.ZombieAI>(FindObjectsSortMode.None);
        Debug.Log($"Found {zombies.Length} active ZombieAI in scene.");

        foreach (var z in zombies)
        {
            float groundY = z.GetGroundHeight(z.transform.position);
            var cc = z.GetComponent<CharacterController>();
            Debug.Log($"[ZombieDiag] '{z.gameObject.name}': Pos={z.transform.position}, GroundY={groundY}, DiffY={z.transform.position.y - groundY}, CC.isGrounded={(cc != null ? cc.isGrounded.ToString() : "NoCC")}");

            // Check SkinnedMeshRenderers
            var smrs = z.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                Debug.Log($"[ZombieDiag] SMR '{smr.gameObject.name}': bounds.min={smr.bounds.min}, bounds.max={smr.bounds.max}, localPos={smr.transform.localPosition}");
            }

            // Check root bone
            var rootBone = z.transform.Find("root");
            if (rootBone != null)
            {
                Debug.Log($"[ZombieDiag] 'root' bone: localPos={rootBone.localPosition}, worldPos={rootBone.position}");
            }
            break; // Just one is enough
        }

        // Also check Zombie_Enemy prefab
        string prefabPath = "Assets/ZombieMale_AAB/Prefabs/URP/Zombie_Enemy.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            var pcc = prefab.GetComponent<CharacterController>();
            Debug.Log($"[ZombieDiag] Prefab '{prefab.name}': CC.center={pcc.center}, CC.height={pcc.height}, CC.minMoveDistance={pcc.minMoveDistance}");
            var smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                Debug.Log($"[ZombieDiag] Prefab SMR '{smr.name}' localPos={smr.transform.localPosition}, localScale={smr.transform.localScale}");
            }
        }
    }
}
