using UnityEngine;

namespace StarterAssets
{
    public enum GunType
    {
        Pistol,
        AssaultRifle,
        Shotgun,
        SMG,
        SniperRifle,
        Knife,
        Crowbar
    }

    /// <summary>
    /// Placed on lootable guns in houses.
    /// Rests on the ground/table with a subtle low-intensity light glow and allows the player to pick it up.
    /// </summary>
    public class GunPickup : MonoBehaviour
    {
        [Header("Gun Identification")]
        public string gunName = "Assault Rifle";
        public GunType gunType = GunType.AssaultRifle;

        [Header("Weapon Stats")]
        public int damage = 45;
        public float fireRate = 0.12f;
        public bool isAutomatic = true;
        public int magazineCapacity = 30;
        public int currentAmmo = 30;
        public int reserveAmmo = 90;
        public float reloadTime = 2.0f;
        public float range = 80f;
        public float spreadAngle = 1.0f;
        public int pelletsPerShot = 1; // 8 for shotgun

        [Header("Visual Floating")]
        public float rotationSpeed = 40f;
        public float bobHeight = 0.08f;
        public float bobFrequency = 2.2f;
        public Light lootGlow;

        [Header("Loot Light Settings")]
        public float glowIntensity = 0.25f;
        public float glowRange = 0.9f;

        [Header("Equipped Weapon Model Prefab")]
        public GameObject weaponModelPrefab;

        private bool _snappedToFloor = false;

        private void Start()
        {
            // Zero out any floating or bobbing
            rotationSpeed = 0f;
            bobHeight = 0f;
            bobFrequency = 0f;

            // Snap firmly to ground, deck, or table surface
            SnapToFloor();

            // Ensure collider for interaction
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.2f, 0.6f, 1.2f);
                box.center = new Vector3(0f, 0.2f, 0f);
            }

            // Subtle low-intensity ground glow to highlight loot without over-brightening rooms
            if (lootGlow == null)
            {
                lootGlow = GetComponentInChildren<Light>();
                if (lootGlow == null)
                {
                    GameObject lightGo = new GameObject("LootLight");
                    lightGo.transform.SetParent(transform, false);
                    lightGo.transform.localPosition = Vector3.up * 0.06f;
                    lootGlow = lightGo.AddComponent<Light>();
                }
            }

            if (lootGlow != null)
            {
                lootGlow.type = LightType.Point;
                lootGlow.range = glowRange;
                lootGlow.intensity = glowIntensity;
                lootGlow.color = GetGunColor(gunType);
            }
        }

        public void SnapToFloor()
        {
            // Guard shack porch specific fix: if gun was spawned floating at chest height (Y ~ 20.48)
            // snap directly to the porch floor deck at Y = 19.36
            if (Mathf.Abs(transform.position.x - 472.0f) < 2.0f && Mathf.Abs(transform.position.z - 142.5f) < 2.0f && transform.position.y > 19.5f)
            {
                transform.position = new Vector3(472.4f, 19.36f, 142.5f);
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                _snappedToFloor = true;
                return;
            }

            int mask = ~((1 << 2) | (1 << 5) | (1 << 8)); // Ignore Raycast (2), UI (5), Player (8)
            Vector3 origin = transform.position + Vector3.up * 1.5f;

            RaycastHit[] hits = Physics.RaycastAll(new Ray(origin, Vector3.down), 8.0f, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.isTrigger) continue;
                if (h.collider.GetComponentInParent<GunPickup>() != null) continue;
                if (h.collider.GetComponentInParent<SurvivalItemPickup>() != null) continue;

                if (Vector3.Dot(h.normal, Vector3.up) > 0.40f)
                {
                    transform.position = h.point + Vector3.up * 0.02f;
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    _snappedToFloor = true;
                    return;
                }
            }

            // Fallback raycast from higher up
            RaycastHit[] fallbackHits = Physics.RaycastAll(new Ray(transform.position + Vector3.up * 4.0f, Vector3.down), 15.0f, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(fallbackHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in fallbackHits)
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider.isTrigger) continue;
                if (h.collider.GetComponentInParent<GunPickup>() != null) continue;
                if (h.collider.GetComponentInParent<SurvivalItemPickup>() != null) continue;

                if (Vector3.Dot(h.normal, Vector3.up) > 0.40f)
                {
                    transform.position = h.point + Vector3.up * 0.02f;
                    transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    _snappedToFloor = true;
                    return;
                }
            }
        }

        private void Update()
        {
            // Ensure the gun rests directly flat on the ground and does not float or bob
            if (!_snappedToFloor && Time.frameCount % 30 == 0)
            {
                SnapToFloor();
            }
        }

        public static Color GetGunColor(GunType type)
        {
            // All weapons including firearms, sniper, shotgun, pistol, knife, and crowbar glow RED
            return new Color(1.0f, 0.15f, 0.15f);
        }
    }
}
