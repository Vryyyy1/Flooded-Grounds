using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class SetupArmatureAction
{
    private const string ControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
    private const string ClipPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Armature_Attack_Punch.anim";

    static SetupArmatureAction()
    {
        EditorApplication.delayCall += Execute;
    }

    [MenuItem("Tools/Setup Armature Action Animation")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("[ArmatureAction] Setting up Action Animation...");

        // 1. Create or load the Attack AnimationClip
        AnimationClip clip = CreatePunchAnimationClip();

        // 2. Add Attack state and transitions to StarterAssetsThirdPerson.controller
        SetupAnimatorController(clip);

        // 3. Ensure Player or Armature in scene has ArmatureActionTrigger
        SetupSceneCharacter();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ArmatureAction] Successfully configured Action Animation!");
    }

    public static AnimationClip CreatePunchAnimationClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Armature_Attack_Punch";
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // Set Humanoid Muscle Curves for a punch / strike action:
        // Duration: 0.5 seconds (15 frames at 30fps)
        // 0.0s: Ready stance
        // 0.15s: Wind-up (pull right arm back, twist chest)
        // 0.25s: Forward thrust / punch impact (extend right arm, chest rotation)
        // 0.5s: Return to neutral

        // Chest Twist Left-Right (Muscle index 4)
        AddMuscleCurve(clip, "Spine", "Chest Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.15f, -0.35f), // Twist back
            new Keyframe(0.25f, 0.45f),  // Forward punch twist
            new Keyframe(0.5f, 0.0f)     // Return
        });

        // Chest Front-Back (Muscle index 3)
        AddMuscleCurve(clip, "Spine", "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.15f, -0.1f),
            new Keyframe(0.25f, 0.25f), // Lean forward into punch
            new Keyframe(0.5f, 0.0f)
        });

        // Right Arm Down-Up
        AddMuscleCurve(clip, "RightArm", "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.2f),
            new Keyframe(0.15f, 0.15f),
            new Keyframe(0.25f, 0.05f),
            new Keyframe(0.5f, -0.2f)
        });

        // Right Arm Front-Back
        AddMuscleCurve(clip, "RightArm", "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.15f, -0.5f), // Draw back
            new Keyframe(0.25f, 0.7f),  // Punch forward
            new Keyframe(0.5f, 0.0f)
        });

        // Right Forearm Stretch
        AddMuscleCurve(clip, "RightArm", "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.3f),
            new Keyframe(0.15f, -0.7f), // Bend elbow back
            new Keyframe(0.25f, 0.9f),  // Extend arm forward
            new Keyframe(0.5f, -0.3f)
        });

        // Right Hand Down-Up
        AddMuscleCurve(clip, "RightArm", "Right Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, 0.1f),
            new Keyframe(0.5f, 0.0f)
        });

        // Close right fist (finger muscles)
        string[] fingerMuscles = new string[]
        {
            "Right Hand.Right Thumb 1 Stretched", "Right Hand.Right Thumb 2 Stretched",
            "Right Hand.Right Index 1 Stretched", "Right Hand.Right Index 2 Stretched",
            "Right Hand.Right Middle 1 Stretched", "Right Hand.Right Middle 2 Stretched",
            "Right Hand.Right Ring 1 Stretched", "Right Hand.Right Ring 2 Stretched",
            "Right Hand.Right Little 1 Stretched", "Right Hand.Right Little 2 Stretched"
        };

        foreach (var muscle in fingerMuscles)
        {
            AddRawMuscleCurve(clip, muscle, new Keyframe[]
            {
                new Keyframe(0.0f, 0.0f),
                new Keyframe(0.15f, -0.8f), // Clench fist
                new Keyframe(0.35f, -0.8f),
                new Keyframe(0.5f, 0.0f)
            });
        }

        // Left Arm guard position
        AddMuscleCurve(clip, "LeftArm", "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.2f, 0.3f), // Guard face
            new Keyframe(0.5f, 0.0f)
        });

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void AddMuscleCurve(AnimationClip clip, string group, string muscleName, Keyframe[] keys)
    {
        AnimationCurve curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        }

        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(Animator),
            propertyName = muscleName
        };

        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void AddRawMuscleCurve(AnimationClip clip, string propertyName, Keyframe[] keys)
    {
        AnimationCurve curve = new AnimationCurve(keys);
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(Animator),
            propertyName = propertyName
        };
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    public static void SetupAnimatorController(AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogWarning($"[ArmatureAction] Controller not found at {ControllerPath}");
            return;
        }

        // Add 'Attack' Trigger parameter if missing
        bool hasAttackParam = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "Attack")
            {
                hasAttackParam = true;
                break;
            }
        }

        if (!hasAttackParam)
        {
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            Debug.Log("[ArmatureAction] Added 'Attack' trigger parameter to AnimatorController.");
        }

        // Check if 'Attack' state already exists in Base Layer
        var rootSm = controller.layers[0].stateMachine;
        AnimatorState attackState = null;
        AnimatorState defaultLocomotionState = null;

        foreach (var childState in rootSm.states)
        {
            if (childState.state.name == "Attack")
            {
                attackState = childState.state;
            }
            if (childState.state.name.Contains("Idle") || childState.state.name.Contains("Blend"))
            {
                defaultLocomotionState = childState.state;
            }
        }

        if (attackState == null)
        {
            attackState = rootSm.AddState("Attack", new Vector3(450, 320, 0));
            attackState.motion = clip;
            attackState.writeDefaultValues = true;
            Debug.Log("[ArmatureAction] Created 'Attack' state in AnimatorController.");
        }
        else
        {
            attackState.motion = clip;
        }

        // Add AnyState -> Attack transition
        bool hasAnyStateTrans = false;
        foreach (var t in rootSm.anyStateTransitions)
        {
            if (t.destinationState == attackState)
            {
                hasAnyStateTrans = true;
                break;
            }
        }

        if (!hasAnyStateTrans)
        {
            var trans = rootSm.AddAnyStateTransition(attackState);
            trans.duration = 0.08f;
            trans.hasExitTime = false;
            trans.hasFixedDuration = true;
            trans.canTransitionToSelf = false;
            trans.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            Debug.Log("[ArmatureAction] Added AnyState -> Attack transition.");
        }

        // Add Attack -> Locomotion exit transition
        if (defaultLocomotionState != null)
        {
            bool hasReturnTrans = false;
            foreach (var t in attackState.transitions)
            {
                if (t.destinationState == defaultLocomotionState)
                {
                    hasReturnTrans = true;
                    break;
                }
            }

            if (!hasReturnTrans)
            {
                var returnTrans = attackState.AddTransition(defaultLocomotionState);
                returnTrans.duration = 0.15f;
                returnTrans.hasExitTime = true;
                returnTrans.exitTime = 0.85f;
                returnTrans.hasFixedDuration = true;
                Debug.Log("[ArmatureAction] Added Attack -> Locomotion return transition.");
            }
        }

        EditorUtility.SetDirty(controller);
    }

    public static void SetupSceneCharacter()
    {
        // 1. Remove rogue ArmatureActionTrigger from any non-player objects in the scene
        var allTriggers = Object.FindObjectsByType<ArmatureActionTrigger>(FindObjectsSortMode.None);
        foreach (var t in allTriggers)
        {
            bool isPlayer = t.CompareTag("Player") || t.transform.root.CompareTag("Player") || t.transform.root.name.Contains("Player");
            if (!isPlayer)
            {
                Undo.DestroyObjectImmediate(t);
                Debug.Log($"[ArmatureAction] Cleaned rogue ArmatureActionTrigger from '{t.gameObject.name}'.");
            }
        }

        // 2. Only configure player
        var player = GameObject.Find("PlayerCapsule");
        if (player != null)
        {
            var anim = player.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                var trigger = anim.gameObject.GetComponent<ArmatureActionTrigger>();
                if (trigger == null)
                {
                    trigger = Undo.AddComponent<ArmatureActionTrigger>(anim.gameObject);
                    Debug.Log($"[ArmatureAction] Added ArmatureActionTrigger to player '{anim.gameObject.name}'.");
                }
            }
        }
    }
}
