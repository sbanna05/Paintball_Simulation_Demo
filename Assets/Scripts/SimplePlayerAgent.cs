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
    [SerializeField] private Transform aimTargetTransform;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] agentSpawnPoints;
    [SerializeField] private Transform[] targetSpawnPoints;

    private StarterAssetsInputs inputs;
    private CharacterController characterController;
    private RayPerceptionSensorComponent3D raySensor;

    private float maxEpisodeTime = 120f;
    private float episodeTimer;
    private bool isSpawning;
    private string targetTag = "Player_2";

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
    }

    public bool IsHeuristic() => StepCount <= 0 || CompletedEpisodes == 0;
    public void OnShotFired() => AddReward(-0.01f);

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        StartCoroutine(SafeSpawn());
    }

    private IEnumerator SafeSpawn()
    {
        isSpawning = true;
        if (characterController != null) characterController.enabled = false;

        yield return new WaitForFixedUpdate();

        // 1. Ágens teleportálása és forgatása
        if (agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        // 2. Célpont teleportálása
        if (targetPlayer != null && targetSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, targetSpawnPoints.Length);
            targetPlayer.SetPositionAndRotation(targetSpawnPoints[idx].position, targetSpawnPoints[idx].rotation);
        }

        // 3. Célzó gömb (IK Target) kényszerítése az ágens elé
        // Ez megakadályozza, hogy spawn után máshova nézzen
        if (aimTargetTransform != null)
        {
            aimTargetTransform.localPosition = new Vector3(0f, 1.5f, 10f);
        }

        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        if (characterController != null) characterController.enabled = true;
        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.forward); // 3
        if (targetPlayer != null)
        {
            Vector3 toTarget = targetPlayer.position - transform.position;
            sensor.AddObservation(toTarget.normalized); // 3
            sensor.AddObservation(toTarget.magnitude / 50f); // 1
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }
        sensor.AddObservation(aimTargetTransform.localPosition.y / 2.5f); // 1 (Célzó magassága)
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isSpawning) return;
        episodeTimer += Time.fixedDeltaTime;

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float rotateY = actions.ContinuousActions[2];
        float aimVertical = actions.ContinuousActions[3];

        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;


        if (inputs != null)
        {
            inputs.move = new Vector2(moveX, moveZ);
            inputs.aim = actions.DiscreteActions[0] == 1;
            inputs.shoot = actions.DiscreteActions[1] == 1;

            if (!IsHeuristic())
            {
                transform.Rotate(Vector3.up, rotateY * 200f * Time.deltaTime);
                Vector3 lp = aimTargetTransform.localPosition;
                lp.y = Mathf.Clamp(lp.y + aimVertical * 5f * Time.deltaTime, 0.5f, 3.0f);
                aimTargetTransform.localPosition = lp;
            }
        }

        float distance = targetPlayer != null ? Vector3.Distance(transform.position, targetPlayer.position) : 0f;

        if (distance < 3f)
        {
            AddReward(-0.01f);
        }
        else if (distance > 7f && distance < 15f)
        {
            AddReward(0.005f);
        }

        // Reward for looking at enemy
        if (IsEnemyVisible() && targetPlayer != null)
        {
            Vector3 dirToTarget = (targetPlayer.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToTarget);

            if (dot > 0.7f)
            {
                AddReward(dot * 0.03f);

                if (shouldAim)
                {
                    AddReward(0.02f);
                }
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-5f);
            Debug.Log("Episode timeout");
            EndEpisode();
        }
    }

    private bool IsEnemyVisible()
    {
        if (raySensor == null) return false;

        var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput()).RayOutputs;
        foreach (var ray in rayOutputs)
        {
            if (ray.HitGameObject != null)
            {
                if (ray.HitGameObject.CompareTag(targetTag) || ray.HitGameObject.CompareTag("NearMiss"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (isSpawning) return;

        if (tag == targetTag)
        {
            AddReward(15f);
            Debug.Log("<color=green>DIRECT HIT!</color>");
            EndEpisode();
        }
        else if (tag == "NearMiss")
        {
            AddReward(0.5f);
            Debug.Log("<color=yellow>NEAR MISS!</color>");
        }
        else
        {
            AddReward(-0.05f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Horizontal");
        cont[1] = Input.GetAxis("Vertical");
        cont[2] = Input.GetAxis("Mouse X");
        cont[3] = Input.GetAxis("Mouse Y");

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetMouseButton(1) ? 1 : 0; // Aim
        disc[1] = Input.GetMouseButton(0) ? 1 : 0; // Shoot
    }
}