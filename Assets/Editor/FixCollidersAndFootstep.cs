using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// One-click fix for:
///   1. Negative-scale BoxColliders  →  abs() the scale on their parent transforms
///   2. Missing OnFootstep receiver  →  adds FootstepHandler to the Armature object
/// Run via Tools → Fix Colliders & Footstep Receiver
/// Also runs once automatically on Editor load (edit mode only).
/// </summary>
[InitializeOnLoad]
public class FixCollidersAndFootstep
{
    static FixCollidersAndFootstep()
    {
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Execute();
    }

    [MenuItem("Tools/Fix Colliders and Footstep Receiver")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

        int colliderFixed = 0;
        int footstepFixed = 0;

        // ── 1. Fix negative-scale BoxColliders & parents ───────────────────
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
        foreach (BoxCollider bc in allBoxes)
        {
            if (bc.size.x < 0 || bc.size.y < 0 || bc.size.z < 0)
            {
                Undo.RecordObject(bc, "Fix Negative Box Size");
                bc.size = new Vector3(Mathf.Abs(bc.size.x), Mathf.Abs(bc.size.y), Mathf.Abs(bc.size.z));
                EditorUtility.SetDirty(bc);
                colliderFixed++;
            }

            Transform t = bc.transform;
            while (t != null)
            {
                Vector3 ls = t.localScale;
                if (ls.x < 0 || ls.y < 0 || ls.z < 0)
                {
                    Undo.RecordObject(t, "Fix Negative Scale");
                    t.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
                    EditorUtility.SetDirty(t);
                    colliderFixed++;
                    Debug.Log($"[FixColliders] Fixed negative scale on '{t.name}' → {t.localScale}");
                }
                t = t.parent;
            }
        }

        // ── 2. Add FootstepHandler to Armature ──────────────────────────────
        // Find every GameObject named "Armature" that also has an Animator
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            bool isArmature = go.name.IndexOf("Armature", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || go.name.IndexOf("Skeleton", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isArmature) continue;

            Animator anim = go.GetComponent<Animator>();
            if (anim == null) continue;

            // Only add if not already present
            FootstepHandler existing = go.GetComponent<FootstepHandler>();
            if (existing == null)
            {
                Undo.AddComponent<FootstepHandler>(go);
                footstepFixed++;
                Debug.Log($"[FixFootstep] Added FootstepHandler to '{go.name}'");
            }
        }

        // ── 3. Also check the PlayerCapsule root (some rigs animate from there) ─
        GameObject player = GameObject.Find("PlayerCapsule");
        if (player != null)
        {
            // Walk up from PlayerCapsule — check it and immediate children for Animator
            Animator[] animators = player.GetComponentsInChildren<Animator>(true);
            foreach (Animator a in animators)
            {
                if (a.gameObject.GetComponent<FootstepHandler>() == null)
                {
                    Undo.AddComponent<FootstepHandler>(a.gameObject);
                    footstepFixed++;
                    Debug.Log($"[FixFootstep] Added FootstepHandler to '{a.gameObject.name}' (child of PlayerCapsule)");
                }
            }
        }

        // ── Save ─────────────────────────────────────────────────────────────
        if (colliderFixed > 0 || footstepFixed > 0)
        {
            if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[FixCollidersAndFootstep] Done. Colliders fixed: {colliderFixed}, FootstepHandlers added: {footstepFixed}. Scene saved.");
            }
        }
        else
        {
            Debug.Log("[FixCollidersAndFootstep] Nothing to fix — all clean.");
        }
    }
}

