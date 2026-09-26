using UnityEngine;

namespace WrestleGame
{
    public class WrestlingRing : MonoBehaviour
    {
        public static WrestlingRing Instance { get; private set; }

        [Header("Ring Dimensions")]
        public Vector3 ringCenter = new Vector3(4.55f, 0.65f, -0.09f);
        public float halfWidthX = 1.75f;  // Strict inner ropes bound X
        public float halfDepthZ = 1.35f;  // Strict inner ropes bound Z
        public float matStandingY = 1.12f; // Exact standing height on the canvas

        [Header("Ropes Physics")]
        public float ropeBounceForce = 4.2f;
        public float ropeTriggerMargin = 0.1f;

        private float ropeShakeTime = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            var ringGo = GameObject.Find("Ring");
            if (ringGo != null)
            {
                ringCenter = ringGo.transform.position;
            }
        }

        private void Update()
        {
            if (ropeShakeTime > 0f)
            {
                ropeShakeTime -= Time.deltaTime;
            }
        }

        public Vector3 ClampPosition(Vector3 pos, float bodyRadius = 0.25f, bool lockToMat = true)
        {
            float minX = ringCenter.x - halfWidthX + bodyRadius;
            float maxX = ringCenter.x + halfWidthX - bodyRadius;
            float minZ = ringCenter.z - halfDepthZ + bodyRadius;
            float maxZ = ringCenter.z + halfDepthZ - bodyRadius;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            if (lockToMat)
            {
                pos.y = matStandingY;
            }
            return pos;
        }

        public bool CheckRopeCollision(Vector3 pos, Vector3 moveDir, float bodyRadius, out Vector3 bounceDirection)
        {
            bounceDirection = Vector3.zero;
            float minX = ringCenter.x - halfWidthX + bodyRadius;
            float maxX = ringCenter.x + halfWidthX - bodyRadius;
            float minZ = ringCenter.z - halfDepthZ + bodyRadius;
            float maxZ = ringCenter.z + halfDepthZ - bodyRadius;

            bool hit = false;

            if (pos.x <= minX + ropeTriggerMargin && moveDir.x < -0.1f)
            {
                bounceDirection += Vector3.right;
                hit = true;
            }
            else if (pos.x >= maxX - ropeTriggerMargin && moveDir.x > 0.1f)
            {
                bounceDirection += Vector3.left;
                hit = true;
            }

            if (pos.z <= minZ + ropeTriggerMargin && moveDir.z < -0.1f)
            {
                bounceDirection += Vector3.forward;
                hit = true;
            }
            else if (pos.z >= maxZ - ropeTriggerMargin && moveDir.z > 0.1f)
            {
                bounceDirection += Vector3.back;
                hit = true;
            }

            if (hit)
            {
                bounceDirection = bounceDirection.normalized;
                TriggerRopeShake();
            }

            return hit;
        }

        public void TriggerRopeShake()
        {
            ropeShakeTime = 0.35f;
        }

        public Vector3 GetSpawnPosition(bool isPlayer1)
        {
            float xOffset = isPlayer1 ? -0.85f : 0.85f;
            return new Vector3(ringCenter.x + xOffset, matStandingY, ringCenter.z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 size = new Vector3(halfWidthX * 2f, 0.1f, halfDepthZ * 2f);
            Gizmos.DrawWireCube(new Vector3(ringCenter.x, matStandingY, ringCenter.z), size);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(new Vector3(ringCenter.x, matStandingY + 0.4f, ringCenter.z), size);
            Gizmos.DrawWireCube(new Vector3(ringCenter.x, matStandingY + 0.7f, ringCenter.z), size);
        }
    }
}



