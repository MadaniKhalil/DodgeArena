using UnityEngine;

namespace DodgeArena
{
    /// <summary>
    /// Fires balls from a random edge of the arena towards (roughly) the agent.
    /// Speed and fire rate come from the agent, which reads them from the
    /// curriculum (environment_parameters in the YAML file).
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        private const float SpawnEdge = 9.2f;
        private const float SpawnSpread = 8.5f;
        private const float BallHeight = 0.5f;
        private const float BallDiameter = 0.7f;

        [Tooltip("Material used for the spawned balls.")]
        public Material ballMaterial;

        [Tooltip("Random offset (metres) added to the aim point, so not every ball is a direct hit.")]
        public float aimNoise = 2f;

        private Transform agentTransform;
        private float ballSpeed = 6f;
        private float spawnInterval = 1f;
        private float timer;

        public void Configure(Transform agent, float speed, float interval)
        {
            agentTransform = agent;
            ballSpeed = speed;
            spawnInterval = Mathf.Max(0.1f, interval);
        }

        public void ResetBalls()
        {
            foreach (Ball ball in GetComponentsInChildren<Ball>())
            {
                Destroy(ball.gameObject);
            }
            // Short grace period at the start of each episode.
            timer = -1f;
        }

        private void FixedUpdate()
        {
            if (agentTransform == null)
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
            Vector3 localStart = RandomEdgePoint();
            Vector3 worldStart = transform.TransformPoint(localStart);

            Vector3 aim = agentTransform.position + new Vector3(
                Random.Range(-aimNoise, aimNoise), 0f, Random.Range(-aimNoise, aimNoise));
            Vector3 direction = aim - worldStart;
            direction.y = 0f;
            direction.Normalize();

            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Ball";
            ballObject.tag = Tags.Ball;
            ballObject.transform.SetParent(transform, false);
            ballObject.transform.position = worldStart;
            ballObject.transform.localScale = Vector3.one * BallDiameter;
            ballObject.GetComponent<Collider>().isTrigger = true;
            if (ballMaterial != null)
            {
                ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
            }

            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            ballObject.AddComponent<Ball>().Launch(direction * ballSpeed, transform);
        }

        private static Vector3 RandomEdgePoint()
        {
            float along = Random.Range(-SpawnSpread, SpawnSpread);
            switch (Random.Range(0, 4))
            {
                case 0: return new Vector3(along, BallHeight, SpawnEdge);
                case 1: return new Vector3(along, BallHeight, -SpawnEdge);
                case 2: return new Vector3(SpawnEdge, BallHeight, along);
                default: return new Vector3(-SpawnEdge, BallHeight, along);
            }
        }
    }
}
