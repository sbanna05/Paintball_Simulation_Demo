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

    private StarterAssetsInputs inputs;
    private CharacterController characterController;
    private RayPerceptionSensorComponent3D raySensor;
    private ShooterController shooterController;
    private RigBuilder rigBuilder;
    private ThirdPersonController tpc;

    private float maxEpisodeTime = 120f;
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
        hasSeenTarget = false;
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
        }

        yield return new WaitForFixedUpdate();

        if (agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        if (aimTarget) aimTarget.localPosition = new Vector3(0, 1.5f, 10f);

        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f);

        if (characterController) characterController.enabled = true;
        if (shooterController) shooterController.enabled = true;
        if (rigBuilder) rigBuilder.enabled = true;
        if (tpc) { tpc.enabled = true; }

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

            float dot = Vector3.Dot(transform.forward, toTarget.normalized);
            sensor.AddObservation(dot);
        }
        else { sensor.AddObservation(Vector3.zero); sensor.AddObservation(0f); sensor.AddObservation(0f); }

        if (aimTarget != null) sensor.AddObservation(aimTarget.localPosition.y / 3f);
        else sensor.AddObservation(0f);

        Vector3 gunToEnemy = opponentAgent.transform.position - shooterController.gunBarrel.position;
        sensor.AddObservation(gunToEnemy.normalized.y);


    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isSpawning) return;
        episodeTimer += Time.fixedDeltaTime;

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float lookX = actions.ContinuousActions[2];
        float lookY = actions.ContinuousActions[3];

        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;
        bool shouldJump = actions.DiscreteActions[2] == 1;
        bool shouldSprint = actions.DiscreteActions[3] == 1;

        if (inputs != null)
        {
            inputs.move = new Vector2(moveX, moveZ);

            if (!IsHeuristic())
            {
                transform.Rotate(Vector3.up, lookX * 200f * Time.deltaTime);
                inputs.look = new Vector2(lookX, lookY);
            }
            else
            {
                inputs.look = new Vector2(lookX, lookY);
            }

            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot;
            inputs.jump = false;
            inputs.sprint = shouldSprint;

        }

        // Vertikális célzás (AimTarget IK)
        if (aimTarget != null && !IsHeuristic())
        {
            Vector3 lp = aimTarget.localPosition;
            lp.y = Mathf.Clamp(lp.y + (lookY * Time.deltaTime * 3f), 0.5f, 4f);
            aimTarget.localPosition = lp;
        }

        // --- JUTALMAZÁSI LOGIKA ---
        AddReward(-0.0002f);

        if (opponentAgent != null)
        {
            float distance = Vector3.Distance(transform.position, opponentAgent.transform.position);

            // 1. Távolságtartás (Halálzóna büntetés 8m alatt)
            if (distance < 8f)
            {
                float proximityPenalty = Mathf.Pow(8f - distance, 2) * -0.01f;
                AddReward(proximityPenalty - 0.005f);
            }
            // 2. Sniper zóna jutalom (10m - 25m)
            else if (distance >= 10f && distance <= 25f)
            {
                AddReward(0.005f);
            }

            // 3. Célzás és láthatóság
            if (IsEnemyVisible())
            {
                AddReward(0.001f); // Látómezőben tartás

                Vector3 dirToTarget = (opponentAgent.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(shooterController.gunBarrel.forward, dirToTarget);

                if (dot > 0.85f)
                {
                    AddReward(0.005f * dot); // Pontos célzás bónusz
                }

                if (!hasSeenTarget)
                {
                    hasSeenTarget = true;
                    AddReward(2f);
                    Debug.Log("<color=cyan>TARGET ACQUIRED!</color>");
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

    public void OnShotFired()
    {
        if (IsEnemyVisible())
           AddReward(-0.01f);
        else
           AddReward(-0.03f);
    }

    public void GetHit()
    {
        if (isSpawning) return;
        AddReward(-25.0f);
        Debug.Log(gameObject.name + ": <color=red>ELTALÁLTAK</color>");
        EndEpisode();
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (isSpawning) return;
        float distance = Vector3.Distance(transform.position, hitObject.transform.position);

        if (tag == targetTag)
        {
            if (distance < 8.0f)
            {
                AddReward(-2.0f);
                Debug.Log($"<color=red>TOO CLOSE! Dist: {distance:F1}m | Penalty: -2.0</color>");
            }
            else
            {
                float timeBonus = Mathf.Clamp01(1f - episodeTimer / maxEpisodeTime);
                float distanceMultiplier = Mathf.Clamp(distance / 10f, 1.0f, 3.0f);
                float finalReward = (60f * distanceMultiplier) + (10f * timeBonus);

                AddReward(finalReward);
                Debug.Log($"<color=green>SNIPER HIT! Dist: {distance:F1}m | Reward: {finalReward:F1}</color>");
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
            AddReward(-0.05f);
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
                    return true;
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
}