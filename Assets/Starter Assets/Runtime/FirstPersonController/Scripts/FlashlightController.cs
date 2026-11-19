using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class FlashlightController : MonoBehaviour
    {
        [Header("Flashlight")]
        [Tooltip("The Light component used as the flashlight beam")]
        public Light flashlightLight;

        [Tooltip("Secondary close-range fill light when flashlight is on")]
        public Light fillLight;

        [Header("Beam Settings")]
        public float beamIntensity = 32.0f;
        public float beamRange = 60.0f;
        public float outerSpotAngle = 68.0f;
        public float innerSpotAngle = 44.0f;
        public Color beamColor = new Color(1.0f, 0.98f, 0.92f, 1.0f);

        [Tooltip("Key used to toggle flashlight")]
        public KeyCode toggleKey = KeyCode.F;

        [Tooltip("Whether the flashlight starts ON or OFF")]
        public bool startOn = false;

        private bool _isOn;
        private Camera _mainCam;

        public bool IsOn => _isOn;

        private void Awake()
        {
            // Deduplicate if both PlayerCapsule and PlayerCameraRoot have a FlashlightController
            FlashlightController[] childControllers = GetComponentsInChildren<FlashlightController>(true);
            if (childControllers != null && childControllers.Length > 1)
            {
                foreach (var cc in childControllers)
                {
                    if (cc != null && cc != this && cc.transform.IsChildOf(transform))
                    {
                        // Let the child (PlayerCameraRoot) controller own the flashlight
                        enabled = false;
                        return;
                    }
                }
            }

            // Ensure upgraded high-power values even if older low values were serialized
            if (beamIntensity < 20f) beamIntensity = 32.0f;
            if (beamRange < 45f) beamRange = 60.0f;
            if (outerSpotAngle < 60f) outerSpotAngle = 68.0f;
            if (innerSpotAngle < 38f) innerSpotAngle = 44.0f;

            if (flashlightLight == null)
            {
                Transform existing = transform.Find("FlashlightBeam");
                if (existing != null) flashlightLight = existing.GetComponent<Light>();
                if (flashlightLight == null) flashlightLight = GetComponentInChildren<Light>(true);
            }

            if (flashlightLight == null)
            {
                GameObject lightGo = new GameObject("FlashlightBeam");
                lightGo.transform.SetParent(this.transform, false);
                lightGo.transform.localPosition = new Vector3(0.2f, -0.1f, 0.25f);
                lightGo.transform.localRotation = Quaternion.identity;
                flashlightLight = lightGo.AddComponent<Light>();
            }

            ApplyBeamSettings();

            _isOn = startOn;
            SetLightsEnabled(_isOn);
        }

        public void ApplyBeamSettings()
        {
            if (flashlightLight != null)
            {
                flashlightLight.type = LightType.Spot;
                flashlightLight.range = beamRange;
                flashlightLight.spotAngle = outerSpotAngle;
                flashlightLight.innerSpotAngle = innerSpotAngle;
                flashlightLight.intensity = beamIntensity;
                flashlightLight.color = beamColor;
                flashlightLight.shadows = LightShadows.Soft;
            }

            if (fillLight == null && flashlightLight != null)
            {
                Transform fillT = flashlightLight.transform.Find("FlashlightFill");
                if (fillT == null)
                {
                    GameObject fillGo = new GameObject("FlashlightFill");
                    fillGo.transform.SetParent(flashlightLight.transform, false);
                    fillGo.transform.localPosition = Vector3.zero;
                    fillLight = fillGo.AddComponent<Light>();
                }
                else
                {
                    fillLight = fillT.GetComponent<Light>();
                }
            }

            if (fillLight != null)
            {
                fillLight.type = LightType.Point;
                fillLight.range = 8.5f;
                fillLight.intensity = 2.4f;
                fillLight.color = beamColor;
                fillLight.shadows = LightShadows.None;
            }
        }

        private void SetLightsEnabled(bool on)
        {
            if (flashlightLight != null) flashlightLight.enabled = on;
            if (fillLight != null) fillLight.enabled = on;
        }

        private void Update()
        {
            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#endif

            if (!togglePressed)
            {
                try
                {
                    if (Input.GetKeyDown(toggleKey))
                    {
                        togglePressed = true;
                    }
                }
                catch { }
            }

            if (togglePressed)
            {
                Toggle();
            }
        }

        private void LateUpdate()
        {
            if (_isOn && flashlightLight != null)
            {
                if (_mainCam == null) _mainCam = Camera.main;
                if (_mainCam != null)
                {
                    flashlightLight.transform.rotation = _mainCam.transform.rotation;
                }
            }
        }

        public void Toggle()
        {
            _isOn = !_isOn;
            SetLightsEnabled(_isOn);
            Debug.Log($"[Flashlight] Flashlight turned {(_isOn ? "ON" : "OFF")}");
        }

        public void TurnOn()
        {
            _isOn = true;
            SetLightsEnabled(true);
        }

        public void TurnOff()
        {
            _isOn = false;
            SetLightsEnabled(false);
        }
    }
}
