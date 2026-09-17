using UnityEngine;

namespace MassRPG.Client.Camera
{
    /// <summary>
    /// Restricted MassRPG camera: fixed oblique orientation with smooth bounded zoom and follow.
    /// Input is deliberately injected through ApplyZoomDelta so this controller is not tied to a
    /// specific Unity input package. There is no free orbit or over-the-shoulder mode.
    /// </summary>
    public sealed class BoundedObliqueCameraRig : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Transform followTarget;
        [SerializeField] private float yawDegrees = 45f;
        [SerializeField] private float pitchDegrees = 55f;
        [SerializeField] private float minimumDistance = 7f;
        [SerializeField] private float maximumDistance = 24f;
        [SerializeField] private float startingDistance = 13f;
        [SerializeField] private float zoomSensitivity = 1.5f;
        [SerializeField] private float followSharpness = 14f;
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);

        private float _desiredDistance;
        private bool _initialized;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        public float DesiredDistance => _desiredDistance;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponentInChildren<UnityEngine.Camera>();
            InitializeDistance();
        }

        private void OnValidate()
        {
            minimumDistance = Mathf.Max(0.1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            startingDistance = Mathf.Clamp(startingDistance, minimumDistance, maximumDistance);
            pitchDegrees = Mathf.Clamp(pitchDegrees, 15f, 85f);
            followSharpness = Mathf.Max(0f, followSharpness);
            zoomSensitivity = Mathf.Max(0.01f, zoomSensitivity);
        }

        public void ApplyZoomDelta(float delta)
        {
            InitializeDistance();
            _desiredDistance = Mathf.Clamp(
                _desiredDistance - delta * zoomSensitivity,
                minimumDistance,
                maximumDistance);
        }

        public void SetNormalizedZoom(float normalized)
        {
            InitializeDistance();
            _desiredDistance = Mathf.Lerp(minimumDistance, maximumDistance, Mathf.Clamp01(normalized));
        }

        private void LateUpdate()
        {
            if (targetCamera == null || followTarget == null) return;
            InitializeDistance();

            var rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var focus = followTarget.position + lookAtOffset;
            var desiredPosition = focus - rotation * Vector3.forward * _desiredDistance;
            var t = followSharpness <= 0f ? 1f : 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);

            targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, desiredPosition, t);
            targetCamera.transform.rotation = rotation;
        }

        private void InitializeDistance()
        {
            if (_initialized) return;
            _desiredDistance = Mathf.Clamp(startingDistance, minimumDistance, maximumDistance);
            _initialized = true;
        }
    }
}
