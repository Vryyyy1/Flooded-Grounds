using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using StarterAssets;

[InitializeOnLoad]
public static class FixGamePlayAndPlayability
{
    private const string ControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
    private const string MaskPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Player_UpperBodyMask.mask";

    static FixGamePlayAndPlayability()
    {
        EditorApplication.delayCall += ExecuteFix;
    }

    [MenuItem("Tools/Fix Game Playability (All Controls & Camera)")]
    public static void ExecuteFix()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("==================== [FixGamePlay] STARTING COMPLETE REPAIR ====================");

        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogError("[FixGamePlay] 'PlayerCapsule' not found in active scene!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Fix Game Playability");

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0) playerLayer = 8;
        player.layer = playerLayer;
        player.tag = "Player";

        // Ground mask: include all layers except Ignore Raycast (2), UI (5), and Player (playerLayer)
        int groundMask = ~((1 << 2) | (1 << 5) | (1 << playerLayer));

        // 1. Configure CharacterController
        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = Undo.AddComponent<CharacterController>(player);
        if (cc != null)
        {
            cc.enabled = true;
            cc.height = 2.0f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 1.0f, 0f);
            cc.stepOffset = 0.5f;
            cc.slopeLimit = 60f;
            cc.skinWidth = 0.05f;
            cc.minMoveDistance = 0f;
            Debug.Log("[FixGamePlay] CharacterController configured: height=2.0, center=(0, 1.0, 0), stepOffset=0.5");
        }

        // 2. Configure PlayerCameraRoot
        Transform camRoot = player.transform.Find("PlayerCameraRoot");
        if (camRoot == null)
        {
            var rootGo = new GameObject("PlayerCameraRoot");
            rootGo.transform.SetParent(player.transform, false);
            camRoot = rootGo.transform;
        }
        camRoot.localPosition = new Vector3(0f, 1.55f, 0f);
        camRoot.localRotation = Quaternion.identity;
        camRoot.localScale = Vector3.one;

        // 3. Configure FirstPersonController
        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null)
        {
            fpc.enabled = true;
            fpc.MoveSpeed = 4.5f;
            fpc.SprintSpeed = 7.5f;
            fpc.RotationSpeed = 1.2f;
            fpc.JumpHeight = 1.3f;
            fpc.Gravity = -15.0f;
            fpc.GroundedOffset = 0.05f;
            fpc.GroundedRadius = 0.35f;
            fpc.GroundLayers = groundMask;
            fpc.CinemachineCameraTarget = camRoot.gameObject;
            fpc.TopClamp = 89f;
            fpc.BottomClamp = -89f;
            Debug.Log("[FixGamePlay] FirstPersonController configured with target=PlayerCameraRoot, MoveSpeed=4.5");
        }

        // 4. Configure StarterAssetsInputs
        var inputs = player.GetComponent<StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.enabled = true;
            inputs.cursorLocked = true;
            inputs.cursorInputForLook = true;
            Debug.Log("[FixGamePlay] StarterAssetsInputs configured: cursorLocked=true, cursorInputForLook=true");
        }

        // 5. Configure PlayerHealth
        var health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.maxHealth = 100f;
            health.currentHealth = 100f;
            health.isDead = false;
        }

        // 6. Configure Armature & Animator
        Transform armature = player.transform.Find("Armature");
        if (armature == null)
        {
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Armature") { armature = t; break; }
            }
        }

        if (armature != null)
        {
            armature.localPosition = Vector3.zero;
            armature.localRotation = Quaternion.identity;
            armature.localScale = Vector3.one;
            SetLayerRecursively(armature.gameObject, 2); // Ignore Raycast

            // Disable child colliders on skeleton to prevent blocking movement
            foreach (var col in armature.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            var anim = armature.GetComponent<Animator>();
            if (anim == null) anim = Undo.AddComponent<Animator>(armature.gameObject);

            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Debug.Log($"[FixGamePlay] Assigned '{ctrl.name}' to Armature Animator (applyRootMotion=false).");
            }

            if (fpc != null)
            {
                fpc.CharacterAnimator = anim;
            }

            // Ensure FootstepHandler is attached to receive all AnimationEvents (OnFootstep, OnLand, etc.)
            var footstep = armature.GetComponent<FootstepHandler>();
            if (footstep == null) footstep = Undo.AddComponent<FootstepHandler>(armature.gameObject);

            var playerFootstep = player.GetComponent<FootstepHandler>();
            if (playerFootstep == null) playerFootstep = Undo.AddComponent<FootstepHandler>(player);

            // 7. Configure WeaponHolder under Right_Hand
            Transform rightHand = null;
            foreach (var t in armature.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Right_Hand" || t.name == "RightHand" || t.name.EndsWith("Hand.R"))
                {
                    rightHand = t;
                    break;
                }
            }

            Transform fpHolder = camRoot != null ? camRoot.Find("FP_WeaponHolder") : null;
            if (fpHolder == null && camRoot != null)
            {
                var holderGo = new GameObject("FP_WeaponHolder");
                holderGo.transform.SetParent(camRoot, false);
                holderGo.transform.localPosition = new Vector3(0.18f, -0.16f, 0.40f);
                holderGo.transform.localRotation = Quaternion.identity;
                holderGo.transform.localScale = Vector3.one;
                fpHolder = holderGo.transform;
            }

            var weaponManager = player.GetComponent<PlayerWeaponManager>();
            if (weaponManager != null && fpHolder != null)
            {
                weaponManager.weaponHoldPoint = fpHolder;
                weaponManager.startWithRifle = false;
                Debug.Log("[FixGamePlay] First-Person FP_WeaponHolder mounted to PlayerCameraRoot and linked to PlayerWeaponManager.");
            }

            if (armature != null)
            {
                foreach (var rend in armature.GetComponentsInChildren<Renderer>(true))
                {
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        // 8. Configure PlayerFollowCamera in Scene
        var followCamGo = GameObject.Find("PlayerFollowCamera");
        if (followCamGo != null)
        {
            var cmCam = followCamGo.GetComponent<CinemachineCamera>();
            if (cmCam != null)
            {
                cmCam.enabled = true;
                cmCam.Target.TrackingTarget = camRoot;
                cmCam.Target.LookAtTarget = camRoot;

                var lens = cmCam.Lens;
                lens.NearClipPlane = 0.05f;
                cmCam.Lens = lens;
            }

            var tpFollow = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
            if (tpFollow == null) tpFollow = Undo.AddComponent<CinemachineThirdPersonFollow>(followCamGo);
            if (tpFollow != null)
            {
                tpFollow.CameraDistance = 0f;
                tpFollow.ShoulderOffset = Vector3.zero;
                tpFollow.VerticalArmLength = 0f;
                tpFollow.CameraSide = 1.0f;
                tpFollow.Damping = Vector3.zero;

                var avoid = tpFollow.AvoidObstacles;
                avoid.Enabled = false;
                tpFollow.AvoidObstacles = avoid;
            }

            var zoom = followCamGo.GetComponent<CameraZoomController>();
            if (zoom == null) zoom = Undo.AddComponent<CameraZoomController>(followCamGo);
            if (zoom != null)
            {
                zoom.thirdPersonFollow = tpFollow;
                zoom.cameraDistance = 0f;
                zoom.minDistance = 0f;
                zoom.maxDistance = 0f;
                zoom.enabled = false;
            }
            Debug.Log("[FixGamePlay] PlayerFollowCamera configured: TrackingTarget=PlayerCameraRoot, CameraDistance=0 (First-Person Lock)");
        }

        // 9. Configure MainCamera in Scene
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.nearClipPlane = 0.05f;
            var brain = mainCam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
            if (brain != null) brain.enabled = true;
            Debug.Log("[FixGamePlay] MainCamera configured: CinemachineBrain enabled, nearClipPlane=0.05");
        }

        // 10. Snap Player to floor / bridge
        if (Physics.Raycast(player.transform.position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 oldPos = player.transform.position;
            player.transform.position = new Vector3(oldPos.x, hit.point.y, oldPos.z);
            Debug.Log($"[FixGamePlay] Snapped PlayerCapsule to ground Y={hit.point.y:F2} (hit '{hit.collider.gameObject.name}')");
        }

        // 11. Save Scene and Assets
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[FixGamePlay] Scene and Prefabs saved successfully!");
        }

        Debug.Log("==================== [FixGamePlay] REPAIR COMPLETE! GAME IS READY TO PLAY! ====================");
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

