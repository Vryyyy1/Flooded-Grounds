using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class FixArmatureSetup
{
    private const string ControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";

    [MenuItem("Tools/Fix Armature Character (Animations & Floating)")]
    public static void ExecuteFix()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("=== [ArmatureFix] Executing Complete Fix ===");

        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogError("[ArmatureFix] 'PlayerCapsule' not found in active scene!");
            return;
        }

        Undo.RecordObject(player, "Fix PlayerCapsule");

        // 1. Locate Armature child
        Transform armature = player.transform.Find("Armature");
        if (armature == null)
        {
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Armature") { armature = t; break; }
            }
        }

        if (armature == null)
        {
            Debug.LogError("[ArmatureFix] 'Armature' child not found under PlayerCapsule!");
            return;
        }

        Undo.RecordObject(armature, "Fix Armature Transform");

        // Set Armature local position directly on floor level of CharacterController
        // In Starter Assets, Armature origin (0,0,0) corresponds to feet on ground
        armature.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        armature.localRotation = Quaternion.identity;
        armature.localScale = Vector3.one;
        Debug.Log("[ArmatureFix] Reset Armature localPosition to (0, 0, 0)");

        // 2. Remove or disable any colliders inside Armature/Skeleton that cause self-collision/floating
        var childColliders = armature.GetComponentsInChildren<Collider>(true);
        foreach (var col in childColliders)
        {
            Undo.RecordObject(col, "Disable Armature Child Collider");
            col.enabled = false;
            Debug.Log($"[ArmatureFix] Disabled internal collider on '{col.gameObject.name}' to stop CharacterController self-collision.");
        }

        // Set Armature and all children to Player layer (layer 8) or Ignore Raycast (layer 2)
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0) playerLayer = 2; // fallback to Ignore Raycast
        SetLayerRecursively(armature.gameObject, playerLayer);
        Debug.Log($"[ArmatureFix] Set Armature hierarchy to layer '{LayerMask.LayerToName(playerLayer)}' ({playerLayer}).");

        // 3. Configure Animator on Armature
        var anim = armature.GetComponent<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(armature.gameObject);

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller != null)
        {
            Undo.RecordObject(anim, "Assign Animator Controller");
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Debug.Log($"[ArmatureFix] Assigned '{controller.name}' to Armature Animator with applyRootMotion=false.");
        }
        else
        {
            Debug.LogError($"[ArmatureFix] AnimatorController not found at {ControllerPath}!");
        }

        // 4. Configure CharacterController on PlayerCapsule
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            Undo.RecordObject(cc, "Configure CharacterController");
            cc.center = new Vector3(0f, 0.93f, 0f);
            cc.height = 2.0f;
            cc.radius = 0.5f;
            cc.skinWidth = 0.05f;
            cc.stepOffset = 0.5f;
            cc.slopeLimit = 60f;
            cc.minMoveDistance = 0f;
            Debug.Log("[ArmatureFix] CharacterController configured (center=0.93, height=2.0, skinWidth=0.05).");
        }

        // 5. Configure FirstPersonController on PlayerCapsule
        var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
        if (fpc != null)
        {
            Undo.RecordObject(fpc, "Configure FirstPersonController");
            fpc.CharacterAnimator = anim;
            fpc.MoveSpeed = 4.0f;
            fpc.SprintSpeed = 7.0f;
            fpc.RotationSpeed = 1.2f;
            fpc.JumpHeight = 1.3f;
            fpc.Gravity = -15.0f;
            fpc.GroundedOffset = -0.05f;
            fpc.GroundedRadius = 0.28f;

            // Ensure GroundLayers strictly ignores player itself and Ignore Raycast
            fpc.GroundLayers = ~((1 << 2) | (1 << 5) | (1 << playerLayer) | (1 << player.layer));
            Debug.Log($"[ArmatureFix] FirstPersonController hooked up to Animator, GroundLayers mask set to {fpc.GroundLayers.value}.");
        }

        // 6. Ensure ArmatureActionTrigger is present
        var actionTrigger = armature.GetComponent<ArmatureActionTrigger>();
        if (actionTrigger == null)
        {
            actionTrigger = Undo.AddComponent<ArmatureActionTrigger>(armature.gameObject);
            Debug.Log("[ArmatureFix] Added ArmatureActionTrigger to Armature.");
        }

        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        Debug.Log("=== [ArmatureFix] Complete! Standing, walking, sprinting animations ready and floating fixed! ===");
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
