using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

#if !UNITY_IOS || !UNITY_ANDROID
		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;
#endif

		[Header("Input Blocked Flag")]
		public static bool InputBlocked = false;

		private Vector2 _smoothLook = Vector2.zero;

		private void Start()
		{
			SetCursorState(cursorLocked);
		}

		private void Update()
		{
			if (InputBlocked)
			{
				move = Vector2.zero;
				look = Vector2.zero;
				jump = false;
				sprint = false;
				SetCursorState(false);
				return;
			}

			// In gameplay, click inside game window locks the cursor if unlocked
			if (cursorLocked && Cursor.lockState != CursorLockMode.Locked)
			{
				if (Input.GetMouseButtonDown(0))
				{
					SetCursorState(true);
				}
			}

			// Guaranteed input polling fallback so movement and camera look ALWAYS respond immediately
			PollInputs();
		}

		private void PollInputs()
		{
			// 1. WASD / Arrow movement polling
			Vector2 dir = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
			if (Keyboard.current != null)
			{
				if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) dir.y += 1f;
				if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) dir.y -= 1f;
				if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) dir.x -= 1f;
				if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) dir.x += 1f;
			}
			else
#endif
			{
				dir.x = Input.GetAxisRaw("Horizontal");
				dir.y = Input.GetAxisRaw("Vertical");
			}

			move = dir.normalized;

			// 2. Mouse Look polling (pixel delta with normal smooth FPS sensitivity)
			if (cursorInputForLook && (Cursor.lockState == CursorLockMode.Locked || cursorLocked))
			{
				Vector2 mouseDelta = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
				if (Mouse.current != null)
				{
					mouseDelta = Mouse.current.delta.ReadValue();
				}
				else
#endif
				{
					mouseDelta.x = Input.GetAxisRaw("Mouse X") * 10f;
					mouseDelta.y = Input.GetAxisRaw("Mouse Y") * 10f;
				}

				Vector2 targetLook = mouseDelta * 0.12f;
				// Responsive smoothing: eliminates discrete sub-pixel spikes while maintaining 0 perceived latency
				_smoothLook = Vector2.Lerp(_smoothLook, targetLook, 1f - Mathf.Exp(-45f * Time.deltaTime));
				if (targetLook == Vector2.zero && _smoothLook.sqrMagnitude < 0.0001f)
				{
					_smoothLook = Vector2.zero;
				}
				look = _smoothLook;
			}
			else
			{
				_smoothLook = Vector2.zero;
				look = Vector2.zero;
			}

			// 3. Jump & Sprint polling
#if ENABLE_INPUT_SYSTEM
			if (Keyboard.current != null)
			{
				if (Keyboard.current.spaceKey.wasPressedThisFrame) jump = true;
				sprint = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
			}
			else
#endif
			{
				if (Input.GetKeyDown(KeyCode.Space)) jump = true;
				sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			}
		}

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
#endif

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

#if !UNITY_IOS || !UNITY_ANDROID
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !newState;
		}
#endif
	}
}
