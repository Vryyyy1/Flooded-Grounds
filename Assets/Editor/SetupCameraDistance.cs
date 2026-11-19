using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using StarterAssets;

public static class SetupCameraDistance
{
    private const string FollowCamPrefabPath = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerFollowCamera.prefab";
    private const string PlayerCapsulePrefabPath = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab";

    [MenuItem("Tools/Setup First Person Mode")]
    public static void Execute()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("=== [SetupFirstPerson] Configuring First Person View & Low Poly Weapon Setup ===");

        // 1. Configure in Active Scene
        ConfigureSceneCamera();

        // 2. Configure Prefabs so changes persist everywhere
        ConfigurePrefabs();

        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupFirstPerson] Scene and Prefabs saved successfully!");
        }

        Debug.Log("=== [SetupFirstPerson] Complete! True First Person mode is active! ===");
    }

    public static void ConfigureSceneCamera()
    {
        // 1. Adjust PlayerCapsule, PlayerCameraRoot, and Armature
        var player = GameObject.Find("PlayerCapsule");
        Transform fpHolder = null;

        if (player != null)
        {
            Undo.RecordObject(player, "Configure PlayerCapsule First Person");
            player.tag = "Player";
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) player.layer = playerLayer;

            var camRoot = player.transform.Find("PlayerCameraRoot");
            if (camRoot != null)
            {
                Undo.RecordObject(camRoot, "Configure PlayerCameraRoot");
                // Position pivot at true eye height (1.55m, 0.05m forward)
                camRoot.localPosition = new Vector3(0f, 1.60f, 0.05f);
                Debug.Log($"[SetupFirstPerson] Set PlayerCameraRoot localPosition to {camRoot.localPosition}");

                // Setup FP_WeaponHolder under camera root
                // Deduplicate any extra holders
                var holders = camRoot.GetComponentsInChildren<Transform>(true);
                Transform primaryHolder = null;
                foreach (var t in holders)
                {
                    if (t != null && t.name == "FP_WeaponHolder" && t.parent == camRoot)
                    {
                        if (primaryHolder == null)
                        {
                            primaryHolder = t;
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(t.gameObject);
                        }
                    }
                }

                if (primaryHolder == null)
                {
                    var holderGo = new GameObject("FP_WeaponHolder");
                    Undo.RegisterCreatedObjectUndo(holderGo, "Create FP_WeaponHolder");
                    holderGo.transform.SetParent(camRoot, false);
                    holderGo.transform.localPosition = new Vector3(0.18f, -0.14f, 0.35f);
                    holderGo.transform.localRotation = Quaternion.identity;
                    fpHolder = holderGo.transform;
                }
                else
                {
                    primaryHolder.localPosition = new Vector3(0.18f, -0.14f, 0.35f);
                    primaryHolder.localRotation = Quaternion.identity;
                    fpHolder = primaryHolder;
                }

                // Clean up any stray root FP_WeaponHolder objects in the scene
                var allSceneHolders = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var go in allSceneHolders)
                {
                    if (go != null && go.name == "FP_WeaponHolder" && (go.transform.parent == null || !go.transform.IsChildOf(player.transform)))
                    {
                        Undo.DestroyObjectImmediate(go);
                    }
                }
            }

            // Armature: hide geometry from camera view to prevent clipping inside head/chest, but keep shadows
            var armature = player.transform.Find("Armature");
            if (armature != null)
            {
                Undo.RecordObject(armature, "Configure Armature");
                foreach (var col in armature.GetComponentsInChildren<Collider>(true))
                {
                    Undo.DestroyObjectImmediate(col);
                }
                foreach (var rend in armature.GetComponentsInChildren<Renderer>(true))
                {
                    Undo.RecordObject(rend, "Set ShadowsOnly");
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                SetLayerRecursively(armature.gameObject, 2); // Ignore Raycast

                // Ensure FootstepHandler is attached
                if (armature.GetComponent<FootstepHandler>() == null)
                {
                    Undo.AddComponent<FootstepHandler>(armature.gameObject);
                }
            }

            if (player.GetComponent<FootstepHandler>() == null)
            {
                Undo.AddComponent<FootstepHandler>(player);
            }

            // Connect PlayerWeaponManager to FP_WeaponHolder
            var pwm = player.GetComponent<PlayerWeaponManager>();
            if (pwm != null && fpHolder != null)
            {
                Undo.RecordObject(pwm, "Set Weapon Hold Point");
                pwm.weaponHoldPoint = fpHolder;
                pwm.startWithRifle = false;
            }
        }

        // 2. Adjust PlayerFollowCamera in Scene
        var followCamGo = GameObject.Find("PlayerFollowCamera");
        if (followCamGo != null)
        {
            Undo.RecordObject(followCamGo, "Configure PlayerFollowCamera");

            // CinemachineCamera settings: near clip plane 0.03m so FP weapon never clips
            var cmCam = followCamGo.GetComponent<CinemachineCamera>();
            if (cmCam != null)
            {
                Undo.RecordObject(cmCam, "Configure CinemachineCamera");
                var lens = cmCam.Lens;
                lens.NearClipPlane = 0.02f;
                cmCam.Lens = lens;
            }

            // CinemachineThirdPersonFollow settings: 0m distance for pure First-Person
            var tpFollow = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
            if (tpFollow == null)
            {
                tpFollow = Undo.AddComponent<CinemachineThirdPersonFollow>(followCamGo);
            }

            if (tpFollow != null)
            {
                Undo.RecordObject(tpFollow, "Configure CinemachineThirdPersonFollow");
                tpFollow.CameraDistance = 0f;
                tpFollow.ShoulderOffset = Vector3.zero;
                tpFollow.VerticalArmLength = 0f;
                tpFollow.CameraSide = 1.0f;
                tpFollow.Damping = Vector3.zero;

                var avoid = tpFollow.AvoidObstacles;
                avoid.Enabled = false;
                tpFollow.AvoidObstacles = avoid;
            }

            // CameraZoomController: disable or clamp to 0 so mouse wheel never pushes camera back
            var zoomCtrl = followCamGo.GetComponent<CameraZoomController>();
            if (zoomCtrl != null)
            {
                Undo.RecordObject(zoomCtrl, "Configure CameraZoomController");
                zoomCtrl.cameraDistance = 0f;
                zoomCtrl.minDistance = 0f;
                zoomCtrl.maxDistance = 0f;
                zoomCtrl.enabled = false;
            }
        }

        // 3. Configure MainCamera near clip plane
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Configure MainCamera");
            mainCam.nearClipPlane = 0.02f;
        }
    }

    public static void ConfigurePrefabs()
    {
        // 1. Update PlayerFollowCamera.prefab
        var followCamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FollowCamPrefabPath);
        if (followCamPrefab != null)
        {
            var cmCam = followCamPrefab.GetComponent<CinemachineCamera>();
            if (cmCam != null)
            {
                var lens = cmCam.Lens;
                lens.NearClipPlane = 0.02f;
                cmCam.Lens = lens;
            }

            var tpFollow = followCamPrefab.GetComponent<CinemachineThirdPersonFollow>();
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

            var zoom = followCamPrefab.GetComponent<CameraZoomController>();
            if (zoom != null)
            {
                zoom.cameraDistance = 0f;
                zoom.minDistance = 0f;
                zoom.maxDistance = 0f;
                zoom.enabled = false;
            }

            EditorUtility.SetDirty(followCamPrefab);
            Debug.Log("[SetupFirstPerson] Updated PlayerFollowCamera.prefab!");
        }

        // 2. Update PlayerCapsule.prefab
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCapsulePrefabPath);
        if (playerPrefab != null)
        {
            playerPrefab.tag = "Player";
            var root = playerPrefab.transform.Find("PlayerCameraRoot");
            if (root != null)
            {
                root.localPosition = new Vector3(0f, 1.60f, 0.05f);
                var fpHolder = root.Find("FP_WeaponHolder");
                if (fpHolder == null)
                {
                    var holderGo = new GameObject("FP_WeaponHolder");
                    holderGo.transform.SetParent(root, false);
                    holderGo.transform.localPosition = new Vector3(0.20f, -0.18f, 0.38f);
                    holderGo.transform.localRotation = Quaternion.identity;
                }
            }

            var armature = playerPrefab.transform.Find("Armature");
            if (armature != null)
            {
                foreach (var rend in armature.GetComponentsInChildren<Renderer>(true))
                {
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                if (armature.GetComponent<FootstepHandler>() == null)
                {
                    armature.gameObject.AddComponent<FootstepHandler>();
                }
            }

            if (playerPrefab.GetComponent<FootstepHandler>() == null)
            {
                playerPrefab.AddComponent<FootstepHandler>();
            }

            EditorUtility.SetDirty(playerPrefab);
            Debug.Log("[SetupFirstPerson] Updated PlayerCapsule.prefab!");
        }
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
