using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.IO;
using StarterAssets;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class SetupZombieSystem
{
    private const string ZombieFbxPath = "Assets/ZombieMale_AAB/Model/ZombieMale_AAB.fbx";
    private const string ZombieUrpPrefabPath = "Assets/ZombieMale_AAB/Prefabs/URP/ZombieMale_AAB_URP.prefab";
    private const string ZombieEnemyPrefabPath = "Assets/ZombieMale_AAB/Prefabs/URP/Zombie_Enemy.prefab";

    private const string AnimFolder = "Assets/ZombieMale_AAB/Animations";
    private const string WalkClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Walk.anim";
    private const string ChaseClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Chase.anim";
    private const string IdleClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Idle.anim";
    private const string AttackClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Attack.anim";
    private const string HitClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Hit.anim";
    private const string DeathClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Death.anim";
    private const string ZombieRunClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Run.anim";
    private const string MutantRunClipPath = "Assets/ZombieMale_AAB/Animations/Mutant_Run.anim";
    private const string ZombieScreamingClipPath = "Assets/ZombieMale_AAB/Animations/Zombie_Screaming.anim";
    private const string MutantSwipingClipPath = "Assets/ZombieMale_AAB/Animations/Mutant_Swiping.anim";
    private const string ControllerPath = "Assets/ZombieMale_AAB/Animations/ZombieAnimator.controller";

    private const string PlayerShootClipPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Player_Shoot.anim";
    private const string PlayerPickupClipPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Player_Pickup.anim";
    private const string PlayerControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";

    private static readonly string[] ClawMuscles = new string[]
    {
        "Right Hand.Right Thumb 1 Stretched", "Right Hand.Right Thumb 2 Stretched",
        "Right Hand.Right Index 1 Stretched", "Right Hand.Right Index 2 Stretched",
        "Right Hand.Right Middle 1 Stretched", "Right Hand.Right Middle 2 Stretched",
        "Left Hand.Left Thumb 1 Stretched", "Left Hand.Left Thumb 2 Stretched",
        "Left Hand.Left Index 1 Stretched", "Left Hand.Left Index 2 Stretched",
        "Left Hand.Left Middle 1 Stretched", "Left Hand.Left Middle 2 Stretched"
    };

    // static SetupZombieSystem() auto-execution removed to prevent overriding during playmode

    [MenuItem("Tools/Setup Zombies (Animations, AI & Spawner)")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("=== [ZombieSetup] Configuring Zombie System ===");

        // 1. Ensure animation folder exists
        if (!AssetDatabase.IsValidFolder("Assets/ZombieMale_AAB/Animations"))
        {
            AssetDatabase.CreateFolder("Assets/ZombieMale_AAB", "Animations");
        }

        // 2. Create custom Humanoid Zombie Animation Clips (Authentic Shambling Walk, Predatory Chase, Idle, Visceral Attack)
        AnimationClip walkClip = CreateZombieWalkClip();
        AnimationClip chaseClip = CreateZombieChaseClip();
        AnimationClip idleClip = CreateZombieIdleClip();
        AnimationClip attackClip = CreateZombieAttackClip();
        AnimationClip hitClip = CreateZombieHitClip();
        AnimationClip deathClip = CreateZombieDeathClip();
        AnimationClip zombieRunClip = CreateZombieRunClip();
        AnimationClip mutantRunClip = CreateMutantRunClip();
        AnimationClip screamingClip = CreateZombieScreamingClip();
        AnimationClip mutantSwipeClip = CreateMutantSwipingClip();

        // 3. Create Player Animation Clips (Recoil Kick & Loot Presentation Pickup)
        AnimationClip shootClip = CreatePlayerShootClip();
        AnimationClip pickupClip = CreatePlayerPickupClip();

        // 4. Create / configure Zombie AnimatorController
        AnimatorController controller = SetupZombieAnimatorController(idleClip, walkClip, chaseClip, attackClip, hitClip, deathClip);

        // 5. Create / configure Zombie_Enemy.prefab
        GameObject zombiePrefab = ConfigureZombiePrefab(controller);

        // 6. Configure Player in Scene (Health, Combat, Shoot & Pickup Animations)
        ConfigurePlayerInScene();
        ConfigurePlayerAnimatorController(deathClip, shootClip, pickupClip);

        // 7. Place Spawners across all houses and forests in active Scene
        SetupSceneZombies(zombiePrefab);

        // Save
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ZombieSetup] Scene and assets saved successfully!");
        }

        Debug.Log("=== [ZombieSetup] Zombie System Setup COMPLETE! Ready to Play! ===");
    }

    private static void SetClipLoop(AnimationClip clip, bool loop, bool bakeY = true, bool bakeXZ = true)
    {
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.loopBlendPositionY = bakeY;
        settings.loopBlendPositionXZ = bakeXZ;
        settings.loopBlendOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalOrientation = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    public static AnimationClip CreateZombieWalkClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Walk";
            AssetDatabase.CreateAsset(clip, WalkClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 1.6s Authentic Shambling Drag Limp:
        // Torso slouched forward, heavy dead weight, asymmetrical sway
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.52f),
            new Keyframe(0.4f, 0.48f),
            new Keyframe(0.8f, 0.56f),
            new Keyframe(1.2f, 0.50f),
            new Keyframe(1.6f, 0.52f)
        });

        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.38f),
            new Keyframe(0.8f, 0.42f),
            new Keyframe(1.6f, 0.38f)
        });

        // Asymmetric limp sway: leans hard to bad leg side as it drags
        AddMuscleCurve(clip, "Spine Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.12f),
            new Keyframe(0.4f, 0.02f),
            new Keyframe(0.8f, -0.22f), // heavy limp lurch
            new Keyframe(1.2f, -0.06f),
            new Keyframe(1.6f, 0.12f)
        });

        AddMuscleCurve(clip, "Spine Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, -0.10f),
            new Keyframe(0.4f, 0.08f),
            new Keyframe(0.8f, 0.14f),
            new Keyframe(1.2f, -0.04f),
            new Keyframe(1.6f, -0.10f)
        });

        // Eerie broken neck sideways tilt and scanning head
        AddMuscleCurve(clip, "Head Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.26f),
            new Keyframe(0.8f, -0.18f),
            new Keyframe(1.6f, -0.26f)
        });

        AddMuscleCurve(clip, "Head Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.24f),
            new Keyframe(0.8f, 0.28f),
            new Keyframe(1.6f, 0.24f)
        });

        AddMuscleCurve(clip, "Head Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, -0.12f),
            new Keyframe(0.8f, 0.15f),
            new Keyframe(1.6f, -0.12f)
        });

        // Outstretched undead claw reach
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.58f),
            new Keyframe(0.8f, 0.65f),
            new Keyframe(1.6f, 0.58f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.44f),
            new Keyframe(0.8f, 0.52f),
            new Keyframe(1.6f, 0.44f)
        });

        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.12f),
            new Keyframe(0.8f, -0.05f),
            new Keyframe(1.6f, -0.12f)
        });

        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.24f),
            new Keyframe(0.8f, -0.18f),
            new Keyframe(1.6f, -0.24f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.32f),
            new Keyframe(0.8f, -0.22f),
            new Keyframe(1.6f, -0.32f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.42f),
            new Keyframe(0.8f, -0.30f),
            new Keyframe(1.6f, -0.42f)
        });

        AddMuscleCurve(clip, "Left Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.35f),
            new Keyframe(0.8f, -0.25f),
            new Keyframe(1.6f, -0.35f)
        });

        AddMuscleCurve(clip, "Right Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.40f),
            new Keyframe(0.8f, -0.30f),
            new Keyframe(1.6f, -0.40f)
        });

        // Claw fingers
        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, -0.55f),
                new Keyframe(0.8f, -0.65f),
                new Keyframe(1.6f, -0.55f)
            });
        }

        // Limping Legs: Left steps forward, Right drags stiffly
        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.32f),  // forward stride
            new Keyframe(0.4f, 0.10f),  // plant
            new Keyframe(0.8f, -0.28f), // push back
            new Keyframe(1.2f, -0.05f), // swing
            new Keyframe(1.6f, 0.32f)
        });

        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.45f), // knee flex
            new Keyframe(0.4f, 0.05f),  // contact
            new Keyframe(0.8f, -0.15f), // weight
            new Keyframe(1.2f, -0.50f), // lift
            new Keyframe(1.6f, -0.45f)
        });

        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, -0.28f), // trailing drag
            new Keyframe(0.4f, -0.18f),
            new Keyframe(0.8f, 0.20f),  // hitch drag forward
            new Keyframe(1.2f, 0.05f),
            new Keyframe(1.6f, -0.28f)
        });

        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.08f), // stiff damaged knee
            new Keyframe(0.4f, -0.12f),
            new Keyframe(0.8f, -0.15f),
            new Keyframe(1.2f, -0.05f),
            new Keyframe(1.6f, -0.08f)
        });

        AddMuscleCurve(clip, "Left Foot Up-Down", new Keyframe[]
        {
            new Keyframe(0.0f, 0.25f),  // toe up on stride
            new Keyframe(0.4f, -0.15f), // foot plants on ground
            new Keyframe(0.8f, -0.30f), // push off
            new Keyframe(1.2f, 0.12f),
            new Keyframe(1.6f, 0.25f)
        });

        AddMuscleCurve(clip, "Right Foot Up-Down", new Keyframe[]
        {
            new Keyframe(0.0f, -0.28f), // drags limp toe along the ground
            new Keyframe(0.8f, 0.08f),  // drags forward
            new Keyframe(1.6f, -0.28f)
        });

        SetClipLoop(clip, true);
        Debug.Log("[ZombieSetup] Created Zombie_Walk.anim with full limping leg & foot locomotion!");
        return clip;
    }

    public static AnimationClip CreateZombieChaseClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ChaseClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Chase";
            AssetDatabase.CreateAsset(clip, ChaseClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 0.85s Aggressive Charging Predatory Chase:
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.70f),
            new Keyframe(0.42f, 0.78f),
            new Keyframe(0.85f, 0.70f)
        });

        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.44f),
            new Keyframe(0.42f, 0.50f),
            new Keyframe(0.85f, 0.44f)
        });

        AddMuscleCurve(clip, "Spine Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, -0.16f),
            new Keyframe(0.42f, 0.16f),
            new Keyframe(0.85f, -0.16f)
        });

        AddMuscleCurve(clip, "Spine Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, -0.20f),
            new Keyframe(0.42f, 0.20f),
            new Keyframe(0.85f, -0.20f)
        });

        AddMuscleCurve(clip, "Head Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.22f),
            new Keyframe(0.42f, -0.32f),
            new Keyframe(0.85f, -0.22f)
        });

        // Frantic claw pumps reaching forward
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.70f),
            new Keyframe(0.42f, 0.90f),
            new Keyframe(0.85f, 0.70f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.90f),
            new Keyframe(0.42f, 0.70f),
            new Keyframe(0.85f, 0.90f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f),
            new Keyframe(0.42f, 0.55f),
            new Keyframe(0.85f, -0.15f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, 0.55f),
            new Keyframe(0.42f, -0.15f),
            new Keyframe(0.85f, 0.55f)
        });

        AddMuscleCurve(clip, "Left Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.45f),
            new Keyframe(0.85f, -0.45f)
        });

        AddMuscleCurve(clip, "Right Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.45f),
            new Keyframe(0.85f, -0.45f)
        });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, -0.75f),
                new Keyframe(0.85f, -0.75f)
            });
        }

        // Fast charging strides
        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.55f),
            new Keyframe(0.22f, 0.10f),
            new Keyframe(0.43f, -0.45f),
            new Keyframe(0.65f, -0.10f),
            new Keyframe(0.85f, 0.55f)
        });

        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.75f),
            new Keyframe(0.22f, 0.15f),
            new Keyframe(0.43f, -0.30f),
            new Keyframe(0.65f, -0.80f),
            new Keyframe(0.85f, -0.75f)
        });

        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, -0.45f),
            new Keyframe(0.22f, -0.10f),
            new Keyframe(0.43f, 0.55f),
            new Keyframe(0.65f, 0.10f),
            new Keyframe(0.85f, -0.45f)
        });

        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.30f),
            new Keyframe(0.22f, -0.80f),
            new Keyframe(0.43f, -0.75f),
            new Keyframe(0.65f, 0.15f),
            new Keyframe(0.85f, -0.30f)
        });

        SetClipLoop(clip, true);
        Debug.Log("[ZombieSetup] Created Zombie_Chase.anim!");
        return clip;
    }

    public static AnimationClip CreateZombieIdleClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Idle";
            AssetDatabase.CreateAsset(clip, IdleClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 2.4s Eerie Menacing Ragged Breathing Idle:
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.44f),
            new Keyframe(1.2f, 0.48f),
            new Keyframe(2.4f, 0.44f)
        });

        // Ragged deep chest breathing cycle
        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.20f),
            new Keyframe(1.2f, 0.42f),
            new Keyframe(2.4f, 0.20f)
        });

        AddMuscleCurve(clip, "Spine Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.04f),
            new Keyframe(1.2f, -0.04f),
            new Keyframe(2.4f, 0.04f)
        });

        // Slow eerie searching head movements
        AddMuscleCurve(clip, "Head Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.30f),
            new Keyframe(1.0f, -0.15f), // raises chin scenting air
            new Keyframe(1.7f, -0.20f),
            new Keyframe(2.4f, -0.30f)
        });

        AddMuscleCurve(clip, "Head Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.24f),
            new Keyframe(1.2f, 0.28f),
            new Keyframe(2.4f, 0.24f)
        });

        AddMuscleCurve(clip, "Head Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f),
            new Keyframe(1.2f, 0.18f),
            new Keyframe(2.4f, -0.15f)
        });

        // Hanging dead arms with curled claws
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.24f),
            new Keyframe(1.2f, 0.28f),
            new Keyframe(2.4f, 0.24f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.20f),
            new Keyframe(1.2f, 0.24f),
            new Keyframe(2.4f, 0.20f)
        });

        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.42f),
            new Keyframe(1.2f, -0.38f),
            new Keyframe(2.4f, -0.42f)
        });

        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.48f),
            new Keyframe(1.2f, -0.44f),
            new Keyframe(2.4f, -0.48f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.32f),
            new Keyframe(1.2f, -0.28f),
            new Keyframe(2.4f, -0.32f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.28f),
            new Keyframe(1.2f, -0.24f),
            new Keyframe(2.4f, -0.28f)
        });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, -0.45f),
                new Keyframe(1.2f, -0.55f),
                new Keyframe(2.4f, -0.45f)
            });
        }

        // Fixed grounded stance (no twitching feet)
        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.06f), new Keyframe(2.4f, 0.06f) });
        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[] { new Keyframe(0.0f, -0.06f), new Keyframe(2.4f, -0.06f) });
        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[] { new Keyframe(0.0f, -0.15f), new Keyframe(2.4f, -0.15f) });
        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[] { new Keyframe(0.0f, -0.15f), new Keyframe(2.4f, -0.15f) });

        SetClipLoop(clip, true);
        Debug.Log("[ZombieSetup] Created Zombie_Idle.anim!");
        return clip;
    }

    public static AnimationClip CreateZombieAttackClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Attack";
            AssetDatabase.CreateAsset(clip, AttackClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 1.2s Visceral Upper Body Combat Attack:
        // Full torso strike arc: Windup -> Explosive strike -> Recovery

        // Full torso strike arc: Windup -> Explosive strike -> Recovery
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.45f),
            new Keyframe(0.25f, 0.12f),  // arches back to wind up
            new Keyframe(0.45f, 0.78f),  // violent forward lunge
            new Keyframe(0.75f, 0.58f),  // tear follow-through
            new Keyframe(1.2f, 0.45f)    // returns to combat stance
        });

        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.25f),
            new Keyframe(0.25f, -0.32f), // chest arches back
            new Keyframe(0.45f, 0.65f),  // slam forward into swipe
            new Keyframe(0.75f, 0.38f),
            new Keyframe(1.2f, 0.25f)
        });

        AddMuscleCurve(clip, "Head Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f),
            new Keyframe(0.25f, 0.42f),  // snarls back
            new Keyframe(0.45f, -0.32f), // snaps forward
            new Keyframe(0.75f, -0.18f),
            new Keyframe(1.2f, -0.15f)
        });

        // Visceral claw strike downward
        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.10f),
            new Keyframe(0.25f, 0.72f),  // raise claw high
            new Keyframe(0.45f, -0.28f), // downward strike
            new Keyframe(0.75f, -0.18f),
            new Keyframe(1.2f, -0.10f)
        });

        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.10f),
            new Keyframe(0.25f, 0.65f),  // raise claw
            new Keyframe(0.45f, -0.32f), // downward strike
            new Keyframe(0.75f, -0.18f),
            new Keyframe(1.2f, -0.10f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.35f),
            new Keyframe(0.25f, -0.55f),
            new Keyframe(0.45f, 0.88f),  // thrust forward
            new Keyframe(0.75f, 0.55f),
            new Keyframe(1.2f, 0.35f)
        });

        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.35f),
            new Keyframe(0.25f, -0.50f),
            new Keyframe(0.45f, 0.82f),
            new Keyframe(0.75f, 0.50f),
            new Keyframe(1.2f, 0.35f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.25f),
            new Keyframe(0.25f, -0.85f),
            new Keyframe(0.45f, 0.85f),  // extend strike
            new Keyframe(0.75f, 0.50f),
            new Keyframe(1.2f, -0.25f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.25f),
            new Keyframe(0.25f, -0.80f),
            new Keyframe(0.45f, 0.80f),
            new Keyframe(0.75f, 0.45f),
            new Keyframe(1.2f, -0.25f)
        });

        AddMuscleCurve(clip, "Right Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.10f),
            new Keyframe(0.25f, 0.35f),
            new Keyframe(0.45f, -0.65f), // tearing talons
            new Keyframe(0.75f, -0.40f),
            new Keyframe(1.2f, -0.10f)
        });

        AddMuscleCurve(clip, "Left Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.10f),
            new Keyframe(0.25f, 0.35f),
            new Keyframe(0.45f, -0.65f),
            new Keyframe(0.75f, -0.40f),
            new Keyframe(1.2f, -0.10f)
        });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, -0.30f),
                new Keyframe(0.25f, -0.10f),
                new Keyframe(0.45f, -0.85f), // tight tearing claw
                new Keyframe(0.75f, -0.60f),
                new Keyframe(1.2f, -0.30f)
            });
        }

        // Grounded combat stance for legs (keeps feet firmly planted on the ground!)
        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.15f),
            new Keyframe(0.45f, 0.22f), // lunges slightly forward on swing
            new Keyframe(1.2f, 0.15f)
        });

        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f),
            new Keyframe(0.45f, -0.22f), // braced back leg
            new Keyframe(1.2f, -0.15f)
        });

        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f), // braced knee
            new Keyframe(0.45f, -0.20f),
            new Keyframe(1.2f, -0.15f)
        });

        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f),
            new Keyframe(0.45f, -0.20f),
            new Keyframe(1.2f, -0.15f)
        });

        AddMuscleCurve(clip, "Left Foot Up-Down", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(1.2f, 0.0f)
        });

        AddMuscleCurve(clip, "Right Foot Up-Down", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(1.2f, 0.0f)
        });

        SetClipLoop(clip, false, bakeY: true, bakeXZ: true);
        Debug.Log("[ZombieSetup] Created Zombie_Attack.anim with grounded combat legs!");
        return clip;
    }

    public static AnimationClip CreateZombieHitClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HitClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Hit";
            AssetDatabase.CreateAsset(clip, HitClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 0.35s Visceral Bullet/Punch Impact Flinch Reaction:
        // Torso violently jolts backward and twists away from point of impact
        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.20f),
            new Keyframe(0.10f, -0.65f), // violent recoil impulse
            new Keyframe(0.22f, -0.15f),
            new Keyframe(0.35f, 0.20f)
        });

        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.45f),
            new Keyframe(0.10f, -0.30f), // spine snaps back
            new Keyframe(0.22f, 0.25f),
            new Keyframe(0.35f, 0.45f)
        });

        AddMuscleCurve(clip, "Chest Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.10f, 0.35f),  // right shoulder jolts back
            new Keyframe(0.35f, 0.0f)
        });

        // Head snaps backward with broken neck twist
        AddMuscleCurve(clip, "Head Nod Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.20f),
            new Keyframe(0.10f, 0.55f),  // head thrown violently back
            new Keyframe(0.22f, 0.10f),
            new Keyframe(0.35f, -0.20f)
        });

        AddMuscleCurve(clip, "Head Tilt Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.20f),
            new Keyframe(0.10f, -0.40f), // head snaps sideways
            new Keyframe(0.35f, 0.20f)
        });

        // Arms thrown wide apart in pain reflex
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.50f),
            new Keyframe(0.10f, 0.10f),
            new Keyframe(0.35f, 0.50f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.50f),
            new Keyframe(0.10f, 0.05f),
            new Keyframe(0.35f, 0.50f)
        });

        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.15f),
            new Keyframe(0.10f, 0.40f), // arm flies up/out
            new Keyframe(0.35f, -0.15f)
        });

        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, -0.20f),
            new Keyframe(0.10f, 0.45f),
            new Keyframe(0.35f, -0.20f)
        });

        // Fingers briefly spread open in shock
        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, -0.50f),
                new Keyframe(0.10f, 0.40f),  // splay open
                new Keyframe(0.35f, -0.50f)  // return to claw
            });
        }

        // Grounded leg stance during hit stun
        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.10f),
            new Keyframe(0.10f, 0.15f),
            new Keyframe(0.35f, 0.10f)
        });

        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, -0.10f),
            new Keyframe(0.10f, -0.15f),
            new Keyframe(0.35f, -0.10f)
        });

        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.12f),
            new Keyframe(0.10f, -0.16f),
            new Keyframe(0.35f, -0.12f)
        });

        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.12f),
            new Keyframe(0.10f, -0.16f),
            new Keyframe(0.35f, -0.12f)
        });

        SetClipLoop(clip, false, bakeY: true, bakeXZ: true);
        Debug.Log("[ZombieSetup] Created Zombie_Hit.anim with high-impact flinch and grounded legs!");
        return clip;
    }

    public static AnimationClip CreateZombieDeathClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Death";
            AssetDatabase.CreateAsset(clip, DeathClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 2.4s Full-Body Backward Zombie Death Animation (Matching 4-Panel Reference Storyboard):
        // Panel 1 - THE HIT (0.00s - 0.22s): Fatal chest shot, violent backward torso reel, head snapped back, claws spread wide
        // Panel 2 - THE FALL (0.22s - 0.70s): Knees buckle forward deeply, hips drop and slide back, arms fall limp
        // Panel 3 - THE IMPACT (0.70s - 1.20s): Upper back and hips SLAM into the ground, whiplash head bounce, legs splay in wide V
        // Panel 4 - TARGET ELIMINATED (1.20s - 2.40s): Complete limp repose, head lolled sideways to right, flat on back

        // --- Center of Mass Translation (Hips plunge down to floor and slide back) ---
        AddMuscleCurve(clip, "RootT.y", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.02f), // upright at shot
            new Keyframe(0.50f, -0.52f), // buckling down in mid-fall
            new Keyframe(0.70f, -0.78f),
            new Keyframe(0.90f, -0.94f), // hard ground impact
            new Keyframe(1.05f, -0.89f), // rebound bounce
            new Keyframe(1.25f, -0.90f), // settled flat on floor
            new Keyframe(2.40f, -0.90f)
        });

        AddMuscleCurve(clip, "RootT.z", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.06f),
            new Keyframe(0.50f, -0.48f),
            new Keyframe(0.70f, -0.68f),
            new Keyframe(0.90f, -0.78f),
            new Keyframe(1.25f, -0.82f),
            new Keyframe(2.40f, -0.82f)
        });

        // --- Torso & Spine (Panel 1 Extreme Backward Arch -> Panel 3 Ground Slam -> Panel 4 Flat Rest) ---
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.00f,  0.05f),
            new Keyframe(0.22f, -0.85f), // violent backward reel on hit
            new Keyframe(0.50f, -0.96f), // deep crescent arch over buckling knees
            new Keyframe(0.70f, -0.85f),
            new Keyframe(0.90f, -0.55f), // slams against ground
            new Keyframe(1.05f, -0.35f), // floor bounce
            new Keyframe(1.25f, -0.38f), // resting flat on back
            new Keyframe(2.40f, -0.38f)
        });

        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.90f), // chest thrust up & back from bullet impact
            new Keyframe(0.50f, -0.92f),
            new Keyframe(0.70f, -0.80f),
            new Keyframe(0.90f, -0.45f), // back slams dirt
            new Keyframe(1.05f, -0.30f),
            new Keyframe(1.25f, -0.32f),
            new Keyframe(2.40f, -0.32f)
        });

        AddMuscleCurve(clip, "Chest Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f,  0.20f), // right shoulder jolted back by impact
            new Keyframe(0.60f,  0.10f),
            new Keyframe(0.90f, -0.05f),
            new Keyframe(1.25f,  0.00f),
            new Keyframe(2.40f,  0.00f)
        });

        AddMuscleCurve(clip, "Spine Left-Right", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f,  0.15f),
            new Keyframe(0.70f, -0.15f),
            new Keyframe(1.20f, -0.08f),
            new Keyframe(2.40f, -0.08f)
        });

        AddMuscleCurve(clip, "Spine Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.15f),
            new Keyframe(0.70f,  0.12f),
            new Keyframe(1.20f,  0.05f),
            new Keyframe(2.40f,  0.05f)
        });

        // --- Head & Neck (Panel 1 Snapped Back Jaw to Sky -> Panel 3 Dirt Slam Rebound -> Panel 4 Lolling to Right) ---
        AddMuscleCurve(clip, "Head Nod Down-Up", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f,  0.92f), // head thrown hard back, throat exposed to sky
            new Keyframe(0.50f,  0.70f),
            new Keyframe(0.90f,  0.80f), // back of head strikes dirt
            new Keyframe(1.02f, -0.28f), // chin whips forward in rebound
            new Keyframe(1.25f,  0.15f), // settles on floor
            new Keyframe(2.40f,  0.15f)
        });

        AddMuscleCurve(clip, "Neck Nod Down-Up", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f,  0.48f),
            new Keyframe(0.50f,  0.35f),
            new Keyframe(0.90f,  0.40f),
            new Keyframe(1.02f, -0.15f),
            new Keyframe(1.25f,  0.10f),
            new Keyframe(2.40f,  0.10f)
        });

        AddMuscleCurve(clip, "Head Tilt Left-Right", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.18f),
            new Keyframe(0.70f,  0.15f),
            new Keyframe(1.05f,  0.05f),
            new Keyframe(1.35f,  0.48f), // Panel 4: head falls sideways to the right in final death repose
            new Keyframe(2.40f,  0.48f)
        });

        AddMuscleCurve(clip, "Head Turn Left-Right", new Keyframe[]
        {
            new Keyframe(0.00f, 0.00f),
            new Keyframe(0.22f, -0.10f),
            new Keyframe(1.05f, 0.05f),
            new Keyframe(1.35f, 0.28f), // head turned to side
            new Keyframe(2.40f, 0.28f)
        });

        // --- Upper & Lower Legs (Panel 2 Buckling Knees -> Panel 3 Straightening Flat -> Panel 4 Wide V Splay) ---
        AddMuscleCurve(clip, "Left Upper Leg Front-Back", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f,  0.12f),
            new Keyframe(0.50f, -0.78f), // thighs drive back as knees buckle forward
            new Keyframe(0.70f, -0.65f),
            new Keyframe(0.90f, -0.30f),
            new Keyframe(1.20f, -0.10f), // slides out flat on ground
            new Keyframe(2.40f, -0.10f)
        });

        AddMuscleCurve(clip, "Right Upper Leg Front-Back", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f,  0.10f),
            new Keyframe(0.50f, -0.74f),
            new Keyframe(0.70f, -0.60f),
            new Keyframe(0.90f, -0.25f),
            new Keyframe(1.20f, -0.08f),
            new Keyframe(2.40f, -0.08f)
        });

        AddMuscleCurve(clip, "Left Lower Leg Stretch", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.25f),
            new Keyframe(0.50f, -0.92f), // knees buckle deep forward
            new Keyframe(0.70f, -0.75f),
            new Keyframe(0.90f, -0.30f),
            new Keyframe(1.05f,  0.42f), // legs straighten out flat on floor
            new Keyframe(1.25f,  0.48f),
            new Keyframe(2.40f,  0.48f)
        });

        AddMuscleCurve(clip, "Right Lower Leg Stretch", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.22f, -0.22f),
            new Keyframe(0.50f, -0.88f),
            new Keyframe(0.70f, -0.70f),
            new Keyframe(0.90f, -0.25f),
            new Keyframe(1.05f,  0.38f),
            new Keyframe(1.25f,  0.44f),
            new Keyframe(2.40f,  0.44f)
        });

        // Panel 3 & 4 Wide V Splay
        AddMuscleCurve(clip, "Left Upper Leg In-Out", new Keyframe[]
        {
            new Keyframe(0.00f, 0.00f),
            new Keyframe(0.50f, 0.05f),
            new Keyframe(0.90f, 0.30f),
            new Keyframe(1.25f, 0.38f), // splayed wide outward
            new Keyframe(2.40f, 0.38f)
        });

        AddMuscleCurve(clip, "Right Upper Leg In-Out", new Keyframe[]
        {
            new Keyframe(0.00f,  0.00f),
            new Keyframe(0.50f, -0.05f),
            new Keyframe(0.90f, -0.28f),
            new Keyframe(1.25f, -0.38f), // splayed wide outward
            new Keyframe(2.40f, -0.38f)
        });

        // --- Arms & Hands (Panel 1 Flung High -> Panel 2 Falling Limp -> Panel 3 Floor Slap -> Panel 4 Sprawled at Sides) ---
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.00f,  0.30f),
            new Keyframe(0.22f, -0.70f), // flung backward on shot
            new Keyframe(0.50f, -0.45f), // falling limp
            new Keyframe(0.70f, -0.35f),
            new Keyframe(1.05f, -0.25f), // slapped onto floor
            new Keyframe(1.30f, -0.25f), // sprawling relaxed at side
            new Keyframe(2.40f, -0.25f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.00f,  0.30f),
            new Keyframe(0.22f, -0.80f),
            new Keyframe(0.50f, -0.45f),
            new Keyframe(0.70f, -0.35f),
            new Keyframe(1.05f, -0.25f),
            new Keyframe(1.30f, -0.25f),
            new Keyframe(2.40f, -0.25f)
        });

        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.00f, -0.10f),
            new Keyframe(0.22f, -0.20f), // hangs naturally down at sides
            new Keyframe(0.50f, -0.20f), // drops limp
            new Keyframe(0.70f, -0.45f),
            new Keyframe(0.90f, -0.55f),
            new Keyframe(1.05f, -0.68f), // flat on ground
            new Keyframe(2.40f, -0.68f)
        });

        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.00f, -0.10f),
            new Keyframe(0.22f, -0.20f),
            new Keyframe(0.50f, -0.20f),
            new Keyframe(0.70f, -0.45f),
            new Keyframe(0.90f, -0.55f),
            new Keyframe(1.05f, -0.68f),
            new Keyframe(2.40f, -0.68f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.00f, -0.30f),
            new Keyframe(0.22f,  0.60f), // arms fling straight out
            new Keyframe(0.50f, -0.25f),
            new Keyframe(1.05f,  0.30f), // resting flat along floor
            new Keyframe(2.40f,  0.30f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.00f, -0.30f),
            new Keyframe(0.22f,  0.60f),
            new Keyframe(0.50f, -0.25f),
            new Keyframe(1.05f,  0.30f),
            new Keyframe(2.40f,  0.30f)
        });

        // Hands & Fingers (Panel 1 Wide Shock Splay -> Panel 2 Limp Relaxation -> Panel 4 Dead Repose)
        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.00f, -0.50f), // initial claw grip
                new Keyframe(0.22f,  0.85f), // Panel 1: wide shock splay!
                new Keyframe(0.50f,  0.05f), // Panel 2: limp curling
                new Keyframe(1.05f, -0.15f), // Panel 4: loose relaxed repose
                new Keyframe(2.40f, -0.15f)
            });
        }

        SetClipLoop(clip, false, bakeY: false, bakeXZ: false);
        Debug.Log("[ZombieSetup] Created Zombie_Death.anim matching cinematic 4-panel storyboard!");
        return clip;
    }

    public static AnimationClip CreateZombieRunClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieRunClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Run";
            AssetDatabase.CreateAsset(clip, ZombieRunClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // Feral forward hunched sprint with reaching outstretched claws
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.75f), new Keyframe(0.42f, 0.85f), new Keyframe(0.85f, 0.75f) });
        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.50f), new Keyframe(0.42f, 0.58f), new Keyframe(0.85f, 0.50f) });
        AddMuscleCurve(clip, "Head Down-Up", new Keyframe[] { new Keyframe(0.0f, -0.30f), new Keyframe(0.42f, -0.40f), new Keyframe(0.85f, -0.30f) });
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.75f), new Keyframe(0.42f, 0.95f), new Keyframe(0.85f, 0.75f) });
        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.95f), new Keyframe(0.42f, 0.75f), new Keyframe(0.85f, 0.95f) });
        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[] { new Keyframe(0.0f, -0.10f), new Keyframe(0.42f, 0.60f), new Keyframe(0.85f, -0.10f) });
        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[] { new Keyframe(0.0f, 0.60f), new Keyframe(0.42f, -0.10f), new Keyframe(0.85f, 0.60f) });

        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.60f), new Keyframe(0.42f, -0.50f), new Keyframe(0.85f, 0.60f) });
        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[] { new Keyframe(0.0f, -0.50f), new Keyframe(0.42f, 0.60f), new Keyframe(0.85f, -0.50f) });
        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[] { new Keyframe(0.0f, -0.70f), new Keyframe(0.21f, 0.20f), new Keyframe(0.42f, -0.35f), new Keyframe(0.63f, -0.85f), new Keyframe(0.85f, -0.70f) });
        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[] { new Keyframe(0.0f, -0.35f), new Keyframe(0.21f, -0.85f), new Keyframe(0.42f, -0.70f), new Keyframe(0.63f, 0.20f), new Keyframe(0.85f, -0.35f) });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[] { new Keyframe(0.0f, -0.80f), new Keyframe(0.85f, -0.80f) });
        }

        SetClipLoop(clip, true);
        return clip;
    }

    public static AnimationClip CreateMutantRunClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MutantRunClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Mutant_Run";
            AssetDatabase.CreateAsset(clip, MutantRunClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // Heavy, loping mutant brute charge with deep swagger and bounding stride
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.58f), new Keyframe(0.55f, 0.68f), new Keyframe(1.10f, 0.58f) });
        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.40f), new Keyframe(0.55f, 0.50f), new Keyframe(1.10f, 0.40f) });
        AddMuscleCurve(clip, "Spine Left-Right", new Keyframe[] { new Keyframe(0.0f, -0.35f), new Keyframe(0.55f, 0.35f), new Keyframe(1.10f, -0.35f) });
        AddMuscleCurve(clip, "Spine Twist Left-Right", new Keyframe[] { new Keyframe(0.0f, -0.25f), new Keyframe(0.55f, 0.25f), new Keyframe(1.10f, -0.25f) });

        // Heavy asymmetrical ape/mutant bounding arm swings
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.90f), new Keyframe(0.55f, -0.45f), new Keyframe(1.10f, 0.90f) });
        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, -0.45f), new Keyframe(0.55f, 0.90f), new Keyframe(1.10f, -0.45f) });
        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[] { new Keyframe(0.0f, 0.25f), new Keyframe(0.55f, -0.35f), new Keyframe(1.10f, 0.25f) });
        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[] { new Keyframe(0.0f, -0.35f), new Keyframe(0.55f, 0.25f), new Keyframe(1.10f, -0.35f) });

        AddMuscleCurve(clip, "Left UpperLeg Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.70f), new Keyframe(0.55f, -0.55f), new Keyframe(1.10f, 0.70f) });
        AddMuscleCurve(clip, "Right UpperLeg Front-Back", new Keyframe[] { new Keyframe(0.0f, -0.55f), new Keyframe(0.55f, 0.70f), new Keyframe(1.10f, -0.55f) });
        AddMuscleCurve(clip, "Left LowerLeg Stretch", new Keyframe[] { new Keyframe(0.0f, -0.65f), new Keyframe(0.28f, 0.15f), new Keyframe(0.55f, -0.30f), new Keyframe(0.83f, -0.80f), new Keyframe(1.10f, -0.65f) });
        AddMuscleCurve(clip, "Right LowerLeg Stretch", new Keyframe[] { new Keyframe(0.0f, -0.30f), new Keyframe(0.28f, -0.80f), new Keyframe(0.55f, -0.65f), new Keyframe(0.83f, 0.15f), new Keyframe(1.10f, -0.30f) });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[] { new Keyframe(0.0f, -0.70f), new Keyframe(1.10f, -0.70f) });
        }

        SetClipLoop(clip, true);
        return clip;
    }

    public static AnimationClip CreateZombieScreamingClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieScreamingClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Zombie_Screaming";
            AssetDatabase.CreateAsset(clip, ZombieScreamingClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 1.0s Bloodcurdling roar / alert screech: chest thrust forward, head thrown all the way back, arms flung wide open
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.20f), new Keyframe(0.35f, -0.65f), new Keyframe(0.70f, -0.60f), new Keyframe(1.0f, 0.35f) });
        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.10f), new Keyframe(0.35f, -0.85f), new Keyframe(0.70f, -0.80f), new Keyframe(1.0f, 0.20f) });
        AddMuscleCurve(clip, "Head Nod Down-Up", new Keyframe[] { new Keyframe(0.0f, 0.00f), new Keyframe(0.35f, 0.95f), new Keyframe(0.70f, 0.92f), new Keyframe(1.0f, -0.15f) });
        AddMuscleCurve(clip, "Neck Nod Down-Up", new Keyframe[] { new Keyframe(0.0f, 0.00f), new Keyframe(0.35f, 0.65f), new Keyframe(0.70f, 0.60f), new Keyframe(1.0f, 0.10f) });

        // Arms thrown wide open to the sides trembling in feral rage
        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[] { new Keyframe(0.0f, -0.10f), new Keyframe(0.35f, 0.55f), new Keyframe(0.70f, 0.52f), new Keyframe(1.0f, -0.10f) });
        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[] { new Keyframe(0.0f, -0.10f), new Keyframe(0.35f, 0.55f), new Keyframe(0.70f, 0.52f), new Keyframe(1.0f, -0.10f) });
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.20f), new Keyframe(0.35f, -0.30f), new Keyframe(0.70f, -0.28f), new Keyframe(1.0f, 0.30f) });
        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.20f), new Keyframe(0.35f, -0.30f), new Keyframe(0.70f, -0.28f), new Keyframe(1.0f, 0.30f) });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[] { new Keyframe(0.0f, -0.45f), new Keyframe(0.35f, 0.85f), new Keyframe(0.70f, 0.80f), new Keyframe(1.0f, -0.45f) });
        }

        SetClipLoop(clip, false);
        return clip;
    }

    public static AnimationClip CreateMutantSwipingClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MutantSwipingClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Mutant_Swiping";
            AssetDatabase.CreateAsset(clip, MutantSwipingClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 1.1s Powerful horizontal cleave tearing from right to left
        AddMuscleCurve(clip, "Spine Twist Left-Right", new Keyframe[] { new Keyframe(0.0f, 0.00f), new Keyframe(0.25f, 0.65f), new Keyframe(0.50f, -0.75f), new Keyframe(0.80f, -0.35f), new Keyframe(1.1f, 0.00f) });
        AddMuscleCurve(clip, "Chest Twist Left-Right", new Keyframe[] { new Keyframe(0.0f, 0.00f), new Keyframe(0.25f, 0.75f), new Keyframe(0.50f, -0.85f), new Keyframe(0.80f, -0.40f), new Keyframe(1.1f, 0.00f) });
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.40f), new Keyframe(0.25f, 0.15f), new Keyframe(0.50f, 0.70f), new Keyframe(1.1f, 0.40f) });

        // Right arm swings in massive horizontal arc across the front
        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.20f), new Keyframe(0.25f, -0.65f), new Keyframe(0.50f, 0.85f), new Keyframe(1.1f, 0.20f) });
        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[] { new Keyframe(0.0f, -0.10f), new Keyframe(0.25f, 0.45f), new Keyframe(0.50f, -0.15f), new Keyframe(1.1f, -0.10f) });
        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[] { new Keyframe(0.0f, -0.20f), new Keyframe(0.25f, -0.55f), new Keyframe(0.50f, 0.60f), new Keyframe(1.1f, -0.20f) });

        // Left arm balances
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[] { new Keyframe(0.0f, 0.20f), new Keyframe(0.25f, 0.45f), new Keyframe(0.50f, -0.35f), new Keyframe(1.1f, 0.20f) });

        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[] { new Keyframe(0.0f, -0.55f), new Keyframe(0.50f, -0.85f), new Keyframe(1.1f, -0.55f) });
        }

        SetClipLoop(clip, false);
        return clip;
    }

    public static AnimationClip CreatePlayerShootClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerShootClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Player_Shoot";
            AssetDatabase.CreateAsset(clip, PlayerShootClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 0.35s High-Impact Rifle & Pistol Recoil Animation:
        // Torso absorbs recoil backward and twists with right trigger shoulder
        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.05f),
            new Keyframe(0.05f, -0.28f), // sharp backward recoil impulse
            new Keyframe(0.18f, -0.05f),
            new Keyframe(0.35f, 0.05f)
        });

        AddMuscleCurve(clip, "Chest Twist Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.05f),
            new Keyframe(0.05f, 0.18f),  // right shoulder kicks backward
            new Keyframe(0.20f, 0.07f),
            new Keyframe(0.35f, 0.05f)
        });

        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.05f, -0.15f),
            new Keyframe(0.35f, 0.0f)
        });

        // Right Arm (Trigger hand & weapon stock)
        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.55f),  // raised aiming forward
            new Keyframe(0.05f, 0.25f), // kicks back into shoulder
            new Keyframe(0.18f, 0.48f),
            new Keyframe(0.35f, 0.55f)
        });

        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.10f),
            new Keyframe(0.05f, 0.28f), // muzzle rise / kick up
            new Keyframe(0.18f, 0.12f),
            new Keyframe(0.35f, 0.10f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.35f),
            new Keyframe(0.05f, -0.65f), // elbow absorbs recoil
            new Keyframe(0.18f, -0.40f),
            new Keyframe(0.35f, -0.35f)
        });

        // Left Arm (Supports rifle fore-grip / tactical posture)
        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.60f),  // forward holding weapon
            new Keyframe(0.05f, 0.45f), // recoils back
            new Keyframe(0.20f, 0.58f),
            new Keyframe(0.35f, 0.60f)
        });

        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.05f),
            new Keyframe(0.05f, 0.18f),
            new Keyframe(0.35f, 0.05f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.30f),
            new Keyframe(0.05f, -0.45f),
            new Keyframe(0.35f, -0.30f)
        });

        // Head stays focused on crosshair, slight nod from blast
        AddMuscleCurve(clip, "Head Nod Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.05f, 0.12f),
            new Keyframe(0.20f, 0.0f),
            new Keyframe(0.35f, 0.0f)
        });

        // Finger grip tightens on trigger
        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, -0.40f),
                new Keyframe(0.05f, -0.70f),
                new Keyframe(0.35f, -0.40f)
            });
        }

        SetClipLoop(clip, false);
        Debug.Log("[ZombieSetup] Created Player_Shoot.anim with authentic weapon recoil!");
        return clip;
    }

    public static AnimationClip CreatePlayerPickupClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerPickupClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = "Player_Pickup";
            AssetDatabase.CreateAsset(clip, PlayerPickupClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = 30f;
        clip.legacy = false;

        // 0.70s Expressive Weapon Looting Animation:
        // Torso & Spine bend forward to reach down
        AddMuscleCurve(clip, "Spine Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, 0.42f), // deep torso reach down
            new Keyframe(0.50f, 0.10f),
            new Keyframe(0.70f, 0.0f)
        });

        AddMuscleCurve(clip, "Chest Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, 0.30f),
            new Keyframe(0.50f, 0.05f),
            new Keyframe(0.70f, 0.0f)
        });

        AddMuscleCurve(clip, "Spine Left-Right", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, -0.15f), // slight lean toward dominant right arm
            new Keyframe(0.70f, 0.0f)
        });

        // Right Arm reaches down, grabs, then sweeps up to chest
        AddMuscleCurve(clip, "Right Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, -0.65f), // arm extends down to floor
            new Keyframe(0.48f, 0.25f),  // sweeps up to chest height
            new Keyframe(0.70f, 0.0f)
        });

        AddMuscleCurve(clip, "Right Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, 0.40f),  // reaches forward/down
            new Keyframe(0.48f, 0.65f),  // sweeps up forward
            new Keyframe(0.70f, 0.0f)
        });

        AddMuscleCurve(clip, "Right Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.20f),
            new Keyframe(0.25f, 0.40f),  // arm straightens down
            new Keyframe(0.48f, -0.60f), // arm bends to raise gun
            new Keyframe(0.70f, -0.20f)
        });

        AddMuscleCurve(clip, "Right Hand Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, -0.50f), // fingers grab weapon handle
            new Keyframe(0.50f, 0.15f),  // ready flick
            new Keyframe(0.70f, 0.0f)
        });

        // Left Arm comes in at 0.35s to meet the gun and support it
        AddMuscleCurve(clip, "Left Arm Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.35f, -0.30f),
            new Keyframe(0.50f, 0.15f),  // meets right hand on gun
            new Keyframe(0.70f, 0.0f)
        });

        AddMuscleCurve(clip, "Left Arm Front-Back", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.35f, 0.20f),
            new Keyframe(0.50f, 0.55f),
            new Keyframe(0.70f, 0.0f)
        });

        AddMuscleCurve(clip, "Left Forearm Stretch", new Keyframe[]
        {
            new Keyframe(0.0f, -0.20f),
            new Keyframe(0.50f, -0.45f),
            new Keyframe(0.70f, -0.20f)
        });

        // Head looks down at the weapon then looks up
        AddMuscleCurve(clip, "Head Nod Down-Up", new Keyframe[]
        {
            new Keyframe(0.0f, 0.0f),
            new Keyframe(0.25f, 0.35f),  // looks down at gun
            new Keyframe(0.50f, 0.10f),
            new Keyframe(0.70f, 0.0f)
        });

        // Fingers wrap around the grip
        foreach (var m in ClawMuscles)
        {
            AddMuscleCurve(clip, m, new Keyframe[]
            {
                new Keyframe(0.0f, 0.0f),
                new Keyframe(0.25f, -0.75f), // tight grip
                new Keyframe(0.70f, -0.30f)
            });
        }

        SetClipLoop(clip, false);
        Debug.Log("[ZombieSetup] Created Player_Pickup.anim with rich reaching and equipping motion!");
        return clip;
    }

    public static string CanonicalMuscleName(string name)
    {
        switch (name)
        {
            case "Head Down-Up": return "Head Nod Down-Up";
            case "Head Left-Right": return "Head Tilt Left-Right";
            case "Head Twist Left-Right": return "Head Turn Left-Right";
            case "Neck Down-Up": return "Neck Nod Down-Up";
            case "Neck Left-Right": return "Neck Tilt Left-Right";
            case "Neck Twist Left-Right": return "Neck Turn Left-Right";
            case "Left UpperLeg Front-Back": return "Left Upper Leg Front-Back";
            case "Right UpperLeg Front-Back": return "Right Upper Leg Front-Back";
            case "Left UpperLeg In-Out": return "Left Upper Leg In-Out";
            case "Right UpperLeg In-Out": return "Right Upper Leg In-Out";
            case "Left UpperLeg Twist In-Out": return "Left Upper Leg Twist In-Out";
            case "Right UpperLeg Twist In-Out": return "Right Upper Leg Twist In-Out";
            case "Left LowerLeg Stretch": return "Left Lower Leg Stretch";
            case "Right LowerLeg Stretch": return "Right Lower Leg Stretch";
            case "Left LowerLeg Twist In-Out": return "Left Lower Leg Twist In-Out";
            case "Right LowerLeg Twist In-Out": return "Right Lower Leg Twist In-Out";
            case "Left UpperLeg Stretch": return "Left Lower Leg Stretch";
            case "Right UpperLeg Stretch": return "Right Lower Leg Stretch";
            default: return name;
        }
    }

    private static void AddMuscleCurve(AnimationClip clip, string muscleName, Keyframe[] keys)
    {
        muscleName = CanonicalMuscleName(muscleName);
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

    private static AnimationClip LoadFbxClip(string fbxPath, string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (assets == null) return null;
        foreach (var a in assets)
        {
            if (a is AnimationClip c && c.name == clipName) return c;
        }
        foreach (var a in assets)
        {
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        }
        return null;
    }

    private static AvatarMask GetOrCreateUpperBodyMask()
    {
        string maskPath = "Assets/ZombieMale_AAB/Animations/Zombie_UpperBodyMask.mask";
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            mask.name = "Zombie_UpperBodyMask";
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        return mask;
    }

    public static AnimatorController SetupZombieAnimatorController(AnimationClip idleClip, AnimationClip walkClip, AnimationClip chaseClip, AnimationClip attackClip, AnimationClip hitClip, AnimationClip deathClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        // Parameters
        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

        // Load FBX Humanoid clips for 100% stable feet-on-ground locomotion (prevents sinking into terrain)
        AnimationClip fbxIdle = LoadFbxClip("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx", "Idle");
        AnimationClip fbxWalk = LoadFbxClip("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Locomotion--Walk_N.anim.fbx", "Walk_N");
        AnimationClip fbxRun = LoadFbxClip("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Locomotion--Run_N.anim.fbx", "Run_N");

        var rootSm = controller.layers[0].stateMachine;

        // Clear all accumulated AnyState transitions on rootSm (fixes duplicate attack/hit transition accumulation)
        ClearAnyStateTransitions(rootSm);

        // Find existing states on Layer 0 (Base Layer)
        var states = rootSm.states;
        AnimatorState idleState = null;
        AnimatorState walkState = null;
        AnimatorState chaseState = null;
        AnimatorState attackState = null;
        AnimatorState hitState = null;
        AnimatorState deathState = null;

        foreach (var cs in states)
        {
            if (cs.state.name == "Idle") idleState = cs.state;
            else if (cs.state.name == "Walk") walkState = cs.state;
            else if (cs.state.name == "Chase") chaseState = cs.state;
            else if (cs.state.name == "Attack") attackState = cs.state;
            else if (cs.state.name == "Hit") hitState = cs.state;
            else if (cs.state.name == "Die") deathState = cs.state;
        }

        if (idleState == null) idleState = rootSm.AddState("Idle", new Vector3(250, 50, 0));
        if (walkState == null) walkState = rootSm.AddState("Walk", new Vector3(250, 150, 0));
        if (chaseState == null) chaseState = rootSm.AddState("Chase", new Vector3(250, 250, 0));
        if (attackState == null) attackState = rootSm.AddState("Attack", new Vector3(500, 50, 0));
        if (hitState == null) hitState = rootSm.AddState("Hit", new Vector3(500, 150, 0));
        if (deathState == null) deathState = rootSm.AddState("Die", new Vector3(500, 250, 0));

        // Base layer: uses custom authentic zombie humanoid animation clips with full grounded leg curves
        idleState.motion = fbxIdle != null ? fbxIdle : idleClip;
        walkState.motion = fbxWalk != null ? fbxWalk : walkClip;
        walkState.speed = 0.85f;
        chaseState.motion = fbxRun != null ? fbxRun : chaseClip;
        chaseState.speed = 1.0f;
        attackState.motion = fbxIdle != null ? fbxIdle : attackClip;
        attackState.speed = 1.0f;
        hitState.motion = fbxIdle != null ? fbxIdle : hitClip;
        hitState.speed = 1.0f;
        deathState.motion = fbxIdle != null ? fbxIdle : deathClip;

        rootSm.defaultState = idleState;

        // Reset transitions on Layer 0 states
        ClearTransitions(idleState);
        ClearTransitions(walkState);
        ClearTransitions(chaseState);
        ClearTransitions(attackState);
        ClearTransitions(hitState);
        ClearTransitions(deathState);

        // Idle <-> Walk (Speed threshold 0.1)
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.2f;
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.2f;
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Walk <-> Chase (Speed threshold 0.6)
        var walkToChase = walkState.AddTransition(chaseState);
        walkToChase.hasExitTime = false;
        walkToChase.duration = 0.2f;
        walkToChase.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");

        var chaseToWalk = chaseState.AddTransition(walkState);
        chaseToWalk.hasExitTime = false;
        chaseToWalk.duration = 0.2f;
        chaseToWalk.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");

        // Idle <-> Chase
        var idleToChase = idleState.AddTransition(chaseState);
        idleToChase.hasExitTime = false;
        idleToChase.duration = 0.2f;
        idleToChase.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");

        var chaseToIdle = chaseState.AddTransition(idleState);
        chaseToIdle.hasExitTime = false;
        chaseToIdle.duration = 0.2f;
        chaseToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // AnyState -> Attack (Single Layer Base Execution - NEVER sinks or glitches!)
        var anyToAttack = rootSm.AddAnyStateTransition(attackState);
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.10f;
        anyToAttack.canTransitionToSelf = false;
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        var attackToChase = attackState.AddTransition(chaseState);
        attackToChase.hasExitTime = true;
        attackToChase.exitTime = 0.85f;
        attackToChase.duration = 0.15f;
        attackToChase.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");

        var attackToWalk = attackState.AddTransition(walkState);
        attackToWalk.hasExitTime = true;
        attackToWalk.exitTime = 0.85f;
        attackToWalk.duration = 0.15f;
        attackToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.85f;
        attackToIdle.duration = 0.15f;

        // AnyState -> Hit
        var anyToHit = rootSm.AddAnyStateTransition(hitState);
        anyToHit.hasExitTime = false;
        anyToHit.duration = 0.08f;
        anyToHit.canTransitionToSelf = false;
        anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");

        var hitToChase = hitState.AddTransition(chaseState);
        hitToChase.hasExitTime = true;
        hitToChase.exitTime = 0.85f;
        hitToChase.duration = 0.15f;
        hitToChase.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");

        var hitToWalk = hitState.AddTransition(walkState);
        hitToWalk.hasExitTime = true;
        hitToWalk.exitTime = 0.85f;
        hitToWalk.duration = 0.15f;
        hitToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var hitToIdle = hitState.AddTransition(idleState);
        hitToIdle.hasExitTime = true;
        hitToIdle.exitTime = 0.85f;
        hitToIdle.duration = 0.15f;

        // AnyState -> Die
        var anyToDie = rootSm.AddAnyStateTransition(deathState);
        anyToDie.hasExitTime = false;
        anyToDie.duration = 0.15f;
        anyToDie.canTransitionToSelf = false;
        anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

        // Keep ONLY Layer 0 (Base Layer). Discard Layer 1 Upper Body completely!
        var baseLayer = controller.layers[0];
        baseLayer.avatarMask = null;
        baseLayer.defaultWeight = 1.0f;
        baseLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = new AnimatorControllerLayer[] { baseLayer };

        EditorUtility.SetDirty(controller);
        Debug.Log("[ZombieSetup] Configured ZombieAnimator.controller with single clean Base Layer (100% grounded feet, no sinking, no glitch)!");
        return controller;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    private static void ClearTransitions(AnimatorState state)
    {
        while (state.transitions.Length > 0)
        {
            state.RemoveTransition(state.transitions[0]);
        }
    }

    private static void ClearAnyStateTransitions(AnimatorStateMachine sm)
    {
        while (sm.anyStateTransitions.Length > 0)
        {
            sm.RemoveAnyStateTransition(sm.anyStateTransitions[0]);
        }
    }

    public static GameObject ConfigureZombiePrefab(AnimatorController controller)
    {
        // Load base URP prefab or FBX
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieUrpPrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[ZombieSetup] Could not load base prefab at {ZombieUrpPrefabPath}!");
            return null;
        }

        // Instantiate temporary object to construct the enemy prefab
        GameObject tempGo = Object.Instantiate(basePrefab);
        tempGo.name = "Zombie_Enemy";
        tempGo.tag = "Untagged"; // Keep tag distinct from Player

        // 1. CharacterController
        var cc = tempGo.GetComponent<CharacterController>();
        if (cc == null) cc = tempGo.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.95f, 0f);
        cc.height = 1.9f;
        cc.radius = 0.38f;
        cc.skinWidth = 0.05f;
        cc.stepOffset = 0.5f;
        cc.slopeLimit = 60f;
        cc.minMoveDistance = 0f;
        cc.excludeLayers = (1 << 2) | (1 << 8);

        // 2. Animator
        var anim = tempGo.GetComponent<Animator>();
        if (anim == null) anim = tempGo.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Ensure NavMeshAgent baseOffset is 0 if present (Fix 3: prevents vertical offset pull)
        var nma = tempGo.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nma != null)
        {
            nma.baseOffset = 0f;
        }

        // Locate Avatar from FBX
        Avatar zombieAvatar = null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ZombieFbxPath);
        foreach (var a in assets)
        {
            if (a is Avatar av)
            {
                zombieAvatar = av;
                break;
            }
        }
        if (zombieAvatar != null)
        {
            anim.avatar = zombieAvatar;
            Debug.Log($"[ZombieSetup] Assigned Avatar '{zombieAvatar.name}' to Zombie Animator.");
        }

        // 3. ZombieAI Component
        var ai = tempGo.GetComponent<ZombieAI>();
        if (ai == null) ai = tempGo.AddComponent<ZombieAI>();
        ai.detectionRange = 28f;
        ai.loseTargetRange = 45f;
        ai.attackRange = 1.9f;
        ai.walkSpeed = 2.0f;
        ai.chaseSpeed = 3.8f;
        ai.attackDamage = 15f;
        ai.attackCooldown = 1.35f;
        ai.maxHealth = 60f;
        ai.currentHealth = 60f;

        // Save as Zombie_Enemy.prefab
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempGo, ZombieEnemyPrefabPath);
        Object.DestroyImmediate(tempGo);

        Debug.Log($"[ZombieSetup] Saved Zombie_Enemy.prefab at {ZombieEnemyPrefabPath}!");
        return savedPrefab;
    }

    public static void ConfigurePlayerInScene()
    {
        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogWarning("[ZombieSetup] PlayerCapsule not found in active scene!");
            return;
        }

        Undo.RecordObject(player, "Configure Player Health and Combat");

        // 1. PlayerHealth
        var health = player.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<PlayerHealth>(player);
            Debug.Log("[ZombieSetup] Added PlayerHealth component to PlayerCapsule.");
        }
        health.maxHealth = 100f;
        health.currentHealth = 100f;

        // 2. PlayerCombat
        var combat = player.GetComponent<PlayerCombat>();
        if (combat == null)
        {
            combat = Undo.AddComponent<PlayerCombat>(player);
            Debug.Log("[ZombieSetup] Added PlayerCombat component to PlayerCapsule.");
        }
        combat.punchDamage = 30f;
        combat.attackRange = 2.0f;
        combat.attackRadius = 1.2f;
        // 3. Ensure Player Animator has Apply Root Motion disabled to prevent clip displacement
        var anim = player.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }
    }

    public static void ConfigurePlayerAnimatorController(AnimationClip deathClip, AnimationClip shootClip = null, AnimationClip pickupClip = null)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (controller == null) return;

        EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Shoot", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Pickup", AnimatorControllerParameterType.Trigger);

        var rootSm = controller.layers[0].stateMachine;

        // Clean Layer 0: Remove Shoot and Pickup states and transitions from Base Layer!
        // Base Layer must ONLY drive locomotion and full-body death to keep hips and feet firmly grounded!
        for (int i = rootSm.anyStateTransitions.Length - 1; i >= 0; i--)
        {
            var trans = rootSm.anyStateTransitions[i];
            if (trans.destinationState == null || trans.destinationState.name == "Shoot" || trans.destinationState.name == "Pickup")
            {
                rootSm.RemoveAnyStateTransition(trans);
            }
            else
            {
                foreach (var cond in trans.conditions)
                {
                    if (cond.parameter == "Shoot" || cond.parameter == "Pickup")
                    {
                        rootSm.RemoveAnyStateTransition(trans);
                        break;
                    }
                }
            }
        }

        foreach (var cs in rootSm.states)
        {
            if (cs.state.name == "Shoot" || cs.state.name == "Pickup")
            {
                rootSm.RemoveState(cs.state);
            }
        }

        // Ensure Die state on Layer 0
        AnimatorState dieState = null;
        foreach (var cs in rootSm.states)
        {
            if (cs.state.name == "Die") dieState = cs.state;
        }

        if (dieState == null)
        {
            dieState = rootSm.AddState("Die", new Vector3(550, 420, 0));
            dieState.motion = deathClip;
            dieState.writeDefaultValues = true;

            var trans = rootSm.AddAnyStateTransition(dieState);
            trans.hasExitTime = false;
            trans.duration = 0.15f;
            trans.canTransitionToSelf = false;
            trans.AddCondition(AnimatorConditionMode.If, 0, "Die");

            EditorUtility.SetDirty(controller);
            Debug.Log("[ZombieSetup] Added 'Die' state and transition to StarterAssetsThirdPerson.controller!");
        }
        else if (deathClip != null)
        {
            dieState.motion = deathClip;
        }

        // Configure Layer 1: PlayerUpperBody Layer
        // Uses AvatarMask (Root and Legs disabled) so shooting recoil NEVER drops the player into the terrain!
        AvatarMask upperMask = GetOrCreateUpperBodyMask();
        var layers = controller.layers;
        if (layers.Length < 2)
        {
            controller.AddLayer("PlayerUpperBody");
            layers = controller.layers;
        }

        layers[1].name = "PlayerUpperBody";
        layers[1].avatarMask = upperMask;
        layers[1].defaultWeight = 1.0f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;

        var upperSm = layers[1].stateMachine;
        ClearAnyStateTransitions(upperSm);

        AnimatorState emptyState = null;
        AnimatorState shootUpper = null;
        AnimatorState pickupUpper = null;

        foreach (var cs in upperSm.states)
        {
            if (cs.state.name == "Empty") emptyState = cs.state;
            else if (cs.state.name == "Shoot") shootUpper = cs.state;
            else if (cs.state.name == "Pickup") pickupUpper = cs.state;
        }

        if (emptyState == null) emptyState = upperSm.AddState("Empty", new Vector3(250, 50, 0));
        if (shootUpper == null) shootUpper = upperSm.AddState("Shoot", new Vector3(500, 50, 0));
        if (pickupUpper == null) pickupUpper = upperSm.AddState("Pickup", new Vector3(500, 150, 0));

        emptyState.motion = null;
        emptyState.writeDefaultValues = true;
        shootUpper.motion = shootClip;
        shootUpper.writeDefaultValues = true;
        pickupUpper.motion = pickupClip;
        pickupUpper.writeDefaultValues = true;

        upperSm.defaultState = emptyState;

        ClearTransitions(emptyState);
        ClearTransitions(shootUpper);
        ClearTransitions(pickupUpper);

        // AnyState -> Shoot (on Layer 1)
        if (shootClip != null)
        {
            var anyToShoot = upperSm.AddAnyStateTransition(shootUpper);
            anyToShoot.hasExitTime = false;
            anyToShoot.duration = 0.05f;
            anyToShoot.canTransitionToSelf = true;
            anyToShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");

            var shootToEmpty = shootUpper.AddTransition(emptyState);
            shootToEmpty.hasExitTime = true;
            shootToEmpty.exitTime = 0.80f;
            shootToEmpty.duration = 0.10f;
        }

        // AnyState -> Pickup (on Layer 1)
        if (pickupClip != null)
        {
            var anyToPickup = upperSm.AddAnyStateTransition(pickupUpper);
            anyToPickup.hasExitTime = false;
            anyToPickup.duration = 0.08f;
            anyToPickup.canTransitionToSelf = false;
            anyToPickup.AddCondition(AnimatorConditionMode.If, 0, "Pickup");

            var pickupToEmpty = pickupUpper.AddTransition(emptyState);
            pickupToEmpty.hasExitTime = true;
            pickupToEmpty.exitTime = 0.85f;
            pickupToEmpty.duration = 0.15f;
        }

        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        Debug.Log("[ZombieSetup] Configured StarterAssetsThirdPerson.controller with PlayerUpperBody layer (prevents shooting sinking glitch)!");
    }

    private struct SpawnerLocationDef
    {
        public string name;
        public Vector3 position;
        public float radius;
        public int hordeSize;

        public SpawnerLocationDef(string n, Vector3 pos, float rad, int count)
        {
            name = n;
            position = pos;
            radius = rad;
            hordeSize = count;
        }
    }

    public static void SetupSceneZombies(GameObject zombiePrefab)
    {
        if (zombiePrefab == null) return;

        var player = GameObject.Find("PlayerCapsule");
        Vector3 playerPos = player != null ? player.transform.position : new Vector3(479f, 19f, 148f);

        // 1. Remove all old spawners in the scene
        foreach (var oldSpawner in Object.FindObjectsByType<ZombieSpawner>(FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(oldSpawner.gameObject);
        }

        var oldRoot = GameObject.Find("Map_Zombie_Spawners");
        if (oldRoot != null)
        {
            Undo.DestroyObjectImmediate(oldRoot);
        }

        // 2. Remove all existing pre-placed zombies in the scene
        foreach (var oldZ in Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(oldZ.gameObject);
        }

        // 3. Create Root Container for all map spawners
        GameObject spawnerRoot = new GameObject("Map_Zombie_Spawners");
        Undo.RegisterCreatedObjectUndo(spawnerRoot, "Create Map Spawners Root");

        // 4. Define all House, Village, Cabin, Depot and Forest spawner locations across Flooded Grounds
        SpawnerLocationDef[] spawnerDefs = new SpawnerLocationDef[]
        {
            // ==========================================
            // HOUSES, VILLAGES, CABINS & BUILDINGS
            // ==========================================
            new SpawnerLocationDef("Barn_Compound_Houses", new Vector3(565f, 29.5f, 95f), 42f, 15),
            new SpawnerLocationDef("Eastern_Farmland_Cabins", new Vector3(595f, 28.5f, 120f), 38f, 15),
            new SpawnerLocationDef("Riverside_Cabins_Pier", new Vector3(545f, 18.2f, 285f), 42f, 15),
            new SpawnerLocationDef("North_Stilt_Cabins_Houses", new Vector3(500f, 21.5f, 365f), 40f, 15),
            new SpawnerLocationDef("Central_Village_BrickHouses", new Vector3(375f, 18.2f, 260f), 42f, 15),
            new SpawnerLocationDef("Upper_Town_Avenue_Houses", new Vector3(430f, 19.5f, 420f), 40f, 15),
            new SpawnerLocationDef("Grand_Villa_Estate_Houses", new Vector3(555f, 25.5f, 445f), 42f, 15),
            new SpawnerLocationDef("Hillside_Villa_Manor_Houses", new Vector3(525f, 22.0f, 525f), 42f, 15),
            new SpawnerLocationDef("Industrial_Warehouse_Depot", new Vector3(250f, 26.5f, 435f), 40f, 15),
            new SpawnerLocationDef("Shipping_Cargo_Harbor_Docks", new Vector3(230f, 21.0f, 410f), 38f, 15),
            new SpawnerLocationDef("West_Road_Barricade_Outpost", new Vector3(305f, 19.5f, 285f), 38f, 15),

            // ==========================================
            // FORESTS, WOODS & NATURE RESERVES
            // ==========================================
            new SpawnerLocationDef("East_Pine_Forest", new Vector3(620f, 26.5f, 175f), 45f, 15),
            new SpawnerLocationDef("Far_East_Border_Ridge_Forest", new Vector3(655f, 25.0f, 230f), 42f, 15),
            new SpawnerLocationDef("Ruined_Church_Deep_Forest", new Vector3(645f, 17.5f, 480f), 45f, 15),
            new SpawnerLocationDef("North_Cliffside_Pine_Forest", new Vector3(480f, 28.5f, 565f), 45f, 15),
            new SpawnerLocationDef("Lighthouse_Coastal_Forest", new Vector3(210f, 22.5f, 465f), 40f, 15),
            new SpawnerLocationDef("West_Murky_Swamp_Forest", new Vector3(330f, 16.5f, 220f), 42f, 15),
            new SpawnerLocationDef("South_River_Woods_Forest", new Vector3(430f, 16.5f, 85f), 38f, 15),
            new SpawnerLocationDef("SouthWest_Pine_Grove_Forest", new Vector3(370f, 18.0f, 110f), 40f, 15)
        };

        const float SafeRadius = 60f;
        int activeSpawnersCount = 0;

        foreach (var def in spawnerDefs)
        {
            float distToSpawn = Vector3.Distance(new Vector3(def.position.x, 0, def.position.z), new Vector3(playerPos.x, 0, playerPos.z));
            if (distToSpawn < SafeRadius)
            {
                Debug.LogWarning($"[ZombieSetup] Skipped spawner '{def.name}' - too close to player spawn safe zone ({distToSpawn:F1}m < {SafeRadius}m)!");
                continue;
            }

            GameObject spGo = new GameObject($"ZombieSpawner_{def.name}");
            Undo.RegisterCreatedObjectUndo(spGo, $"Create Spawner {def.name}");
            spGo.transform.SetParent(spawnerRoot.transform);
            spGo.transform.position = def.position;

            var spawner = spGo.AddComponent<ZombieSpawner>();
            spawner.zoneName = def.name;
            spawner.zombiePrefab = zombiePrefab;
            if (player != null) spawner.targetPlayer = player.transform;
            spawner.playerSpawnPoint = playerPos;
            spawner.playerSpawnSafeRadius = SafeRadius;
            spawner.targetZombies = def.hordeSize;
            spawner.initialZombies = def.hordeSize;
            spawner.spawnRadius = def.radius;
            spawner.minGroundHeight = 15.6f;
            spawner.minPlayerDistance = 15f;
            spawner.minDistanceBetweenZombies = 3.5f;
            spawner.spawnCheckInterval = 5f;

            EditorUtility.SetDirty(spGo);
            activeSpawnersCount++;
            Debug.Log($"[ZombieSetup] Created '{spGo.name}' at {def.position} (Horde: {def.hordeSize} zombies, Radius: {def.radius}m, Dist from player spawn: {distToSpawn:F1}m)");
        }

        Debug.Log($"[ZombieSetup] Successfully configured {activeSpawnersCount} zombie spawners across all houses and forests with at least 15 zombies each!");
    }
}
