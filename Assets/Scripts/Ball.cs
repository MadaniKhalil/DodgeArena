using UnityEngine;

namespace DodgeArena
{
    /// <summary>
    /// A projectile that flies in a straight line across the arena.
    /// Uses a kinematic Rigidbody + trigger collider so it never pushes anything physically;
    /// the agent detects hits through OnTriggerEnter.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Ball : MonoBehaviour
    {
        private const float MaxLifetimeSeconds = 10f;
        private const float AreaHalfSize = 11f;

        private Vector3 velocity;
        private Rigidbody body;
        private Transform areaRoot;
        private float age;

        public void Launch(Vector3 worldVelocity, Transform area)
        {
            velocity = worldVelocity;
            areaRoot = area;
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);

            Vector3 local = areaRoot.InverseTransformPoint(transform.position);
            bool isOutside = Mathf.Abs(local.x) > AreaHalfSize || Mathf.Abs(local.z) > AreaHalfSize;
            if (isOutside || age > MaxLifetimeSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Balls break on walls and obstacles, so obstacles can be used as cover.
            if (other.CompareTag(Tags.Wall))
            {
                Destroy(gameObject);
            }
        }
    }
}
