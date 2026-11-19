using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class SetupLowPolyShootingAnimation
{
    private const string ArFireFbxPath = "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Animations/Character/ARs/A_FP_PCH_AR_Fire.fbx";
    private const string HandgunFireFbxPath = "Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Animations/Character/Handguns/A_FP_PCH_Handgun_Fire.fbx";
    private const string PlayerShootClipPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Player_Shoot.anim";
    private const string ControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";

    [MenuItem("Tools/Setup Low Poly Shooting Animation")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("[LowPolyShooting] Setting up Low Poly Shooting Animation on Character Armature...");

        // 1. Ensure both FBX models are imported as Humanoid
        EnsureHumanoidImport(ArFireFbxPath);
        EnsureHumanoidImport(HandgunFireFbxPath);

        // 2. Load the AnimationClip from the FBX
        AnimationClip arFireClip = LoadClipFromFbx(ArFireFbxPath);
        AnimationClip handgunFireClip = LoadClipFromFbx(HandgunFireFbxPath);

        if (arFireClip != null)
        {
            Debug.Log($"[LowPolyShooting] Loaded AR Fire Clip: '{arFireClip.name}' (isHumanMotion={arFireClip.isHumanMotion}, length={arFireClip.length}s)");
        }
        else
        {
            Debug.LogWarning($"[LowPolyShooting] Could not load AR Fire Clip from {ArFireFbxPath}");
        }

        // 3. Update StarterAssetsThirdPerson.controller PlayerUpperBody layer
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            var layers = controller.layers;
            if (layers.Length > 1 && layers[1].name == "PlayerUpperBody")
            {
                var upperSm = layers[1].stateMachine;
                foreach (var cs in upperSm.states)
                {
                    if (cs.state.name == "Shoot")
                    {
                        if (arFireClip != null)
                        {
                            cs.state.motion = arFireClip;
                            cs.state.speed = 1.0f;
                            Debug.Log($"[LowPolyShooting] Assigned '{arFireClip.name}' to 'Shoot' state in PlayerUpperBody layer!");
                        }
                    }
                }
            }
            EditorUtility.SetDirty(controller);
        }

        // 4. Also update FixGamePlayAndPlayability so character armature gets the right setup
        FixGamePlayAndPlayability.ExecuteFix();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LowPolyShooting] Successfully updated character armature shooting animation!");
    }

    private static void EnsureHumanoidImport(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                Debug.Log($"[LowPolyShooting] Configured '{fbxPath}' as Humanoid.");
            }
        }
    }

    private static AnimationClip LoadClipFromFbx(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}
