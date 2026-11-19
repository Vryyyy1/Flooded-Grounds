using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Builds a physics ragdoll at runtime on a Humanoid character and switches the
    /// character from animation to physics ("active ragdoll" death blend).
    /// Bones are built kinematic so the death flinch animation can keep driving them,
    /// then <see cref="Activate"/> hands control over to physics.
    /// </summary>
    public class ZombieRagdoll : MonoBehaviour
    {
        private struct BoneDef
        {
            public HumanBodyBones bone;
            public HumanBodyBones child;   // used to size/orient the capsule and joint twist axis
            public HumanBodyBones parent;  // ragdoll parent (joint connects to this bone)
            public float radius;           // world-space radius in meters
            public float mass;
            public bool isRoot;
            public bool isHead;

            public BoneDef(HumanBodyBones bone, HumanBodyBones child, HumanBodyBones parent, float radius, float mass, bool isRoot = false, bool isHead = false)
            {
                this.bone = bone;
                this.child = child;
                this.parent = parent;
                this.radius = radius;
                this.mass = mass;
                this.isRoot = isRoot;
                this.isHead = isHead;
            }
        }

        private static readonly BoneDef[] Bones =
        {
            new BoneDef(HumanBodyBones.Hips,          HumanBodyBones.Spine,         HumanBodyBones.LastBone,     0.14f, 12f, isRoot: true),
            new BoneDef(HumanBodyBones.Spine,         HumanBodyBones.Head,          HumanBodyBones.Hips,         0.15f, 16f),
            new BoneDef(HumanBodyBones.Head,          HumanBodyBones.LastBone,      HumanBodyBones.Spine,        0.11f, 4f,  isHead: true),

            new BoneDef(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  HumanBodyBones.Spine,        0.05f, 2.5f),
            new BoneDef(HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,      HumanBodyBones.LeftUpperArm, 0.04f, 1.5f),
            new BoneDef(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.Spine,        0.05f, 2.5f),
            new BoneDef(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,     HumanBodyBones.RightUpperArm,0.04f, 1.5f),

            new BoneDef(HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  HumanBodyBones.Hips,         0.075f, 7f),
            new BoneDef(HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,      HumanBodyBones.LeftUpperLeg, 0.06f, 4f),
            new BoneDef(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.Hips,         0.075f, 7f),
            new BoneDef(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,     HumanBodyBones.RightUpperLeg,0.06f, 4f),
        };

        private Animator _animator;
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private readonly List<CharacterJoint> _joints = new List<CharacterJoint>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private Rigidbody _hipsBody;
        private Rigidbody _spineBody;
        private bool _active;

        /// <summary>True when the animator has a valid humanoid avatar a ragdoll can be built on.</summary>
        public static bool CanBuild(Animator animator)
        {
            return animator != null && animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Hips) != null;
        }

        /// <summary>Adds (or reuses) a ragdoll on the animator's object and builds all bodies kinematically.</summary>
        public static ZombieRagdoll Build(Animator animator)
        {
            if (!CanBuild(animator)) return null;

            var ragdoll = animator.GetComponent<ZombieRagdoll>();
            if (ragdoll == null) ragdoll = animator.gameObject.AddComponent<ZombieRagdoll>();
            ragdoll.BuildInternal(animator);
            return ragdoll;
        }

        private void BuildInternal(Animator animator)
        {
            if (_bodies.Count > 0) return;
            _animator = animator;

            var bodyByBone = new Dictionary<HumanBodyBones, Rigidbody>();

            foreach (var def in Bones)
            {
                Transform bone = animator.GetBoneTransform(def.bone);
                if (bone == null) continue;

                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = def.mass;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.maxAngularVelocity = 12f;
                bodyByBone[def.bone] = rb;
                _bodies.Add(rb);

                Transform child = def.child == HumanBodyBones.LastBone ? null : animator.GetBoneTransform(def.child);
                AddCollider(bone, child, def);
            }

            // Connect the joints now that every rigidbody exists
            foreach (var def in Bones)
            {
                if (def.isRoot) continue;
                if (!bodyByBone.TryGetValue(def.bone, out var rb)) continue;

                Rigidbody parentBody = null;
                HumanBodyBones parentBone = def.parent;
                // Fall back up the chain if a parent bone is missing from the rig
                while (parentBody == null && parentBone != HumanBodyBones.LastBone)
                {
                    if (!bodyByBone.TryGetValue(parentBone, out parentBody))
                    {
                        parentBone = FindParentBone(parentBone);
                    }
                }
                if (parentBody == null) continue;

                Transform child = def.child == HumanBodyBones.LastBone ? null : animator.GetBoneTransform(def.child);
                AddJoint(rb, parentBody, child, def);
            }

            bodyByBone.TryGetValue(HumanBodyBones.Hips, out _hipsBody);
            bodyByBone.TryGetValue(HumanBodyBones.Spine, out _spineBody);
        }

        private static HumanBodyBones FindParentBone(HumanBodyBones bone)
        {
            foreach (var d in Bones)
            {
                if (d.bone == bone) return d.parent;
            }
            return HumanBodyBones.LastBone;
        }

        private void AddCollider(Transform bone, Transform child, BoneDef def)
        {
            Vector3 lossy = bone.lossyScale;
            float avgScale = Mathf.Max(0.0001f, (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y) + Mathf.Abs(lossy.z)) / 3f);

            if (def.isHead || child == null)
            {
                var sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = def.radius / avgScale;
                // Head bone origin sits at the neck: push the sphere up along world up
                sphere.center = bone.InverseTransformVector(Vector3.up * def.radius);
                _colliders.Add(sphere);
                return;
            }

            Vector3 childLocal = bone.InverseTransformPoint(child.position);
            int axis = DominantAxis(childLocal);
            float worldLen = Vector3.Distance(bone.position, child.position);

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.radius = def.radius / avgScale;
            capsule.height = Mathf.Max(worldLen / Mathf.Max(0.0001f, Mathf.Abs(lossy[axis])), capsule.radius * 2f);
            capsule.center = childLocal * 0.5f;
            _colliders.Add(capsule);
        }

        private void AddJoint(Rigidbody body, Rigidbody parentBody, Transform child, BoneDef def)
        {
            var joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;

            Vector3 twistAxis = Vector3.right;
            if (child != null)
            {
                twistAxis = body.transform.InverseTransformPoint(child.position).normalized;
            }
            else if (def.isHead)
            {
                twistAxis = body.transform.InverseTransformVector(Vector3.up).normalized;
            }

            Vector3 swingAxis = Vector3.Cross(twistAxis, Vector3.up);
            if (swingAxis.sqrMagnitude < 0.01f) swingAxis = Vector3.Cross(twistAxis, Vector3.right);
            swingAxis.Normalize();

            joint.axis = twistAxis;
            joint.swingAxis = swingAxis;

            float twist = 30f, swing1 = 45f, swing2 = 30f;
            switch (def.bone)
            {
                case HumanBodyBones.Spine:
                    twist = 20f; swing1 = 30f; swing2 = 25f; break;
                case HumanBodyBones.Head:
                    twist = 35f; swing1 = 40f; swing2 = 30f; break;
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm:
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg:
                    twist = 8f; swing1 = 70f; swing2 = 10f; break;
            }

            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = swing2 };

            _joints.Add(joint);
        }

        private static int DominantAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return 0;
            if (ay >= ax && ay >= az) return 1;
            return 2;
        }

        /// <summary>
        /// Hands the character over to physics and shoves it along <paramref name="pushDirection"/>.
        /// After <paramref name="settleDelay"/> seconds the physics components are stripped so the
        /// corpse keeps its pose without costing performance or blocking the player.
        /// </summary>
        public void Activate(Vector3 pushDirection, float pushSpeed, float settleDelay, Vector3? hitPoint = null)
        {
            if (_active) return;
            _active = true;

            if (_animator != null) _animator.enabled = false;

            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude < 0.001f) pushDirection = -transform.forward;
            pushDirection.Normalize();

            foreach (var rb in _bodies)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Strongest shove on the torso (where the shot lands), lighter on the limbs
            if (_spineBody != null)
                _spineBody.AddForce(pushDirection * pushSpeed + Vector3.up * (pushSpeed * 0.25f), ForceMode.VelocityChange);
            if (_hipsBody != null)
                _hipsBody.AddForce(pushDirection * (pushSpeed * 0.5f), ForceMode.VelocityChange);

            // Extra shove on the bone closest to the impact so the body tumbles realistically
            if (hitPoint.HasValue)
            {
                Rigidbody nearest = null;
                float best = float.MaxValue;
                foreach (var rb in _bodies)
                {
                    float d = (rb.worldCenterOfMass - hitPoint.Value).sqrMagnitude;
                    if (d < best) { best = d; nearest = rb; }
                }
                if (nearest != null)
                    nearest.AddForce(pushDirection * (pushSpeed * 0.6f) + Vector3.up * (pushSpeed * 0.2f), ForceMode.VelocityChange);
            }

            StartCoroutine(SettleRoutine(settleDelay));
        }

        private IEnumerator SettleRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Joints must go first: a Rigidbody can't be removed while a joint depends on it
            foreach (var j in _joints) if (j != null) Destroy(j);
            yield return null;
            foreach (var rb in _bodies) if (rb != null) Destroy(rb);
            foreach (var c in _colliders) if (c != null) Destroy(c);

            _joints.Clear();
            _bodies.Clear();
            _colliders.Clear();
        }
    }
}
