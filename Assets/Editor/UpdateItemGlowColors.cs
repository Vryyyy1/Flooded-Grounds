using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using StarterAssets;
using System.IO;

namespace IgnitedEditor
{
    [InitializeOnLoad]
    public static class UpdateItemGlowColors
    {
        private const string SessionKey = "ItemGlowColors_Updated_v1";

        public static readonly Color WeaponRed = new Color(1.0f, 0.15f, 0.15f);      // Red for all weapons (firearms, knife, crowbar)
        public static readonly Color RadioYellow = new Color(1.0f, 0.92f, 0.20f);    // Yellow for radio
        public static readonly Color SurvivalGreen = new Color(0.20f, 1.0f, 0.30f);   // Green for survival tools

        static UpdateItemGlowColors()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                ApplyAllItemGlowColors();
            };
        }

        [MenuItem("Tools/Update Item Glow Colors")]
        public static void ApplyAllItemGlowColors()
        {
            int updatedGuns = 0;
            int updatedTools = 0;
            int updatedRadios = 0;

            // 1. Update GunPickup instances in the open scene (Weapons -> RED)
            GunPickup[] guns = Object.FindObjectsByType<GunPickup>(FindObjectsSortMode.None);
            foreach (GunPickup gp in guns)
            {
                if (gp == null) continue;
                Light l = gp.lootGlow != null ? gp.lootGlow : gp.GetComponentInChildren<Light>();
                if (l != null)
                {
                    l.color = WeaponRed;
                    l.intensity = 0.25f;
                    l.range = 0.90f;
                    EditorUtility.SetDirty(l);
                }
                EditorUtility.SetDirty(gp);
                updatedGuns++;
            }

            // 2. Update SurvivalItemPickup instances in the open scene
            SurvivalItemPickup[] items = Object.FindObjectsByType<SurvivalItemPickup>(FindObjectsSortMode.None);
            foreach (SurvivalItemPickup sp in items)
            {
                if (sp == null) continue;
                Color targetCol = SurvivalItemPickup.GetItemGlowColor(sp.itemType);

                Light l = sp.GetComponentInChildren<Light>();
                if (l != null)
                {
                    l.color = targetCol;
                    l.intensity = 0.25f;
                    l.range = 0.85f;
                    EditorUtility.SetDirty(l);
                }
                EditorUtility.SetDirty(sp);
                updatedTools++;
            }

            // 3. Update RadioInteraction instances in the open scene (Radio -> YELLOW)
            RadioInteraction[] radios = Object.FindObjectsByType<RadioInteraction>(FindObjectsSortMode.None);
            foreach (RadioInteraction ri in radios)
            {
                if (ri == null) continue;
                ri.activeLightColor = RadioYellow;
                ri.SetupRadioLight();
                if (ri.radioLight != null)
                {
                    ri.radioLight.color = RadioYellow;
                    EditorUtility.SetDirty(ri.radioLight);
                }
                if (ri.dialSpotLight != null)
                {
                    ri.dialSpotLight.color = RadioYellow;
                    EditorUtility.SetDirty(ri.dialSpotLight);
                }
                EditorUtility.SetDirty(ri);
                updatedRadios++;
            }

            // 4. Update Prefab Assets on disk
            UpdatePrefabLights("Assets/Resources/Guns", WeaponRed);
            UpdatePrefabLights("Assets/Prefabs/Guns", WeaponRed);
            UpdateSurvivalToolPrefabs("Assets/Resources/SurvivalTools/Prefabs");
            UpdateSurvivalToolPrefabs("Assets/Prefabs/Survival");
            UpdateRadioPrefabs();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UpdateItemGlowColors] Configured: {updatedGuns} weapons (Red), {updatedTools} survival tools/melee (Green/Red), and {updatedRadios} radios (Yellow).");
        }

        private static void UpdatePrefabLights(string folder, Color color)
        {
            if (!Directory.Exists(folder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Light[] lights = prefab.GetComponentsInChildren<Light>(true);
                bool modified = false;
                foreach (Light l in lights)
                {
                    if (l != null)
                    {
                        l.color = color;
                        l.intensity = 0.25f;
                        l.range = 0.90f;
                        modified = true;
                    }
                }

                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }
        }

        private static void UpdateSurvivalToolPrefabs(string folder)
        {
            if (!Directory.Exists(folder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                SurvivalItemPickup sp = prefab.GetComponent<SurvivalItemPickup>();
                Color targetColor = (sp != null)
                    ? SurvivalItemPickup.GetItemGlowColor(sp.itemType)
                    : (path.ToLower().Contains("knife") || path.ToLower().Contains("crowbar") ? WeaponRed : SurvivalGreen);

                Light[] lights = prefab.GetComponentsInChildren<Light>(true);
                bool modified = false;
                foreach (Light l in lights)
                {
                    if (l != null)
                    {
                        l.color = targetColor;
                        l.intensity = 0.25f;
                        l.range = 0.85f;
                        modified = true;
                    }
                }

                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }
        }

        private static void UpdateRadioPrefabs()
        {
            string[] paths = new[]
            {
                "Assets/AK STUDIO ART/Radio/Prefabs/Radio.prefab",
                "Assets/Resources/Radio.prefab"
            };

            foreach (string p in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;

                RadioInteraction ri = prefab.GetComponent<RadioInteraction>();
                if (ri != null)
                {
                    ri.activeLightColor = RadioYellow;
                    EditorUtility.SetDirty(ri);
                }

                Light[] lights = prefab.GetComponentsInChildren<Light>(true);
                foreach (Light l in lights)
                {
                    if (l != null)
                    {
                        l.color = RadioYellow;
                    }
                }
                EditorUtility.SetDirty(prefab);
            }
        }
    }
}

