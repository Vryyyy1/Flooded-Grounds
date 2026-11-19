using UnityEngine;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation / Mouse sensitivity speed of the character")]
		[Range(0.1f, 3.0f)]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump (realistic human combat jump is ~0.75m)")]
		public float JumpHeight = 0.75f;
		[Tooltip("The character uses its own gravity value. Realistic FPS gravity is -22 m/s2")]
		public float Gravity = -22.0f;
		[Tooltip("Multiplier applied to gravity while falling down to give a punchy, weighted landing")]
		public float FallGravityMultiplier = 1.35f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Swimming")]
		[Tooltip("Whether the player can swim on water")]
		public bool EnableSwimming = true;
		[Tooltip("True while the player is actively swimming on water")]
		public bool IsSwimming = false;
		[Tooltip("World-space Y height of the water surface")]
		public float WaterSurfaceY = 16.0f;
		[Tooltip("Submerge offset for player root while floating so eyes (+1.55m) stay ~0.40m above water")]
		public float SwimSubmergeOffset = 1.15f;
		[Tooltip("Normal swimming speed in m/s")]
		public float SwimSpeed = 3.2f;
		[Tooltip("Fast swimming speed when holding Sprint in m/s")]
		public float FastSwimSpeed = 5.0f;

		private bool _waterDetected = false;
		private Bounds _waterBounds;
		private AudioSource _swimAudioSource;
		private AudioClip _swimStrokeClip;
		private AudioClip _waterSplashClip;
		private float _nextSwimStrokeTime = 0f;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;
		[Tooltip("Invert vertical camera look direction")]
		public bool InvertPitch = false;

		[Header("Character Animation")]
		[Tooltip("The Animator component on the Armature character (auto-detected in children if empty)")]
		public Animator CharacterAnimator;
		private bool _hasAnimator;
		private float _animationBlend;

		// animation IDs
		private int _animIDSpeed;
		private int _animIDGrounded;
		private int _animIDJump;
		private int _animIDFreeFall;
		private int _animIDMotionSpeed;

		// camera motion offsets (e.g. from CamcorderCameraEffect / headbob)
		[HideInInspector] public Vector3 CameraPositionOffset = Vector3.zero;
		[HideInInspector] public Quaternion CameraRotationOffset = Quaternion.identity;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.0001f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				if (_playerInput != null && !string.IsNullOrEmpty(_playerInput.currentControlScheme))
				{
					return _playerInput.currentControlScheme == "KeyboardMouse";
				}
				return true;
				#else
				return true;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
			if (_input == null)
			{
				_input = GetComponentInParent<StarterAssetsInputs>() ?? GetComponentInChildren<StarterAssetsInputs>();
			}
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// Include all environment layers as ground if default is only Layer 0
			if (GroundLayers.value <= 1)
			{
				GroundLayers = ~((1 << 2) | (1 << 5) | (1 << gameObject.layer));
			}

			// Ensure CharacterController smoothly handles bridge planks and steps
			if (_controller != null)
			{
				if (_controller.stepOffset < 0.5f) _controller.stepOffset = 0.5f;
				if (_controller.slopeLimit < 60f) _controller.slopeLimit = 60f;
				if (_controller.skinWidth < 0.05f) _controller.skinWidth = 0.05f;
			}

			// Ensure Flashlight is attached to camera target so it points where player looks
			var camTarget = CinemachineCameraTarget != null ? CinemachineCameraTarget : gameObject;
			if (camTarget.GetComponent<FlashlightController>() == null)
			{
				camTarget.AddComponent<FlashlightController>();
			}


			// Configure camera distance safely behind the character to prevent clipping/glitching
			ConfigureCameraView();

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			// Enforce realistic jump height and gravity if using legacy floaty values
			if (JumpHeight > 0.85f) JumpHeight = 0.75f;
			if (Gravity > -18.0f) Gravity = -22.0f;

			InitWaterSwimmingSystem();

			// Hook up Animator on child Armature
			if (CharacterAnimator == null)
			{
				CharacterAnimator = GetComponentInChildren<Animator>();
			}
			_hasAnimator = CharacterAnimator != null && CharacterAnimator.runtimeAnimatorController != null;
			AssignAnimationIDs();
		}

		private void ConfigureCameraView()
		{
			// 1. Position camera target at eye/head height (1.55m, 0.05m forward)
			if (CinemachineCameraTarget != null)
			{
				CinemachineCameraTarget.transform.localPosition = new Vector3(0f, 1.55f, 0.05f);
			}

			// 2. Attach MainCamera directly to CinemachineCameraTarget (PlayerCameraRoot)
			// This completely eliminates Cinemachine interpolation lag, obstacle collision jitter, and noise
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
				if (_mainCamera == null && Camera.main != null)
				{
					_mainCamera = Camera.main.gameObject;
				}
			}

			if (_mainCamera != null && CinemachineCameraTarget != null)
			{
				// Disable CinemachineBrain on MainCamera to stop external orbital calculations and damping
				var brain = _mainCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
				if (brain != null)
				{
					brain.enabled = false;
				}

				// Parent MainCamera directly to the player's camera root
				_mainCamera.transform.SetParent(CinemachineCameraTarget.transform, false);
				_mainCamera.transform.localPosition = Vector3.zero;
				_mainCamera.transform.localRotation = Quaternion.identity;

				var camComp = _mainCamera.GetComponent<Camera>();
				if (camComp != null)
				{
					camComp.nearClipPlane = 0.02f;
				}
			}

			// 3. Disable PlayerFollowCamera to ensure no Cinemachine Virtual Camera or Perlin noise runs
			var followCamGo = GameObject.Find("PlayerFollowCamera");
			if (followCamGo != null)
			{
				var perlin = followCamGo.GetComponent<Unity.Cinemachine.CinemachineBasicMultiChannelPerlin>();
				if (perlin != null)
				{
					perlin.enabled = false;
					perlin.AmplitudeGain = 0f;
					perlin.FrequencyGain = 0f;
				}

				var tpFollow = followCamGo.GetComponent<Unity.Cinemachine.CinemachineThirdPersonFollow>();
				if (tpFollow != null)
				{
					tpFollow.enabled = false;
				}

				followCamGo.SetActive(false);
			}

			// 4. Hide 3rd-person character mesh from first-person camera to prevent clipping into head/chest
			var armature = transform.Find("Armature");
			if (armature != null)
			{
				foreach (var r in armature.GetComponentsInChildren<Renderer>(true))
				{
					r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
				}
			}

			// 5. Ensure FP_WeaponHolder exists under camera target
			if (CinemachineCameraTarget != null)
			{
				var fpHolder = CinemachineCameraTarget.transform.Find("FP_WeaponHolder");
				if (fpHolder == null)
				{
					GameObject holderGo = new GameObject("FP_WeaponHolder");
					holderGo.transform.SetParent(CinemachineCameraTarget.transform, false);
					holderGo.transform.localPosition = new Vector3(0.18f, -0.16f, 0.40f);
					holderGo.transform.localRotation = Quaternion.identity;
				}
			}
		}

		private void Update()
		{
			CameraRotation();
			JumpAndGravity();
			GroundedCheck();
			Move();
		}

		private void LateUpdate()
		{
			// Apply walking motion effect and lock MainCamera relative to camera target
			if (_mainCamera != null && CinemachineCameraTarget != null && _mainCamera.transform.parent == CinemachineCameraTarget.transform)
			{
				_mainCamera.transform.localPosition = CameraPositionOffset;
				_mainCamera.transform.localRotation = CameraRotationOffset;
			}
		}

		private void GroundedCheck()
		{
			// Position sphere check accurately relative to controller bottom to prevent floating
			float bottomY = _controller != null 
				? (_controller.transform.position.y + _controller.center.y - (_controller.height * 0.5f) + GroundedRadius)
				: (transform.position.y - GroundedOffset);
			Vector3 spherePosition = new Vector3(transform.position.x, bottomY, transform.position.z);
			bool sphereHit = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

			float groundY = GetGroundHeight(transform.position);
			UpdateSwimmingState(groundY);

			bool nearGround = !IsSwimming && (groundY > -900f && Mathf.Abs(transform.position.y - groundY) <= 0.25f);

			Grounded = !IsSwimming && ((_controller != null && _controller.isGrounded) || sphereHit || nearGround);

			// Emergency fall-through rescue ONLY if player fell deep through geometry into the void
			if (groundY > -900f && transform.position.y < (groundY - 1.2f))
			{
				transform.position = new Vector3(transform.position.x, groundY + 0.1f, transform.position.z);
				if (_verticalVelocity < 0.0f) _verticalVelocity = 0.0f;
			}

			if (_hasAnimator)
			{
				CharacterAnimator.SetBool(_animIDGrounded, Grounded);
			}
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input != null && _input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				float pitchSign = InvertPitch ? 1.0f : -1.0f;
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier * pitchSign;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
			else
			{
				_rotationVelocity = 0.0f;
			}
		}

		private void Move()
		{
			// set target speed based on swimming state or land walk/sprint speed
			float targetSpeed;
			if (IsSwimming)
			{
				targetSpeed = (_input != null && _input.sprint) ? FastSwimSpeed : SwimSpeed;
			}
			else
			{
				targetSpeed = (_input != null && _input.sprint) ? SprintSpeed : MoveSpeed;
			}

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input == null || _input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = (_input != null && _input.analogMovement) ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			// Play rhythmic water stroke sound when swimming and moving
			if (IsSwimming && _input != null && _input.move != Vector2.zero && Time.time >= _nextSwimStrokeTime)
			{
				float strokeInterval = _input.sprint ? 0.52f : 0.78f;
				_nextSwimStrokeTime = Time.time + strokeInterval;
				if (_swimAudioSource != null && _swimStrokeClip != null)
				{
					_swimAudioSource.pitch = Random.Range(0.90f, 1.10f);
					_swimAudioSource.PlayOneShot(_swimStrokeClip, 0.35f);
				}
			}

			// Physics movement handled naturally by CharacterController without vertical jitter

			// update animator if character is attached
			_animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
			if (_animationBlend < 0.01f) _animationBlend = 0f;

			if (_hasAnimator)
			{
				CharacterAnimator.SetFloat(_animIDSpeed, _animationBlend);
				CharacterAnimator.SetFloat(_animIDMotionSpeed, inputMagnitude);
			}
		}

		private void JumpAndGravity()
		{
			if (IsSwimming)
			{
				_fallTimeoutDelta = FallTimeout;
				if (_hasAnimator)
				{
					CharacterAnimator.SetBool(_animIDJump, false);
					CharacterAnimator.SetBool(_animIDFreeFall, false);
				}

				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}

				// Player cannot jump while swimming
				if (_input != null)
				{
					_input.jump = false;
				}

				// Surface buoyancy
				float baseFloatY = WaterSurfaceY - SwimSubmergeOffset;
				float bobOffset = Mathf.Sin(Time.time * 2.2f) * 0.035f;
				float targetFloatY = baseFloatY + bobOffset;

				if (transform.position.y > targetFloatY + 0.05f)
				{
					// In air above water surface during a water jump: apply gravity to pull player back to water
					_verticalVelocity += Gravity * Time.deltaTime;
				}
				else
				{
					// Smooth surface buoyancy with gentle water wave bobbing
					float diffY = targetFloatY - transform.position.y;
					float desiredVelY = Mathf.Clamp(diffY * 6.5f, -4.0f, 6.0f);
					_verticalVelocity = Mathf.Lerp(_verticalVelocity, desiredVelY, Time.deltaTime * 8.0f);
				}
				return;
			}

			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// update animator
				if (_hasAnimator)
				{
					CharacterAnimator.SetBool(_animIDJump, false);
					CharacterAnimator.SetBool(_animIDFreeFall, false);
				}

				// stop our velocity dropping infinitely when grounded (firm downward ground stick)
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -4f;
				}

				// Jump
				if (_input != null && _input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
					if (_hasAnimator)
					{
						CharacterAnimator.SetBool(_animIDJump, true);
					}
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}
				else
				{
					if (_hasAnimator)
					{
						CharacterAnimator.SetBool(_animIDFreeFall, true);
					}
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply realistic asymmetric gravity: faster fall for weighty, grounded landing
			float currentGravity = (_verticalVelocity < 0.0f) ? (Gravity * FallGravityMultiplier) : Gravity;
			if (_verticalVelocity > -35.0f)
			{
				_verticalVelocity += currentGravity * Time.deltaTime;
			}
			_verticalVelocity = Mathf.Max(_verticalVelocity, -35.0f);
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void AssignAnimationIDs()
		{
			_animIDSpeed = Animator.StringToHash("Speed");
			_animIDGrounded = Animator.StringToHash("Grounded");
			_animIDJump = Animator.StringToHash("Jump");
			_animIDFreeFall = Animator.StringToHash("FreeFall");
			_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
		}

		private void InitWaterSwimmingSystem()
		{
			// Ensure Layer 4 (Water) is excluded from GroundLayers so water is swimmable
			GroundLayers &= ~(1 << 4);

			// Locate WaterPlane in scene
			GameObject waterGo = GameObject.Find("WaterPlane 2");
			if (waterGo == null) waterGo = GameObject.Find("WaterPlane");
			if (waterGo != null)
			{
				WaterSurfaceY = waterGo.transform.position.y;
				Renderer r = waterGo.GetComponent<Renderer>();
				if (r != null)
				{
					_waterBounds = r.bounds;
					_waterBounds.Expand(new Vector3(30f, 50f, 30f));
					_waterDetected = true;
				}
			}

			InitProceduralSwimAudio();
		}

		private void UpdateSwimmingState(float groundY)
		{
			if (!EnableSwimming)
			{
				IsSwimming = false;
				return;
			}

			if (_waterDetected)
			{
				Vector3 p = transform.position;
				if (p.x < _waterBounds.min.x || p.x > _waterBounds.max.x || p.z < _waterBounds.min.z || p.z > _waterBounds.max.z)
				{
					IsSwimming = false;
					return;
				}
			}

			float floatRootY = WaterSurfaceY - SwimSubmergeOffset;
			bool deepEnoughForSwim = (groundY < floatRootY - 0.05f);

			if (!IsSwimming)
			{
				if (deepEnoughForSwim && transform.position.y <= floatRootY + 0.08f)
				{
					IsSwimming = true;
					// If player fell/spawned deep below the water surface, bring them right near the surface
					if (transform.position.y < floatRootY - 0.65f)
					{
						transform.position = new Vector3(transform.position.x, floatRootY - 0.25f, transform.position.z);
					}
					_verticalVelocity *= 0.25f;
					if (_swimAudioSource != null && _waterSplashClip != null)
					{
						_swimAudioSource.pitch = Random.Range(0.92f, 1.05f);
						_swimAudioSource.PlayOneShot(_waterSplashClip, 0.50f);
					}
				}
			}
			else
			{
				// Exit swimming ONLY when reaching shallow shore or firmly landing on a solid surface (dock/boat) above water
				bool hasLandedOnSolidSurface = false;
				if (transform.position.y > floatRootY + 0.20f && _verticalVelocity <= 0.1f)
				{
					hasLandedOnSolidSurface = (_controller != null && _controller.isGrounded) || Physics.Raycast(transform.position, Vector3.down, 0.35f, GroundLayers, QueryTriggerInteraction.Ignore);
				}

				if (!deepEnoughForSwim || hasLandedOnSolidSurface)
				{
					IsSwimming = false;
				}
			}
		}

		private void InitProceduralSwimAudio()
		{
			_swimAudioSource = gameObject.AddComponent<AudioSource>();
			_swimAudioSource.spatialBlend = 0.2f;
			_swimAudioSource.playOnAwake = false;

			int sampleRate = 22050;
			// 1. Swim stroke slosh sound (0.38s filtered water swoosh)
			int strokeSamples = Mathf.RoundToInt(sampleRate * 0.38f);
			float[] strokeData = new float[strokeSamples];
			float prev = 0f;
			for (int i = 0; i < strokeSamples; i++)
			{
				float t = (float)i / strokeSamples;
				float env = Mathf.Sin(t * Mathf.PI);
				float white = Random.Range(-1f, 1f);
				prev = prev * 0.82f + white * 0.18f;
				strokeData[i] = prev * env * 0.7f;
			}
			_swimStrokeClip = AudioClip.Create("SwimStroke", strokeSamples, 1, sampleRate, false);
			_swimStrokeClip.SetData(strokeData, 0);

			// 2. Water entry splash sound (0.45s)
			int splashSamples = Mathf.RoundToInt(sampleRate * 0.45f);
			float[] splashData = new float[splashSamples];
			float prevS = 0f;
			for (int i = 0; i < splashSamples; i++)
			{
				float t = (float)i / splashSamples;
				float env = Mathf.Pow(1f - t, 1.8f);
				float white = Random.Range(-1f, 1f);
				prevS = prevS * 0.72f + white * 0.28f;
				splashData[i] = prevS * env * 0.9f;
			}
			_waterSplashClip = AudioClip.Create("WaterSplash", splashSamples, 1, sampleRate, false);
			_waterSplashClip.SetData(splashData, 0);
		}

		public float GetGroundHeight(Vector3 pos)
		{
			float groundY = -999f;
			int groundMask = ~((1 << 2) | (1 << 4) | (1 << gameObject.layer));

			if (Physics.Raycast(pos + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 1.5f, groundMask, QueryTriggerInteraction.Ignore))
			{
				if (hit.normal.y >= 0.65f) groundY = hit.point.y;
			}

			if (Terrain.activeTerrain != null)
			{
				float terrY = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
				if (terrY > groundY) groundY = terrY;
			}

			return groundY;
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}

		// AnimationEvent receivers for StarterAssets animation clips
		private void OnFootstep(AnimationEvent animationEvent)
		{
		}

		private void OnLand(AnimationEvent animationEvent)
		{
		}
	}
}



