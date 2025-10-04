using UnityEngine;

namespace ECM2.Walkthrough.Ex25
{
    public class ThirdPersonCharacter : Character
    {
        [Space(15.0f)]
        public GameObject followTarget;
        
        [Tooltip("The default distance behind the Follow target.")]
        [SerializeField]
        public float followDistance = 5.0f;

        [Tooltip("The minimum distance to Follow target.")]
        [SerializeField]
        public float followMinDistance;

        [Tooltip("The maximum distance to Follow target.")]
        [SerializeField]
        public float followMaxDistance = 10.0f;
        
        protected float _cameraYaw;
        protected float _cameraPitch;

        protected float _currentFollowDistance;
        protected float _followDistanceSmoothVelocity;
        
        /// <summary>
        /// Add input (affecting Yaw).
        /// This is applied to the camera's rotation.
        /// </summary>

        public virtual void AddControlYawInput(float value)
        {
            _cameraYaw = MathLib.ClampAngle(_cameraYaw + value, -180.0f, 180.0f);
        }
        
        /// <summary>
        /// Add input (affecting Pitch).
        /// This is applied to the camera's rotation.
        /// </summary>

        public virtual void AddControlPitchInput(float value, float minValue = -80.0f, float maxValue = 80.0f)
        {
            _cameraPitch = MathLib.ClampAngle(_cameraPitch + value, minValue, maxValue);
        }
        
        /// <summary>
        /// Adds input (affecting follow distance).
        /// </summary>

        public virtual void AddControlZoomInput(float value)
        {
            followDistance = Mathf.Clamp(followDistance - value, followMinDistance, followMaxDistance);
        }
        
        /// <summary>
        /// Update camera's rotation applying current _cameraPitch and _cameraYaw values.
        /// </summary>

        protected virtual void UpdateCameraRotation()
        {
            cameraTransform.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0.0f);
        }
        
        /// <summary>
        /// Update camera's position maintaining _currentFollowDistance from target. 
        /// </summary>

        protected virtual void UpdateCameraPosition()
        {
            _currentFollowDistance =
                Mathf.SmoothDamp(_currentFollowDistance, followDistance, ref _followDistanceSmoothVelocity, 0.2f);

            cameraTransform.position =
                followTarget.transform.position - cameraTransform.forward * _currentFollowDistance;
        }
        
        /// <summary>
        /// Update camera's position and rotation.
        /// </summary>

        protected virtual void UpdateCamera()
        {
            UpdateCameraRotation();
            UpdateCameraPosition();
        }

        protected override void Start()
        {
            base.Start();
            
            Vector3 eulerAngles = cameraTransform.eulerAngles;

            _cameraPitch = eulerAngles.x;
            _cameraYaw = eulerAngles.y;

            _currentFollowDistance = followDistance;
        }

        private void LateUpdate()
        {
            UpdateCamera();
        }
    }
}
