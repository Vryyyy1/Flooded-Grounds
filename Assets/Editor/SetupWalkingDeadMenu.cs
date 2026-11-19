using UnityEngine;
using UnityEditor;
using System.IO;

namespace IgnitedEditor
{
    [InitializeOnLoad]
    public static class SetupWalkingDeadMenu
    {
        private const string SessionKey = "WalkingDeadMenu_Setup_v2";
        private const string SourceImagePath = "C:/Users/PC-33-Student/.gemini/antigravity/brain/eceaabdb-8f09-4d23-b03a-ffe562aea7ac/.user_uploaded/media_1794373742145_508f9ade.jpg";
        private const string DestDir = "Assets/Resources/UI";
        private const string BackgroundPath = "Assets/Resources/UI/MenuBackground.png";
        private const string BackgroundRawPath = "Assets/Resources/UI/MenuBackground_Raw.png";
        private const string BrushBannerPath = "Assets/Resources/UI/MenuBrushBanner.png";

        static SetupWalkingDeadMenu()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(SessionKey, false)) return;
                SessionState.SetBool(SessionKey, true);
                GenerateAssets();
            };
        }

        [MenuItem("Tools/Setup Walking Dead Menu Assets")]
        public static void GenerateAssets()
        {
            if (!Directory.Exists(DestDir))
            {
                Directory.CreateDirectory(DestDir);
            }

            // 1. Process and Clean Background Image
            ProcessBackgroundImage();

            // 2. Generate Brush Banner Texture
            GenerateBrushBanner();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupWalkingDeadMenu] Menu assets generated successfully!");
        }

        private static void ProcessBackgroundImage()
        {
            if (!File.Exists(SourceImagePath))
            {
                Debug.LogError($"[SetupWalkingDeadMenu] Source image not found at: {SourceImagePath}");
                return;
            }

            byte[] bytes = File.ReadAllBytes(SourceImagePath);
            Texture2D srcTex = new Texture2D(2, 2);
            srcTex.LoadImage(bytes);

            int w = srcTex.width;
            int h = srcTex.height;
            Texture2D cleanTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color[] srcPixels = srcTex.GetPixels();
            Color[] dstPixels = new Color[srcPixels.Length];

            // Inpaint any UI watermark at bottom-right ("Cameras" button)
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    Color c = srcPixels[idx];

                    // Unity texture coords: y=0 is bottom
                    // Cameras button is located at bottom right: x: 930..1016, y: 7..42
                    bool isCameraWatermark = (x >= 928 && x <= 1018 && y >= 6 && y <= 42);

                    if (isCameraWatermark)
                    {
                        // Sample from adjacent natural swamp / water pixels to the left
                        int sampleX = Mathf.Clamp(x - 70, 0, w - 1);
                        int sampleY = Mathf.Clamp(y + 20, 0, h - 1);
                        c = srcTex.GetPixel(sampleX, sampleY);
                    }

                    dstPixels[idx] = c;
                }
            }

            cleanTex.SetPixels(dstPixels);
            cleanTex.Apply();

            byte[] pngBytes = cleanTex.EncodeToPNG();
            File.WriteAllBytes(BackgroundPath, pngBytes);
            File.WriteAllBytes(BackgroundRawPath, pngBytes);

            Object.DestroyImmediate(srcTex);
            Object.DestroyImmediate(cleanTex);

            ConfigureTextureImport(BackgroundPath);
            ConfigureTextureImport(BackgroundRawPath);
        }

        private static void GenerateBrushBanner()
        {
            int w = 512;
            int h = 96;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];

            Color brushWhite = new Color(0.98f, 0.98f, 1.0f, 1.0f);
            Color brushSoft = new Color(0.88f, 0.88f, 0.92f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                float ny = ((float)y / h) * 2f - 1f; // -1 to 1

                for (int x = 0; x < w; x++)
                {
                    float nx = ((float)x / w); // 0 to 1

                    // Irregular torn / painted brush edges along top, bottom, and ends
                    float edgeNoiseTop = Mathf.Sin(nx * 35f) * 0.08f + Mathf.Cos(nx * 75f) * 0.04f;
                    float edgeNoiseBottom = Mathf.Cos(nx * 40f) * 0.08f + Mathf.Sin(nx * 85f) * 0.04f;

                    float topBound = 0.78f + edgeNoiseTop;
                    float bottomBound = -0.78f + edgeNoiseBottom;

                    // Rough feathered ends on left and right
                    float leftCut = Mathf.Clamp01(nx * 14f - Mathf.Abs(Mathf.Sin(ny * 12f)) * 0.4f);
                    float rightCut = Mathf.Clamp01((1f - nx) * 12f - Mathf.Abs(Mathf.Cos(ny * 10f)) * 0.5f);
                    float endFade = leftCut * rightCut;

                    if (ny <= topBound && ny >= bottomBound && endFade > 0.05f)
                    {
                        // Internal bristle stroke variation
                        float bristle = Mathf.PerlinNoise(nx * 15f, ny * 4f);
                        float stroke = Mathf.Sin((nx * 120f) + (ny * 15f)) * 0.05f;
                        Color c = Color.Lerp(brushSoft, brushWhite, Mathf.Clamp01(bristle + stroke));

                        // Edge antialiasing
                        float edgeDist = Mathf.Min(topBound - ny, ny - bottomBound);
                        float alpha = Mathf.Clamp01(edgeDist * 18f) * endFade;
                        c.a = alpha;
                        pixels[y * w + x] = c;
                    }
                    else
                    {
                        pixels[y * w + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(BrushBannerPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            ConfigureTextureImport(BrushBannerPath);
        }

        private static void ConfigureTextureImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.GUI;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }
    }
}
