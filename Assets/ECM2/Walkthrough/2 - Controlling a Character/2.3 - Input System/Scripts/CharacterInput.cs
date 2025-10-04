using UnityEngine;
using UnityEngine.InputSystem;

namespace ECM2.Walkthrough.Ex23
{
    /// <summary>
    /// This example shows how to make use of the new Input System,
    /// in particular, the PlayerInput component to control a character.
    /// </summary>
    
    public class CharacterInput : MonoBehaviour
    {
        /// <summary>
        /// Our controlled character.
        /// </summary>
        
        [Tooltip("Character to be controlled.\n" +
                 "If not assigned, this will look into this GameObject.")]
        [SerializeField]
        private Character _character;
        
        /// <summary>
        /// Current movement input values.
        /// </summary>
        
        private Vector2 _movementInput;
        
        /// <summary>
        /// Movement InputAction event handler.
        /// </summary>

        public void OnMove(InputAction.CallbackContext context)
        {
            _movementInput = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Jump InputAction event handler.
        /// </summary>

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started)
                _character.Jump();
            else if (context.canceled)
                _character.StopJumping();
        }

        /// <summary>
        /// Crouch InputAction event handler.
        /// </summary>

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.started)
                _character.Crouch();
            else if (context.canceled)
                _character.UnCrouch();
        }
        
        /// <summary>
        /// Handle polled input here (ie: movement, look, etc.)
        /// </summary>

        protected virtual void HandleInput()
        {
            // Compose a movement direction vector in world space

            Vector3 movementDirection = Vector3.zero;

            movementDirection += Vector3.forward * _movementInput.y;
            movementDirection += Vector3.right * _movementInput.x;

            // If character has a camera assigned,
            // make movement direction relative to this camera view direction

            if (_character.camera)
            {               
                movementDirection 
                    = movementDirection.relativeTo(_character.cameraTransform);
            }
        
            // Set character's movement direction vector

            _character.SetMovementDirection(movementDirection);
        }

        protected virtual void Awake()
        {
            // If character not assigned, attempts to cache from this current GameObject
            
            if (_character == null)
                _character = GetComponent<Character>();
        }

        protected virtual void Update()
        {
            HandleInput();
        }
    }
}
