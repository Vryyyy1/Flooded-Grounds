using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using StarterAssets;

namespace IgnitedEditor
{
    // [InitializeOnLoad] (Disabled so ZombieAI's 6-state video animation runs cleanly without FBX reimport delays)
    public static class SetupMNDZombieAnimations
    {
        private const string FbxDir = "Assets/MN D/FBX";
        private const string ControllerPath = "Assets/ZombieMale_AAB/Animations/ZombieAnimator.controller";
        private const string SessionKey = "MNDZombieAnimations_Realistic_v4";

        static SetupMNDZombieAnimations()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ConfigureAnimationsAndController();
            };
        }

        [MenuItem("Tools/Setup Realistic Zombie Animations (Blend Tree & Combos)")]
        public static void ConfigureAnimationsAndController()
        {
            Debug.Log("=== [MND] Configuring Realistic Zombie Animations (Locomotion Blend Tree, Lunges & Hit Reactions) ===");

            // 1. Configure all FBX files in Assets/MN D/FBX to Humanoid with proper loop settings
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/IDOL.FBX", "IDOL", loop: true, firstFrame: 0, lastFrame: 70);
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/IDOL 2.FBX", "IDOL 2", loop: true, firstFrame: 100, lastFrame: 150);
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/WALKING.FBX", "WALKING", loop: true, firstFrame: 200, lastFrame: 320);
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/RUN.FBX", "RUN", loop: true, firstFrame: 400, lastFrame: 520);
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/ATTACK.FBX", "ATTACK", loop: false, firstFrame: 600, lastFrame: 700);
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/KRIT ATTACK.FBX", "KRIT ATTACK", loop: false, firstFrame: 800, lastFrame: 850);
            ConfigureFbxAsHumanoid("Assets/MN D/FBX/DEATH.FBX", "DEATH", loop: false, firstFrame: 900, lastFrame: 940);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 2. Load the humanoid animation clips from the imported FBX files
            AnimationClip idleClip = LoadClipFromFbx("Assets/MN D/FBX/IDOL.FBX", "IDOL");
            AnimationClip idle2Clip = LoadClipFromFbx("Assets/MN D/FBX/IDOL 2.FBX", "IDOL 2");
            AnimationClip walkClip = LoadClipFromFbx("Assets/MN D/FBX/WALKING.FBX", "WALKING");
            AnimationClip runClip = LoadClipFromFbx("Assets/MN D/FBX/RUN.FBX", "RUN");
            AnimationClip attackClip = LoadClipFromFbx("Assets/MN D/FBX/ATTACK.FBX", "ATTACK");
            AnimationClip kritAttackClip = LoadClipFromFbx("Assets/MN D/FBX/KRIT ATTACK.FBX", "KRIT ATTACK");
            AnimationClip deathClip = LoadClipFromFbx("Assets/MN D/FBX/DEATH.FBX", "DEATH");

            Debug.Log($"[MND] Loaded clips - Idle: {idleClip?.name}, Walk: {walkClip?.name}, Run: {runClip?.name}, Attack: {attackClip?.name}, KritAttack: {kritAttackClip?.name}, Death: {deathClip?.name}");

            // 3. Build Realistic Animator Controller
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Ensure Parameters
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "AttackHeavy", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

            var rootSm = controller.layers[0].stateMachine;

            // Clear any old messy states and transitions to ensure clean setup
            var oldStates = rootSm.states;
            for (int i = oldStates.Length - 1; i >= 0; i--)
            {
                rootSm.RemoveState(oldStates[i].state);
            }
            var oldAny = rootSm.anyStateTransitions;
            for (int i = oldAny.Length - 1; i >= 0; i--)
            {
                rootSm.RemoveAnyStateTransition(oldAny[i]);
            }

            // --- A. Locomotion 1D Blend Tree (Speed: 0 = Idle, 0.35 = Walk, 1.0 = Run) ---
            BlendTree blendTree;
            controller.CreateBlendTreeInController("LocomotionBlend", out blendTree);
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.useAutomaticThresholds = false;

            if (idleClip != null) blendTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) blendTree.AddChild(walkClip, 0.35f);
            if (runClip != null) blendTree.AddChild(runClip, 1.0f);

            var children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion == walkClip) children[i].timeScale = 2.1f;
                else if (children[i].motion == runClip) children[i].timeScale = 3.5f;
            }
            blendTree.children = children;

            AnimatorState locomotionState = rootSm.AddState("Locomotion", new Vector3(200, 100, 0));
            locomotionState.motion = blendTree;
            rootSm.defaultState = locomotionState;

            // --- B. Attack States (Normal Claw Lunge & Heavy Cleave) ---
            AnimatorState attack1State = rootSm.AddState("Attack_Normal", new Vector3(450, 40, 0));
            attack1State.motion = attackClip != null ? attackClip : idleClip;
            attack1State.speed = 1.05f;

            var toAttack1 = rootSm.AddAnyStateTransition(attack1State);
            toAttack1.hasExitTime = false;
            toAttack1.duration = 0.12f;
            toAttack1.canTransitionToSelf = false;
            toAttack1.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var attack1ToLoco = attack1State.AddTransition(locomotionState);
            attack1ToLoco.hasExitTime = true;
            attack1ToLoco.exitTime = 0.85f;
            attack1ToLoco.duration = 0.22f;

            AnimatorState attack2State = rootSm.AddState("Attack_Heavy", new Vector3(450, 120, 0));
            attack2State.motion = kritAttackClip != null ? kritAttackClip : attackClip;
            attack2State.speed = 1.05f;

            var toAttack2 = rootSm.AddAnyStateTransition(attack2State);
            toAttack2.hasExitTime = false;
            toAttack2.duration = 0.12f;
            toAttack2.canTransitionToSelf = false;
            toAttack2.AddCondition(AnimatorConditionMode.If, 0, "AttackHeavy");

            var attack2ToLoco = attack2State.AddTransition(locomotionState);
            attack2ToLoco.hasExitTime = true;
            attack2ToLoco.exitTime = 0.85f;
            attack2ToLoco.duration = 0.22f;

            // --- C. Hit Flinch / Stagger State ---
            AnimatorState hitState = rootSm.AddState("Hit", new Vector3(450, 200, 0));
            hitState.motion = idle2Clip != null ? idle2Clip : idleClip;
            hitState.speed = 1.25f;

            var toHit = rootSm.AddAnyStateTransition(hitState);
            toHit.hasExitTime = false;
            toHit.duration = 0.08f;
            toHit.canTransitionToSelf = false;
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");

            var hitToLoco = hitState.AddTransition(locomotionState);
            hitToLoco.hasExitTime = true;
            hitToLoco.exitTime = 0.70f;
            hitToLoco.duration = 0.18f;

            // --- D. Die Collapse State ---
            AnimatorState dieState = rootSm.AddState("Die", new Vector3(450, 280, 0));
            dieState.motion = deathClip != null ? deathClip : idleClip;
            dieState.speed = 1.0f;

            var toDie = rootSm.AddAnyStateTransition(dieState);
            toDie.hasExitTime = false;
            toDie.duration = 0.10f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

            var toDieBool = rootSm.AddAnyStateTransition(dieState);
            toDieBool.hasExitTime = false;
            toDieBool.duration = 0.10f;
            toDieBool.canTransitionToSelf = false;
            toDieBool.AddCondition(AnimatorConditionMode.If, 0, "IsDead");

            EditorUtility.SetDirty(controller);

            // 4. Update URP Material if needed
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/MN D/MATERIAL/New Material.mat");
            if (mat != null)
            {
                if (mat.shader == null || mat.shader.name.Contains("Standard") || mat.shader.name.Contains("InternalErrorShader") || mat.shader.name.Contains("Hidden"))
                {
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null)
                    {
                        mat.shader = urpLit;
                        EditorUtility.SetDirty(mat);
                        Debug.Log("[MND] Cyber Monster material upgraded to Universal Render Pipeline/Lit!");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== [MND] Realistic Locomotion Blend Tree & Combat Animations successfully configured! ===");
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static void ConfigureFbxAsHumanoid(string fbxPath, string clipName, bool loop, int firstFrame, int lastFrame)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[MND] ModelImporter not found for: {fbxPath}");
                return;
            }

            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            // Setup clip settings
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0)
            {
                var clip = clips[0];
                clip.name = clipName;
                clip.firstFrame = firstFrame;
                clip.lastFrame = lastFrame;
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                importer.clipAnimations = new ModelImporterClipAnimation[] { clip };
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip LoadClipFromFbx(string fbxPath, string clipName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null) return null;

            foreach (var a in assets)
            {
                if (a is AnimationClip c && (c.name == clipName || c.name.Equals(clipName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return c;
                }
            }

            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }

            return null;
        }
    }
}
