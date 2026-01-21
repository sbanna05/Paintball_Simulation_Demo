using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using StarterAssets;
using System.Collections;

public class SimplePlayerAgent : Agent
{
    [Header("References")]
    [SerializeField] private SimplePlayerAgent opponentAgent;
    [SerializeField] private Transform aimTargetTransform;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] agentSpawnPoints;

    private StarterAssetsInputs inputs;
    private CharacterController characterController;
    private RayPerceptionSensorComponent3D raySensor;

    private float maxEpisodeTime = 120f;
    private float episodeTimer;
    private bool isSpawning;
    private string targetTag = "Player";

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
    }

    private void Update()
    {
        // Csak akkor avatkozunk be az inputba, ha az ágens irányít (Inference/Training)
        // Heuristic módban hagyjuk a StarterAssets rendszert működni
        if (!IsHeuristic())
        {
            if (inputs != null)
            {
                inputs.look = Vector2.zero; // Megállítja a kamera egér általi rángatását
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
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
        if (inputs != null) { inputs.move = Vector2.zero; inputs.look = Vector2.zero; }

        yield return new WaitForFixedUpdate();

        if (agentSpawnPoints != null && agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        if (aimTargetTransform != null) aimTargetTransform.localPosition = new Vector3(0, 1.5f, 10f);

        Physics.SyncTransforms();
        if (characterController != null) characterController.enabled = true;
        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.forward);

        if (opponentAgent != null)
        {
            Vector3 toTarget = opponentAgent.transform.position - transform.position;
            sensor.AddObservation(toTarget.normalized);
            sensor.AddObservation(toTarget.magnitude / 50f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }

        if (aimTargetTransform != null)
            sensor.AddObservation(aimTargetTransform.localPosition.y / 2.5f);
        else
            sensor.AddObservation(0f);
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

        transform.Rotate(Vector3.up, rotateY * 200f * Time.deltaTime);

        if (inputs != null)
        {
            inputs.move = new Vector2(moveX, moveZ);
            inputs.look = Vector2.zero; // AI módban ne vigye el a kamera a célzást
            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot;
        }

        if (aimTargetTransform != null && !IsHeuristic())
        {
            Vector3 lp = aimTargetTransform.localPosition;
            lp.y = Mathf.Clamp(lp.y + aimVertical * 5f * Time.deltaTime, 0.5f, 3f);
            aimTargetTransform.localPosition = lp;
        }

        if (opponentAgent != null)
        {
            float distance = Vector3.Distance(transform.position, opponentAgent.transform.position);

            if (distance < 5f)
            {
                AddReward(-0.1f);
            }
            else if (distance > 8f && distance < 18f)
            {
                AddReward(0.1f);
            }

            if (IsEnemyVisible())
            {
                Vector3 dirToTarget = (opponentAgent.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, dirToTarget);

                if (dot > 0.7f)
                {
                    AddReward(dot * 0.03f);

                    if (shouldAim)
                    {
                        AddReward(0.05f);
                    }
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

    public void GetHit()
    {
        if (isSpawning) return;
        AddReward(-10.0f);
        Debug.Log(gameObject.name + ": <color=red>ELTALÁLTAK</color>");
        EndEpisode();
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (isSpawning) return;

        if (tag == targetTag)
        {
            AddReward(20f);
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
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
    }
}