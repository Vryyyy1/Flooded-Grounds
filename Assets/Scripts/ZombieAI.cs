using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace StarterAssets
{
    /// <summary>
    /// Zombie AI controller: detects player, chases across terrain/bridges,
    /// performs visceral lunging attack animations, reacts to gunfire/punches,
    /// and executes a grounded 4-panel backward death collapse without ever sinking into the floor.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ZombieAI : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Wandering,
            Screaming,
            Chasing,
            Attacking,
            Hit,
            Dead
        }

        public enum RunStyle
        {
            ZombieRun,
            MutantRun
        }

        public enum AttackStyle
        {
            ZombieAttack,
            MutantSwiping
        }

        [Header("Animation Variations")]
        public RunStyle currentRunStyle = RunStyle.ZombieRun;
        public AttackStyle currentAttackStyle = AttackStyle.ZombieAttack;

        [Header("State")]
        public State currentState = State.Idle;

        [Header("Target & Detection")]
        [Tooltip("The player to hunt. Auto-detected by Player tag if null.")]
        public Transform targetPlayer;
        [Tooltip("Distance at which zombie detects the player")]
        public float detectionRange = 36f;
        [Tooltip("Distance at which zombie loses track of the player")]
        public float loseTargetRange = 55f;
        [Tooltip("Distance at which zombie can attack the player")]
        public float attackRange = 2.1f;

        [Header("Movement")]
        public float walkSpeed = 1.65f;
        public float chaseSpeed = 4.65f;
        public float rotationSpeed = 8.5f;
        public float gravity = -18.0f;

        [Header("Water Barrier")]
        [Tooltip("World Y level of the water surface; water acts as an impassable barrier for zombies")]
        public float waterSurfaceY = 16.0f;
        [Tooltip("Minimum dry ground margin above the water surface required for zombies to step")]
        public float waterShoreMargin = 0.22f;

        [Header("Attack Settings")]
        public float attackDamage = 18f;
        public float attackCooldown = 0.95f;
        public float attackDamageDelay = 0.35f; // Moment in claw swipe animation when damage registers

        [Header("Health")]
        public float maxHealth = 60f;
        public float currentHealth = 60f;
        public bool isDead = false;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip growlClip;
        public AudioClip attackClip;
        public AudioClip hitClip;
        public AudioClip deathClip;
        public AudioClip screamClip;

        // Components
        private CharacterController _controller;
        private Animator _animator;
        private float _verticalVelocity;
        private bool _isAttacking;
        private Coroutine _attackCoroutine;
        private float _screamEndTime = -999f;
        private float _lastAttackTime = -999f;
        private float _hitStartTime = -999f;
        private float _deathStartTime = -999f;
        private float _deathGroundY = 0f;
        private int _attackVariant = 0;
        private int _hitVariant = 0;
        private int _deathVariant = 0;
        private Vector3 _spawnPosition;
        private Vector3 _wanderTarget;
        private float _wanderTimer;
        private float _growlTimer;
        private float _animTimeOffset;

        // Cached Skeleton Bones for Grounded Posture & Procedural Undead Animation
        [Header("Shoulder & Arm Posture Stabilization")]
        [Tooltip("Locks clavicles/shoulders to their authentic modeled bind pose, preventing Mecanim retargeting from pinching, twisting, or shoving shoulders into the neck.")]
        public bool stabilizeShoulders = true;
        [Tooltip("Fine-tune right shoulder pitch angle (+ tilts down/forward, - tilts up/back)")]
        [Range(-30f, 30f)] public float rightShoulderPitchOffset = 0f;
        [Tooltip("Fine-tune right shoulder roll/splay (+ spreads outward, - tucks inward)")]
        [Range(-30f, 30f)] public float rightShoulderRollOffset = 0f;
        [Tooltip("Fine-tune left shoulder pitch angle")]
        [Range(-30f, 30f)] public float leftShoulderPitchOffset = 0f;
        [Tooltip("Fine-tune left shoulder roll/splay")]
        [Range(-30f, 30f)] public float leftShoulderRollOffset = 0f;

        [Header("Leg Separation & Anti-Sticking Guard")]
        [Tooltip("Guarantees minimum lateral clearance between both legs so thighs, knees, and feet never stick to each other or intersect during walk/run.")]
        public bool preventLegSticking = true;
        [Tooltip("Minimum lateral distance (in meters) maintained between both knees and feet.")]
        [Range(0.12f, 0.35f)] public float minLegSeparation = 0.22f;

        [Header("Legs Posture & Stride Alignment")]
        [Tooltip("Allows fine-tuning of leg posture and stride angles.")]
        public bool stabilizeLegs = true;

        [Header("Left Leg & Foot Tuning")]
        [Tooltip("Splays left leg outward (-) or inward (+)")]
        [Range(-30f, 30f)] public float leftLegSplayOffset = 0f;
        [Tooltip("Turns left knee and foot outward (-) or inward (+)")]
        [Range(-30f, 30f)] public float leftLegYawOffset = 0f;
        [Tooltip("Fine-tune left thigh pitch (+ tilts forward, - tilts back)")]
        [Range(-30f, 30f)] public float leftLegPitchOffset = 0f;
        [Tooltip("Fine-tune left calf / knee roll alignment")]
        [Range(-30f, 30f)] public float leftCalfRollOffset = 0f;
        [Tooltip("Fine-tune left calf / knee yaw alignment")]
        [Range(-30f, 30f)] public float leftCalfYawOffset = 0f;
        [Tooltip("Pitches left foot down to plant flat on the ground (+ toes down/flat, - toes up)")]
        [Range(-45f, 45f)] public float leftFootPitchOffset = 12f;
        [Tooltip("Levels left foot sole horizontally (+ roll out, - roll in)")]
        [Range(-30f, 30f)] public float leftFootRollOffset = 0f;
        [Tooltip("Turns left foot outward (-) or inward (+)")]
        [Range(-30f, 30f)] public float leftFootYawOffset = 0f;

        [Header("Right Leg & Foot Tuning")]
        [Tooltip("Splays right leg outward (-) or inward (+)")]
        [Range(-30f, 30f)] public float rightLegSplayOffset = 0f;
        [Tooltip("Turns right knee and foot outward (-) or inward (+)")]
        [Range(-30f, 30f)] public float rightLegYawOffset = 0f;
        [Tooltip("Fine-tune right thigh pitch")]
        [Range(-30f, 30f)] public float rightLegPitchOffset = 0f;
        [Tooltip("Fine-tune right calf / knee roll alignment")]
        [Range(-30f, 30f)] public float rightCalfRollOffset = 0f;
        [Tooltip("Fine-tune right calf / knee yaw alignment")]
        [Range(-30f, 30f)] public float rightCalfYawOffset = 0f;
        [Tooltip("Pitches right foot to plant flat on the ground (+ toes down/flat, - toes up)")]
        [Range(-45f, 45f)] public float rightFootPitchOffset = 4f;
        [Tooltip("Levels right foot sole horizontally")]
        [Range(-30f, 30f)] public float rightFootRollOffset = 0f;
        [Tooltip("Turns right foot outward (-) or inward (+)")]
        [Range(-30f, 30f)] public float rightFootYawOffset = 0f;

        private struct BoneBindInfo
        {
            public Transform bone;
            public Vector3 localPos;
            public Quaternion localRot;
        }
        private BoneBindInfo[] _bindBones;
        private Transform _hipsBone;
        private Transform _spineBone;
        private Transform _chestBone;
        private Transform _upperChestBone;
        private Transform _neckBone;
        private Transform _headBone;
        private Transform _leftClavicle;
        private Transform _rightClavicle;
        private Vector3 _bindClavicleLPos;
        private Quaternion _bindClavicleLRot;
        private Vector3 _bindClavicleRPos;
        private Quaternion _bindClavicleRRot;
        private bool _hasCapturedClavicleBind = false;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;
        private Transform _leftUpperLeg;
        private Transform _leftLowerLeg;
        private Transform _leftFoot;
        private Transform _rightUpperLeg;
        private Transform _rightLowerLeg;
        private Transform _rightFoot;
        private Vector3 _bindHipsLocalPos = new Vector3(0f, -0.02280866f, 0.9589678f);
        private Quaternion _bindHipsLocalRot = new Quaternion(0.04253178f, 0.7058265f, 0.04253178f, 0.7058265f);
        private float _standingHipsHeight = 0.92f;
        private bool _isBip001Rig = false;
        private bool _hasEquippedWeapon = false;
        public bool isRadioAmbushMonster = false;

        // Animation Hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackHeavyHash = Animator.StringToHash("AttackHeavy");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

        private float _targetSpeed = 0f;
        private float _currentSpeedParam = 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            CacheSkeletonBones();
            _animTimeOffset = Random.Range(0f, 10f);

            var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.baseOffset = 0f;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f; // 3D sound
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 35f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }

            if (maxHealth < 1500f) maxHealth = 1500f;
            currentHealth = maxHealth;
            _spawnPosition = transform.position;
            _wanderTarget = _spawnPosition;

            // Aggressive tuning safeguard (ensures prefab serialized overrides don't keep zombies sluggish)
            if (chaseSpeed < 4.5f) chaseSpeed = 4.65f;
            if (detectionRange < 34f) detectionRange = 36f;
            if (loseTargetRange < 50f) loseTargetRange = 55f;
            if (rotationSpeed < 8f) rotationSpeed = 8.5f;
            if (attackRange < 2.0f) attackRange = 2.1f;
            if (attackCooldown > 1.0f) attackCooldown = 0.95f;
            if (attackDamageDelay > 0.38f) attackDamageDelay = 0.35f;

            // Remove any rogue ArmatureActionTrigger so player clicks never trigger zombie attack!
            var rogueTriggers = GetComponentsInChildren<ArmatureActionTrigger>(true);
            foreach (var rt in rogueTriggers)
            {
                Destroy(rt);
            }

            // Stagger animation variations
            currentRunStyle = (UnityEngine.Random.value > 0.5f) ? RunStyle.ZombieRun : RunStyle.MutantRun;

            // Generate procedural sounds if none assigned
            InitProceduralAudio();
        }

        private void CacheSkeletonBones()
        {
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            List<BoneBindInfo> bindList = new List<BoneBindInfo>(allTransforms.Length);

            foreach (Transform t in allTransforms)
            {
                if (t == transform) continue;
                string n = t.name;
                switch (n)
                {
                    // UE4 Mannequin (ZombieMale_AAB, BookHeadMonster)
                    case "pelvis": _hipsBone = t; break;
                    case "spine_01": _spineBone = t; break;
                    case "spine_02": _chestBone = t; break;
                    case "spine_03": _upperChestBone = t; break;
                    case "neck_01": _neckBone = t; break;
                    case "head": _headBone = t; break;
                    case "clavicle_l": _leftClavicle = t; break;
                    case "clavicle_r": _rightClavicle = t; break;
                    case "upperarm_l": _leftUpperArm = t; break;
                    case "lowerarm_l": _leftLowerArm = t; break;
                    case "hand_l": _leftHand = t; break;
                    case "upperarm_r": _rightUpperArm = t; break;
                    case "lowerarm_r": _rightLowerArm = t; break;
                    case "hand_r": _rightHand = t; break;
                    case "thigh_l": _leftUpperLeg = t; break;
                    case "calf_l": _leftLowerLeg = t; break;
                    case "foot_l": _leftFoot = t; break;
                    case "thigh_r": _rightUpperLeg = t; break;
                    case "calf_r": _rightLowerLeg = t; break;
                    case "foot_r": _rightFoot = t; break;

                    // 3ds Max Bip001 (MN D)
                    case "Bip001 Pelvis": _hipsBone = t; _isBip001Rig = true; break;
                    case "Bip001 Spine": _spineBone = t; _isBip001Rig = true; break;
                    case "Bip001 Spine1": _chestBone = t; break;
                    case "Bip001 Spine2": _upperChestBone = t; break;
                    case "Bip001 Neck": _neckBone = t; break;
                    case "Bip001 Head": _headBone = t; break;
                    case "Bip001 L Clavicle": _leftClavicle = t; break;
                    case "Bip001 R Clavicle": _rightClavicle = t; break;
                    case "Bip001 L UpperArm": _leftUpperArm = t; break;
                    case "Bip001 L Forearm": _leftLowerArm = t; break;
                    case "Bip001 L Hand": _leftHand = t; break;
                    case "Bip001 R UpperArm": _rightUpperArm = t; break;
                    case "Bip001 R Forearm": _rightLowerArm = t; break;
                    case "Bip001 R Hand": _rightHand = t; break;
                    case "Bip001 L Thigh": _leftUpperLeg = t; break;
                    case "Bip001 L Calf": _leftLowerLeg = t; break;
                    case "Bip001 L Foot": _leftFoot = t; break;
                    case "Bip001 R Thigh": _rightUpperLeg = t; break;
                    case "Bip001 R Calf": _rightLowerLeg = t; break;
                    case "Bip001 R Foot": _rightFoot = t; break;
                }

                // Store bind pose for skeleton bones (skip mesh renderer objects)
                if (t.GetComponent<SkinnedMeshRenderer>() == null && n != "root")
                {
                    bindList.Add(new BoneBindInfo
                    {
                        bone = t,
                        localPos = t.localPosition,
                        localRot = t.localRotation
                    });
                }
            }

            if (_animator != null && _animator.isHuman)
            {
                if (_hipsBone == null) _hipsBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
                if (_spineBone == null) _spineBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
                if (_chestBone == null) _chestBone = _animator.GetBoneTransform(HumanBodyBones.Chest);
                if (_upperChestBone == null) _upperChestBone = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (_neckBone == null) _neckBone = _animator.GetBoneTransform(HumanBodyBones.Neck);
                if (_headBone == null) _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
                if (_leftClavicle == null) _leftClavicle = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                if (_rightClavicle == null) _rightClavicle = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                if (_leftUpperArm == null) _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                if (_leftLowerArm == null) _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (_leftHand == null) _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (_rightUpperArm == null) _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (_rightLowerArm == null) _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (_rightHand == null) _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (_leftUpperLeg == null) _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                if (_leftLowerLeg == null) _leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                if (_leftFoot == null) _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (_rightUpperLeg == null) _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                if (_rightLowerLeg == null) _rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                if (_rightFoot == null) _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            // Generic / Mixamo / Demon fallback bone matching if any key bones are still null
            foreach (Transform t in allTransforms)
            {
                if (t == transform || t.GetComponent<SkinnedMeshRenderer>() != null) continue;
                string lower = t.name.ToLowerInvariant();
                if (_hipsBone == null && (lower.Contains("hips") || lower.Contains("pelvis"))) _hipsBone = t;
                else if (_spineBone == null && lower.Contains("spine") && !lower.Contains("1") && !lower.Contains("2") && !lower.Contains("3")) _spineBone = t;
                else if (_chestBone == null && (lower.Contains("chest") || lower.Contains("spine1") || lower.Contains("spine_02"))) _chestBone = t;
                else if (_neckBone == null && lower.Contains("neck")) _neckBone = t;
                else if (_headBone == null && lower.Contains("head") && !lower.Contains("end") && !lower.Contains("top")) _headBone = t;
                else if (_rightHand == null && lower.Contains("hand") && (lower.Contains("r") || lower.Contains("right")) && !lower.Contains("thumb") && !lower.Contains("index") && !lower.Contains("middle") && !lower.Contains("ring") && !lower.Contains("pinky")) _rightHand = t;
                else if (_leftHand == null && lower.Contains("hand") && (lower.Contains("l") || lower.Contains("left")) && !lower.Contains("thumb") && !lower.Contains("index") && !lower.Contains("middle") && !lower.Contains("ring") && !lower.Contains("pinky")) _leftHand = t;
                else if (_leftClavicle == null && (lower.Contains("clavicle_l") || lower.Contains("shoulder_l") || lower.Contains("l_shoulder") || lower.Contains("l clavicle"))) _leftClavicle = t;
                else if (_rightClavicle == null && (lower.Contains("clavicle_r") || lower.Contains("shoulder_r") || lower.Contains("r_shoulder") || lower.Contains("r clavicle"))) _rightClavicle = t;
            }

            if (_leftClavicle != null)
            {
                _bindClavicleLPos = _leftClavicle.localPosition;
                _bindClavicleLRot = _leftClavicle.localRotation;
            }
            if (_rightClavicle != null)
            {
                _bindClavicleRPos = _rightClavicle.localPosition;
                _bindClavicleRRot = _rightClavicle.localRotation;
            }
            _hasCapturedClavicleBind = (_leftClavicle != null || _rightClavicle != null);

            if (_hipsBone != null)
            {
                if (_hipsBone.localPosition.magnitude > 0.5f || _hipsBone.name != "pelvis")
                {
                    _bindHipsLocalPos = _hipsBone.localPosition;
                }
                _bindHipsLocalRot = _hipsBone.localRotation;
                float initialH = _hipsBone.position.y - transform.position.y;
                if (initialH >= 0.65f && initialH <= 1.35f)
                {
                    _standingHipsHeight = initialH;
                }
            }

            _bindBones = bindList.ToArray();
        }

        public void MakeHighlyAggressive(Transform player)
        {
            if (player != null) targetPlayer = player;
            isRadioAmbushMonster = true;
            detectionRange = 450f;
            loseTargetRange = 600f;
            chaseSpeed = Random.Range(5.2f, 6.2f);
            walkSpeed = 3.8f;
            rotationSpeed = 12.5f;
            attackRange = 2.35f;
            attackCooldown = 0.65f;
            attackDamageDelay = 0.25f;
            attackDamage = 24f;
            currentState = State.Chasing;
            currentRunStyle = (Random.value > 0.4f) ? RunStyle.MutantRun : RunStyle.ZombieRun;
            if (_animator != null)
            {
                _targetSpeed = 1.0f;
            }
        }

        public void AttachHorrorWeaponToHand(bool isDemonic = false)
        {
            if (_hasEquippedWeapon) return;
            if (_hipsBone == null && _rightHand == null) CacheSkeletonBones();

            Transform attachParent = _rightHand != null ? _rightHand : (_rightLowerArm != null ? _rightLowerArm : transform);
            _hasEquippedWeapon = true;
            currentAttackStyle = AttackStyle.MutantSwiping;
            attackDamage = Mathf.Max(attackDamage, isDemonic ? 28f : 24f);
            attackRange = Mathf.Max(attackRange, 2.45f);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            GameObject weaponRoot = new GameObject(isDemonic ? "DemonHorrorCleaver_Weapon" : "HorrorSpikedWeapon");
            weaponRoot.transform.SetParent(attachParent, false);
            weaponRoot.transform.localPosition = new Vector3(0.04f, 0.02f, 0.04f);
            weaponRoot.transform.localRotation = Quaternion.Euler(15f, 90f, -85f);

            // 1. Iron Handle
            Material handleMat = new Material(urpLit);
            Color handleCol = new Color(0.16f, 0.09f, 0.06f);
            handleMat.color = handleCol;
            if (handleMat.HasProperty("_BaseColor")) handleMat.SetColor("_BaseColor", handleCol);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "WeaponHandle";
            handle.transform.SetParent(weaponRoot.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            handle.transform.localScale = new Vector3(0.036f, 0.34f, 0.036f);
            if (handle.TryGetComponent<Collider>(out var hc)) Destroy(hc);
            if (handle.TryGetComponent<Renderer>(out var hr)) hr.sharedMaterial = handleMat;

            // 2. Jagged Blood-Stained Horror Cleaver / Battle Axe Blade
            Material bladeMat = new Material(urpLit);
            Color bladeCol = isDemonic ? new Color(0.45f, 0.06f, 0.06f) : new Color(0.32f, 0.28f, 0.28f);
            bladeMat.color = bladeCol;
            if (bladeMat.HasProperty("_BaseColor")) bladeMat.SetColor("_BaseColor", bladeCol);
            if (bladeMat.HasProperty("_Metallic")) bladeMat.SetFloat("_Metallic", 0.82f);
            if (bladeMat.HasProperty("_Smoothness")) bladeMat.SetFloat("_Smoothness", 0.55f);
            if (isDemonic && bladeMat.HasProperty("_EmissionColor"))
            {
                bladeMat.EnableKeyword("_EMISSION");
                bladeMat.SetColor("_EmissionColor", new Color(0.55f, 0.05f, 0.02f));
            }

            GameObject mainBlade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mainBlade.name = "CleaverBlade";
            mainBlade.transform.SetParent(weaponRoot.transform, false);
            mainBlade.transform.localPosition = new Vector3(0.09f, 0.44f, 0f);
            mainBlade.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            mainBlade.transform.localScale = new Vector3(0.22f, 0.36f, 0.022f);
            if (mainBlade.TryGetComponent<Collider>(out var bc)) Destroy(bc);
            if (mainBlade.TryGetComponent<Renderer>(out var br)) br.sharedMaterial = bladeMat;

            // 3. Serrated Dorsal Hook / Spikes
            for (int i = 0; i < 3; i++)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = $"HorrorSpike_{i}";
                spike.transform.SetParent(weaponRoot.transform, false);
                spike.transform.localPosition = new Vector3(-0.05f, 0.34f + i * 0.09f, 0f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                spike.transform.localScale = new Vector3(0.09f, 0.09f, 0.026f);
                if (spike.TryGetComponent<Collider>(out var sc)) Destroy(sc);
                if (spike.TryGetComponent<Renderer>(out var sr)) sr.sharedMaterial = bladeMat;
            }

            // 4. If Demon Horror Creature: add demonic horns & glowing eyes on head + dark crimson demon hide
            if (isDemonic)
            {
                if (_headBone != null)
                {
                    Material hornMat = new Material(urpLit);
                    Color hornCol = new Color(0.10f, 0.04f, 0.04f);
                    hornMat.color = hornCol;
                    if (hornMat.HasProperty("_BaseColor")) hornMat.SetColor("_BaseColor", hornCol);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        horn.name = side < 0 ? "DemonHorn_L" : "DemonHorn_R";
                        horn.transform.SetParent(_headBone, false);
                        horn.transform.localPosition = new Vector3(side * 0.08f, 0.14f, 0.04f);
                        horn.transform.localRotation = Quaternion.Euler(-28f, 0f, side * -26f);
                        horn.transform.localScale = new Vector3(0.035f, 0.14f, 0.035f);
                        if (horn.TryGetComponent<Collider>(out var hCol)) Destroy(hCol);
                        if (horn.TryGetComponent<Renderer>(out var hRen)) hRen.sharedMaterial = hornMat;
                    }
                }

                // Tint mesh renderers with a terrifying dark blood-demon crimson hue
                Renderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    Material demonMat = new Material(r.sharedMaterial);
                    Color dCol = new Color(0.72f, 0.22f, 0.20f, 1f);
                    demonMat.color = dCol;
                    if (demonMat.HasProperty("_BaseColor")) demonMat.SetColor("_BaseColor", dCol);
                    r.material = demonMat;
                }
            }
        }

        private void RestoreBindPose()
        {
            if (_bindBones == null) return;
            for (int i = 0; i < _bindBones.Length; i++)
            {
                if (_bindBones[i].bone != null)
                {
                    _bindBones[i].bone.localPosition = _bindBones[i].localPos;
                    _bindBones[i].bone.localRotation = _bindBones[i].localRot;
                }
            }
            if (_hipsBone != null)
            {
                _hipsBone.localPosition = _bindHipsLocalPos;
                _hipsBone.localRotation = _bindHipsLocalRot;
            }
        }

        /// <summary>
        /// Rotates a bone in character-aligned world axes (independent of local FBX bone orientation).
        /// pitchDeg: around transform.right (+ tilts upward bones forward, - tilts upward bones backward; for downward limbs, - swings forward/up, + swings backward)
        /// yawDeg: around transform.up (+ turns right, - turns left)
        /// rollDeg: around transform.forward (+ tilts upward bones left / brings left A-pose arm down; - tilts upward bones right / brings right A-pose arm down)
        /// </summary>
        private void RotateBoneCharSpace(Transform bone, float pitchDeg, float yawDeg, float rollDeg)
        {
            if (bone == null) return;
            Quaternion q = Quaternion.AngleAxis(yawDeg, transform.up)
                         * Quaternion.AngleAxis(rollDeg, transform.forward)
                         * Quaternion.AngleAxis(pitchDeg, transform.right);
            bone.rotation = q * bone.rotation;
        }

        private void Start()
        {
            LocatePlayer();
            InitWaterBarrier();
            // Snap zombie to ground surface on spawn so it never starts submerged in terrain
            float initialGround = GetGroundHeight(transform.position);
            if (initialGround > -900f)
            {
                transform.position = new Vector3(transform.position.x, initialGround, transform.position.z);
            }
            EnsureOutOfWaterOnStart();
            // Stagger initial wander timer and animation speed for organic individual pacing
            _wanderTimer = Random.Range(1f, 4f);
            _growlTimer = Random.Range(2f, 8f);
            if (_animator != null)
            {
                _animator.speed = Random.Range(0.92f, 1.08f);
            }
        }

        private int _cachedGroundFrame = -1;
        private float _cachedGroundY = -999f;
        private Vector3 _cachedGroundPos = new Vector3(-9999f, -9999f, -9999f);
        private Vector3 _lastDryPosition;
        private bool _hasLastDryPosition = false;
        private bool _isStoppedAtWater = false;
        private static float _cachedSceneWaterY = -999f;

        private void Update()
        {
            if (isDead)
            {
                // Keep dead zombie's root anchored on the ground surface (never push downward!)
                if (_deathGroundY > -900f)
                {
                    transform.position = new Vector3(transform.position.x, _deathGroundY, transform.position.z);
                }
                return;
            }

            // Keep player reference
            if (targetPlayer == null)
            {
                LocatePlayer();
                if (targetPlayer == null) return;
            }

            float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

            // LOD Performance: For distant wandering zombies, stagger updates across frames to preserve high FPS
            if (distToPlayer > 60f && (currentState == State.Idle || currentState == State.Wandering))
            {
                if ((Time.frameCount + GetHashCode()) % 3 != 0)
                {
                    return;
                }
            }

            // Apply gravity with reliable ground height checking
            float groundY = GetGroundHeight(transform.position);
            if (groundY > (waterSurfaceY + waterShoreMargin))
            {
                _lastDryPosition = new Vector3(transform.position.x, groundY, transform.position.z);
                _hasLastDryPosition = true;
            }
            bool isNearGround = (groundY > -900f && transform.position.y <= groundY + 0.15f);

            if (_controller.isGrounded || isNearGround)
            {
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
                _verticalVelocity = Mathf.Max(_verticalVelocity, -12.0f); // Terminal velocity prevents tunneling
            }

            // Periodic ambient growl
            _growlTimer -= Time.deltaTime;
            if (_growlTimer <= 0f && distToPlayer < detectionRange * 1.2f)
            {
                PlaySound(growlClip, 0.6f);
                _growlTimer = Random.Range(6f, 14f);
            }

            // State Machine
            switch (currentState)
            {
                case State.Idle:
                case State.Wandering:
                    HandleWanderState(distToPlayer);
                    break;

                case State.Screaming:
                    HandleScreamState(distToPlayer);
                    break;

                case State.Chasing:
                    HandleChaseState(distToPlayer);
                    break;

                case State.Attacking:
                    HandleAttackingState(distToPlayer);
                    break;

                case State.Hit:
                    // Maintain gravity and grounding during hit stun so bullet impacts don't sink the zombie
                    Vector3 hitMove = Vector3.up * (_verticalVelocity * Time.deltaTime);
                    _controller.Move(hitMove);
                    break;
            }

            // Enforce strict water barrier so zombies can never step into or swim on water
            EnforceDryPositionBarrier();

            // Refresh groundY at clamped position and keep zombie feet firmly on ground surface
            groundY = GetGroundHeight(transform.position);
            if (groundY > -900f && transform.position.y < groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                _verticalVelocity = 0f;
            }

            // Smoothly damp animator Speed parameter for organic acceleration/deceleration
            if (_animator != null && currentState != State.Dead)
            {
                _currentSpeedParam = Mathf.MoveTowards(_currentSpeedParam, _targetSpeed, Time.deltaTime * 3.5f);
                _animator.SetFloat(SpeedHash, _currentSpeedParam);

                // Dynamically sync animation playback speed to actual movement speed so feet plant realistically
                if (currentState == State.Chasing)
                {
                    // Scale playback proportionally with chaseSpeed relative to baseline (4.8 m/s)
                    float chaseRatio = chaseSpeed / 4.8f;
                    _animator.speed = Mathf.Clamp(chaseRatio * Mathf.Max(0.4f, _currentSpeedParam), 0.75f, 1.35f);
                }
                else if (currentState == State.Wandering)
                {
                    float normalizedWalk = Mathf.Clamp01(_currentSpeedParam / 0.35f);
                    float walkRatio = walkSpeed / 1.65f;
                    _animator.speed = Mathf.Clamp(walkRatio * Mathf.Max(0.7f, normalizedWalk), 0.85f, 1.25f);
                }
                else if (currentState == State.Attacking)
                {
                    _animator.speed = 1.15f;
                }
                else if (currentState == State.Hit)
                {
                    _animator.speed = 1.25f;
                }
                else
                {
                    _animator.speed = 1.0f;
                }
            }
        }

        private void LateUpdate()
        {
            if (_hipsBone == null) return;

            if (isDead || currentState == State.Dead)
            {
                if (_deathGroundY > -900f)
                {
                    transform.position = new Vector3(transform.position.x, _deathGroundY, transform.position.z);
                }
                ApplyVideoDeathAnimation();
                return;
            }

            // 1. Ensure character root is never below ground
            float groundY = GetGroundHeight(transform.position);
            if (groundY > -900f && transform.position.y < groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
            }

            // 2. Apply full 6-state Zombie Animation from reference video (Idles, Walks, Runs, Attacks, Hits)
            ApplyVideoZombieAnimation();

            // 3. Subtle head tracking toward player when walking/chasing
            if (targetPlayer != null && (currentState == State.Wandering || currentState == State.Chasing) && _headBone != null)
            {
                ApplyRealisticHeadTracking();
            }

            // 4. Anti-Sticking Leg & Stance Clearance Guard:
            if (preventLegSticking && _leftUpperLeg != null && _rightUpperLeg != null)
            {
                if (_leftLowerLeg != null && _rightLowerLeg != null)
                {
                    Vector3 lKneeChar = transform.InverseTransformPoint(_leftLowerLeg.position);
                    Vector3 rKneeChar = transform.InverseTransformPoint(_rightLowerLeg.position);
                    float kneeSep = rKneeChar.x - lKneeChar.x;
                    if (kneeSep < minLegSeparation)
                    {
                        float deficit = minLegSeparation - kneeSep;
                        float pushAngle = Mathf.Clamp(deficit * 65f, 0f, 22f);
                        RotateBoneCharSpace(_leftUpperLeg, 0f, 0f, -pushAngle);
                        RotateBoneCharSpace(_rightUpperLeg, 0f, 0f, pushAngle);
                    }
                }

                if (_leftFoot != null && _rightFoot != null)
                {
                    Vector3 lFootChar = transform.InverseTransformPoint(_leftFoot.position);
                    Vector3 rFootChar = transform.InverseTransformPoint(_rightFoot.position);
                    float footSep = rFootChar.x - lFootChar.x;
                    if (footSep < minLegSeparation)
                    {
                        float deficit = minLegSeparation - footSep;
                        float pushAngle = Mathf.Clamp(deficit * 45f, 0f, 18f);
                        RotateBoneCharSpace(_leftUpperLeg, 0f, 0f, -pushAngle);
                        RotateBoneCharSpace(_rightUpperLeg, 0f, 0f, pushAngle);
                    }
                }
            }

            // 5. Feet Ground Contact & Surface Alignment Guard:
            if (_leftFoot != null && _rightFoot != null)
            {
                float leftFootGround = GetGroundHeight(_leftFoot.position);
                float rightFootGround = GetGroundHeight(_rightFoot.position);
                float lowestPenetration = 0f;
                if (leftFootGround > -900f && _leftFoot.position.y < leftFootGround)
                {
                    lowestPenetration = Mathf.Max(lowestPenetration, leftFootGround - _leftFoot.position.y);
                }
                if (rightFootGround > -900f && _rightFoot.position.y < rightFootGround)
                {
                    lowestPenetration = Mathf.Max(lowestPenetration, rightFootGround - _rightFoot.position.y);
                }
                if (lowestPenetration > 0.005f && lowestPenetration < 0.35f)
                {
                    transform.position += Vector3.up * lowestPenetration;
                }
            }
        }

        /// <summary>
        /// Evaluates the exact 6 Zombie Animation states shown in Screen Recording 2026-11-12 092929.mp4:
        ///  - Idles   (0:00 - 0:04): Hunched asymmetrical stance, bent knees, drooping head, arms hanging loosely in front of thighs.
        ///  - Hits    (0:05 - 0:11): Torso & head snap back, forearms flail up defensively or twist sideways.
        ///  - Attacks (0:12 - 0:17): 1) Left horizontal swipe, 2) Right hook slash, 3) Two-handed overhead raise & downward smash.
        ///  - Walks   (0:18 - 0:23): Deep forward hunch, dragging/limping shamble, arms dangling straight down in front of knees.
        ///  - Runs    (0:24 - 0:28): Forward-leaning crouched sprint, elbows bent in front of chest pumping forward/back.
        /// </summary>
        private void ApplyVideoZombieAnimation()
        {
            RestoreBindPose();

            // Keep clavicles in natural anatomical bind pose so shoulders never pinch or flare sideways
            if (_hasCapturedClavicleBind)
            {
                if (_leftClavicle != null)
                {
                    _leftClavicle.localPosition = _bindClavicleLPos;
                    _leftClavicle.localRotation = _bindClavicleLRot;
                }
                if (_rightClavicle != null)
                {
                    _rightClavicle.localPosition = _bindClavicleRPos;
                    _rightClavicle.localRotation = _bindClavicleRRot;
                }
            }

            float t = Time.time + _animTimeOffset;
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            Vector3 down = -up;
            Vector3 fwd = transform.forward;

            if (currentState == State.Hit)
            {
                // =========================================================
                // HITS (Video 0:05 - 0:11)
                // =========================================================
                float hitDur = 0.62f;
                float tNorm = Mathf.Clamp01((Time.time - _hitStartTime) / hitDur);
                float impact = tNorm < 0.22f
                    ? Mathf.Sin((tNorm / 0.22f) * Mathf.PI * 0.5f)
                    : Mathf.Cos(((tNorm - 0.22f) / 0.78f) * Mathf.PI * 0.5f);
                float wobble = Mathf.Sin(tNorm * Mathf.PI * 3f) * (1f - tNorm);

                if (_hitVariant % 2 == 0)
                {
                    // Hit 1 (0:05): Upper body & head snap back, both forearms raise in front of chest/face
                    OffsetHipsCharSpace(new Vector3(wobble * 0.04f, -0.08f - 0.04f * impact, -0.10f * impact));
                    RotateBoneCharSpace(_hipsBone, -8f * impact, wobble * 8f, 0f);
                    RotateBoneCharSpace(_spineBone, Mathf.Lerp(16f, -18f, impact), -12f * impact, 5f * impact);
                    RotateBoneCharSpace(_chestBone, Mathf.Lerp(14f, -22f, impact), wobble * 10f, 6f * impact);
                    RotateBoneCharSpace(_neckBone, Mathf.Lerp(12f, -16f, impact), 0f, 0f);
                    RotateBoneCharSpace(_headBone, Mathf.Lerp(10f, -28f, impact), wobble * 15f, -14f * impact);

                    PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                        Mathf.Lerp(-16f, -58f, impact), 0f, Mathf.Lerp(-5f, -22f, impact), Mathf.Lerp(28f, 82f, impact));
                    PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                        Mathf.Lerp(-18f, -64f, impact), 0f, Mathf.Lerp(5f, 22f, impact), Mathf.Lerp(32f, 85f, impact));
                }
                else
                {
                    // Hit 2 (0:08 - 0:09): Heavy torso twist & backward arch, arms flung outward
                    OffsetHipsCharSpace(new Vector3(0.05f * impact, -0.09f - 0.05f * impact, -0.12f * impact));
                    RotateBoneCharSpace(_hipsBone, -10f * impact, 14f * impact, -5f * impact);
                    RotateBoneCharSpace(_spineBone, Mathf.Lerp(16f, -20f, impact), 22f * impact, -12f * impact);
                    RotateBoneCharSpace(_chestBone, Mathf.Lerp(14f, -24f, impact), 28f * impact, -14f * impact);
                    RotateBoneCharSpace(_neckBone, Mathf.Lerp(12f, -14f, impact), 10f * impact, 0f);
                    RotateBoneCharSpace(_headBone, Mathf.Lerp(10f, -24f, impact), 20f * impact, 16f * impact);

                    PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                        Mathf.Lerp(-16f, 12f, impact), 0f, Mathf.Lerp(-5f, -42f, impact), Mathf.Lerp(28f, 35f, impact));
                    PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                        Mathf.Lerp(-18f, -28f, impact), 0f, Mathf.Lerp(5f, 45f, impact), Mathf.Lerp(32f, 40f, impact));
                }

                PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, down, right, fwd, -1f,
                    -18f * impact - 6f, -6f, -4f, 24f + 18f * impact, leftFootPitchOffset);
                PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, down, right, fwd, 1f,
                    12f * impact - 14f, 8f, 5f, 26f + 14f * impact, rightFootPitchOffset);
                return;
            }

            if (currentState == State.Attacking && _isAttacking)
            {
                // =========================================================
                // ATTACKS (Video 0:12 - 0:17)
                // =========================================================
                float atkDur = Mathf.Max(0.55f, attackCooldown * 0.9f);
                float tNorm = Mathf.Clamp01((Time.time - _lastAttackTime) / atkDur);
                float windup = Mathf.Sin(Mathf.Clamp01(tNorm / 0.36f) * Mathf.PI * 0.5f);
                float strike = tNorm < 0.36f ? 0f : Mathf.Sin(Mathf.Clamp01((tNorm - 0.36f) / 0.34f) * Mathf.PI);
                float recover = tNorm < 0.65f ? 1f : 1f - (tNorm - 0.65f) / 0.35f;

                int v = _attackVariant % 3;
                if (v == 0)
                {
                    // Attack 1 (0:12): Wide Right-Arm Horizontal Claw Slash across body
                    float yaw = Mathf.Lerp(30f * windup, -44f, strike) * recover;
                    float spPitch = Mathf.Lerp(10f, 28f, strike) * recover + 16f * (1f - recover);

                    OffsetHipsCharSpace(new Vector3(0f, -0.08f - 0.04f * strike, 0.08f * strike));
                    RotateBoneCharSpace(_hipsBone, 0f, yaw * 0.25f, 0f);
                    RotateBoneCharSpace(_spineBone, spPitch * 0.5f, yaw * 0.4f, -6f * strike);
                    RotateBoneCharSpace(_chestBone, spPitch * 0.5f, yaw * 0.6f, -8f * strike);
                    RotateBoneCharSpace(_neckBone, 10f, -yaw * 0.3f, 0f);
                    RotateBoneCharSpace(_headBone, 8f - 12f * strike, -yaw * 0.4f, -10f);

                    PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                        Mathf.Lerp(-100f * windup, -78f, strike) * recover - 18f * (1f - recover),
                        Mathf.Lerp(38f * windup, -58f, strike) * recover,
                        Mathf.Lerp(42f * windup, -12f, strike) * recover,
                        Mathf.Lerp(45f, 22f, strike));

                    PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                        -16f + 24f * strike, 0f, -18f * strike - 5f, 32f);
                }
                else if (v == 1)
                {
                    // Attack 2 (0:14): Left-Arm Lunging Hook Slash
                    float yaw = Mathf.Lerp(-30f * windup, 44f, strike) * recover;
                    float spPitch = Mathf.Lerp(10f, 30f, strike) * recover + 16f * (1f - recover);

                    OffsetHipsCharSpace(new Vector3(0f, -0.08f - 0.04f * strike, 0.08f * strike));
                    RotateBoneCharSpace(_hipsBone, 0f, yaw * 0.25f, 0f);
                    RotateBoneCharSpace(_spineBone, spPitch * 0.5f, yaw * 0.4f, 6f * strike);
                    RotateBoneCharSpace(_chestBone, spPitch * 0.5f, yaw * 0.6f, 8f * strike);
                    RotateBoneCharSpace(_neckBone, 10f, -yaw * 0.3f, 0f);
                    RotateBoneCharSpace(_headBone, 8f - 12f * strike, -yaw * 0.4f, 10f);

                    PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                        Mathf.Lerp(-100f * windup, -78f, strike) * recover - 18f * (1f - recover),
                        Mathf.Lerp(-38f * windup, 58f, strike) * recover,
                        Mathf.Lerp(-42f * windup, 12f, strike) * recover,
                        Mathf.Lerp(45f, 22f, strike));

                    PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                        -18f + 24f * strike, 0f, 18f * strike + 5f, 32f);
                }
                else
                {
                    // Attack 3 (0:16 - 0:17): Two-Handed Overhead Raise & Violent Downward Smash!
                    float torsoPitch = Mathf.Lerp(-12f * windup, 42f, strike) * recover + 16f * (1f - recover);
                    float armPitch = Mathf.Lerp(-152f * windup, -38f, strike) * recover - 18f * (1f - recover);
                    float elbow = Mathf.Lerp(26f, 38f, strike);

                    OffsetHipsCharSpace(new Vector3(0f, -0.08f - 0.06f * strike, 0.09f * strike));
                    RotateBoneCharSpace(_spineBone, torsoPitch * 0.45f, 0f, 0f);
                    RotateBoneCharSpace(_chestBone, torsoPitch * 0.55f, 0f, 0f);
                    RotateBoneCharSpace(_neckBone, 8f, 0f, 0f);
                    RotateBoneCharSpace(_headBone, torsoPitch * 0.3f, 0f, 0f);

                    PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                        armPitch, 0f, -10f * windup - 4f, elbow);
                    PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                        armPitch, 0f, 10f * windup + 4f, elbow);
                }

                PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, down, right, fwd, -1f,
                    -18f - 10f * strike, -6f, -4f, 26f + 12f * strike, leftFootPitchOffset);
                PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, down, right, fwd, 1f,
                    8f + 10f * strike, 8f, 5f, 22f, rightFootPitchOffset);
                return;
            }

            if (currentState == State.Chasing && _targetSpeed > 0.5f)
            {
                // =========================================================
                // RUNS (Video 0:24 - 0:28)
                // Forward-leaning crouched zombie run, elbows bent in front of chest pumping forward/back
                // =========================================================
                float phi = t * 9.2f;
                float s = Mathf.Sin(phi);
                float c = Mathf.Cos(phi);

                OffsetHipsCharSpace(new Vector3(s * 0.03f, -0.10f + Mathf.Abs(s) * 0.055f, 0f));
                RotateBoneCharSpace(_hipsBone, 0f, s * 9f, s * 4f);
                RotateBoneCharSpace(_spineBone, 22f + Mathf.Abs(s) * 4f, -s * 11f, -s * 5f);
                RotateBoneCharSpace(_chestBone, 18f + Mathf.Abs(c) * 4f, -s * 14f, -s * 5f);
                RotateBoneCharSpace(_neckBone, -6f, 0f, 0f);
                RotateBoneCharSpace(_headBone, -12f + c * 6f, s * 7f, s * 7f - 6f);

                // Arms tucked close to torso, elbows bent ~62 deg, pumping forward/back like Video 0:24-0:28
                PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                    -32f + s * 28f, -s * 8f, -4f, 62f - c * 14f);
                PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                    -32f - s * 28f, -s * 8f, 4f, 62f + c * 14f);

                // Crouched running leg stride
                float leftKnee = Mathf.Max(0f, c) * 64f + 20f;
                float rightKnee = Mathf.Max(0f, -c) * 64f + 20f;
                PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, down, right, fwd, -1f,
                    -42f * s - 12f, 0f, -2f, leftKnee, leftFootPitchOffset);
                PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, down, right, fwd, 1f,
                    42f * s - 12f, 0f, 2f, rightKnee, rightFootPitchOffset);
                return;
            }

            if ((currentState == State.Wandering || currentState == State.Chasing) && _targetSpeed > 0.1f)
            {
                // =========================================================
                // WALKS (Video 0:18 - 0:23)
                // Deep forward hunch, dragging/limping shamble, arms dangling straight down in front of knees
                // =========================================================
                float phi = t * 4.4f;
                float theta = phi + 0.22f * Mathf.Sin(phi); // Asymmetric zombie limp
                float s = Mathf.Sin(theta);
                float c = Mathf.Cos(theta);

                OffsetHipsCharSpace(new Vector3(s * 0.045f, -0.09f - Mathf.Abs(s) * 0.05f, 0f));
                RotateBoneCharSpace(_hipsBone, 0f, s * 7f, s * 5f);
                RotateBoneCharSpace(_spineBone, 24f + Mathf.Abs(s) * 5f, -s * 8f, -s * 8f);
                RotateBoneCharSpace(_chestBone, 20f + Mathf.Abs(c) * 4f, -s * 11f, -s * 6f);
                RotateBoneCharSpace(_neckBone, 14f, 0f, s * 5f);
                RotateBoneCharSpace(_headBone, 12f + c * 8f, -s * 7f, s * 12f - 8f);

                // Arms dangle loosely downward in front of knees (never out to the sides!)
                PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                    -16f + s * 16f, 0f, -3f, 24f + c * 8f);
                PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                    -18f - s * 16f, 0f, 3f, 28f - c * 8f);

                // Asymmetric shambling legs
                float leftKnee = Mathf.Max(0f, c) * 42f + 16f;
                float rightKnee = Mathf.Max(0f, -c) * 48f + 22f;
                PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, down, right, fwd, -1f,
                    -26f * s - 8f, -4f, -3f, leftKnee, leftFootPitchOffset);
                PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, down, right, fwd, 1f,
                    22f * s - 6f, 5f, 3f, rightKnee, rightFootPitchOffset);
                return;
            }

            // =========================================================
            // IDLES (Video 0:00 - 0:04)
            // Hunched asymmetrical posture, bent knees, cocked drooping head, arms hanging loosely in front
            // =========================================================
            float s1 = Mathf.Sin(t * 1.7f);
            float c1 = Mathf.Cos(t * 1.7f);
            float s2 = Mathf.Sin(t * 1.1f);

            OffsetHipsCharSpace(new Vector3(s1 * 0.025f, -0.07f - Mathf.Abs(s1) * 0.015f, 0f));
            RotateBoneCharSpace(_hipsBone, 0f, s2 * 4f, s1 * 3f);
            RotateBoneCharSpace(_spineBone, 17f + s1 * 2f, s1 * 3f, -6f + s1 * 2.5f);
            RotateBoneCharSpace(_chestBone, 15f + c1 * 2.5f, s2 * 5f, -5f + s1 * 2.5f);
            RotateBoneCharSpace(_neckBone, 13f, 0f, -5f);
            RotateBoneCharSpace(_headBone, 12f + c1 * 5f, s2 * 9f, -15f + s1 * 5f);

            PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, down, right, fwd, -1f,
                -14f + s1 * 4f, 0f, -3f, 26f + s1 * 3f);
            PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, down, right, fwd, 1f,
                -18f - s1 * 4f, 0f, 4f, 32f - c1 * 3f);

            PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, down, right, fwd, -1f,
                -6f - s1 * 1.5f, -6f, -4f, 20f + s1 * 2f, leftFootPitchOffset);
            PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, down, right, fwd, 1f,
                -16f + s1 * 1.5f, 8f, 6f, 28f - s1 * 2f, rightFootPitchOffset);
        }

        /// <summary>
        /// Evaluates the 2 Death animations from Screen Recording 2026-11-12 092929.mp4 (0:29 - 0:32):
        ///  - Variant 0 (0:29): Backward knockdown flat onto back, arms & legs thrown upward then settling on ground.
        ///  - Variant 1 (0:30 - 0:32): Staggers, drops onto both knees, then collapses face-first onto the ground.
        /// </summary>
        private void ApplyVideoDeathAnimation()
        {
            if (_deathVariant % 2 == 0)
            {
                ApplyProceduralDeathPose();
                return;
            }

            // Variant 1 (0:30 - 0:32): Stagger -> Drop to Knees -> Face-Plant Collapse!
            RestoreBindPose();

            float dt = Mathf.Max(0f, Time.time - _deathStartTime);
            float tNorm = Mathf.Clamp01(dt / 1.25f);
            float kneePhase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tNorm / 0.48f));
            float facePlant = tNorm < 0.48f ? 0f : Mathf.SmoothStep(0f, 1f, (tNorm - 0.48f) / 0.52f);

            float hipsHeight = Mathf.Lerp(0.92f, 0.46f, kneePhase);
            hipsHeight = Mathf.Lerp(hipsHeight, 0.16f, facePlant);
            float hipsFwdOffset = Mathf.Lerp(0f, 0.42f, facePlant);
            float hipsPitch = 84f * facePlant;

            Vector3 targetHipsWorldPos = transform.position + transform.forward * hipsFwdOffset;
            float groundUnderHips = GetGroundHeight(targetHipsWorldPos);
            if (groundUnderHips < -900f) groundUnderHips = transform.position.y;
            targetHipsWorldPos.y = groundUnderHips + hipsHeight;

            _hipsBone.position = targetHipsWorldPos;
            RotateBoneCharSpace(_hipsBone, hipsPitch, 0f, 0f);

            float spinePitch = Mathf.Lerp(18f * kneePhase, 8f, facePlant);
            RotateBoneCharSpace(_spineBone, spinePitch, 0f, 6f * kneePhase * (1f - facePlant));
            RotateBoneCharSpace(_chestBone, spinePitch, 0f, 0f);
            RotateBoneCharSpace(_neckBone, Mathf.Lerp(14f * kneePhase, -12f, facePlant), 0f, 0f);
            RotateBoneCharSpace(_headBone, Mathf.Lerp(18f * kneePhase, -20f, facePlant), 24f * facePlant, 0f);

            Quaternion bodyRot = Quaternion.AngleAxis(hipsPitch, transform.right);
            Vector3 bodyDown = bodyRot * (-transform.up);
            Vector3 bodyRight = transform.right;
            Vector3 bodyFwd = bodyRot * transform.forward;

            PoseArm(_leftUpperArm, _leftLowerArm, _leftHand, bodyDown, bodyRight, bodyFwd, -1f,
                Mathf.Lerp(-24f, -45f, kneePhase * (1f - facePlant)), 0f, -32f * facePlant - 5f, Mathf.Lerp(32f, 55f, facePlant));
            PoseArm(_rightUpperArm, _rightLowerArm, _rightHand, bodyDown, bodyRight, bodyFwd, 1f,
                Mathf.Lerp(-24f, -45f, kneePhase * (1f - facePlant)), 0f, 32f * facePlant + 5f, Mathf.Lerp(32f, 55f, facePlant));

            float kneeBend = Mathf.Lerp(24f, 106f, kneePhase) * (1f - facePlant * 0.65f);
            float thighPitch = Mathf.Lerp(-12f, -18f, kneePhase) * (1f - facePlant);
            PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, bodyDown, bodyRight, bodyFwd, -1f,
                thighPitch, -6f, -5f, kneeBend, leftFootPitchOffset);
            PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, bodyDown, bodyRight, bodyFwd, 1f,
                thighPitch, 6f, 5f, kneeBend, rightFootPitchOffset);
        }

        private void OffsetHipsCharSpace(Vector3 charOffset)
        {
            if (_hipsBone == null) return;
            _hipsBone.position += transform.right * charOffset.x
                                + transform.up * charOffset.y
                                + transform.forward * charOffset.z;
        }

        private void PoseArm(Transform upperArm, Transform lowerArm, Transform hand,
                             Vector3 refDown, Vector3 refRight, Vector3 refFwd,
                             float sideSign, float upperPitch, float upperYaw, float upperRoll, float elbowBend)
        {
            if (upperArm == null || lowerArm == null) return;

            // 1. Align upperArm from bind A/T-pose straight down along refDown
            Vector3 curUpper = (lowerArm.position - upperArm.position).normalized;
            Vector3 baseDown = (refDown + refRight * (sideSign * 0.09f)).normalized;
            if (curUpper.sqrMagnitude > 0.0001f)
            {
                upperArm.rotation = Quaternion.FromToRotation(curUpper, baseDown) * upperArm.rotation;
            }

            // 2. Rotate upperArm in character space
            Quaternion upperRot = Quaternion.AngleAxis(upperYaw, -refDown)
                                * Quaternion.AngleAxis(upperRoll, refFwd)
                                * Quaternion.AngleAxis(upperPitch, refRight);
            upperArm.rotation = upperRot * upperArm.rotation;

            // 3. Align lowerArm with upperArm and bend elbow forward/up
            if (hand != null)
            {
                Vector3 newUpperDir = (lowerArm.position - upperArm.position).normalized;
                Vector3 curLower = (hand.position - lowerArm.position).normalized;
                if (curLower.sqrMagnitude > 0.0001f && newUpperDir.sqrMagnitude > 0.0001f)
                {
                    lowerArm.rotation = Quaternion.FromToRotation(curLower, newUpperDir) * lowerArm.rotation;
                }

                Vector3 elbowAxis = Quaternion.AngleAxis(upperYaw, -refDown) * refRight;
                lowerArm.rotation = Quaternion.AngleAxis(-Mathf.Abs(elbowBend), elbowAxis) * lowerArm.rotation;
            }
        }

        private void PoseLeg(Transform upperLeg, Transform lowerLeg, Transform foot,
                             Vector3 refDown, Vector3 refRight, Vector3 refFwd,
                             float sideSign, float thighPitch, float thighYaw, float thighRoll, float kneeBend, float footPitch)
        {
            if (upperLeg == null || lowerLeg == null) return;

            Vector3 curThigh = (lowerLeg.position - upperLeg.position).normalized;
            Vector3 baseDown = (refDown + refRight * (sideSign * 0.06f)).normalized;
            if (curThigh.sqrMagnitude > 0.0001f)
            {
                upperLeg.rotation = Quaternion.FromToRotation(curThigh, baseDown) * upperLeg.rotation;
            }

            Quaternion thighRot = Quaternion.AngleAxis(thighYaw, -refDown)
                                * Quaternion.AngleAxis(thighRoll, refFwd)
                                * Quaternion.AngleAxis(thighPitch, refRight);
            upperLeg.rotation = thighRot * upperLeg.rotation;

            if (foot != null)
            {
                Vector3 newThighDir = (lowerLeg.position - upperLeg.position).normalized;
                Vector3 curCalf = (foot.position - lowerLeg.position).normalized;
                if (curCalf.sqrMagnitude > 0.0001f && newThighDir.sqrMagnitude > 0.0001f)
                {
                    lowerLeg.rotation = Quaternion.FromToRotation(curCalf, newThighDir) * lowerLeg.rotation;
                }

                Vector3 kneeAxis = Quaternion.AngleAxis(thighYaw, -refDown) * refRight;
                lowerLeg.rotation = Quaternion.AngleAxis(Mathf.Abs(kneeBend), kneeAxis) * lowerLeg.rotation;

                if (Mathf.Abs(footPitch) > 0.01f)
                {
                    foot.rotation = Quaternion.AngleAxis(footPitch, kneeAxis) * foot.rotation;
                }
            }
        }

        private void ApplyRealisticHeadTracking()
        {
            if (_headBone == null || targetPlayer == null) return;

            Vector3 targetLookPos = targetPlayer.position + Vector3.up * 1.4f; // Player eye / chest level
            Vector3 dir = (targetLookPos - _headBone.position).normalized;

            // Only track when player is within natural forward view cone (up to 75 degrees)
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle < 75f)
            {
                Quaternion targetLookRot = Quaternion.LookRotation(dir, Vector3.up);
                _headBone.rotation = Quaternion.Slerp(_headBone.rotation, targetLookRot, Time.deltaTime * 5.5f);
            }
        }

        /// <summary>
        /// Full-Body 4-Panel Storyboard Backward Death Collapse:
        /// Panel 1 (0.00s - 0.22s): THE HIT - Upright chest shot, torso & head arch sharply back, arms flung wide open
        /// Panel 2 (0.22s - 0.72s): THE FALL - Knees buckle forward, hips drop and shift back, body pitches backward
        /// Panel 3 (0.72s - 1.15s): THE IMPACT - Back & hips slam flat onto mud (0.90s), head whiplash bounce, legs splay in V
        /// Panel 4 (1.15s+): TARGET ELIMINATED - Motionless flat repose on back on top of ground, head lolled to right
        /// </summary>
        private void ApplyProceduralDeathPose()
        {
            // Start from clean bind pose so no Animator clip can pull pelvis to Y=0 or freeze A-pose
            RestoreBindPose();

            // Bring arms down from 45-deg bind A-pose to natural sides before applying death angles
            RotateBoneCharSpace(_leftUpperArm, 0f, 0f, 38f);
            RotateBoneCharSpace(_rightUpperArm, 0f, 0f, -38f);

            float dt = Mathf.Max(0f, Time.time - _deathStartTime);

            float hipsHeight = 0.95f;
            float hipsBackOffset = 0.0f;
            float hipsPitch = 0f;       // negative pitches entire body backward onto its back (-88 deg = flat on back)
            float spinePitch = 0f;
            float chestPitch = 0f;
            float neckPitch = 0f;
            float headPitch = 0f;
            float headYaw = 0f;
            float headRoll = 0f;

            float thighPitch = 0f;
            float calfPitch = 0f;
            float legSplay = 0f;

            float armPitch = 0f;
            float armOutward = 0f;
            float forearmPitch = 0f;

            if (dt < 0.22f)
            {
                // PANEL 1: THE HIT (0.00s -> 0.22s)
                float u = Mathf.SmoothStep(0f, 1f, dt / 0.22f);
                hipsHeight = Mathf.Lerp(0.95f, 0.91f, u);
                hipsBackOffset = Mathf.Lerp(0f, 0.08f, u);
                hipsPitch = Mathf.Lerp(0f, -14f, u);

                spinePitch = Mathf.Lerp(10f, -22f, u);
                chestPitch = Mathf.Lerp(8f, -26f, u);
                neckPitch = Mathf.Lerp(0f, -18f, u);
                headPitch = Mathf.Lerp(-10f, -35f, u);

                thighPitch = Mathf.Lerp(0f, 14f, u);
                calfPitch = Mathf.Lerp(5f, 22f, u);
                legSplay = Mathf.Lerp(0f, 6f, u);

                armPitch = Mathf.Lerp(0f, 6f, u);
                armOutward = Mathf.Lerp(0f, 6f, u);
                forearmPitch = Mathf.Lerp(-12f, -6f, u);
            }
            else if (dt < 0.72f)
            {
                // PANEL 2: THE FALL (0.22s -> 0.72s) - Knees buckle, pelvis drops & slides backward
                float u = Mathf.SmoothStep(0f, 1f, (dt - 0.22f) / 0.50f);
                hipsHeight = Mathf.Lerp(0.91f, 0.34f, u);
                hipsBackOffset = Mathf.Lerp(0.08f, 0.55f, u);
                hipsPitch = Mathf.Lerp(-14f, -68f, u);

                spinePitch = Mathf.Lerp(-22f, -18f, u);
                chestPitch = Mathf.Lerp(-26f, -18f, u);
                neckPitch = Mathf.Lerp(-18f, -12f, u);
                headPitch = Mathf.Lerp(-35f, -24f, u);

                // Knees buckle deeply as hips drop
                thighPitch = Mathf.Lerp(14f, -38f, u);
                calfPitch = Mathf.Lerp(22f, 72f, u);
                legSplay = Mathf.Lerp(6f, 14f, u);

                armPitch = Mathf.Lerp(6f, 2f, u);
                armOutward = Mathf.Lerp(6f, 12f, u);
                forearmPitch = Mathf.Lerp(-6f, -2f, u);
            }
            else if (dt < 1.15f)
            {
                // PANEL 3: THE IMPACT (0.72s -> 1.15s) - Upper back & hips slam flat onto ground at 0.90s
                float u = Mathf.Clamp01((dt - 0.72f) / 0.18f); // reaches ground impact at 0.90s
                float bounce = 0f;
                if (dt > 0.90f)
                {
                    float bu = (dt - 0.90f) / 0.25f;
                    bounce = Mathf.Sin(bu * Mathf.PI) * 0.04f;
                }

                hipsHeight = Mathf.Lerp(0.34f, 0.14f, u) + bounce;
                hipsBackOffset = Mathf.Lerp(0.55f, 0.78f, u);
                hipsPitch = Mathf.Lerp(-68f, -88f, u);

                // Spine flattens against the ground on impact
                spinePitch = Mathf.Lerp(-18f, 4f, u);
                chestPitch = Mathf.Lerp(-18f, 6f, u);
                neckPitch = Mathf.Lerp(-12f, 4f, u);

                // Whiplash head bounce on impact
                if (dt < 0.90f)
                {
                    headPitch = Mathf.Lerp(-24f, -10f, u);
                }
                else
                {
                    float hu = (dt - 0.90f) / 0.25f;
                    headPitch = Mathf.Lerp(-10f, 8f, hu) + Mathf.Sin(hu * Mathf.PI) * 22f;
                }

                // Legs straighten out flat on the ground and splay in a wide V
                float lu = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dt - 0.72f) / 0.35f));
                thighPitch = Mathf.Lerp(-38f, -4f, lu);
                calfPitch = Mathf.Lerp(72f, 6f, lu);
                legSplay = Mathf.Lerp(14f, 22f, lu);

                armPitch = Mathf.Lerp(2f, 0f, lu);
                armOutward = Mathf.Lerp(12f, 15f, lu);
                forearmPitch = Mathf.Lerp(-2f, 0f, lu);
            }
            else
            {
                // PANEL 4: TARGET ELIMINATED (1.15s+) - Complete flat repose on ground, head lolls to right
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dt - 1.15f) / 0.30f));
                hipsHeight = 0.14f;
                hipsBackOffset = 0.78f;
                hipsPitch = -88f;

                spinePitch = 4f;
                chestPitch = 6f;
                neckPitch = 4f;
                headPitch = 8f;
                headYaw = Mathf.Lerp(0f, 42f, settle);
                headRoll = Mathf.Lerp(0f, -14f, settle);

                thighPitch = -4f;
                calfPitch = 6f;
                legSplay = 22f;

                armPitch = 0f;
                armOutward = 15f;
                forearmPitch = 0f;
            }

            // Position & pitch pelvis in world space relative to grounded root
            Vector3 targetHipsWorldPos = transform.position - transform.forward * hipsBackOffset;
            float groundUnderHips = GetGroundHeight(targetHipsWorldPos);
            if (groundUnderHips < -900f) groundUnderHips = transform.position.y;
            targetHipsWorldPos.y = groundUnderHips + hipsHeight;

            _hipsBone.position = targetHipsWorldPos;
            RotateBoneCharSpace(_hipsBone, hipsPitch, 0f, 0f);

            // Apply torso & head rotations
            RotateBoneCharSpace(_spineBone, spinePitch, 0f, 0f);
            RotateBoneCharSpace(_chestBone, chestPitch, 0f, 0f);
            RotateBoneCharSpace(_neckBone, neckPitch, 0f, 0f);
            RotateBoneCharSpace(_headBone, headPitch, headYaw, headRoll);

            // Apply leg rotations (wide V splay when flat)
            RotateBoneCharSpace(_leftUpperLeg, thighPitch, -legSplay, -legSplay * 0.5f);
            RotateBoneCharSpace(_rightUpperLeg, thighPitch, legSplay, legSplay * 0.5f);
            RotateBoneCharSpace(_leftLowerLeg, calfPitch, 0f, 0f);
            RotateBoneCharSpace(_rightLowerLeg, calfPitch, 0f, 0f);

            // Apply arm rotations (flung open on hit, sprawling on ground at rest)
            RotateBoneCharSpace(_leftUpperArm, armPitch, 0f, -armOutward);
            RotateBoneCharSpace(_rightUpperArm, armPitch, 0f, armOutward);
            RotateBoneCharSpace(_leftLowerArm, forearmPitch, 0f, 0f);
            RotateBoneCharSpace(_rightLowerArm, forearmPitch, 0f, 0f);

            // Sloped-terrain extremity guard: ensure head, chest, hips, and feet never clip below ground surface
            float liftNeeded = 0f;
            Transform[] checkBones = new Transform[] { _hipsBone, _chestBone, _headBone, _leftFoot, _rightFoot };
            for (int i = 0; i < checkBones.Length; i++)
            {
                Transform b = checkBones[i];
                if (b == null) continue;
                float bg = GetGroundHeight(b.position);
                if (bg > -900f)
                {
                    float minAllowedY = bg + 0.11f;
                    if (b.position.y < minAllowedY)
                    {
                        float diff = minAllowedY - b.position.y;
                        if (diff > liftNeeded) liftNeeded = diff;
                    }
                }
            }
            if (liftNeeded > 0f)
            {
                _hipsBone.position += Vector3.up * liftNeeded;
            }
        }

        private void HandleWanderState(float distToPlayer)
        {
            // Detect player with dynamic grass stealth (hiding in tall grass reduces detection by up to 85%!)
            var crawl = _playerCrawl != null ? _playerCrawl : PlayerCrawl.Instance;
            float stealth = crawl != null ? crawl.stealthFactor : 1.0f;
            float effectiveDetect = detectionRange * stealth;

            if (distToPlayer <= effectiveDetect)
            {
                if (distToPlayer <= 10f)
                {
                    currentState = State.Chasing;
                    currentRunStyle = (Random.value > 0.45f) ? RunStyle.ZombieRun : RunStyle.MutantRun;
                    _targetSpeed = 1.0f;
                }
                else
                {
                    TriggerScream();
                }
                return;
            }

            // Wander around spawn position (only picking dry targets outside of water)
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0f)
            {
                PickDryWanderTarget();
                _wanderTimer = Random.Range(5f, 10f);
            }

            float distToWander = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                 new Vector3(_wanderTarget.x, 0, _wanderTarget.z));

            if (distToWander > 1.5f)
            {
                Vector3 moveDir = (_wanderTarget - transform.position);
                moveDir.y = 0;
                moveDir.Normalize();

                if (TryResolveDryMoveDirection(moveDir, walkSpeed, false, out Vector3 safeMoveDir))
                {
                    _isStoppedAtWater = false;
                    Quaternion targetRot = Quaternion.LookRotation(safeMoveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * 0.5f);

                    Vector3 move = safeMoveDir * (walkSpeed * Time.deltaTime);
                    move.y = _verticalVelocity * Time.deltaTime;
                    _controller.Move(move);

                    _targetSpeed = 0.35f; // Pure 100% walk animation threshold in LocomotionBlend (avoids phase-mismatched run blend)
                }
                else
                {
                    // Water barrier ahead: stop moving and pick a new dry wander target away from the shoreline
                    _isStoppedAtWater = true;
                    PickDryWanderTarget();
                    Vector3 move = Vector3.up * (_verticalVelocity * Time.deltaTime);
                    _controller.Move(move);
                    _targetSpeed = 0.0f;
                }
            }
            else
            {
                Vector3 move = Vector3.up * (_verticalVelocity * Time.deltaTime);
                _controller.Move(move);
                _targetSpeed = 0.0f;
            }
        }

        public void TriggerScream()
        {
            currentState = State.Screaming;
            _screamEndTime = Time.time + 0.65f;
            PlaySound(screamClip != null ? screamClip : growlClip, 1.0f);
            _targetSpeed = 0.0f;
        }

        private void HandleScreamState(float distToPlayer)
        {
            if (targetPlayer != null)
            {
                Vector3 lookDir = (targetPlayer.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * 1.5f);
                }
            }

            Vector3 move = Vector3.up * (_verticalVelocity * Time.deltaTime);
            _controller.Move(move);

            // Scream finishes -> unleash aggressive charge in chosen run style!
            if (Time.time >= _screamEndTime)
            {
                currentState = State.Chasing;
                currentRunStyle = (Random.value > 0.45f) ? RunStyle.ZombieRun : RunStyle.MutantRun;
                _targetSpeed = 1.0f;
            }
        }

        private void HandleChaseState(float distToPlayer)
        {
            var crawl = _playerCrawl != null ? _playerCrawl : PlayerCrawl.Instance;
            float effectiveLoseRange = loseTargetRange;
            if (crawl != null && crawl.isCrawling)
            {
                effectiveLoseRange = crawl.isInGrass ? loseTargetRange * 0.35f : loseTargetRange * 0.6f;
            }

            if (distToPlayer > effectiveLoseRange)
            {
                currentState = State.Wandering;
                _targetSpeed = 0.0f;
                return;
            }

            if (distToPlayer <= attackRange)
            {
                currentState = State.Attacking;
                _attackCoroutine = StartCoroutine(AttackRoutine());
                return;
            }

            // Move toward player
            Vector3 targetDir = (targetPlayer.position - transform.position);
            targetDir.y = 0;
            targetDir.Normalize();

            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            // Simple raycast obstacle avoidance for walls / trees / bridge railings (ignoring other zombies, player & triggers)
            Vector3 steerDir = targetDir;
            Ray rayLeft = new Ray(transform.position + Vector3.up * 0.6f - transform.right * 0.35f, transform.forward);
            Ray rayRight = new Ray(transform.position + Vector3.up * 0.6f + transform.right * 0.35f, transform.forward);
            int obstacleMask = ~((1 << 2) | (1 << 4) | (1 << 8)); // Ignore Raycast, Water, Player

            if (Physics.Raycast(rayLeft, out RaycastHit hitL, 1.2f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hitL.collider != null && !hitL.collider.isTrigger && hitL.collider.GetComponentInParent<ZombieAI>() == null && hitL.collider.transform.root != transform.root)
                {
                    steerDir += transform.right * 0.6f;
                }
            }
            else if (Physics.Raycast(rayRight, out RaycastHit hitR, 1.2f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hitR.collider != null && !hitR.collider.isTrigger && hitR.collider.GetComponentInParent<ZombieAI>() == null && hitR.collider.transform.root != transform.root)
                {
                    steerDir -= transform.right * 0.6f;
                }
            }
            steerDir.Normalize();

            // Water Barrier Check: zombies immediately stop running if they step on or reach water
            // (Radio ambush monsters can charge across the shallow path between the Hedge Maze & Radio house when the player is on land)
            bool allowAmbushCrossing = isRadioAmbushMonster && targetPlayer != null && !IsPositionInWater(targetPlayer.position);
            if (allowAmbushCrossing || (!IsPositionInWater(transform.position) && IsStepDirectionDry(steerDir, chaseSpeed)))
            {
                _isStoppedAtWater = false;
                if (steerDir.sqrMagnitude > 0.001f)
                {
                    Quaternion moveRot = Quaternion.LookRotation(steerDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, moveRot, Time.deltaTime * rotationSpeed);
                }

                Vector3 move = steerDir * (chaseSpeed * Time.deltaTime);
                move.y = _verticalVelocity * Time.deltaTime;
                _controller.Move(move);

                _targetSpeed = 1.0f;
            }
            else if (TryResolveDryMoveDirection(steerDir, chaseSpeed, true, out Vector3 tangentDir))
            {
                _isStoppedAtWater = false;
                if (tangentDir.sqrMagnitude > 0.001f)
                {
                    Quaternion moveRot = Quaternion.LookRotation(tangentDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, moveRot, Time.deltaTime * rotationSpeed);
                }

                Vector3 move = tangentDir * (chaseSpeed * Time.deltaTime);
                move.y = _verticalVelocity * Time.deltaTime;
                _controller.Move(move);

                _targetSpeed = 1.0f;
            }
            else
            {
                // Stopped by water barrier: immediately stop running and stand at the shoreline
                _isStoppedAtWater = true;
                Vector3 move = Vector3.up * (_verticalVelocity * Time.deltaTime);
                _controller.Move(move);

                _targetSpeed = 0.0f;
            }
        }

        private void HandleAttackingState(float distToPlayer)
        {
            _targetSpeed = 0.0f;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }

            // While an attack swing is executing, stay locked in attack state
            if (_isAttacking)
            {
                float atkTime = Time.time - _lastAttackTime;

                // Softly track player during initial windup (first 0.20s)
                if (atkTime < 0.20f && targetPlayer != null)
                {
                    Vector3 lookDir = (targetPlayer.position - transform.position);
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
                    }
                }

                // Realistic forward lunge momentum during active claw strike (0.08s to 0.32s)
                Vector3 move = Vector3.up * (_verticalVelocity * Time.deltaTime);
                if (atkTime >= 0.08f && atkTime <= 0.32f && distToPlayer > 1.05f)
                {
                    move += transform.forward * (3.4f * Time.deltaTime);
                }
                _controller.Move(move);
                return;
            }

            // Attack has concluded: check cooldown and either attack again or resume chase
            if (Time.time >= _lastAttackTime + attackCooldown)
            {
                if (distToPlayer <= attackRange * 1.15f)
                {
                    _attackCoroutine = StartCoroutine(AttackRoutine());
                }
                else
                {
                    currentState = State.Chasing;
                    _targetSpeed = 1.0f;
                }
            }
            else
            {
                // In cooldown between strikes: keep smooth facing
                if (targetPlayer != null)
                {
                    Vector3 lookDir = (targetPlayer.position - transform.position);
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 4f);
                    }
                }

                Vector3 move = Vector3.up * (_verticalVelocity * Time.deltaTime);
                _controller.Move(move);
            }
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            _lastAttackTime = Time.time;
            _attackVariant = (_attackVariant + 1) % 3;
            currentAttackStyle = (Random.value > 0.5f) ? AttackStyle.MutantSwiping : AttackStyle.ZombieAttack;

            // Clean trigger: reset first so triggers never stack or repeat prematurely
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.ResetTrigger(AttackHash);
                _animator.ResetTrigger(AttackHeavyHash);
                if (Random.value > 0.45f)
                {
                    _animator.SetTrigger(AttackHash);
                }
                else
                {
                    _animator.SetTrigger(AttackHeavyHash);
                }
                _targetSpeed = 0f;
                _currentSpeedParam = 0f;
                _animator.SetFloat(SpeedHash, 0f);
            }

            // Play visceral attack snarling/claw sound
            PlaySound(attackClip, 1.0f);

            // Wait for claw impact frame in animation
            yield return new WaitForSeconds(attackDamageDelay);

            // Check if player is still in front and within attack range
            if (!isDead && targetPlayer != null)
            {
                float currentDist = Vector3.Distance(transform.position, targetPlayer.position);
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toPlayer);

                if (currentDist <= attackRange * 1.35f && dot > 0.25f)
                {
                    // Deal damage to player
                    var playerHealth = targetPlayer.GetComponent<PlayerHealth>();
                    if (playerHealth == null) playerHealth = targetPlayer.GetComponentInParent<PlayerHealth>();
                    if (playerHealth == null) playerHealth = targetPlayer.GetComponentInChildren<PlayerHealth>();

                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(attackDamage, transform.position);
                    }
                    else
                    {
                        Debug.Log($"[ZombieAI] Zombie clawed player for {attackDamage} damage!");
                    }
                }
            }

            // Wait for attack animation to finish playing smoothly
            float remaining = Mathf.Max(0.2f, attackCooldown - attackDamageDelay);
            yield return new WaitForSeconds(remaining);

            _isAttacking = false;
            _attackCoroutine = null;
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (isDead) return;

            _lastHitPoint = hitPoint;
            _lastHitDamage = damage;

            currentHealth -= damage;
            PlaySound(hitClip, 0.9f);

            // Blood / hit visual effect
            CreateHitSpark(hitPoint);

            if (currentHealth <= 0f)
            {
                Die(hitDirection);
            }
            else
            {
                _hitStartTime = Time.time;
                _hitVariant = (_hitVariant + 1) % 2;
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                    _animator.ResetTrigger(HitHash);
                    _animator.SetTrigger(HitHash);
                }
                StartCoroutine(HitStunRoutine(0.58f));
            }
        }

        private IEnumerator HitStunRoutine(float duration)
        {
            currentState = State.Hit;
            _isAttacking = false;
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }

            yield return new WaitForSeconds(duration);

            if (!isDead)
            {
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                }
                // Always chase player after being hit
                currentState = State.Chasing;
            }
        }

        public void Die(Vector3 hitDirection)
        {
            if (isDead) return;

            isDead = true;
            currentState = State.Dead;
            _isAttacking = false;
            _deathStartTime = Time.time;
            _deathVariant = Random.Range(0, 2);
            _deathGroundY = GetGroundHeight(transform.position);
            if (_deathGroundY > -900f)
            {
                transform.position = new Vector3(transform.position.x, _deathGroundY, transform.position.z);
            }

            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }

            // 1. Disable CharacterController so the dead corpse never blocks player movement or gunfire
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            // 2. Disable NavMeshAgent if present
            var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.enabled = false;
            }

            // 3. Trigger Animator Die state
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                if (_animator.layerCount > 1)
                {
                    _animator.SetLayerWeight(1, 0f);
                }
                _animator.SetBool(IsDeadHash, true);
                _animator.ResetTrigger(Animator.StringToHash("Die"));
                _animator.SetTrigger(Animator.StringToHash("Die"));
            }

            // 4. Start the 4-phase cinematic death sequence (The Hit, The Fall, The Impact, Target Eliminated)
            StartCoroutine(CinematicDeathSequence(hitDirection));

            // Clean up corpse after 45 seconds
            Destroy(gameObject, 45f);
        }

        [Header("Death Animation")]
        [Tooltip("Blend the death animation into a physics ragdoll (requires a Humanoid avatar)")]
        public bool useRagdollDeath = false;
        [Tooltip("Seconds the death flinch animation plays before physics takes over")]
        public float ragdollDelay = 0.25f;
        [Tooltip("How hard (m/s) the ragdoll is shoved along the hit direction")]
        public float ragdollPushSpeed = 3.5f;
        [Tooltip("Seconds after the ragdoll activates before its physics is removed to freeze the corpse")]
        public float ragdollSettleTime = 5f;
        [Tooltip("Seconds it takes the zombie to topple onto the ground (fallback when ragdoll is unavailable)")]
        public float deathFallDuration = 1.0f;

        private Vector3 _lastHitPoint;
        private float _lastHitDamage = 35f;

        private PlayerCrawl _playerCrawl;

        private void LocatePlayer()
        {
            var pGo = GameObject.FindWithTag("Player");
            if (pGo == null) pGo = GameObject.Find("PlayerCapsule");
            if (pGo != null)
            {
                targetPlayer = pGo.transform;
                _playerCrawl = pGo.GetComponent<PlayerCrawl>();
            }
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.85f, 1.15f);
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private static GameObject[] _zombieBloodVfx;

        private static GameObject GetRandomBloodPrefab()
        {
            if (_zombieBloodVfx == null || _zombieBloodVfx.Length == 0)
            {
                var all = Resources.LoadAll<GameObject>("BloodVFX");
                if (all != null && all.Length > 0)
                {
                    var list = new System.Collections.Generic.List<GameObject>();
                    foreach (var go in all)
                    {
                        if (go != null && !go.name.Contains("Bunch"))
                        {
                            list.Add(go);
                        }
                    }
                    _zombieBloodVfx = list.Count > 0 ? list.ToArray() : all;
                }
            }
            if (_zombieBloodVfx != null && _zombieBloodVfx.Length > 0)
            {
                return _zombieBloodVfx[Random.Range(0, _zombieBloodVfx.Length)];
            }
            return null;
        }

        private void SpawnGroundedBloodSplat(Vector3 worldRefPos, float scale, float lifetime = 2.5f)
        {
            GameObject prefab = GetRandomBloodPrefab();
            if (prefab == null) return;

            float groundY = GetGroundHeight(worldRefPos);
            if (groundY < -900f) groundY = transform.position.y;

            Vector3 floorPos = new Vector3(
                worldRefPos.x + Random.Range(-0.16f, 0.16f),
                groundY + 0.02f,
                worldRefPos.z + Random.Range(-0.16f, 0.16f));
            Quaternion floorRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject vfx = Instantiate(prefab, floorPos, floorRot);
            vfx.name = "FreeBloodVFX_GroundSplat";
            vfx.transform.localScale = Vector3.one * scale;

            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                var sizeOverLife = ps.sizeOverLifetime;
                sizeOverLife.enabled = false;

                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null && psr.enabled)
                {
                    psr.pivot = Vector3.zero;
                }
            }

            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Play(true);
            }

            Destroy(vfx, lifetime);
        }

        private void SpawnWoundBloodBurst(Vector3 woundPos, float size, float lifetime = 0.9f)
        {
            GameObject prefab = GetRandomBloodPrefab();
            if (prefab == null) return;

            GameObject vfx = Instantiate(prefab, woundPos, Quaternion.identity);
            vfx.name = "FreeBloodVFX_WoundBurst";

            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startLifetime = 0.48f;
                main.startSpeed = Random.Range(0.25f, 0.65f);
                main.gravityModifier = 0.75f;
                main.startSize3D = false;
                main.startSize = size;
                main.startRotation3D = true;
                main.startRotationX = 0f;
                main.startRotationY = 0f;
                main.startRotationZ = Random.Range(0f, Mathf.PI * 2f);

                var sizeOverLife = ps.sizeOverLifetime;
                sizeOverLife.enabled = false;

                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null && psr.enabled)
                {
                    psr.alignment = ParticleSystemRenderSpace.View;
                    psr.pivot = Vector3.zero;
                }
            }

            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Play(true);
            }

            Destroy(vfx, lifetime);
        }

        private void CreateHitSpark(Vector3 position)
        {
            // Spawn floor blood splatter flat on the ground beneath the hit zombie (never floating in mid-air)
            SpawnGroundedBloodSplat(position, Random.Range(0.45f, 0.62f), 2.2f);
        }

        private IEnumerator CinematicDeathSequence(Vector3 hitDirection)
        {
            // ==========================================
            // PHASE 1: "THE HIT" (0.00s)
            // Torso arches back, blood burst on chest, dying groan
            // ==========================================
            PlaySound(deathClip, 1.0f);
            Vector3 chestPos = transform.position + Vector3.up * 1.35f + transform.forward * 0.12f;
            CreateChestBloodBurst(chestPos, -hitDirection);

            // ==========================================
            // PHASE 2: "THE FALL" (0.00s -> 0.90s)
            // Center of mass drops, knees buckle forward deeply, arching backward
            // ==========================================
            yield return new WaitForSeconds(0.90f);

            // ==========================================
            // PHASE 3: "THE IMPACT" (0.90s)
            // Upper back and hips SLAM into ground!
            // Mud/dirt splash explosion & heavy ground slam thud
            // ==========================================
            Vector3 impactPos = transform.position - transform.forward * 0.65f;
            float gy = GetGroundHeight(impactPos);
            if (gy > -900f) impactPos.y = gy;
            CreateGroundImpactMudSplash(impactPos);
            PlayGroundImpactSound();

            // ==========================================
            // PHASE 4: "TARGET ELIMINATED" (1.25s)
            // Full rest pose. Show "TARGET ELIMINATED" UI banner & drop ammo loot!
            // ==========================================
            yield return new WaitForSeconds(0.35f);

            // Trigger "TARGET ELIMINATED" on Player HUD
            if (PlayerWeaponManager.Instance != null)
            {
                PlayerWeaponManager.Instance.ShowTargetEliminated();
            }
            else if (targetPlayer != null)
            {
                var pwm = targetPlayer.GetComponentInChildren<PlayerWeaponManager>();
                if (pwm != null) pwm.ShowTargetEliminated();
            }

            // Spawn 3D ammo drop on ground beside corpse (Panel 4)
            Vector3 lootPos = transform.position + transform.right * 0.45f - transform.forward * 0.30f;
            float lgy = GetGroundHeight(lootPos);
            if (lgy > -900f) lootPos.y = lgy;
            SpawnAmmoLootDrop(lootPos);
        }

        private void CreateChestBloodBurst(Vector3 pos, Vector3 burstDir)
        {
            // 1. Compact 3D camera-facing wound burst on the chest
            SpawnWoundBloodBurst(pos, 0.52f, 1.0f);

            // 2. Grounded blood splat flat on the floor beneath the dying zombie
            SpawnGroundedBloodSplat(pos, 0.80f, 3.0f);
        }

        private void CreateGroundImpactMudSplash(Vector3 pos)
        {
            GameObject mudRoot = new GameObject("ImpactSplash_Mud");
            mudRoot.transform.position = pos;

            for (int i = 0; i < 12; i++)
            {
                GameObject clod = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clod.transform.SetParent(mudRoot.transform);
                float angle = (i / 12f) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 0.2f, 0.05f, Mathf.Sin(angle) * 0.2f);
                clod.transform.position = pos + offset;
                clod.transform.localScale = new Vector3(Random.Range(0.08f, 0.16f), Random.Range(0.04f, 0.08f), Random.Range(0.08f, 0.16f));

                var r = clod.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = new Color(0.24f, 0.18f, 0.12f); // Wet mud / dirt brown
                }

                var col = clod.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rb = clod.AddComponent<Rigidbody>();
                Vector3 spray = (new Vector3(Mathf.Cos(angle), Random.Range(0.4f, 1.0f), Mathf.Sin(angle))).normalized;
                rb.linearVelocity = spray * Random.Range(1.8f, 3.8f);
            }

            Destroy(mudRoot, 2.0f);
        }

        private static AudioClip _cachedImpactThud;

        private void PlayGroundImpactSound()
        {
            if (_cachedImpactThud == null)
            {
                _cachedImpactThud = CreateSynthImpactThud();
            }
            PlaySound(_cachedImpactThud, 1.0f);
        }

        private static AudioClip CreateSynthImpactThud()
        {
            int sampleRate = 22050;
            float length = 0.45f;
            int totalSamples = (int)(sampleRate * length);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 14f);
                float osc = Mathf.Sin(2f * Mathf.PI * 65f * (1f - t * 0.8f) * t); // Deep 65Hz heavy thud
                float mudNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 22f); // Mud slap noise transient
                data[i] = (osc * 0.7f + mudNoise * 0.5f) * env;
            }

            AudioClip clip = AudioClip.Create("BodyGroundImpact", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void SpawnAmmoLootDrop(Vector3 pos)
        {
            GameObject dropGo = new GameObject("AmmoDrop_Cartridges");
            dropGo.transform.position = pos + Vector3.up * 0.06f;

            for (int i = 0; i < 2; i++)
            {
                GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bullet.transform.SetParent(dropGo.transform);
                bullet.transform.localPosition = new Vector3((i - 0.5f) * 0.08f, 0f, 0f);
                bullet.transform.localRotation = Quaternion.Euler(0f, Random.Range(20f, 70f), 90f);
                bullet.transform.localScale = new Vector3(0.025f, 0.06f, 0.025f);

                var r = bullet.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = new Color(0.95f, 0.75f, 0.25f); // Polished brass gold
                }

                var col = bullet.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            var lightGo = new GameObject("AmmoGlow");
            lightGo.transform.SetParent(dropGo.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.85f, 0.35f);
            l.range = 2.0f;
            l.intensity = 1.2f;

            dropGo.AddComponent<AmmoDropTrigger>();
        }

        private void InitProceduralAudio()
        {
            if (growlClip == null) growlClip = CreateSynthGroan(120f, 0.8f);
            if (attackClip == null) attackClip = CreateSynthSlash(450f, 0.4f);
            if (hitClip == null) hitClip = CreateSynthGroan(220f, 0.3f);
            if (deathClip == null) deathClip = CreateSynthGroan(90f, 1.2f);
            if (screamClip == null) screamClip = CreateSynthScream(340f, 0.70f);
        }

        private static AudioClip CreateSynthGroan(float baseFreq, float length)
        {
            int sampleRate = 22050;
            int totalSamples = (int)(sampleRate * length);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / length * Mathf.PI);
                // Low pitched growling waveform with noise modulation
                float osc = Mathf.Sin(2f * Mathf.PI * baseFreq * t * (1f - t * 0.3f));
                float noise = (Random.value * 2f - 1f) * 0.25f;
                data[i] = (osc + noise) * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("ZombieGroan", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthScream(float baseFreq, float length)
        {
            int sampleRate = 22050;
            int totalSamples = (int)(sampleRate * length);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / length * Mathf.PI);
                float pitchMod = baseFreq * (1.0f + Mathf.Sin(2f * Mathf.PI * 16f * t) * 0.25f + t * 0.35f);
                float osc = Mathf.Sin(2f * Mathf.PI * pitchMod * t);
                float harsh = Mathf.Clamp(osc * 2.4f, -1f, 1f);
                float noise = (Random.value * 2f - 1f) * 0.35f;
                data[i] = (harsh * 0.65f + noise * 0.35f) * env;
            }

            AudioClip clip = AudioClip.Create("ZombieScream", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSynthSlash(float baseFreq, float length)
        {
            int sampleRate = 22050;
            int totalSamples = (int)(sampleRate * length);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Pow(1f - (t / length), 2.5f);
                float noise = (Random.value * 2f - 1f);
                float osc = Mathf.Sin(2f * Mathf.PI * baseFreq * (1f - t * 0.7f) * t);
                data[i] = (noise * 0.7f + osc * 0.3f) * env * 0.8f;
            }

            AudioClip clip = AudioClip.Create("ZombieSlash", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void InitWaterBarrier()
        {
            if (_cachedSceneWaterY < -900f)
            {
                GameObject waterGo = GameObject.Find("WaterPlane 2");
                if (waterGo == null) waterGo = GameObject.Find("WaterPlane");
                _cachedSceneWaterY = (waterGo != null) ? waterGo.transform.position.y : 16.0f;
            }
            if (waterSurfaceY < 15.8f)
            {
                waterSurfaceY = _cachedSceneWaterY;
            }
            if (waterShoreMargin < 0.20f)
            {
                waterShoreMargin = 0.22f;
            }
        }

        private void EnsureOutOfWaterOnStart()
        {
            if (!IsPositionInWater(transform.position))
            {
                float gY = SampleGroundHeightAt(transform.position);
                if (gY > -900f)
                {
                    _lastDryPosition = new Vector3(transform.position.x, gY, transform.position.z);
                    _hasLastDryPosition = true;
                }
                return;
            }

            // Zombie started in water: relocate to nearest dry land above waterSurfaceY + waterShoreMargin
            float[] radii = new float[] { 3f, 6f, 10f, 15f, 22f, 30f, 42f };
            for (int r = 0; r < radii.Length; r++)
            {
                float radius = radii[r];
                for (int a = 0; a < 16; a++)
                {
                    float angle = (a / 16f) * Mathf.PI * 2f;
                    Vector3 candidate = new Vector3(
                        transform.position.x + Mathf.Cos(angle) * radius,
                        transform.position.y,
                        transform.position.z + Mathf.Sin(angle) * radius);

                    float gY = SampleGroundHeightAt(candidate);
                    if (gY > (waterSurfaceY + waterShoreMargin + 0.15f))
                    {
                        Vector3 dryPos = new Vector3(candidate.x, gY, candidate.z);
                        TeleportCharacterSafe(dryPos);
                        _spawnPosition = dryPos;
                        _wanderTarget = dryPos;
                        _lastDryPosition = dryPos;
                        _hasLastDryPosition = true;
                        return;
                    }
                }
            }
        }

        private void PickDryWanderTarget()
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 randCircle = Random.insideUnitCircle * 10f;
                Vector3 candidate = _spawnPosition + new Vector3(randCircle.x, 0f, randCircle.y);
                if (!IsPositionInWater(candidate))
                {
                    _wanderTarget = candidate;
                    return;
                }
            }
            // Fallback to last known dry position or spawn position
            _wanderTarget = _hasLastDryPosition ? _lastDryPosition : _spawnPosition;
        }

        private bool TryResolveDryMoveDirection(Vector3 desiredDir, float speed, bool isChasing, out Vector3 safeDir)
        {
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude < 0.0001f)
            {
                safeDir = Vector3.zero;
                return false;
            }
            desiredDir.Normalize();

            if (IsStepDirectionDry(desiredDir, speed))
            {
                safeDir = desiredDir;
                return true;
            }

            // Water is directly ahead as a barrier: test shoreline tangent angles to see if zombie can skirt along dry bank
            float[] testAngles = new float[] { 35f, -35f, 65f, -65f, 85f, -85f };
            float currentDistToPlayer = (isChasing && targetPlayer != null)
                ? Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(targetPlayer.position.x, targetPlayer.position.z))
                : 0f;

            for (int i = 0; i < testAngles.Length; i++)
            {
                Vector3 candidateDir = Quaternion.Euler(0f, testAngles[i], 0f) * desiredDir;
                candidateDir.y = 0f;
                candidateDir.Normalize();

                if (!IsStepDirectionDry(candidateDir, speed))
                {
                    continue;
                }

                if (isChasing && targetPlayer != null)
                {
                    // When chasing a player across/in water, only slide along the bank if it brings the zombie closer to the player
                    Vector3 testStep = transform.position + candidateDir * 0.6f;
                    float newDistToPlayer = Vector2.Distance(new Vector2(testStep.x, testStep.z), new Vector2(targetPlayer.position.x, targetPlayer.position.z));
                    if (newDistToPlayer >= currentDistToPlayer - 0.08f)
                    {
                        continue;
                    }
                }

                safeDir = candidateDir;
                return true;
            }

            safeDir = Vector3.zero;
            return false;
        }

        private bool IsStepDirectionDry(Vector3 dir, float speed)
        {
            float stepDist = Mathf.Max(speed * Time.deltaTime, 0.35f);
            Vector3 p1 = transform.position + dir * stepDist;
            Vector3 p2 = transform.position + dir * 0.85f;

            if (IsPositionInWater(p1) || IsPositionInWater(p2))
            {
                return false;
            }

            // Also check capsule left/right shoulders at 0.65m ahead so zombie never clips into shoreline water
            Vector3 right = new Vector3(dir.z, 0f, -dir.x) * 0.30f;
            Vector3 pLeft = transform.position + dir * 0.65f - right;
            Vector3 pRight = transform.position + dir * 0.65f + right;
            if (IsPositionInWater(pLeft) || IsPositionInWater(pRight))
            {
                return false;
            }

            return true;
        }

        private void EnforceDryPositionBarrier()
        {
            if (isRadioAmbushMonster && targetPlayer != null && !IsPositionInWater(targetPlayer.position))
            {
                return;
            }

            if (IsPositionInWater(transform.position))
            {
                _isStoppedAtWater = true;
                _targetSpeed = 0.0f;
                if (_hasLastDryPosition)
                {
                    TeleportCharacterSafe(_lastDryPosition);
                    _verticalVelocity = 0f;
                }
                else
                {
                    EnsureOutOfWaterOnStart();
                }
            }
        }

        private void TeleportCharacterSafe(Vector3 targetPos)
        {
            bool wasEnabled = (_controller != null && _controller.enabled);
            if (wasEnabled) _controller.enabled = false;
            transform.position = targetPos;
            if (wasEnabled) _controller.enabled = true;
        }

        public bool IsPositionInWater(Vector3 pos)
        {
            float gY = SampleGroundHeightAt(pos);
            if (gY < -900f) return true;
            return gY <= (waterSurfaceY + waterShoreMargin);
        }

        private float SampleGroundHeightAt(Vector3 pos)
        {
            float groundY = -999f;
            // Ignore Layer 2 (Ignore Raycast), Layer 4 (Water), and Layer 8 (Player)
            int groundMask = ~((1 << 2) | (1 << 4) | (1 << 8));

            float rayStartY = Mathf.Max(pos.y, waterSurfaceY) + 1.8f;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(pos.x, rayStartY, pos.z), Vector3.down, 18f, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider == _controller) continue;
                if (h.collider.transform.root == transform.root) continue;
                if (h.collider.GetComponentInParent<ZombieAI>() != null) continue;

                groundY = h.point.y;
                break;
            }

            if (Terrain.activeTerrains != null)
            {
                foreach (var terr in Terrain.activeTerrains)
                {
                    if (terr == null || terr.terrainData == null) continue;
                    Vector3 tPos = terr.transform.position;
                    Vector3 tSize = terr.terrainData.size;
                    if (pos.x >= tPos.x && pos.x <= tPos.x + tSize.x &&
                        pos.z >= tPos.z && pos.z <= tPos.z + tSize.z)
                    {
                        float terrY = terr.SampleHeight(pos) + tPos.y;
                        if (terrY > groundY) groundY = terrY;
                    }
                }
            }

            return groundY;
        }

        public float GetGroundHeight(Vector3 pos)
        {
            if (Time.frameCount == _cachedGroundFrame && _cachedGroundY > -900f &&
                (pos.x - _cachedGroundPos.x) * (pos.x - _cachedGroundPos.x) + (pos.z - _cachedGroundPos.z) * (pos.z - _cachedGroundPos.z) < 0.04f)
            {
                return _cachedGroundY;
            }

            _cachedGroundFrame = Time.frameCount;
            _cachedGroundPos = pos;
            float groundY = SampleGroundHeightAt(pos);
            _cachedGroundY = groundY;
            return groundY;
        }

        // AnimationEvent receivers from humanoid / StarterAssets animation clips
        public void OnFootstep(AnimationEvent animationEvent) { }
        public void OnFootstep() { }
        public void OnLand(AnimationEvent animationEvent) { }
        public void OnLand() { }

        private static Texture2D _hpWhiteTex;
        private static GUIStyle _hpTextStyle;

        private void OnGUI()
        {
            if (isDead || currentState == State.Dead) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist > 35f) return;

            Vector3 headPos = (_headBone != null ? _headBone.position : (transform.position + Vector3.up * 1.85f)) + Vector3.up * 0.45f;
            Vector3 sp = cam.WorldToScreenPoint(headPos);
            if (sp.z <= 0f) return;

            if (_hpWhiteTex == null)
            {
                _hpWhiteTex = new Texture2D(1, 1);
                _hpWhiteTex.SetPixel(0, 0, Color.white);
                _hpWhiteTex.Apply();
            }

            if (_hpTextStyle == null)
            {
                _hpTextStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                _hpTextStyle.normal.textColor = Color.white;
            }

            float barW = 130f;
            float barH = 18f;
            float barLeft = sp.x - barW * 0.5f;
            float barTop = (Screen.height - sp.y) - 12f;

            GUI.color = new Color(0.06f, 0.01f, 0.01f, 0.9f);
            GUI.DrawTexture(new Rect(barLeft - 2f, barTop - 2f, barW + 4f, barH + 4f), _hpWhiteTex);

            float ratio = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
            GUI.color = new Color(0.92f, 0.14f, 0.14f, 0.95f);
            GUI.DrawTexture(new Rect(barLeft, barTop, barW * ratio, barH), _hpWhiteTex);
            GUI.color = Color.white;

            GUI.Label(new Rect(barLeft, barTop, barW, barH), $"♥ HP: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}", _hpTextStyle);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }

    public class AmmoDropTrigger : MonoBehaviour
    {
        public int ammoCount = 20;
        private Transform _playerTransform;
        private bool _collected = false;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("PlayerCapsule");
            if (p != null) _playerTransform = p.transform;
            Destroy(gameObject, 60f); // Clean up after 1 minute if untouched
        }

        private void Update()
        {
            if (_collected) return;

            // Gentle floating bob
            transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);

            if ( _playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist <= 2.2f)
                {
                    Collect();
                }
            }
        }

        private void Collect()
        {
            _collected = true;
            if (PlayerWeaponManager.Instance != null)
            {
                PlayerWeaponManager.Instance.AddReserveAmmo(ammoCount);
            }

            // Audio chime
            AudioSource.PlayClipAtPoint(CreatePickupChime(), transform.position, 0.9f);
            Destroy(gameObject);
        }

        private static AudioClip CreatePickupChime()
        {
            int sampleRate = 22050;
            float length = 0.25f;
            int total = (int)(sampleRate * length);
            float[] data = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 12f);
                float tone1 = Mathf.Sin(2f * Mathf.PI * 880f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 1320f * t);
                data[i] = (tone1 * 0.5f + tone2 * 0.5f) * env * 0.7f;
            }
            AudioClip clip = AudioClip.Create("AmmoChime", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
