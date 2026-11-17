using UnityEngine;

namespace DodgeArena
{
    /// <summary>
    /// Fires a ball from a random edge of the arena towards the agent every few seconds.
    /// The agent tells the spawner how fast and how often to fire (see DodgeAgent).
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        private const float SpawnEdge = 9.2f;      // distance from the arena centre to where balls appear
        private const float SpawnSpread = 8.5f;    // how far along that edge a ball can appear
        private const float BallHeight = 0.5f;
        private const float BallDiameter = 0.7f;
        private const float TrailSeconds = 0.35f;  // how long the trail behind a ball is

        [Tooltip("Material used for the balls.")]
        public Material ballMaterial;

        [Tooltip("Random offset (metres) added to the aim, so not every ball is a direct hit.")]
        public float aimNoise = 2f;

        private Transform agent;
        private float ballSpeed;
        private float spawnInterval;
        private float timer;
        private Material trailMaterial;

        public void Configure(Transform agentTransform, float speed, float interval)
        {
            agent = agentTransform;
            ballSpeed = speed;
            spawnInterval = interval;
        }

        public void ResetBalls()
        {
            foreach (Ball ball in GetComponentsInChildren<Ball>())
            {
                Destroy(ball.gameObject);
            }

            // Start below zero so the agent gets one extra second before the first ball.
            timer = -1f;
        }

        private void FixedUpdate()
        {
            if (agent == null)
            {
                return;
            }

            timer += Time.fixedDeltaTime;
            if (timer >= spawnInterval)
            {
                timer = 0f;
                SpawnBall();
            }
        }

        private void SpawnBall()
        {
            Vector3 start = transform.TransformPoint(RandomEdgePoint());

            // Aim at the agent, plus a little randomness.
            Vector3 aim = agent.position + new Vector3(Random.Range(-aimNoise, aimNoise), 0f, Random.Range(-aimNoise, aimNoise));
            Vector3 direction = aim - start;
            direction.y = 0f;
            direction.Normalize();

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.tag = Tags.Ball;
            ball.transform.SetParent(transform, false);
            ball.transform.position = start;
            ball.transform.localScale = Vector3.one * BallDiameter;
            ball.GetComponent<Collider>().isTrigger = true;
            ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;

            Rigidbody body = ball.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            AddTrail(ball);
            ball.AddComponent<Ball>().Launch(direction * ballSpeed, transform);
        }

        /// <summary>Visual only: a short fading line behind the ball. The agent cannot see it.</summary>
        private void AddTrail(GameObject ball)
        {
            if (trailMaterial == null)
            {
                trailMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            Color color = ballMaterial.color;
            TrailRenderer trail = ball.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = TrailSeconds;
            trail.startWidth = BallDiameter;
            trail.endWidth = 0f;
            trail.startColor = new Color(color.r, color.g, color.b, 0.6f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A random point on one of the four edges of the arena.</summary>
        private static Vector3 RandomEdgePoint()
        {
            float along = Random.Range(-SpawnSpread, SpawnSpread);
            int edge = Random.Range(0, 4);

            if (edge == 0) return new Vector3(along, BallHeight, SpawnEdge);   // north
            if (edge == 1) return new Vector3(along, BallHeight, -SpawnEdge);  // south
            if (edge == 2) return new Vector3(SpawnEdge, BallHeight, along);   // east
            return new Vector3(-SpawnEdge, BallHeight, along);                 // west
        }
    }
}
