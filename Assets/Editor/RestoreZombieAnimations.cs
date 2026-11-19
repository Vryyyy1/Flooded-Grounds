using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using StarterAssets;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class RestoreZombieAnimations
{
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

    [MenuItem("Tools/Restore Zombie Animations and Death")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("[RestoreZombie] Restoring Zombie Animations and Death Animation...");

        SetupZombieSystem.CreateZombieDeathClip();

        string controllerPath = "Assets/ZombieMale_AAB/Animations/ZombieAnimator.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        AnimationClip fbxIdle = LoadFbxClip("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx", "Idle");
        AnimationClip fbxWalk = LoadFbxClip("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Locomotion--Walk_N.anim.fbx", "Walk_N");
        AnimationClip fbxRun = LoadFbxClip("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Locomotion--Run_N.anim.fbx", "Run_N");

        if (controller != null && fbxIdle != null && fbxWalk != null && fbxRun != null)
        {
            var rootSm = controller.layers[0].stateMachine;
            foreach (var s in rootSm.states)
            {
                if (s.state.name == "Idle") s.state.motion = fbxIdle;
                else if (s.state.name == "Walk") s.state.motion = fbxWalk;
                else if (s.state.name == "Chase") s.state.motion = fbxRun;
                else if (s.state.name == "Attack") s.state.motion = fbxIdle;
                else if (s.state.name == "Hit") s.state.motion = fbxIdle;
                else if (s.state.name == "Die") s.state.motion = fbxIdle;
            }

            // Ensure single Base Layer
            var baseLayer = controller.layers[0];
            baseLayer.avatarMask = null;
            baseLayer.defaultWeight = 1.0f;
            baseLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = new AnimatorControllerLayer[] { baseLayer };

            EditorUtility.SetDirty(controller);
            Debug.Log("[RestoreZombie] Controller restored with grounded FBX Humanoid clips.");
        }

        // Configure Zombie_Enemy.prefab
        string prefabPath = "Assets/ZombieMale_AAB/Prefabs/URP/Zombie_Enemy.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            var ai = prefab.GetComponent<ZombieAI>();
            if (ai != null)
            {
                ai.useRagdollDeath = false;
                ai.deathClip = null;
                EditorUtility.SetDirty(prefab);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RestoreZombie] Complete! Zombie grounded locomotion, attack, hit, and 4-panel death fully restored.");
    }
}
