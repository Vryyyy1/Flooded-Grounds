using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Entrance and Pause Menu styled after The Walking Dead (Telltale Games).
    /// Features:
    /// - Starts at the Entrance of the game upon launch (Time paused, cursor free).
    /// - Context-aware menu options: PLAY, RESUME GAME, RESTART, EXIT GAME.
    /// - Torn paintbrush highlight banner behind the selected option.
    /// - Atmospheric background and sound effects.
    /// - ESC toggles the Pause Menu during active gameplay.
    /// </summary>
    public class GameMenuManager : MonoBehaviour
    {
        public static GameMenuManager Instance { get; private set; }

        [Header("Menu State")]
        [SerializeField] private bool _isMenuOpen = true;
        [SerializeField] private bool _hasGameStarted = false;

        public bool IsMenuOpen => _isMenuOpen;
        public bool HasGameStarted => _hasGameStarted;

        [Header("Visual Assets")]
        private Texture2D _backgroundTex;
        private Texture2D _brushBannerTex;
        private Texture2D _darkVignetteTex;

        [Header("Audio")]
        private AudioSource _audioSource;
        private AudioClip _hoverSound;
        private AudioClip _selectSound;
        private AudioClip _pauseSound;

        // Navigation state
        private int _selectedIndex = 0;

        // Menu options indices
        private enum MenuOption
        {
            PlayOrResume,
            Restart,
            Exit
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                // Only spawn in gameplay scene (e.g., Scene_A)
                string sceneName = SceneManager.GetActiveScene().name;
                if (sceneName.Contains("Scene_A") || sceneName.Contains("SampleScene") || sceneName.Contains("ZombieApo"))
                {
                    GameObject go = new GameObject("GameMenuManager");
                    go.AddComponent<GameMenuManager>();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadMenuAssets();
            InitProceduralAudio();

            // Setup audio source
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.ignoreListenerPause = true;

            // Open entrance menu immediately upon game load
            OpenEntranceMenu();
        }

        private void LoadMenuAssets()
        {
            _backgroundTex = Resources.Load<Texture2D>("UI/MenuBackground");
            if (_backgroundTex == null)
            {
                _backgroundTex = Resources.Load<Texture2D>("UI/MenuBackground_Clean");
            }
            if (_backgroundTex == null)
            {
                _backgroundTex = Resources.Load<Texture2D>("UI/MenuBackground_Raw");
            }

            _brushBannerTex = Resources.Load<Texture2D>("UI/MenuBrushBanner");

            // Smooth horizontal gradient vignette (eliminates harsh vertical split line)
            // Feathers smoothly from deep moody shadow on the left into transparent over the mansion landscape
            _darkVignetteTex = CreateHorizontalGradientTexture(
                256,
                new Color(0.02f, 0.02f, 0.03f, 0.88f),
                new Color(0.02f, 0.02f, 0.03f, 0.0f)
            );
        }

        private static Texture2D CreateHorizontalGradientTexture(int width, Color startColor, Color endColor)
        {
            Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[width];
            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (width - 1);
                // Gentle non-linear falloff so text area stays shaded while scenery blends seamlessly
                float alphaT = Mathf.Pow(1f - t, 1.8f);
                Color c = Color.Lerp(endColor, startColor, alphaT);
                c.a = startColor.a * alphaT;
                pixels[x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void InitProceduralAudio()
        {
            if (_hoverSound == null) _hoverSound = CreateSynthTick(0.04f);
            if (_selectSound == null) _selectSound = CreateSynthSelect(0.35f);
            if (_pauseSound == null) _pauseSound = CreateSynthPause(0.28f);
        }

        public void OpenEntranceMenu()
        {
            _isMenuOpen = true;
            _hasGameStarted = false;
            _selectedIndex = 0;

            Time.timeScale = 0f;
            StarterAssetsInputs.InputBlocked = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OpenPauseMenu()
        {
            _isMenuOpen = true;
            _selectedIndex = 0; // Highlights RESUME GAME

            Time.timeScale = 0f;
            StarterAssetsInputs.InputBlocked = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            PlaySound(_pauseSound, 0.85f);
        }

        public void StartGameWithIntro()
        {
            _hasGameStarted = true;
            _isMenuOpen = false;

            Time.timeScale = 1f;
            StarterAssetsInputs.InputBlocked = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlaySound(_selectSound, 0.95f);

            // Trigger the cinematic movie wake-up scene
            if (CinematicWakeUpIntro.Instance != null)
            {
                CinematicWakeUpIntro.Instance.StartIntroSequence();
            }
            else
            {
                GameObject go = new GameObject("CinematicWakeUpIntro");
                var intro = go.AddComponent<CinematicWakeUpIntro>();
                intro.StartIntroSequence();
            }
        }

        public void ResumeGame()
        {
            _hasGameStarted = true;
            _isMenuOpen = false;

            Time.timeScale = 1f;
            StarterAssetsInputs.InputBlocked = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlaySound(_selectSound, 0.90f);
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            StarterAssetsInputs.InputBlocked = false;
            _isMenuOpen = true;
            _hasGameStarted = false;

            PlaySound(_selectSound, 1.0f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ExitGame()
        {
            PlaySound(_selectSound, 1.0f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            // Listen for Escape key to toggle pause
            if (WasEscapePressed())
            {
                // If inventory bag is open, let bag handle its own close
                if (PlayerInventoryBag.Instance != null && PlayerInventoryBag.Instance.IsBagOpen)
                {
                    return;
                }

                if (_isMenuOpen)
                {
                    // Only resume if the game has already been started once
                    if (_hasGameStarted)
                    {
                        ResumeGame();
                    }
                }
                else
                {
                    OpenPauseMenu();
                }
            }

            if (!_isMenuOpen) return;

            // Ensure cursor stays free while menu is open
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Keyboard navigation (W/S or Up/Down arrows)
            if (WasUpPressed())
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = 2;
                PlaySound(_hoverSound, 0.75f);
            }
            else if (WasDownPressed())
            {
                _selectedIndex++;
                if (_selectedIndex > 2) _selectedIndex = 0;
                PlaySound(_hoverSound, 0.75f);
            }

            // Enter / Space confirmation
            if (WasSelectPressed())
            {
                ExecuteOption(_selectedIndex);
            }
        }

        private void ExecuteOption(int index)
        {
            switch (index)
            {
                case 0:
                    // PLAY (if at entrance) or RESUME GAME (if paused)
                    if (!_hasGameStarted)
                    {
                        StartGameWithIntro();
                    }
                    else
                    {
                        ResumeGame();
                    }
                    break;
                case 1:
                    // RESTART
                    RestartGame();
                    break;
                case 2:
                    // EXIT GAME
                    ExitGame();
                    break;
            }
        }

        private bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) return true;
            }
            catch { }
            return false;
        }

        private bool WasUpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return true;
            }
            catch { }
            return false;
        }

        private bool WasDownPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return true;
            }
            catch { }
            return false;
        }

        private bool WasSelectPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) return true;
            }
            catch { }
            return false;
        }

        private void OnGUI()
        {
            if (!_isMenuOpen) return;

            Font regFont = StarterAssets.DialogueFontProvider.Regular;
            Font boldFont = StarterAssets.DialogueFontProvider.Bold;
            if (regFont != null) GUI.skin.font = regFont;

            // Priority: render on top of all HUD and UI
            GUI.depth = -100;

            float sw = Screen.width;
            float sh = Screen.height;

            // ================= 1. ATMOSPHERIC SUNSET FOREST BACKGROUND =================
            if (_backgroundTex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _backgroundTex, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.color = new Color(0.12f, 0.08f, 0.02f, 1.0f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            }

            // Left gloomy vignette overlay (soft gradient fadeout, no harsh vertical cut)
            if (_darkVignetteTex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, sw * 0.58f, sh), _darkVignetteTex, ScaleMode.StretchToFill);
            }

            // ================= 2. TITLE / PAUSE HEADER =================
            float leftMargin = Mathf.Max(50f, sw * 0.10f);
            float startY = sh * 0.28f;

            string mainActionText = _hasGameStarted ? "RESUME GAME" : "PLAY";

            // If game is paused, display small atmospheric PAUSE header above options
            if (_hasGameStarted)
            {
                GUIStyle pauseHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    font = boldFont,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.032f), 18, 30),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                pauseHeaderStyle.normal.textColor = new Color(0.92f, 0.35f, 0.20f, 0.90f);

                DrawShadowText(new Rect(leftMargin, startY - 55f, 400f, 40f), "● GAME PAUSED", pauseHeaderStyle, Color.black);
            }
            else
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = boldFont,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.045f), 24, 42),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                titleStyle.normal.textColor = Color.white;

                DrawShadowText(new Rect(leftMargin, startY - 70f, 500f, 50f), "THE WALKING DEAD", titleStyle, Color.black);
            }

            // ================= 3. MENU OPTIONS =================
            string[] options = new string[]
            {
                mainActionText,
                "RESTART",
                "EXIT GAME"
            };

            float buttonHeight = Mathf.Clamp(sh * 0.075f, 48f, 68f);
            float buttonSpacing = Mathf.Clamp(sh * 0.025f, 12f, 22f);
            float bannerWidth = Mathf.Clamp(sw * 0.28f, 280f, 420f);

            Vector2 mousePos = Event.current.mousePosition;

            for (int i = 0; i < options.Length; i++)
            {
                float optionY = startY + i * (buttonHeight + buttonSpacing);
                Rect optionRect = new Rect(leftMargin - 20f, optionY - 4f, bannerWidth + 40f, buttonHeight + 8f);

                // Mouse hover detection
                bool isMouseHover = optionRect.Contains(mousePos);
                if (isMouseHover)
                {
                    if (_selectedIndex != i)
                    {
                        _selectedIndex = i;
                        PlaySound(_hoverSound, 0.70f);
                    }
                }

                bool isSelected = (_selectedIndex == i) || isMouseHover;

                // Draw torn brush-stroke banner behind the active option (just like PLAY in image)
                if (isSelected)
                {
                    if (_brushBannerTex != null)
                    {
                        GUI.color = Color.white;
                        Rect bannerRect = new Rect(leftMargin - 20f, optionY - 4f, bannerWidth + 40f, buttonHeight + 8f);
                        GUI.DrawTexture(bannerRect, _brushBannerTex, ScaleMode.StretchToFill);
                    }
                    else
                    {
                        GUI.color = new Color(0.96f, 0.96f, 0.96f, 0.95f);
                        GUI.DrawTexture(new Rect(leftMargin - 10f, optionY, bannerWidth, buttonHeight), Texture2D.whiteTexture);
                    }
                }

                // Text styling:
                // When pointed at or selected: text turns PURE BLACK so it stands out sharply over the white brush banner!
                // When unselected: text is bold crisp WHITE with dark drop shadow
                GUIStyle optStyle = new GUIStyle(GUI.skin.label)
                {
                    font = boldFont,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(buttonHeight * 0.56f), 22, 38),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };

                if (isSelected)
                {
                    // Active/Hovered option on white banner: Pure deep black text across all states (normal, hover, active)
                    Color blackText = new Color(0.04f, 0.04f, 0.04f, 1.0f);
                    optStyle.normal.textColor = blackText;
                    optStyle.hover.textColor = blackText;
                    optStyle.active.textColor = blackText;
                    optStyle.focused.textColor = blackText;
                    optStyle.onNormal.textColor = blackText;
                    optStyle.onHover.textColor = blackText;

                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = blackText;
                    GUI.Label(new Rect(leftMargin + 15f, optionY, bannerWidth, buttonHeight), options[i], optStyle);
                    GUI.contentColor = prevContent;
                }
                else
                {
                    // Unselected option: bold crisp white with dark drop shadow over background
                    Color whiteText = Color.white;
                    optStyle.normal.textColor = whiteText;
                    optStyle.hover.textColor = whiteText;
                    optStyle.active.textColor = whiteText;
                    optStyle.focused.textColor = whiteText;
                    optStyle.onNormal.textColor = whiteText;
                    optStyle.onHover.textColor = whiteText;

                    DrawOutlineText(new Rect(leftMargin + 10f, optionY, bannerWidth, buttonHeight), options[i], optStyle, Color.white, new Color(0.0f, 0.0f, 0.0f, 0.90f), 2f);
                }

                // Click detection on button
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && optionRect.Contains(Event.current.mousePosition))
                {
                    ExecuteOption(i);
                    Event.current.Use();
                }
            }

            // ================= 4. BOTTOM PROMPT & SURVIVAL INFO =================
            // Bottom-left prompt: (A) Select / [ENTER] Select [W/S] Navigate
            GUIStyle footerPromptStyle = new GUIStyle(GUI.skin.label)
            {
                font = boldFont,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.022f), 13, 17),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            footerPromptStyle.normal.textColor = new Color(0.85f, 0.82f, 0.70f, 0.85f);

            string navPrompt = _hasGameStarted
                ? "[CLICK / ENTER] SELECT      [W/S] NAVIGATE      [ESC] RESUME GAME"
                : "[CLICK / ENTER] SELECT      [W/S] NAVIGATE";

            DrawShadowText(new Rect(leftMargin, sh - 65f, 600f, 30f), navPrompt, footerPromptStyle, Color.black);

            // Bottom-right atmospheric info (matches the caution icon in image)
            GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
            {
                font = regFont,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(sh * 0.022f), 13, 17),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight
            };
            infoStyle.normal.textColor = new Color(0.90f, 0.90f, 0.90f, 0.85f);
            DrawShadowText(new Rect(sw - 480f, sh - 65f, 440f, 30f), "▲ Welcome to The Walking Dead: Infestation", infoStyle, Color.black);
        }

        private void DrawShadowText(Rect r, string text, GUIStyle style, Color shadowColor)
        {
            Color origNormal = style.normal.textColor;
            Color origHover = style.hover.textColor;
            Color origActive = style.active.textColor;

            style.normal.textColor = shadowColor;
            style.hover.textColor = shadowColor;
            style.active.textColor = shadowColor;

            Color prevContent = GUI.contentColor;
            GUI.contentColor = shadowColor;
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);

            style.normal.textColor = origNormal;
            style.hover.textColor = origHover;
            style.active.textColor = origActive;
            GUI.contentColor = origNormal;
            GUI.Label(r, text, style);

            GUI.contentColor = prevContent;
        }

        private void DrawOutlineText(Rect r, string text, GUIStyle style, Color textColor, Color outlineColor, float stroke = 2f)
        {
            Color origNormal = style.normal.textColor;
            Color origHover = style.hover.textColor;
            Color origActive = style.active.textColor;

            style.normal.textColor = outlineColor;
            style.hover.textColor = outlineColor;
            style.active.textColor = outlineColor;

            Color prevContent = GUI.contentColor;
            GUI.contentColor = outlineColor;

            GUI.Label(new Rect(r.x - stroke, r.y, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x + stroke, r.y, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x, r.y - stroke, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x, r.y + stroke, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x + stroke, r.y + stroke, r.width, r.height), text, style);

            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            GUI.contentColor = textColor;
            GUI.Label(r, text, style);

            style.normal.textColor = origNormal;
            style.hover.textColor = origHover;
            style.active.textColor = origActive;
            GUI.contentColor = prevContent;
        }

        private void PlaySound(AudioClip clip, float vol)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.98f, 1.02f);
                _audioSource.PlayOneShot(clip, vol);
            }
        }

        // ================= PROCEDURAL SYNTH SOUNDS =================

        private static AudioClip CreateSynthTick(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                float tone = Mathf.Sin(2f * Mathf.PI * 880f * t);
                data[i] = tone * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("MenuTick", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthSelect(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 12f);
                float sub = Mathf.Sin(2f * Mathf.PI * 130f * (1f - t * 0.5f) * t);
                float hit = Mathf.Sin(2f * Mathf.PI * 340f * t) * Mathf.Exp(-t * 30f);
                data[i] = (sub * 0.75f + hit * 0.4f) * env * 0.9f;
            }

            AudioClip clip = AudioClip.Create("MenuSelect", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthPause(float length)
        {
            int sampleRate = 22050;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);
                float tone = Mathf.Sin(2f * Mathf.PI * 220f * t);
                data[i] = tone * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("MenuPause", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
