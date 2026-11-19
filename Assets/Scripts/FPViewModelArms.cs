using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Generates high-fidelity first-person viewmodel arms and hands that hold weapons
    /// matching the classic Left 4 Dead FPS perspective (two-handed tactical grip, camo sleeves, gloves).
    /// </summary>
    public static class FPViewModelArms
    {
        private static Material _cachedSleeveMat;
        private static Material _cachedGloveMat;
        private static Material _cachedSkinMat;

        public static GameObject BuildArms(Transform weaponTransform, GunType gunType)
        {
            if (weaponTransform == null) return null;

            // Remove existing arms if any
            var existing = weaponTransform.Find("FP_Arms_Viewmodel");
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }

            InitMaterials();

            GameObject armsRoot = new GameObject("FP_Arms_Viewmodel");
            armsRoot.transform.SetParent(weaponTransform, false);
            armsRoot.transform.localPosition = Vector3.zero;
            armsRoot.transform.localRotation = Quaternion.identity;
            armsRoot.transform.localScale = Vector3.one;

            // Determine hand attachment positions based on weapon type
            Vector3 rGripPos;
            Vector3 lGripPos;
            bool isHandgun = (gunType == GunType.Pistol);

            switch (gunType)
            {
                case GunType.Shotgun:
                    rGripPos = new Vector3(0.010f, -0.020f, -0.02f);
                    lGripPos = new Vector3(-0.010f, -0.015f, 0.18f); // Holding pump slider
                    break;
                case GunType.AssaultRifle:
                    rGripPos = new Vector3(0.010f, -0.020f, -0.03f);
                    lGripPos = new Vector3(-0.010f, -0.015f, 0.16f); // Holding handguard
                    break;
                case GunType.SMG:
                    rGripPos = new Vector3(0.010f, -0.020f, -0.02f);
                    lGripPos = new Vector3(-0.010f, -0.015f, 0.10f);
                    break;
                case GunType.SniperRifle:
                    rGripPos = new Vector3(0.010f, -0.020f, -0.04f);
                    lGripPos = new Vector3(-0.010f, -0.015f, 0.22f);
                    break;
                case GunType.Pistol:
                default:
                    rGripPos = new Vector3(0.020f, -0.010f, 0.05f);
                    lGripPos = new Vector3(0.010f, -0.020f, 0.045f); // Supporting right hand
                    break;
            }

            // Build Right Arm (Trigger / Grip Arm)
            BuildRightArm(armsRoot.transform, rGripPos, isHandgun);

            // Build Left Arm (Pump / Handguard / Support Arm)
            BuildLeftArm(armsRoot.transform, lGripPos, isHandgun);

            // Set all children to Layer 2 (Ignore Raycast) and remove colliders
            CleanViewmodelHierarchy(armsRoot);

            return armsRoot;
        }

        private static void BuildRightArm(Transform parent, Vector3 gripPos, bool isHandgun)
        {
            GameObject rArm = new GameObject("Right_Arm");
            rArm.transform.SetParent(parent, false);

            Vector3 forearmStart = isHandgun
                ? new Vector3(0.10f, -0.09f, -0.08f)
                : new Vector3(0.12f, -0.10f, -0.12f); // Enters cleanly from lower right viewport corner

            Vector3 wristPos = gripPos + new Vector3(0.008f, -0.015f, -0.035f);

            // 1. Forearm Sleeve
            CreateLimbSegment("Forearm_Sleeve_R", rArm.transform, forearmStart, wristPos, 0.042f, 0.034f, _cachedSleeveMat);

            // 2. Sleeve Cuff
            CreateCuff("Cuff_R", rArm.transform, wristPos, (wristPos - forearmStart).normalized, 0.038f, _cachedSleeveMat);

            // 3. Tactical Glove Palm
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm_R";
            palm.transform.SetParent(rArm.transform, false);
            palm.transform.localPosition = gripPos + new Vector3(0.004f, -0.008f, -0.005f);
            palm.transform.localRotation = Quaternion.Euler(-10f, 5f, -5f);
            palm.transform.localScale = new Vector3(0.034f, 0.050f, 0.040f);
            SetRendererMaterial(palm, _cachedGloveMat);

            // 4. Thumb (curling over top/side of grip)
            GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            thumb.name = "Thumb_R";
            thumb.transform.SetParent(rArm.transform, false);
            thumb.transform.localPosition = gripPos + new Vector3(-0.010f, 0.010f, -0.010f);
            thumb.transform.localRotation = Quaternion.Euler(55f, 40f, -20f);
            thumb.transform.localScale = new Vector3(0.015f, 0.022f, 0.015f);
            SetRendererMaterial(thumb, _cachedGloveMat);

            // 5. Trigger Finger (extended forward onto trigger)
            GameObject trigFinger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trigFinger.name = "TriggerFinger_R";
            trigFinger.transform.SetParent(rArm.transform, false);
            trigFinger.transform.localPosition = gripPos + new Vector3(-0.008f, 0.004f, 0.025f);
            trigFinger.transform.localRotation = Quaternion.Euler(75f, -10f, 5f);
            trigFinger.transform.localScale = new Vector3(0.012f, 0.025f, 0.012f);
            SetRendererMaterial(trigFinger, _cachedGloveMat);

            // 6. Fingers (wrapped around front of grip)
            for (int i = 0; i < 3; i++)
            {
                GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                finger.name = $"Finger_R_{i + 1}";
                finger.transform.SetParent(rArm.transform, false);
                finger.transform.localPosition = gripPos + new Vector3(0.006f, -0.012f - (i * 0.012f), 0.016f);
                finger.transform.localRotation = Quaternion.Euler(65f, 25f, -10f);
                finger.transform.localScale = new Vector3(0.013f, 0.018f, 0.013f);
                SetRendererMaterial(finger, _cachedGloveMat);
            }
        }

        private static void BuildLeftArm(Transform parent, Vector3 supportPos, bool isHandgun)
        {
            GameObject lArm = new GameObject("Left_Arm");
            lArm.transform.SetParent(parent, false);

            if (isHandgun)
            {
                // Two-handed combat grip: Left hand wraps around the right hand
                Vector3 forearmStart = new Vector3(-0.06f, -0.09f, -0.05f);
                Vector3 wristPos = supportPos + new Vector3(-0.015f, -0.015f, -0.025f);

                CreateLimbSegment("Forearm_Sleeve_L", lArm.transform, forearmStart, wristPos, 0.040f, 0.032f, _cachedSleeveMat);
                CreateCuff("Cuff_L", lArm.transform, wristPos, (wristPos - forearmStart).normalized, 0.036f, _cachedSleeveMat);

                GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                palm.name = "Palm_L";
                palm.transform.SetParent(lArm.transform, false);
                palm.transform.localPosition = supportPos;
                palm.transform.localRotation = Quaternion.Euler(15f, -25f, 10f);
                palm.transform.localScale = new Vector3(0.036f, 0.048f, 0.038f);
                SetRendererMaterial(palm, _cachedGloveMat);
            }
            else
            {
                // Shotgun / Long gun grip: Left arm reaches forward to pump / handguard
                Vector3 forearmStart = new Vector3(-0.14f, -0.11f, -0.04f); // Enters cleanly from lower left viewport
                Vector3 wristPos = supportPos + new Vector3(-0.018f, -0.015f, -0.035f);

                // 1. Forearm Sleeve
                CreateLimbSegment("Forearm_Sleeve_L", lArm.transform, forearmStart, wristPos, 0.042f, 0.033f, _cachedSleeveMat);

                // 2. Sleeve Cuff
                CreateCuff("Cuff_L", lArm.transform, wristPos, (wristPos - forearmStart).normalized, 0.037f, _cachedSleeveMat);

                // 3. Tactical Glove Palm (cupped underneath pump/handguard)
                GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                palm.name = "Palm_L";
                palm.transform.SetParent(lArm.transform, false);
                palm.transform.localPosition = supportPos + new Vector3(0f, -0.010f, 0f);
                palm.transform.localRotation = Quaternion.Euler(-5f, -10f, 20f);
                palm.transform.localScale = new Vector3(0.038f, 0.028f, 0.048f);
                SetRendererMaterial(palm, _cachedGloveMat);

                // 4. Thumb (resting along upper left side of pump slider)
                GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                thumb.name = "Thumb_L";
                thumb.transform.SetParent(lArm.transform, false);
                thumb.transform.localPosition = supportPos + new Vector3(-0.016f, 0.008f, 0.008f);
                thumb.transform.localRotation = Quaternion.Euler(85f, -5f, 30f);
                thumb.transform.localScale = new Vector3(0.014f, 0.022f, 0.014f);
                SetRendererMaterial(thumb, _cachedGloveMat);

                // 5. Fingers (cupping up around right side of pump slider)
                for (int i = 0; i < 3; i++)
                {
                    GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    finger.name = $"Finger_L_{i + 1}";
                    finger.transform.SetParent(lArm.transform, false);
                    finger.transform.localPosition = supportPos + new Vector3(0.016f, 0.006f, -0.014f + (i * 0.015f));
                    finger.transform.localRotation = Quaternion.Euler(75f, 10f, -35f);
                    finger.transform.localScale = new Vector3(0.012f, 0.018f, 0.012f);
                    SetRendererMaterial(finger, _cachedGloveMat);
                }
            }
        }

        private static void CreateLimbSegment(string name, Transform parent, Vector3 start, Vector3 end, float startRadius, float endRadius, Material mat)
        {
            Vector3 dir = end - start;
            float length = dir.magnitude;
            if (length < 0.001f) return;

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);

            cylinder.transform.localPosition = (start + end) * 0.5f;
            cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            float avgRadius = (startRadius + endRadius) * 0.5f;
            cylinder.transform.localScale = new Vector3(avgRadius * 2f, length * 0.5f, avgRadius * 2f);

            SetRendererMaterial(cylinder, mat);
        }

        private static void CreateCuff(string name, Transform parent, Vector3 pos, Vector3 forwardDir, float radius, Material mat)
        {
            GameObject cuff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuff.name = name;
            cuff.transform.SetParent(parent, false);

            cuff.transform.localPosition = pos;
            if (forwardDir.sqrMagnitude > 0.001f)
            {
                cuff.transform.localRotation = Quaternion.FromToRotation(Vector3.up, forwardDir);
            }
            cuff.transform.localScale = new Vector3(radius * 2.1f, 0.014f, radius * 2.1f);

            SetRendererMaterial(cuff, mat);
        }

        private static void SetRendererMaterial(GameObject go, Material mat)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        private static void CleanViewmodelHierarchy(GameObject root)
        {
            // Destroy all colliders
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                Object.Destroy(col);
            }

            // Set layer to 2 (Ignore Raycast)
            SetLayerRecursively(root, 2);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void InitMaterials()
        {
            if (_cachedSleeveMat != null && _cachedGloveMat != null) return;

            // Find shader: Universal Render Pipeline/Lit -> Standard -> Mobile/Diffuse
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            if (litShader == null) litShader = Shader.Find("Mobile/Diffuse");
            if (litShader == null) litShader = Shader.Find("Diffuse");

            // Tactical Military Olive Camo Sleeve
            _cachedSleeveMat = new Material(litShader);
            _cachedSleeveMat.name = "M_FP_Tactical_Sleeve";
            _cachedSleeveMat.color = new Color(0.18f, 0.22f, 0.17f, 1f); // Dark tactical olive camo
            if (_cachedSleeveMat.HasProperty("_Smoothness")) _cachedSleeveMat.SetFloat("_Smoothness", 0.15f);

            // Tactical Carbon Leather Glove
            _cachedGloveMat = new Material(litShader);
            _cachedGloveMat.name = "M_FP_Tactical_Glove";
            _cachedGloveMat.color = new Color(0.11f, 0.11f, 0.12f, 1f); // Matte charcoal black
            if (_cachedGloveMat.HasProperty("_Smoothness")) _cachedGloveMat.SetFloat("_Smoothness", 0.35f);

            // Warm skin tone
            _cachedSkinMat = new Material(litShader);
            _cachedSkinMat.name = "M_FP_Skin_Tone";
            _cachedSkinMat.color = new Color(0.80f, 0.62f, 0.50f, 1f);
            if (_cachedSkinMat.HasProperty("_Smoothness")) _cachedSkinMat.SetFloat("_Smoothness", 0.20f);
        }
    }
}

