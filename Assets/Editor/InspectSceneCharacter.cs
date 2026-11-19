using UnityEngine;
using UnityEditor;

// [InitializeOnLoad] (Disabled to prevent playmode delays)
public static class InspectSceneCharacter
{

    [MenuItem("Tools/Inspect Scene Character")]
    public static void Inspect()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Debug.Log("================ CHARACTER INSPECTION ================");
        var player = GameObject.Find("PlayerCapsule");
        if (player == null)
        {
            Debug.LogError("PlayerCapsule not found in active scene!");
            return;
        }

        Debug.Log($"PlayerCapsule pos={player.transform.position}, localPos={player.transform.localPosition}, layer={player.layer} ({LayerMask.LayerToName(player.layer)})");

        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            Debug.Log($"CharacterController: center={cc.center}, height={cc.height}, radius={cc.radius}, skinWidth={cc.skinWidth}, stepOffset={cc.stepOffset}, isGrounded={cc.isGrounded}");
        }

        var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
        if (fpc != null)
        {
            Debug.Log($"FirstPersonController: MoveSpeed={fpc.MoveSpeed}, SprintSpeed={fpc.SprintSpeed}, GroundedOffset={fpc.GroundedOffset}, GroundedRadius={fpc.GroundedRadius}, GroundLayers={fpc.GroundLayers.value}, CharacterAnimator={fpc.CharacterAnimator}");
        }

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
            Debug.Log($"Armature pos={armature.position}, localPos={armature.localPosition}, localScale={armature.localScale}, active={armature.gameObject.activeSelf}, layer={armature.gameObject.layer} ({LayerMask.LayerToName(armature.gameObject.layer)})");
            var anim = armature.GetComponent<Animator>();
            if (anim != null)
            {
                Debug.Log($"Animator: controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}, avatar={(anim.avatar != null ? anim.avatar.name : "NULL")}, applyRootMotion={anim.applyRootMotion}, cullingMode={anim.cullingMode}, hasStateStandIdle={anim.HasState(0, Animator.StringToHash("Idle Walk Run Blend"))}");
            }
            else
            {
                Debug.LogError("No Animator on Armature!");
            }

            var smrs = armature.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                Debug.Log($"SkinnedMeshRenderer: name={smr.gameObject.name}, enabled={smr.enabled}, sharedMesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "NULL")}, bounds={smr.bounds}");
            }

            var cols = armature.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                Debug.Log($"Armature Collider: name={c.gameObject.name}, enabled={c.enabled}, type={c.GetType().Name}, bounds={c.bounds}");
            }
        }
        else
        {
            Debug.LogError("Armature NOT FOUND under PlayerCapsule!");
        }

        // Check camera
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"MainCamera pos={mainCam.transform.position}, parent={(mainCam.transform.parent != null ? mainCam.transform.parent.name : "ROOT")}");
        }

        // Raycast down to find ground level under PlayerCapsule
        RaycastHit hit;
        if (Physics.Raycast(player.transform.position + Vector3.up * 2f, Vector3.down, out hit, 100f))
        {
            Debug.Log($"Ground raycast under player hit '{hit.collider.gameObject.name}' at point={hit.point}, distance from player.pos={player.transform.position.y - hit.point.y}");
        }
        else
        {
            Debug.LogWarning("Ground raycast under player did NOT hit anything!");
        }
        Debug.Log("======================================================");
    }
}
