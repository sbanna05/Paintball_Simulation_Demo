using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using StarterAssets;
using System.Collections;

public class SimplePlayerAgent : Agent
{
    [Header("References")]
    [SerializeField] private Transform targetPlayer;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] agentSpawnPoints;
    [SerializeField] private Transform[] targetSpawnPoints;

    [Header("Episode Settings")]
    [SerializeField] private float maxEpisodeTime = 60f; // CSÖKKENTVE 150->60!
    [SerializeField] private float stalemateWarningTime = 40f;

    [Header("Tags")]
    [SerializeField] private string targetTag = "Player_2";
    [SerializeField] private string nearMissTag = "NearMiss";

    // Components
    private StarterAssetsInputs inputs;
    private CharacterController characterController;

    // Episode state
    private float episodeTimer;
    private bool episodeEnding;

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        episodeEnding = false;

        // Reset inputs immediately
        if (inputs != null)
        {
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.aim = false;
            inputs.shoot = false;
        }

        // Use coroutine for safe spawn
        StartCoroutine(SafeSpawn());
    }

    private IEnumerator SafeSpawn()
    {
        // Disable CharacterController
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Wait one frame
        yield return null;

        // Spawn agent
        if (agentSpawnPoints != null && agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            if (agentSpawnPoints[idx] != null)
            {
                Vector3 pos = agentSpawnPoints[idx].position;
                pos.y += 0.5f; // Higher safety offset
                transform.position = pos;
                transform.rotation = agentSpawnPoints[idx].rotation;
            }
        }

        // Spawn target
        if (targetPlayer != null && targetSpawnPoints != null && targetSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, targetSpawnPoints.Length);
            if (targetSpawnPoints[idx] != null)
            {
                targetPlayer.position = targetSpawnPoints[idx].position;
                targetPlayer.rotation = targetSpawnPoints[idx].rotation;
            }
        }

        // Wait another frame
        yield return null;

        // Re-enable CharacterController
        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 8 observations
        sensor.AddObservation(transform.forward); // 3

        if (targetPlayer != null)
        {
            Vector3 toTarget = (targetPlayer.position - transform.position).normalized;
            sensor.AddObservation(toTarget); // 3

            float distance = Vector3.Distance(transform.position, targetPlayer.position);
            sensor.AddObservation(distance / 50f); // 1
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // 3
            sensor.AddObservation(1f); // 1
        }

        sensor.AddObservation(IsTargetVisible() ? 1f : 0f); // 1
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (episodeEnding)
            return;

        episodeTimer += Time.fixedDeltaTime;

        // Timeout check
        if (episodeTimer >= maxEpisodeTime)
        {
            if (!episodeEnding)
            {
                episodeEnding = true;
                AddReward(-0.5f);
                Debug.Log("timeout");
                EndEpisode();
            }
            return;
        }

        // Actions
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotateY = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;

        // Apply inputs
        if (inputs != null)
        {
            inputs.move = new Vector2(moveX, moveZ);
            inputs.look = new Vector2(rotateY * 2f, 0f);
            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot;
        }
                
        AddReward(-0.001f); // Time penalty

        // Reward for aiming at target
        if (targetPlayer != null && IsTargetVisible())
        {
            Vector3 dirToTarget = (targetPlayer.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToTarget);

            if (dot > 0.9f && shouldAim)
            {
                AddReward(0.005f);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Horizontal");
        continuous[1] = Input.GetAxis("Vertical");
        continuous[2] = Input.GetAxis("Mouse X");

        var discrete = actionsOut.DiscreteActions;
        discrete[0] = Input.GetMouseButton(1) ? 1 : 0;
        discrete[1] = Input.GetMouseButton(0) ? 1 : 0;
    }

    public void RegisterHit(string tag)
    {
        if (episodeEnding)
            return;

        if (tag == targetTag)
        {
            episodeEnding = true;
            float bonus = (maxEpisodeTime - episodeTimer) / maxEpisodeTime;
            AddReward(1.0f + bonus);
            Debug.Log("win");
            EndEpisode();
        }
        else if (tag == nearMissTag)
        {
            AddReward(0.6f);
            Debug.Log("nearmiss");
        }
        else
        {
            AddReward(-0.8f);
        }
    }

    private bool IsTargetVisible()
    {
        if (targetPlayer == null)
            return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = (targetPlayer.position - origin).normalized;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 50f))
        {
            return hit.collider != null && hit.collider.CompareTag(targetTag);
        }

        return false;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}