using UnityEngine;
using UnityEngine.InputSystem;

namespace WrestleGame
{
    public class WrestlingCamera : MonoBehaviour
    {
        public static WrestlingCamera Instance { get; private set; }

        public CameraViewMode currentMode = CameraViewMode.DynamicAction;

        [Header("Targets")]
        public Transform fighter1;
        public Transform fighter2;
        public Vector3 ringCenter = new Vector3(4.55f, 0.65f, -0.09f);

        [Header("Dynamic Action Cam Settings")]
        public float minDistance = 3.8f;
        public float maxDistance = 6.8f;
        public float minHeight = 1.6f;
        public float maxHeight = 2.4f;
        public float followSmoothTime = 0.22f;
        public float fov = 50f;

        [Header("Orbit Cam Settings")]
        public float orbitSpeed = 120f;
        public float orbitDistance = 5.8f;
        private float orbitYaw = 0f;
        private float orbitPitch = 18f;

        private Vector3 currentVelocity;
        private float shakeIntensity = 0f;
        private float shakeDecay = 4.0f;
        private Camera cam;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        private void Start()
        {
            FindFightersIfNull();
            var ring = GameObject.Find("Ring");
            if (ring != null) ringCenter = ring.transform.position;
        }

        public void FindFightersIfNull()
        {
            if (fighter1 == null) fighter1 = GameObject.Find("Wrestler1")?.transform;
            if (fighter2 == null) fighter2 = GameObject.Find("Wrestler2")?.transform;
        }

        public void SetMode(CameraViewMode mode)
        {
            currentMode = mode;
            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnCombatText("CAMERA: " + mode.ToString(), ringCenter + Vector3.up * 2f, Color.cyan, false, "CameraMode");
            }
        }

        public void NextCameraMode()
        {
            int next = ((int)currentMode + 1) % 5;
            SetMode((CameraViewMode)next);
        }

        public void TriggerShake(float intensity = 0.35f)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        }

        private void LateUpdate()
        {
            FindFightersIfNull();
            HandleInput();

            Vector3 targetPos = transform.position;
            Quaternion targetRot = transform.rotation;

            switch (currentMode)
            {
                case CameraViewMode.DynamicAction:
                    CalculateDynamicActionCam(out targetPos, out targetRot);
                    break;
                case CameraViewMode.ThirdPersonP1:
                    CalculateThirdPersonCam(out targetPos, out targetRot);
                    break;
                case CameraViewMode.RingsideDramatic:
                    CalculateRingsideCam(out targetPos, out targetRot);
                    break;
                case CameraViewMode.OrbitFree:
                    CalculateOrbitCam(out targetPos, out targetRot);
                    break;
                case CameraViewMode.TopDownArena:
                    CalculateTopDownCam(out targetPos, out targetRot);
                    break;
            }

            // Apply camera shake
            if (shakeIntensity > 0.001f)
            {
                Vector3 shakeOffset = new Vector3(
                    Random.Range(-1f, 1f) * shakeIntensity,
                    Random.Range(-1f, 1f) * shakeIntensity * 0.7f,
                    Random.Range(-1f, 1f) * shakeIntensity * 0.5f
                );
                targetPos += shakeOffset;
                shakeIntensity = Mathf.Max(0f, shakeIntensity - Time.deltaTime * shakeDecay);
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, followSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.vKey.wasPressedThisFrame)
            {
                NextCameraMode();
            }

            if (currentMode == CameraViewMode.OrbitFree)
            {
                if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    orbitYaw += delta.x * 0.25f;
                    orbitPitch = Mathf.Clamp(orbitPitch - delta.y * 0.25f, 5f, 75f);
                }

                if (Keyboard.current.leftArrowKey.isPressed) orbitYaw -= orbitSpeed * Time.deltaTime;
                if (Keyboard.current.rightArrowKey.isPressed) orbitYaw += orbitSpeed * Time.deltaTime;
                if (Keyboard.current.upArrowKey.isPressed) orbitPitch = Mathf.Clamp(orbitPitch + orbitSpeed * 0.5f * Time.deltaTime, 5f, 75f);
                if (Keyboard.current.downArrowKey.isPressed) orbitPitch = Mathf.Clamp(orbitPitch - orbitSpeed * 0.5f * Time.deltaTime, 5f, 75f);

                if (Mouse.current != null)
                {
                    float scroll = Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        orbitDistance = Mathf.Clamp(orbitDistance - Mathf.Sign(scroll) * 0.6f, 3.5f, 12f);
                    }
                }
            }
        }

        private void CalculateDynamicActionCam(out Vector3 pos, out Quaternion rot)
        {
            Vector3 p1 = fighter1 != null ? fighter1.position : ringCenter + Vector3.left;
            Vector3 p2 = fighter2 != null ? fighter2.position : ringCenter + Vector3.right;

            Vector3 midPoint = (p1 + p2) * 0.5f;
            float distanceBetween = Vector3.Distance(p1, p2);
            float distFactor = Mathf.Clamp01((distanceBetween - 0.8f) / 3.0f);

            float camDist = Mathf.Lerp(minDistance, maxDistance, distFactor);
            float camHeight = Mathf.Lerp(minHeight, maxHeight, distFactor);

            // Vector perpendicular to fighters line with smooth backward bias
            Vector3 fightersDir = (p2 - p1);
            fightersDir.y = 0;
            if (fightersDir.sqrMagnitude < 0.01f) fightersDir = Vector3.right;

            Vector3 normal = Vector3.Cross(fightersDir.normalized, Vector3.up);
            if (Vector3.Dot(normal, Vector3.back) < 0) normal = -normal;

            Vector3 viewOffset = Vector3.Lerp(Vector3.back, normal, 0.25f).normalized;

            pos = midPoint + viewOffset * camDist + Vector3.up * camHeight;
            Vector3 lookTarget = midPoint + Vector3.up * 0.65f;
            rot = Quaternion.LookRotation(lookTarget - pos);
        }

        private void CalculateThirdPersonCam(out Vector3 pos, out Quaternion rot)
        {
            Transform leader = fighter1 != null ? fighter1 : fighter2;
            Transform target = fighter2 != null ? fighter2 : fighter1;

            if (leader == null)
            {
                pos = ringCenter + Vector3.back * 5f + Vector3.up * 2f;
                rot = Quaternion.LookRotation(ringCenter - pos);
                return;
            }

            Vector3 forward = (target != null && target != leader) ? (target.position - leader.position).normalized : leader.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

            pos = leader.position - forward * 3.4f + Vector3.up * 1.8f;
            Vector3 lookTarget = leader.position + forward * 1.2f + Vector3.up * 0.65f;
            rot = Quaternion.LookRotation(lookTarget - pos);
        }

        private void CalculateRingsideCam(out Vector3 pos, out Quaternion rot)
        {
            Vector3 p1 = fighter1 != null ? fighter1.position : ringCenter;
            Vector3 p2 = fighter2 != null ? fighter2.position : ringCenter;
            Vector3 midPoint = (p1 + p2) * 0.5f;

            pos = ringCenter + new Vector3(-2.6f, 0.45f, -3.4f);
            Vector3 lookTarget = midPoint + Vector3.up * 0.75f;
            rot = Quaternion.LookRotation(lookTarget - pos);
        }

        private void CalculateOrbitCam(out Vector3 pos, out Quaternion rot)
        {
            Vector3 p1 = fighter1 != null ? fighter1.position : ringCenter;
            Vector3 p2 = fighter2 != null ? fighter2.position : ringCenter;
            Vector3 midPoint = (p1 + p2) * 0.5f;

            Quaternion orbitRot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 offset = orbitRot * new Vector3(0, 0, -orbitDistance);
            pos = midPoint + offset + Vector3.up * 0.5f;
            rot = Quaternion.LookRotation((midPoint + Vector3.up * 0.5f) - pos);
        }

        private void CalculateTopDownCam(out Vector3 pos, out Quaternion rot)
        {
            Vector3 p1 = fighter1 != null ? fighter1.position : ringCenter;
            Vector3 p2 = fighter2 != null ? fighter2.position : ringCenter;
            Vector3 midPoint = (p1 + p2) * 0.5f;

            pos = midPoint + new Vector3(0, 6.5f, -0.8f);
            rot = Quaternion.Euler(75f, 0f, 0f);
        }
    }
}

