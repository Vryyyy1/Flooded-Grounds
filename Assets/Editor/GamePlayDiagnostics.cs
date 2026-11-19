using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class GamePlayDiagnostics
{
    [MenuItem("Tools/Run GamePlay Diagnostics")]
    public static void RunDiagnostics()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("==================== [GamePlayDiagnostics] START ====================");
        var scene = EditorSceneManager.GetActiveScene();
        Debug.Log($"[GamePlayDiagnostics] Active Scene: '{scene.name}' (path: {scene.path})");

        // 1. Time scale
        Debug.Log($"[GamePlayDiagnostics] Time.timeScale: {Time.timeScale}");

        // 2. Check PlayerCapsule
        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogError("[GamePlayDiagnostics] CRITICAL: 'PlayerCapsule' not found in active scene!");
            var allPlayers = GameObject.FindGameObjectsWithTag("Player");
            Debug.Log($"[GamePlayDiagnostics] Found {allPlayers.Length} GameObjects tagged 'Player'");
            foreach (var p in allPlayers)
            {
                Debug.Log($"  - Player tagged: {p.name} (active={p.activeInHierarchy})");
            }
        }
        else
        {
            Debug.Log($"[GamePlayDiagnostics] PlayerCapsule found. Active: {player.activeInHierarchy}, Pos: {player.transform.position}, Layer: {player.layer} ({LayerMask.LayerToName(player.layer)})");

            // List all components on PlayerCapsule
            var comps = player.GetComponents<Component>();
            Debug.Log($"[GamePlayDiagnostics] PlayerCapsule has {comps.Length} components:");
            foreach (var c in comps)
            {
                if (c == null)
                {
                    Debug.LogError("[GamePlayDiagnostics] CRITICAL: Missing (MonoBehaviour) component found on PlayerCapsule!");
                }
                else
                {
                    bool isBehaviourEnabled = (c is Behaviour b) ? b.enabled : true;
                    Debug.Log($"  - {c.GetType().FullName} (enabled={isBehaviourEnabled})");
                }
            }

            // Check CharacterController
            var cc = player.GetComponent<CharacterController>();
            if (cc == null)
            {
                Debug.LogError("[GamePlayDiagnostics] CRITICAL: CharacterController missing on PlayerCapsule!");
            }
            else
            {
                Debug.Log($"[GamePlayDiagnostics] CharacterController enabled: {cc.enabled}, height: {cc.height}, radius: {cc.radius}, isGrounded: {cc.isGrounded}");
            }

            // Check Controller scripts
            var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
            Debug.Log($"[GamePlayDiagnostics] FirstPersonController: {(fpc != null ? (fpc.enabled ? "ENABLED" : "DISABLED") : "NOT PRESENT")}");

            // Check StarterAssetsInputs
            var inputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
            Debug.Log($"[GamePlayDiagnostics] StarterAssetsInputs: {(inputs != null ? (inputs.enabled ? "ENABLED" : "DISABLED") : "NOT PRESENT")}");

            // Check UnityEngine.InputSystem.PlayerInput
            var playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                Debug.Log($"[GamePlayDiagnostics] PlayerInput: enabled={playerInput.enabled}, actions={(playerInput.actions != null ? playerInput.actions.name : "NULL")}, currentActionMap={(playerInput.currentActionMap != null ? playerInput.currentActionMap.name : "NULL")}");
            }
            else
            {
                Debug.LogWarning("[GamePlayDiagnostics] PlayerInput component not on PlayerCapsule.");
            }

            // Check Armature & Animator
            var armature = player.transform.Find("Armature");
            if (armature == null)
            {
                foreach (var t in player.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Armature") { armature = t; break; }
                }
            }

            if (armature != null)
            {
                Debug.Log($"[GamePlayDiagnostics] Armature found: active={armature.gameObject.activeInHierarchy}, localPos={armature.localPosition}");
                var anim = armature.GetComponent<Animator>();
                if (anim != null)
                {
                    Debug.Log($"[GamePlayDiagnostics] Animator: enabled={anim.enabled}, controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}, avatar={(anim.avatar != null ? anim.avatar.name : "NULL")}");
                }
                else
                {
                    Debug.LogError("[GamePlayDiagnostics] Animator missing on Armature!");
                }
            }
            else
            {
                Debug.LogError("[GamePlayDiagnostics] Armature not found under PlayerCapsule!");
            }
        }

        // 3. Check Cameras
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[GamePlayDiagnostics] CRITICAL: No MainCamera found in scene!");
        }
        else
        {
            Debug.Log($"[GamePlayDiagnostics] MainCamera: active={mainCam.gameObject.activeInHierarchy}, pos={mainCam.transform.position}");
            var brain = mainCam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            Debug.Log($"[GamePlayDiagnostics] CinemachineBrain on MainCam: {(brain != null ? (brain.enabled ? "ENABLED" : "DISABLED") : "NOT PRESENT")}");
            var listener = mainCam.GetComponent<AudioListener>();
            Debug.Log($"[GamePlayDiagnostics] AudioListener on MainCam: {(listener != null ? (listener.enabled ? "ENABLED" : "DISABLED") : "NOT PRESENT")}");
        }

        var vcam = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (vcam != null)
        {
            Debug.Log($"[GamePlayDiagnostics] CinemachineCamera: name={vcam.gameObject.name}, active={vcam.gameObject.activeInHierarchy}, target={vcam.Target.TrackingTarget}");
        }
        else
        {
            Debug.LogWarning("[GamePlayDiagnostics] No CinemachineCamera found in scene!");
        }

        // 4. Check EventSystem
        var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        Debug.Log($"[GamePlayDiagnostics] EventSystem in scene: {(eventSystem != null ? (eventSystem.enabled ? "ENABLED" : "DISABLED") : "NOT PRESENT")}");

        Debug.Log("==================== [GamePlayDiagnostics] END ====================");
    }
}
