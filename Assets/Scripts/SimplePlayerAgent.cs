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
    [SerializeField] private float maxEpisodeTime = 120f;

    private StarterAssetsInputs inputs;
    private RayPerceptionSensorComponent3D raySensor;
    private CharacterController characterController;

    private string targetTag = "Player_2";
    private float episodeTimer;
    private bool isSpawning;

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        characterController = GetComponent<CharacterController>();
    }

    public bool IsHeuristic() => StepCount <= 0 || CompletedEpisodes == 0;

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        StartCoroutine(SafeSpawn());
    }

    private IEnumerator SafeSpawn()
    {
        isSpawning = true;

        if (agentSpawnPoints != null && agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            if (characterController != null) characterController.enabled = false;

            transform.position = agentSpawnPoints[idx].position;
            transform.rotation = agentSpawnPoints[idx].rotation;

            if (characterController != null) characterController.enabled = true;
        }

        // Célpont (Target) teleportálása
        if (targetPlayer != null && targetSpawnPoints != null && targetSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, targetSpawnPoints.Length);
            targetPlayer.position = targetSpawnPoints[idx].position;
            targetPlayer.rotation = targetSpawnPoints[idx].rotation;
        }

        // Fizikai motor frissítése, hogy az ütközõk ne maradjanak a régi helyükön
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Alapvetõ irányok és távolságok (6 observation)
        sensor.AddObservation(transform.forward);

        if (targetPlayer != null)
        {
            Vector3 toTarget = (targetPlayer.position - transform.position);
            sensor.AddObservation(toTarget.normalized);
            sensor.AddObservation(toTarget.magnitude / 50f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isSpawning) return;

        episodeTimer += Time.fixedDeltaTime;
        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        // --- Mozgás és Irányítás ---
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float rotateY = actions.ContinuousActions[2];

        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;

        if (inputs != null)
        {
            inputs.move = new Vector2(moveX, moveZ);
            // Az ágens forgatása (Mouse X helyett ML action)
            transform.Rotate(Vector3.up, rotateY * 5f);

            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot;
        }

        // 1. Büntetés, ha túl közel megy
        if (distance < 3f)
        {
            AddReward(-0.01f); 
        }
        
        else if (distance > 7f && distance < 15f)
        {
            AddReward(0.005f); 
        }

        if (shouldShoot)
        {
            AddReward(-0.01f); 
        }

        AddReward(-0.0001f);

        if (IsEnemyVisible())
        {
            Vector3 dirToTarget = (targetPlayer.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToTarget);

            // Minél pontosabban néz rá, annál több pont (0.0 és 0.1 között)
            if (dot > 0)
            {
                AddReward(dot * 0.05f);
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-5f);
            Debug.Log("timeout");
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
            AddReward(12f); // Nagy jutalom a gyõzelemért
            Debug.Log("<color=green>TALÁLAT!</color>");
            EndEpisode();
        }
        else if (tag == "NearMiss")
        {
            AddReward(0.3f); 
            Debug.Log("<color=yellow>MAJDNEM! (Near Miss)</color>");
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

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
    }
}