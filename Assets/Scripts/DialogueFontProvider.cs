using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Centralized Dialogue & UI Font Provider.
    /// Ensures that all dialogue, radio broadcasts, subtitles, mission titles,
    /// and HUD notifications share the clean, modern typographic font (Roboto / Liberation).
    /// </summary>
    public static class DialogueFontProvider
    {
        private static Font _regular;
        private static Font _bold;
        private static Font _medium;
        private static bool _initialized = false;

        public static Font Regular
        {
            get
            {
                EnsureLoaded();
                return _regular;
            }
        }

        public static Font Bold
        {
            get
            {
                EnsureLoaded();
                return _bold != null ? _bold : _regular;
            }
        }

        public static Font Medium
        {
            get
            {
                EnsureLoaded();
                return _medium != null ? _medium : _regular;
            }
        }

        public static void EnsureLoaded()
        {
            if (_initialized && _regular != null) return;

            // 1. Try loading from Resources/Fonts
            _regular = Resources.Load<Font>("Fonts/Roboto-Regular");
            _bold = Resources.Load<Font>("Fonts/Roboto-Bold");
            _medium = Resources.Load<Font>("Fonts/Roboto-Medium");

            if (_regular == null)
            {
                _regular = Resources.Load<Font>("Fonts/LiberationSans");
            }

#if UNITY_EDITOR
            // 2. Editor AssetDatabase fallback
            if (_regular == null)
            {
                _regular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Regular.ttf");
            }
            if (_bold == null)
            {
                _bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Bold.ttf");
            }
            if (_medium == null)
            {
                _medium = AssetDatabase.LoadAssetAtPath<Font>("Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/Fonts/Roboto-Medium.ttf");
            }
            if (_regular == null)
            {
                _regular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Art/TextMesh Pro/Fonts/LiberationSans.ttf");
            }
#endif

            // 3. Fallback to Unity built-in legacy font if nothing else is available
            if (_regular == null)
            {
                _regular = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            _initialized = true;
        }

        /// <summary>
        /// Applies the dialogue font to an existing GUIStyle.
        /// </summary>
        public static GUIStyle ApplyFont(this GUIStyle style, bool bold = false)
        {
            if (style == null) return style;
            Font f = bold ? Bold : Regular;
            if (f != null)
            {
                style.font = f;
            }
            return style;
        }

        /// <summary>
        /// Creates a new GUIStyle based on GUI.skin.label with the dialogue font pre-applied.
        /// </summary>
        public static GUIStyle CreateDialogueStyle(int fontSize, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            ApplyFont(s, bold);
            s.fontSize = fontSize;
            s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            s.alignment = alignment;
            if (color.HasValue)
            {
                s.normal.textColor = color.Value;
            }
            return s;
        }
    }
}
