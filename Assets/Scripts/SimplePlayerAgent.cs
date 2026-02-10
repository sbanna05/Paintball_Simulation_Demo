using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using StarterAssets;
using System.Collections;
using Unity.MLAgents.Policies;
using UnityEngine.Animations.Rigging;

public class SimplePlayerAgent : Agent
{
    [Header("References")]
    [SerializeField] private SimplePlayerAgent opponentAgent;
    [SerializeField] private Transform aimTarget;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] agentSpawnPoints;

    [Header("Inference Settings")]
    [SerializeField] private float lookSmoothing = 0.4f;
    [SerializeField] private float moveSmoothing = 0.1f;

    private Vector2 smoothLook;
    private Vector2 smoothMove;

    private StarterAssetsInputs inputs;
    private CharacterController characterController;
    private RayPerceptionSensorComponent3D raySensor;
    private ShooterController shooterController;
    private RigBuilder rigBuilder;
    private ThirdPersonController tpc;

    private float maxEpisodeTime = 100f;
    private float episodeTimer;
    private bool isSpawning;
    private string targetTag = "Player";
    private bool hasSeenTarget = false;

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        shooterController = GetComponent<ShooterController>();
        rigBuilder = GetComponent<RigBuilder>();
        tpc = GetComponent<ThirdPersonController>();
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        smoothLook = Vector2.zero;
        smoothMove = Vector2.zero;
        StartCoroutine(SafeSpawn());
    }

    private IEnumerator SafeSpawn()
    {
        isSpawning = true;

        if (tpc) tpc.enabled = false;
        if (shooterController) shooterController.enabled = false;
        if (rigBuilder) rigBuilder.enabled = false;
        if (characterController) characterController.enabled = false;

        if (inputs)
        {
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.aim = false;
            inputs.jump = false;
            inputs.shoot = false;
            inputs.sprint = false;
        }

        yield return new WaitForFixedUpdate();

        if (agentSpawnPoints != null && agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        if (aimTarget)
        {
            aimTarget.localPosition = new Vector3(0, 1.5f, 10f);
        }

        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f);

        if (characterController) characterController.enabled = true;
        if (shooterController) shooterController.enabled = true;
        if (rigBuilder) rigBuilder.enabled = true;
        if (tpc) tpc.enabled = true;

        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.forward);

        if (opponentAgent != null)
        {
            Vector3 toTarget = opponentAgent.transform.position - transform.position;
            sensor.AddObservation(toTarget.normalized); // 3
            sensor.AddObservation(toTarget.magnitude / 50f); // 1

            float dot = Vector3.Dot(transform.forward, toTarget.normalized);
            sensor.AddObservation(dot); // 1

            Vector3 localEnemyDir = transform.InverseTransformDirection(toTarget.normalized);
            sensor.AddObservation(localEnemyDir); //3
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        if (aimTarget != null)
        {
            sensor.AddObservation(aimTarget.localPosition.y / 3f); //1
        }

        Vector3 gunToEnemy = opponentAgent.transform.position - shooterController.gunBarrel.position;
        sensor.AddObservation(gunToEnemy.normalized.y); //1    
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isSpawning) return;

        episodeTimer += Time.fixedDeltaTime;

        float rawMoveX = actions.ContinuousActions[0];
        float rawMoveZ = actions.ContinuousActions[1];
        float rawLookX = actions.ContinuousActions[2];
        float rawLookY = actions.ContinuousActions[3];

        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;
        bool shouldJump = actions.DiscreteActions[2] == 1;
        bool shouldSprint = actions.DiscreteActions[3] == 1;

        smoothMove = Vector2.Lerp(smoothMove, new Vector2(rawMoveX, rawMoveZ), 1f - moveSmoothing);
        smoothLook = Vector2.Lerp(smoothLook, new Vector2(rawLookX, rawLookY), 1f - lookSmoothing);

        if (inputs != null)
        {
            inputs.move = smoothMove;
            inputs.look = IsHeuristic() ? new Vector2(rawLookX, rawLookY) : smoothLook;
            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot;
            inputs.jump = false;
            inputs.sprint = shouldSprint;
        }

        if (aimTarget != null && !IsHeuristic())
        {
            Vector3 lp = aimTarget.localPosition;
            lp.y = Mathf.Clamp(lp.y + (rawLookY * Time.deltaTime * 3f), 0.5f, 4f);
            aimTarget.localPosition = lp;
        }

        AddReward(-0.00005f);

        if (opponentAgent != null)
        {
            float distance = Vector3.Distance(transform.position, opponentAgent.transform.position);

            if (Time.frameCount % 5 == 0)
            {
                if (distance < 8f) AddReward(-0.002f);
                else if (distance >= 10f && distance <= 25f) AddReward(0.002f);
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-5f);
            Debug.Log($"[{gameObject.name}] TIMEOUT");
            EndEpisode();
        }
    }

    public void OnShotFired()
    {
        if (!IsEnemyVisible())
            AddReward(-0.05f);
    }

    public void GetHit()
    {
        if (isSpawning) return;
        AddReward(-25.0f);
        EndEpisode();
    }
   
    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (isSpawning) return;

        if (shooterController == null || shooterController.gunBarrel == null) return;

        float distance = Vector3.Distance(shooterController.gunBarrel.position, hitObject.transform.position);

        if (tag == targetTag)
        {
            if (distance < 8.0f)
            {
                Debug.Log($"[{gameObject.name}] <color=red>TOO CLOSE!</color> Dist: {distance:F1}m");
            }
            else
            {
                float timeBonus = Mathf.Clamp01(1f - episodeTimer / maxEpisodeTime);
                float distanceMultiplier = Mathf.Clamp(distance / 10f, 1.0f, 3.0f);
                float finalReward = (50f * distanceMultiplier) + (10f * timeBonus);

                AddReward(finalReward);
                Debug.Log($"[{gameObject.name}] <color=green>HIT!</color> Dist: {distance:F1}m | Reward: {finalReward:F1}");
            }

            EndEpisode();
        }
        else if (tag == "NearMiss")
        {
            if (distance > 8f)
            {
                AddReward(0.5f);
                Debug.Log("<color=yellow>NEAR MISS (Safe Dist)!</color>");
            }
        }
        else
        {
            AddReward(-0.01f);
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
                if (ray.HitGameObject.CompareTag("Player") || ray.HitGameObject.CompareTag("NearMiss"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Horizontal");
        cont[1] = Input.GetAxis("Vertical");
        cont[2] = Input.GetAxis("Mouse X") * 10f;
        cont[3] = Input.GetAxis("Mouse Y") * -10f;

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
        disc[2] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        disc[3] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;
    }

    public bool IsHeuristic() => GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly;
    public bool IsSpawning() => isSpawning;
}