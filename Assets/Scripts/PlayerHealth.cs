using UnityEngine;
using System.Collections;

namespace StarterAssets
{
    /// <summary>
    /// Tracks player health, handles zombie attack damage,
    /// triggers death animations, "YOU DIED" overlay, and respawns
    /// the character back at their spawn position.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        public float maxHealth = 100f;
        public float currentHealth = 100f;
        public bool isDead = false;

        [Header("Regeneration")]
        public bool enableRegen = true;
        public float regenRate = 5f; // HP per second
        public float regenDelay = 5f; // Seconds after last hit before regen starts

        [Header("Respawn Settings")]
        [Tooltip("Optional custom spawn point transform. If null, records starting position.")]
        public Transform customSpawnPoint;
        [Tooltip("Seconds to wait before automatically respawning")]
        public float respawnDelay = 3.5f;

        [Header("Damage Feedback")]
        public float screenFlashDuration = 0.5f;

        [Header("Swimming Stamina")]
        [Tooltip("Maximum swimming stamina in seconds before exhaustion begins reducing HP")]
        public float maxSwimStamina = 10.0f;
        [Tooltip("Current remaining swimming stamina in seconds")]
        public float currentSwimStamina = 10.0f;
        [Tooltip("Stamina recovery rate (seconds per second) when back on dry land")]
        public float swimStaminaRecoveryRate = 3.0f;
        [Tooltip("HP slowly drained per second after the 10 seconds of swimming stamina runs out")]
        public float swimExhaustionHpDrainRate = 8.0f;

        // Private State
        private FirstPersonController _fpc;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _lastDamageTime = -999f;
        private float _deathTime = -999f;
        private float _damageFlashAlpha = 0f;
        private bool _isRespawning = false;

        // Textures for GUI
        private Texture2D _redTexture;
        private Texture2D _darkTexture;
        private Texture2D _healthBarBg;
        private Texture2D _healthBarFg;

        private void Awake()
        {
            if (maxHealth < 2500f) maxHealth = 2500f;
            regenRate = 25f;
            currentHealth = maxHealth;
            currentSwimStamina = maxSwimStamina;
            _fpc = GetComponent<FirstPersonController>();
            InitTextures();
        }

        private void Start()
        {
            if (_fpc == null) _fpc = GetComponent<FirstPersonController>();
            if (maxSwimStamina <= 0f || maxSwimStamina > 10.0f) maxSwimStamina = 10.0f;
            currentSwimStamina = maxSwimStamina;

            // Record original spawn point
            if (customSpawnPoint != null)
            {
                _spawnPosition = customSpawnPoint.position;
                _spawnRotation = customSpawnPoint.rotation;
            }
            else
            {
                _spawnPosition = transform.position;
                _spawnRotation = transform.rotation;
            }

            Debug.Log($"[PlayerHealth] Recorded spawn location at {_spawnPosition}");
        }

        private void Update()
        {
            if (isDead) return;

            if (_fpc == null) _fpc = GetComponent<FirstPersonController>();
            bool isSwimming = (_fpc != null && _fpc.IsSwimming);

            if (isSwimming)
            {
                if (currentSwimStamina > 0f)
                {
                    currentSwimStamina = Mathf.Max(0f, currentSwimStamina - Time.deltaTime);
                }
                else
                {
                    // After 10 seconds of swimming stamina is depleted, slowly reduce HP
                    currentHealth -= swimExhaustionHpDrainRate * Time.deltaTime;
                    _lastDamageTime = Time.time;
                    _damageFlashAlpha = Mathf.Max(_damageFlashAlpha, 0.22f + Mathf.Sin(Time.time * 6f) * 0.12f);

                    if (currentHealth <= 0f)
                    {
                        currentHealth = 0f;
                        Die();
                        return;
                    }
                }
            }
            else
            {
                // Swim stamina ONLY recovers when safely grounded on dry land (never while in water or jumping in mid-air)
                bool isSafelyGrounded = (_fpc != null && _fpc.Grounded);
                if (isSafelyGrounded && currentSwimStamina < maxSwimStamina)
                {
                    currentSwimStamina = Mathf.Min(maxSwimStamina, currentSwimStamina + swimStaminaRecoveryRate * Time.deltaTime);
                }
            }

            // Health regeneration
            if (enableRegen && currentHealth < maxHealth && Time.time >= _lastDamageTime + regenDelay)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + regenRate * Time.deltaTime);
            }

            // Fade damage flash
            if (_damageFlashAlpha > 0f)
            {
                _damageFlashAlpha -= Time.deltaTime / screenFlashDuration;
                if (_damageFlashAlpha < 0f) _damageFlashAlpha = 0f;
            }
        }

        public void TakeDamage(float damage, Vector3 attackerPosition)
        {
            if (isDead) return;

            currentHealth -= damage;
            _lastDamageTime = Time.time;
            _damageFlashAlpha = 0.65f;

            Debug.Log($"[PlayerHealth] Player took {damage} damage! Current HP: {currentHealth:F0}/{maxHealth:F0}");

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        public void Die()
        {
            if (isDead) return;

            isDead = true;
            _deathTime = Time.time;
            _isRespawning = true;
            _damageFlashAlpha = 0.85f;

            Debug.LogWarning("[PlayerHealth] Player died! Beginning respawn sequence...");

            // 1. Disable character movement controller
            var fpc = GetComponent<FirstPersonController>();
            if (fpc != null) fpc.enabled = false;

            // 2. Disable combat attacks
            var combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;

            // 3. Clear movement inputs
            var inputs = GetComponent<StarterAssetsInputs>();
            if (inputs != null)
            {
                inputs.move = Vector2.zero;
                inputs.sprint = false;
                inputs.jump = false;
            }

            // 4. Trigger Death Animation on Animator
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
            {
                anim.SetTrigger("Die");
                anim.SetBool("IsDead", true);
            }

            // 5. Start auto-respawn timer
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);

            if (isDead)
            {
                Respawn();
            }
        }

        public void Respawn()
        {
            Debug.Log($"[PlayerHealth] Respawning player at {_spawnPosition}...");

            // 1. Temporarily disable CharacterController to allow position teleportation
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;

            if (cc != null) cc.enabled = true;

            // 2. Reset health state
            currentHealth = maxHealth;
            currentSwimStamina = maxSwimStamina;
            isDead = false;
            _isRespawning = false;
            _damageFlashAlpha = 0f;

            // 3. Reset and re-enable Animator
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
            {
                anim.SetBool("IsDead", false);
                anim.ResetTrigger("Die");
                anim.Play("Idle Walk Run Blend", 0, 0f);
            }

            // 4. Re-enable character movement
            var fpc = GetComponent<FirstPersonController>();
            if (fpc != null)
            {
                fpc.IsSwimming = false;
                fpc.enabled = true;
            }

            // 5. Re-enable combat
            var combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = true;

            // 6. Reset camera target pitch
            var camRoot = transform.Find("PlayerCameraRoot");
            if (camRoot != null)
            {
                camRoot.localRotation = Quaternion.identity;
            }

            // 7. De-aggro nearby zombies within 20m of spawn point so player is not instantly swarmed
            var allZombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            foreach (var z in allZombies)
            {
                if (z != null && Vector3.Distance(z.transform.position, _spawnPosition) < 20f)
                {
                    z.currentState = ZombieAI.State.Wandering;
                }
            }

            Debug.Log("[PlayerHealth] Player successfully respawned!");
        }

        private void InitTextures()
        {
            _redTexture = CreateSolidTexture(new Color(0.85f, 0.05f, 0.05f, 0.5f));
            _darkTexture = CreateSolidTexture(new Color(0.08f, 0.02f, 0.02f, 0.82f));
            _healthBarBg = CreateSolidTexture(new Color(0.15f, 0.15f, 0.15f, 0.85f));
            _healthBarFg = CreateSolidTexture(new Color(0.15f, 0.85f, 0.25f, 0.95f));
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (Boat4EscapeInteraction.Instance != null && Boat4EscapeInteraction.Instance.isEscaping) return;
            if (GameMenuManager.Instance != null && GameMenuManager.Instance.IsMenuOpen) return;
            if (CinematicWakeUpIntro.IsIntroActive) return;

            Font regFont = StarterAssets.DialogueFontProvider.Regular;
            Font boldFont = StarterAssets.DialogueFontProvider.Bold;
            if (regFont != null) GUI.skin.font = regFont;

            // ================= DEATH SCREEN =================
            if (isDead)
            {
                // Fullscreen Dark Red Vignette Overlay
                if (_darkTexture != null)
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _darkTexture);
                }

                float centerX = Screen.width * 0.5f;
                float centerY = Screen.height * 0.5f;

                // "YOU DIED" Title
                GUIStyle deathTitleStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) deathTitleStyle.font = boldFont;
                deathTitleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.width * 0.055f), 40, 72);
                deathTitleStyle.fontStyle = FontStyle.Bold;
                deathTitleStyle.normal.textColor = new Color(0.9f, 0.12f, 0.12f, 1.0f);
                deathTitleStyle.alignment = TextAnchor.MiddleCenter;

                // Drop shadow
                GUIStyle shadowStyle = new GUIStyle(deathTitleStyle);
                shadowStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(centerX - 300 + 3, centerY - 90 + 3, 600, 80), "YOU DIED", shadowStyle);
                GUI.Label(new Rect(centerX - 300, centerY - 90, 600, 80), "YOU DIED", deathTitleStyle);

                // Respawn Countdown
                float elapsed = Time.time - _deathTime;
                float remaining = Mathf.Max(0f, respawnDelay - elapsed);

                GUIStyle subStyle = new GUIStyle(GUI.skin.label);
                if (boldFont != null) subStyle.font = boldFont;
                subStyle.fontSize = 18;
                subStyle.fontStyle = FontStyle.Bold;
                subStyle.normal.textColor = new Color(0.95f, 0.85f, 0.85f, 0.9f);
                subStyle.alignment = TextAnchor.MiddleCenter;

                GUI.Label(new Rect(centerX - 250, centerY + 5, 500, 30), $"Respawning in {remaining:F1}s...", subStyle);

                // Respawn Now button
                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                if (boldFont != null) buttonStyle.font = boldFont;
                buttonStyle.fontSize = 15;
                buttonStyle.fontStyle = FontStyle.Bold;
                if (GUI.Button(new Rect(centerX - 90, centerY + 45, 180, 38), "RESPAWN NOW", buttonStyle))
                {
                    Respawn();
                }

                return;
            }

            // ================= NORMAL IN-GAME HUD =================

            // 1. Damage Flash Overlay
            if (_damageFlashAlpha > 0.01f && _redTexture != null)
            {
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _damageFlashAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _redTexture);
                GUI.color = prevColor;
            }

            // 2. Health Bar HUD (Bottom-Left)
            float barWidth = 220f;
            float barHeight = 22f;
            float margin = 28f;
            float x = margin;
            float y = Screen.height - margin - barHeight - 20f;

            // Background
            if (_healthBarBg != null)
            {
                GUI.DrawTexture(new Rect(x - 2, y - 2, barWidth + 4, barHeight + 4), _healthBarBg);
            }

            // Foreground Fill
            float healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
            Color barColor = healthRatio > 0.5f ? Color.Lerp(Color.yellow, new Color(0.2f, 0.85f, 0.3f), (healthRatio - 0.5f) * 2f)
                                               : Color.Lerp(Color.red, Color.yellow, healthRatio * 2f);

            Color savedColor = GUI.color;
            GUI.color = barColor;
            if (_healthBarFg != null)
            {
                GUI.DrawTexture(new Rect(x, y, barWidth * healthRatio, barHeight), _healthBarFg);
            }
            GUI.color = savedColor;

            // Health Label
            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            if (boldFont != null) textStyle.font = boldFont;
            textStyle.fontSize = 14;
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Label(new Rect(x, y - 1, barWidth, barHeight), $"HP: {currentHealth:F0} / {maxHealth:F0}", textStyle);

            // Subtitle
            GUIStyle subInfoStyle = new GUIStyle(GUI.skin.label);
            if (regFont != null) subInfoStyle.font = regFont;
            subInfoStyle.fontSize = 11;
            subInfoStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(x, y + barHeight + 2, barWidth, 18), "SURVIVE THE INFESTATION", subInfoStyle);

            // 3. Swimming Stamina Bar HUD (shown while swimming or recovering stamina)
            bool isSwimming = (_fpc != null && _fpc.IsSwimming);
            if (isSwimming || currentSwimStamina < maxSwimStamina - 0.05f)
            {
                float stamWidth = 230f;
                float stamHeight = 20f;
                float stamX = (Screen.width - stamWidth) * 0.5f;
                float stamY = Screen.height - 70f;

                if (_healthBarBg != null)
                {
                    GUI.DrawTexture(new Rect(stamX - 2, stamY - 2, stamWidth + 4, stamHeight + 4), _healthBarBg);
                }

                float stamRatio = Mathf.Clamp01(currentSwimStamina / Mathf.Max(0.1f, maxSwimStamina));
                Color stamColor = stamRatio > 0.35f
                    ? Color.Lerp(new Color(0.95f, 0.65f, 0.15f), new Color(0.15f, 0.78f, 0.98f), (stamRatio - 0.35f) / 0.65f)
                    : Color.Lerp(new Color(0.95f, 0.15f, 0.15f), new Color(0.95f, 0.65f, 0.15f), stamRatio / 0.35f);

                Color prevStamColor = GUI.color;
                GUI.color = stamColor;
                if (_healthBarFg != null)
                {
                    GUI.DrawTexture(new Rect(stamX, stamY, stamWidth * stamRatio, stamHeight), _healthBarFg);
                }
                GUI.color = prevStamColor;

                string stamLabel = currentSwimStamina > 0.01f
                    ? $"SWIM STAMINA: {currentSwimStamina:F1}s / {maxSwimStamina:F0}s"
                    : "STAMINA DEPLETED - LOSING HP!";

                GUIStyle stamTextStyle = new GUIStyle(textStyle);
                stamTextStyle.fontSize = 13;
                if (currentSwimStamina <= 0.01f)
                {
                    stamTextStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f);
                }
                GUI.Label(new Rect(stamX, stamY - 1, stamWidth, stamHeight), stamLabel, stamTextStyle);
            }
        }
    }
}
