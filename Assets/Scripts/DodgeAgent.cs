using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgeArena
{
    /// <summary>
    /// The agent must collect as many targets (yellow) as possible while dodging
    /// incoming balls (red) and moving around obstacles (grey blocks).
    ///
    /// Observations:
    ///   - RayPerceptionSensor3D (separate component): sees Target, Ball, Wall tags, stacked 3 frames
    ///     so the network can infer ball movement.
    ///   - 5 vector observations (below).
    /// Actions (continuous, 2): [0] move forward/back, [1] rotate left/right.
    /// Rewards:
    ///   +1     collecting a target (a new one spawns, episode continues)
    ///   -1     getting hit by a ball (episode ends)
    ///   -0.005 per step while touching a wall/obstacle
    ///   -1/MaxStep per step (time pressure)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DodgeAgent : Agent
    {
        private const float ArenaDiagonal = 28.3f;
        private const float TargetReward = 1f;
        private const float HitPenalty = -1f;
        private const float WallPenalty = -0.005f;
        private const float SpawnRange = 8f;
        private const float ObstacleRange = 6f;
        private const float MinObstacleDistance = 2.5f;
        private const int MaxPlacementTries = 50;

        [Header("Scene references")]
        public Transform target;
        public Transform[] obstacles;
        public BallSpawner spawner;

        [Header("Movement")]
        public float moveSpeed = 6f;
        public float turnSpeed = 200f;

        [Header("Difficulty")]
        public float ballSpeed = 5f;
        [Tooltip("Seconds between two balls.")]
        public float spawnInterval = 1.5f;

        private Rigidbody body;

        public override void Initialize()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void OnEpisodeBegin()
        {
            PlaceObstacles();

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            transform.localPosition = FindFreeSpot(0.5f);
            transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Physics.SyncTransforms();

            MoveTarget();
            spawner.Configure(transform, ballSpeed, spawnInterval);
            spawner.ResetBalls();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            sensor.AddObservation(localVelocity.x / moveSpeed);
            sensor.AddObservation(localVelocity.z / moveSpeed);

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);
            sensor.AddObservation(localDirection.x);
            sensor.AddObservation(localDirection.z);
            sensor.AddObservation(toTarget.magnitude / ArenaDiagonal);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            float move = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float turn = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

            // Only the action may rotate the agent: cancel any spin caused by bumping into things.
            body.angularVelocity = Vector3.zero;
            Quaternion rotation = Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
            body.MoveRotation(body.rotation * rotation);

            Vector3 velocity = transform.forward * (move * moveSpeed);
            velocity.y = body.linearVelocity.y;
            body.linearVelocity = velocity;

            AddReward(-1f / Mathf.Max(1, MaxStep));
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Manual control for testing: W/S = move, A/D = rotate.
            ActionSegment<float> continuous = actionsOut.ContinuousActions;
            continuous[0] = Input.GetAxis("Vertical");
            continuous[1] = Input.GetAxis("Horizontal");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(Tags.Target))
            {
                AddReward(TargetReward);
                MoveTarget();
            }
            else if (other.CompareTag(Tags.Ball))
            {
                AddReward(HitPenalty);
                EndEpisode();
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.collider.CompareTag(Tags.Wall))
            {
                AddReward(WallPenalty);
            }
        }

        private void MoveTarget()
        {
            target.localPosition = FindFreeSpot(0.5f);
        }

        private void PlaceObstacles()
        {
            foreach (Transform obstacle in obstacles)
            {
                obstacle.localPosition = new Vector3(
                    Random.Range(-ObstacleRange, ObstacleRange), 0.75f, Random.Range(-ObstacleRange, ObstacleRange));
                obstacle.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            }
            Physics.SyncTransforms();
        }

        /// <summary>Random point on the floor that is not inside or next to an obstacle.</summary>
        private Vector3 FindFreeSpot(float height)
        {
            Vector3 candidate = Vector3.up * height;
            for (int attempt = 0; attempt < MaxPlacementTries; attempt++)
            {
                candidate = new Vector3(Random.Range(-SpawnRange, SpawnRange), height, Random.Range(-SpawnRange, SpawnRange));
                if (IsFarFromObstacles(candidate))
                {
                    return candidate;
                }
            }
            return candidate;
        }

        private bool IsFarFromObstacles(Vector3 localPoint)
        {
            foreach (Transform obstacle in obstacles)
            {
                Vector3 offset = obstacle.localPosition - localPoint;
                offset.y = 0f;
                if (offset.magnitude < MinObstacleDistance)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
