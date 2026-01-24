using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
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
        public bool aim;
        public bool shoot;

        [Header("ML snesing")]
        public bool enablePlayerInput = true;

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("ML-Agents")]
        public bool enablePlayerInput = true;

#if !UNITY_IOS || !UNITY_ANDROID
        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;
#endif

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED


        public void OnMove(InputValue value)
        {
            if (!enablePlayerInput) return;
            MoveInput(value.Get<Vector2>());
        }

        public void OnLook(InputValue value)
        {
            if (!enablePlayerInput) return;

            if (cursorInputForLook)
            {
                LookInput(value.Get<Vector2>());
            }
        }

        public void OnJump(InputValue value)
        {
            if (!enablePlayerInput) return;
            JumpInput(value.isPressed);
        }

        public void OnSprint(InputValue value)
        {
            if (!enablePlayerInput) return;
            SprintInput(value.isPressed);
        }

        public void OnAim(InputValue value)
        {
            if (!enablePlayerInput) return;
            AimInput(value.isPressed);
        }

        public void OnShoot(InputValue value)
        {
            if (!enablePlayerInput) return;
            ShootInput(value.isPressed);

        }

#endif

        // ====== ML / Player k�z�s API ======

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

        public void AimInput(bool newAimState)
        {
            aim = newAimState;
        }

        public void ShootInput(bool newShootState)
        {
            shoot = newShootState;
        }

#if !UNITY_IOS || !UNITY_ANDROID
        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorState(cursorLocked);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
#endif
    }
}
