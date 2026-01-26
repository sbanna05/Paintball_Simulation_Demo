using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using StarterAssets;
using System.Collections;
using Unity.MLAgents.Policies;
using UnityEngine.Animations.Rigging;

public class Player : Agent
{
    [Header("References")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform enemyTarget; // A Dummy
    [SerializeField] private Transform[] agentSpawnPoints;
    [SerializeField] private Transform[] enemySpawnPoints; // A Dummy spawn pontjai

    private StarterAssetsInputs inputs;
    private CharacterController characterController;
    private ShooterController shooterController;
    private RigBuilder rigBuilder;
    private RayPerceptionSensorComponent3D raySensor;
    private ThirdPersonController tpc;

    private float maxEpisodeTime = 100f; // Rövidített idő
    private float episodeTimer;
    private bool isSpawning;
    private string targetTag = "Enemy";

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
        StartCoroutine(SafeSpawnWithDummy());
    }

    private IEnumerator SafeSpawnWithDummy()
    {
        isSpawning = true;

        // 1. Minden lekapcsolása (Critical for Burst)
        if (tpc) tpc.enabled = false;
        if (shooterController) shooterController.enabled = false;
        if (rigBuilder) rigBuilder.enabled = false;
        if (characterController) characterController.enabled = false;

        // 2. Input reset
        if (inputs) { 
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.aim = false;
            inputs.jump = false;
            inputs.shoot = false; 
        }

        yield return new WaitForFixedUpdate();

        // 3. Player Teleport
        if (agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        if (enemyTarget != null && enemySpawnPoints.Length > 0)
        {
            int enemyIdx = Random.Range(0, enemySpawnPoints.Length);
            int attempts = 0;
            while (Vector3.Distance(enemySpawnPoints[enemyIdx].position, transform.position) < 8f && attempts < 20)
            {
                enemyIdx = Random.Range(0, enemySpawnPoints.Length);
                attempts++;
            }
            enemyTarget.SetPositionAndRotation(enemySpawnPoints[enemyIdx].position, enemySpawnPoints[enemyIdx].rotation);
        }

        // 5. Aim Target Reset
        if (aimTarget) aimTarget.localPosition = new Vector3(0, 1.5f, 10f);

        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f); // Biztonsági idő

        // 6. Visszakapcsolás
        if (characterController) characterController.enabled = true;
        if (shooterController) shooterController.enabled = true;
        if (rigBuilder) rigBuilder.enabled = true;
        if (tpc) { tpc.enabled = true; }

        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.forward);

        if (enemyTarget != null)
        {
            Vector3 toTarget = enemyTarget.position - transform.position;
            sensor.AddObservation(toTarget.normalized);
            sensor.AddObservation(toTarget.magnitude / 50f);

            float dot = Vector3.Dot(transform.forward, toTarget.normalized);
            sensor.AddObservation(dot);
        }
        else { sensor.AddObservation(Vector3.zero); sensor.AddObservation(0f); sensor.AddObservation(0f); }

        if (aimTarget != null) sensor.AddObservation(aimTarget.localPosition.y / 5f);
        else sensor.AddObservation(0f);
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
                inputs.look = Vector2.zero;
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

        AddReward(-0.0001f);

        if (aimTarget != null && !IsHeuristic())
        {
            Vector3 lp = aimTarget.localPosition;
            lp.y = Mathf.Clamp(lp.y + (lookY * Time.deltaTime * 5f), 0.5f, 3.5f);
            aimTarget.localPosition = lp;
        }

        if (enemyTarget != null)
        {
            float distance = Vector3.Distance(transform.position, enemyTarget.position);
            Vector3 toEnemy = (enemyTarget.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toEnemy);

            if (dot > 0.5f)
            {
                AddReward(0.005f * dot);
            }

            if (distance < 8) AddReward(-0.02f);
            if (distance >= 8f && distance < 20f) AddReward(0.01f);

            if (shouldAim && IsEnemyVisible())
            {
                AddReward(0.01f);
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-5.0f);
            Debug.Log("<color=red>timeout!</color>");
            EndEpisode();
        }
    }

    public void OnShotFired() => AddReward(-0.01f);

    public void GetHit()
    {
        if (isSpawning) return;
        AddReward(-25.0f);
        Debug.Log(gameObject.name + ": <color=red>ELTALÁLTAK</color>");
        EndEpisode();
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
            AddReward(40.0f); // GYŐZELEM
            Debug.Log("<color=green>DIRECT HIT!</color>");
            EndEpisode();
        }
        else if (tag == "NearMiss")
        {
            if (hitObject.transform.root != transform)
            {
                AddReward(0.4f);
                Debug.Log("<color=yellow>NEAR MISS!</color>");
            }
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
        cont[2] = Input.GetAxis("Mouse X") * 10f;
        cont[3] = Input.GetAxis("Mouse Y") *-10f; 

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
        disc[2] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        disc[3] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;
    }

    public bool IsHeuristic() => GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly;
}