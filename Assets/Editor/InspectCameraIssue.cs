using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public class InspectCameraIssue
{

    [MenuItem("Tools/Inspect Camera Issue")]
    public static void CheckCam()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var mainCam = GameObject.Find("MainCamera");
        if (mainCam != null)
        {
            var brain = mainCam.GetComponent<CinemachineBrain>();
            Debug.Log($"[CameraIssue] MainCamera: pos={mainCam.transform.position}, parent={mainCam.transform.parent?.name}, hasBrain={brain != null}, brainEnabled={brain?.enabled}");
        }

        var followCam = GameObject.Find("PlayerFollowCamera");
        if (followCam != null)
        {
            var cmCam = followCam.GetComponent<CinemachineCamera>();
            if (cmCam != null)
            {
                string trackName = cmCam.Target.TrackingTarget != null ? cmCam.Target.TrackingTarget.name : "null";
                string lookName = cmCam.Target.LookAtTarget != null ? cmCam.Target.LookAtTarget.name : "null";
                Debug.Log($"[CameraIssue] PlayerFollowCamera CinemachineCamera: TrackingTarget={trackName}, LookAtTarget={lookName}, enabled={cmCam.enabled}");
            }
        }

        var camRoot = GameObject.Find("PlayerCameraRoot");
        if (camRoot != null)
        {
            Debug.Log($"[CameraIssue] PlayerCameraRoot: pos={camRoot.transform.position}, localPos={camRoot.transform.localPosition}, parent={camRoot.transform.parent?.name}");
        }
    }
}
