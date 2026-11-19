using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using StarterAssets;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public class SetupPlayerAndFlashlight
{

    [MenuItem("Tools/Setup Player Movement and Flashlight")]
    public static void ExecuteSetup()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var activeScene = EditorSceneManager.GetActiveScene();
        var playerGo = GameObject.Find("PlayerCapsule");
        if (playerGo == null)
        {
            Debug.LogError("[SetupPlayer] 'PlayerCapsule' not found in active scene!");
            return;
        }

        Debug.Log("[SetupPlayer] Configuring PlayerCapsule in scene...");

        // 1. CharacterController setup
        var cc = playerGo.GetComponent<CharacterController>();
        if (cc != null)
        {
            Undo.RecordObject(cc, "Configure CharacterController");
            cc.stepOffset = 0.5f;
            cc.slopeLimit = 60f;
            cc.skinWidth = 0.05f;
            cc.minMoveDistance = 0f;
            Debug.Log("[SetupPlayer] CharacterController: stepOffset=0.5, slopeLimit=60, skinWidth=0.05");
        }

        // 2. FirstPersonController setup
        var fpc = playerGo.GetComponent<FirstPersonController>();
        if (fpc != null)
        {
            Undo.RecordObject(fpc, "Configure FirstPersonController");
            fpc.MoveSpeed = 5.0f;
            fpc.SprintSpeed = 8.5f;
            fpc.RotationSpeed = 1.5f;
            fpc.JumpHeight = 1.4f;

            // Include all environment layers (Default layer 0, Terrain layer 9, etc.) except Ignore Raycast (2), UI (5), Player (8)
            fpc.GroundLayers = ~((1 << 2) | (1 << 5) | (1 << playerGo.layer));
            Debug.Log($"[SetupPlayer] FirstPersonController: MoveSpeed={fpc.MoveSpeed}, SprintSpeed={fpc.SprintSpeed}, GroundLayers mask={fpc.GroundLayers.value}");
        }

        // 3. PlayerInput setup
        var playerInput = playerGo.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            Undo.RecordObject(playerInput, "Configure PlayerInput");
            playerInput.defaultControlScheme = "KeyboardMouse";
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;
            Debug.Log("[SetupPlayer] PlayerInput: defaultControlScheme set to KeyboardMouse");
        }

        // 4. StarterAssetsInputs setup
        var inputs = playerGo.GetComponent<StarterAssetsInputs>();
        if (inputs != null)
        {
            Undo.RecordObject(inputs, "Configure StarterAssetsInputs");
            inputs.cursorLocked = true;
            inputs.cursorInputForLook = true;
        }

        // 5. Flashlight setup on PlayerCameraRoot
        Transform camRoot = playerGo.transform.Find("PlayerCameraRoot");
        GameObject targetHost = camRoot != null ? camRoot.gameObject : playerGo;

        var flashlight = targetHost.GetComponent<FlashlightController>();
        if (flashlight == null)
        {
            flashlight = Undo.AddComponent<FlashlightController>(targetHost);
        }

        Undo.RecordObject(flashlight, "Configure Flashlight");
        flashlight.toggleKey = KeyCode.F;
        flashlight.startOn = false;

        // Ensure Spotlight child
        Transform existingLightT = targetHost.transform.Find("FlashlightBeam");
        GameObject lightGo;
        if (existingLightT == null)
        {
            lightGo = new GameObject("FlashlightBeam");
            Undo.RegisterCreatedObjectUndo(lightGo, "Create FlashlightBeam");
            lightGo.transform.SetParent(targetHost.transform, false);
            lightGo.transform.localPosition = new Vector3(0.2f, -0.1f, 0.25f);
            lightGo.transform.localRotation = Quaternion.identity;
        }
        else
        {
            lightGo = existingLightT.gameObject;
        }

        var lightComp = lightGo.GetComponent<Light>();
        if (lightComp == null)
        {
            lightComp = Undo.AddComponent<Light>(lightGo);
        }

        Undo.RecordObject(lightComp, "Configure Flashlight Light");
        lightComp.type = LightType.Spot;
        lightComp.range = 60.0f;
        lightComp.spotAngle = 68.0f;
        lightComp.innerSpotAngle = 44.0f;
        lightComp.intensity = 32.0f;
        lightComp.color = new Color(1.0f, 0.97f, 0.90f, 1.0f);
        lightComp.shadows = LightShadows.Soft;
        lightComp.enabled = false; // Starts off, pressing F turns it ON

        flashlight.flashlightLight = lightComp;

        Debug.Log($"[SetupPlayer] Flashlight successfully configured on '{targetHost.name}'! (Key: F, Starts: OFF)");

        // Save scene — only safe outside play mode
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[SetupPlayer] Scene_A saved successfully!");
        }
    }
}
