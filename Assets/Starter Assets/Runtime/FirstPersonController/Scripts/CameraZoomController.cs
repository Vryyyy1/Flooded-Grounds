using UnityEngine;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Controls camera distance for CinemachineThirdPersonFollow.
    /// Provides comfortable default distance and smooth mouse wheel zoom to prevent the camera from clipping inside the character.
    /// </summary>
    public class CameraZoomController : MonoBehaviour
    {
        [Header("Cinemachine Component")]
        [Tooltip("The CinemachineThirdPersonFollow component (auto-detected if null)")]
        public CinemachineThirdPersonFollow thirdPersonFollow;

        [Header("Distance Settings")]
        [Tooltip("Default camera distance from character")]
        public float cameraDistance = 0f;

        [Tooltip("Minimum distance from character to avoid seeing inside the model")]
        public float minDistance = 0f;

        [Tooltip("Maximum camera distance (set 0 for first person lock)")]
        public float maxDistance = 0f;

        [Tooltip("Zoom speed when scrolling mouse wheel")]
        public float zoomSensitivity = 1.5f;

        [Tooltip("Smoothing speed for zooming")]
        public float smoothSpeed = 12f;

        private float _targetDistance;

        private void Awake()
        {
            if (thirdPersonFollow == null)
            {
                thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();
            }

            if (thirdPersonFollow != null)
            {
                if (cameraDistance < minDistance) cameraDistance = minDistance;
                thirdPersonFollow.CameraDistance = cameraDistance;
            }
            _targetDistance = cameraDistance;
        }

        private void Update()
        {
            if (!enabled || thirdPersonFollow == null || maxDistance <= 0f) return;

            float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
                if (Mathf.Abs(scrollDelta.y) > 0.01f)
                {
                    scroll = scrollDelta.y;
                }
            }
#endif
            if (Mathf.Abs(scroll) < 0.01f)
            {
                try
                {
                    scroll = Input.GetAxis("Mouse ScrollWheel");
                }
                catch {}
            }

            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Scroll up (positive) zooms in (decreases distance)
                // Scroll down (negative) zooms out (increases distance)
                float sign = scroll > 0f ? -1f : 1f;
                _targetDistance = Mathf.Clamp(_targetDistance + sign * zoomSensitivity * 0.35f, minDistance, maxDistance);
            }

            if (Mathf.Abs(thirdPersonFollow.CameraDistance - _targetDistance) > 0.001f)
            {
                thirdPersonFollow.CameraDistance = Mathf.Lerp(thirdPersonFollow.CameraDistance, _targetDistance, Time.deltaTime * smoothSpeed);
                cameraDistance = thirdPersonFollow.CameraDistance;
            }
        }

        public void SetDistance(float distance)
        {
            _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            if (thirdPersonFollow != null)
            {
                thirdPersonFollow.CameraDistance = _targetDistance;
                cameraDistance = _targetDistance;
            }
        }
    }
}
