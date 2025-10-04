using UnityEngine;
using UnityEngine.InputSystem;

namespace ECM2.Walkthrough.Ex25
{
    /// <summary>
    /// This example shows how to make use of the new Input System,
    /// in particular, the PlayerInput component to control a Third Person Character.
    /// </summary>
    /// 
    public class ThirdPersonInput : MonoBehaviour
    {
        [Tooltip("Character to be controlled.\n" +
                 "If not assigned, this will look into this GameObject.")]
        [SerializeField]
        private ThirdPersonCharacter _character;
        
        [Space(15.0f)]
        public bool invertLook = true;

        [Tooltip("Mouse look sensitivity")]
        public Vector2 sensitivity = new Vector2(0.05f, 0.05f);

        [Space(15.0f)]
        [Tooltip("How far in degrees can you move the camera down.")]
        public float minPitch = -80.0f;

        [Tooltip("How far in degrees can you move the camera up.")]
        public float maxPitch = 80.0f;
        
        /// <summary>
        /// Current movement input values.
        /// </summary>
        
        private Vector2 _movementInput;
        
        /// <summary>
        /// Current look input values.
        /// </summary>
        
        private Vector2 _lookInput;
        
        /// <summary>
        /// Current look input values.
        /// </summary>
        
        private Vector2 _zoomInput;
        
        /// <summary>
        /// Movement InputAction event handler.
        /// </summary>

        public void OnMove(InputAction.CallbackContext context)
        {
            _movementInput = context.ReadValue<Vector2>();
        }
        
        /// <summary>
        /// Look InputAction event handler.
        /// </summary>

        public void OnLook(InputAction.CallbackContext context)
        {
            _lookInput = context.ReadValue<Vector2>();
        }
        
        /// <summary>
        /// Zoom InputAction event handler.
        /// </summary>

        public void OnZoom(InputAction.CallbackContext context)
        {
            _zoomInput = context.ReadValue<Vector2>();
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
            // Camera look
            
            Vector2 lookInput = _lookInput * sensitivity;

            _character.AddControlYawInput(lookInput.x);
            _character.AddControlPitchInput(invertLook ? -lookInput.y : lookInput.y, minPitch, maxPitch);
            
            // Zoom input
            
            Vector2 zoomInput = _zoomInput * sensitivity;
            _character.AddControlZoomInput(zoomInput.y);
            
            // Compose a movement direction vector relative to view direction

            Vector3 movementDirection = Vector3.zero;
            
            movementDirection += Vector3.right * _movementInput.x;
            movementDirection += Vector3.forward * _movementInput.y;

            if (_character.cameraTransform)
                movementDirection = movementDirection.relativeTo(_character.cameraTransform, _character.GetUpVector());
        
            // Set character's movement direction vector

            _character.SetMovementDirection(movementDirection);
        }

        protected virtual void Awake()
        {
            // If character not assigned, attempts to cache from this current GameObject
            
            if (_character == null)
                _character = GetComponent<ThirdPersonCharacter>();
        }

        protected void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        protected virtual void Update()
        {
            HandleInput();
        }
    }
}
