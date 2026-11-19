using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class FullArmatureFixAndSnapping
{

    [MenuItem("Tools/Full Character Fix and Ground Snap")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogError("[FullFix] PlayerCapsule not found!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Full Character Fix");

        // 1. Inspect and log all components in player hierarchy
        var allComponents = player.GetComponentsInChildren<Component>(true);
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;
            Debug.Log($"[FullFix Component] on '{comp.gameObject.name}': {comp.GetType().FullName}");
        }

        // 2. Destroy any CharacterController or Rigidbody on any child of PlayerCapsule
        foreach (var cc in player.GetComponentsInChildren<CharacterController>(true))
        {
            if (cc.gameObject != player)
            {
                Debug.LogWarning($"[FullFix] Destroying rogue CharacterController on '{cc.gameObject.name}'");
                Object.DestroyImmediate(cc);
            }
        }
        foreach (var rb in player.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb.gameObject != player)
            {
                Debug.LogWarning($"[FullFix] Destroying rogue Rigidbody on '{rb.gameObject.name}'");
                Object.DestroyImmediate(rb);
            }
        }
        // Destroy any non-trigger Colliders on Armature or its children so player doesn't self-collide
        var armature = player.transform.Find("Armature");
        if (armature != null)
        {
            foreach (var col in armature.GetComponentsInChildren<Collider>(true))
            {
                Debug.LogWarning($"[FullFix] Destroying internal collider on '{col.gameObject.name}' ({col.GetType().Name})");
                Object.DestroyImmediate(col);
            }
        }

        // 3. Set proper layers
        // Ensure player and all children are on layer 2 (Ignore Raycast) or Layer 8 (Player)
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0) playerLayer = 8; // Layer 8 is what PlayerCapsule currently uses
        player.layer = playerLayer;
        if (armature != null)
        {
            SetLayerRecursively(armature.gameObject, 2); // Ignore Raycast for mesh/visuals
            armature.localPosition = Vector3.zero;
            armature.localRotation = Quaternion.identity;
            armature.localScale = Vector3.one;
        }

        // 4. Configure CharacterController on PlayerCapsule
        var playerCC = player.GetComponent<CharacterController>();
        if (playerCC != null)
        {
            // Starter assets standard capsule: height 1.8 - 2.0, center at y = height/2 = 1.0 (or 0.9)
            // When center = (0, 1.0, 0) and height = 2.0, the base of the capsule is at local Y = 0.0 (ground level)
            playerCC.height = 2.0f;
            playerCC.center = new Vector3(0f, 1.0f, 0f);
            playerCC.radius = 0.45f;
            playerCC.skinWidth = 0.05f;
            playerCC.stepOffset = 0.4f;
            playerCC.slopeLimit = 55f;
            playerCC.minMoveDistance = 0f;
            Debug.Log("[FullFix] CharacterController configured: height=2.0, center=(0, 1.0, 0), radius=0.45, base at local y=0");
        }

        // 5. Raycast down to find bridge / terrain floor and snap PlayerCapsule to touch the ground
        int groundMask = ~((1 << playerLayer) | (1 << 2));
        RaycastHit hit;
        if (Physics.Raycast(player.transform.position + Vector3.up * 2f, Vector3.down, out hit, 100f, groundMask))
        {
            float targetY = hit.point.y;
            Vector3 oldPos = player.transform.position;
            // The capsule base is at local Y = 0 (since center.y = 1.0, height = 2.0 => bottom = 1.0 - 1.0 = 0.0)
            player.transform.position = new Vector3(oldPos.x, targetY, oldPos.z);
            Debug.Log($"[FullFix] Snapped PlayerCapsule from Y={oldPos.y} to ground floor Y={targetY} (hit '{hit.collider.gameObject.name}')");
        }

        // 6. Hook up Animator & StarterAssetsThirdPerson.controller
        if (armature != null)
        {
            var anim = armature.GetComponent<Animator>();
            if (anim == null) anim = armature.gameObject.AddComponent<Animator>();

            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller"
            );
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Debug.Log($"[FullFix] Assigned '{ctrl.name}' to Armature Animator.");
            }

            // Ensure ArmatureActionTrigger is present and configured
            var trigger = armature.GetComponent<ArmatureActionTrigger>();
            if (trigger == null) trigger = armature.gameObject.AddComponent<ArmatureActionTrigger>();
        }

        // 7. Configure FirstPersonController
        var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
        if (fpc != null)
        {
            if (armature != null)
            {
                fpc.CharacterAnimator = armature.GetComponent<Animator>();
            }
            fpc.MoveSpeed = 4.0f;
            fpc.SprintSpeed = 7.0f;
            fpc.RotationSpeed = 1.2f;
            fpc.JumpHeight = 1.2f;
            fpc.Gravity = -15.0f;
            fpc.GroundedOffset = 0.05f;
            fpc.GroundedRadius = 0.35f;
            fpc.GroundLayers = groundMask;
            Debug.Log($"[FullFix] FirstPersonController configured with GroundLayers mask={groundMask}");
        }

        // 8. Save scene and assets
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[FullFix] Scene and Assets saved successfully!");
        }

        Debug.Log("=== [FullFix] Character setup, ground snapping, and animation configuration COMPLETE! ===");
    }

    private static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
