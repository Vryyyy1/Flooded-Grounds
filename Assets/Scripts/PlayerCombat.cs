using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Handles player attack hit detection against zombies.
    /// Hooks into ArmatureActionTrigger or triggers on attack action.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attack Settings")]
        public float attackRange = 1.9f;
        public float attackRadius = 1.0f;
        public float punchDamage = 30f;
        public LayerMask hitLayers = ~0;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip punchImpactClip;

        private ArmatureActionTrigger _actionTrigger;

        private void Awake()
        {
            _actionTrigger = GetComponentInChildren<ArmatureActionTrigger>();
            if (_actionTrigger == null) _actionTrigger = GetComponentInParent<ArmatureActionTrigger>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            if (punchImpactClip == null)
            {
                punchImpactClip = CreatePunchSound();
            }
        }

        private void Start()
        {
            try
            {
                if (_actionTrigger != null && _actionTrigger.onActionTriggered != null)
                {
                    _actionTrigger.onActionTriggered.AddListener(OnPlayerAttack);
                }
            }
            catch (System.Exception)
            {
                _actionTrigger = null;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_actionTrigger != null && _actionTrigger.onActionTriggered != null)
                {
                    _actionTrigger.onActionTriggered.RemoveListener(OnPlayerAttack);
                }
            }
            catch (System.Exception)
            {
                _actionTrigger = null;
            }
        }

        public void OnPlayerAttack()
        {
            // Sphere cast / overlap in front of the player
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 center = origin + transform.forward * (attackRange * 0.7f);

            Collider[] hits = Physics.OverlapSphere(center, attackRadius, hitLayers, QueryTriggerInteraction.Ignore);

            bool hitZombie = false;
            foreach (var col in hits)
            {
                // Don't hit self
                if (col.transform.IsChildOf(transform) || col.gameObject == gameObject) continue;

                var zombie = col.GetComponentInParent<ZombieAI>();
                if (zombie != null && !zombie.isDead)
                {
                    Vector3 hitPoint = col.ClosestPoint(center);
                    zombie.TakeDamage(punchDamage, hitPoint, transform.forward);
                    PlayPunchSound();
                    hitZombie = true;
                    Debug.Log($"[PlayerCombat] Punched zombie for {punchDamage} damage!");
                    break;
                }
            }

            if (!hitZombie)
            {
                // Play whoosh sound if missed
                if (audioSource != null && punchImpactClip != null)
                {
                    audioSource.pitch = 1.4f;
                    audioSource.PlayOneShot(punchImpactClip, 0.3f);
                }
            }
        }

        private void PlayPunchSound()
        {
            if (audioSource != null && punchImpactClip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.15f);
                audioSource.PlayOneShot(punchImpactClip, 0.9f);
            }
        }

        private static AudioClip CreatePunchSound()
        {
            int sampleRate = 22050;
            float length = 0.25f;
            int totalSamples = (int)(sampleRate * length);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Pow(1f - (t / length), 3f);
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * (1f - t * 2f) * t);
                float noise = (Random.value * 2f - 1f) * 0.4f;
                data[i] = (thump * 0.7f + noise) * env;
            }

            AudioClip clip = AudioClip.Create("PunchImpact", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 center = origin + transform.forward * (attackRange * 0.7f);
            Gizmos.DrawWireSphere(center, attackRadius);
        }
    }
}
