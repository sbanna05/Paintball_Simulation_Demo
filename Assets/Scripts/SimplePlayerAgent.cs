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

    private float maxEpisodeTime = 120f;
    private float episodeTimer;
    private bool isSpawning;
    private string targetTag = "Player";

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        shooterController = GetComponent<ShooterController>();
        rigBuilder = GetComponent<RigBuilder>();
    }

    private void Update()
    {
        // Egér zárolása csak Heurisztikus módban fontos a kényelemhez
        if (IsHeuristic() && Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public bool IsHeuristic()
    {
        var bp = GetComponent<BehaviorParameters>();
        return bp != null && bp.BehaviorType == BehaviorType.HeuristicOnly;
    }
    public void OnShotFired() => AddReward(-0.01f);

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        StartCoroutine(SafeSpawn());
    }

    private IEnumerator SafeSpawn()
    {
        isSpawning = true;

        // 1. Minden rendszer lekapcsolása (RigBuilder kritikus!)
        if (rigBuilder) rigBuilder.enabled = false;
        if (characterController) characterController.enabled = false;
        if (shooterController) shooterController.enabled = false;

        // 2. Input reset
        if (inputs)
        {
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.aim = false;
            inputs.shoot = false;
        }

        yield return new WaitForFixedUpdate();

        // 3. Teleportálás
        if (agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        if (aimTarget) aimTarget.localPosition = new Vector3(0, 1.5f, 10f);

        Physics.SyncTransforms();

        // 4. Extra várakozás a biztonság kedvéért
        yield return new WaitForSeconds(0.1f);

        // 5. Rendszerek visszakapcsolása
        if (characterController) characterController.enabled = true;
        if (shooterController) shooterController.enabled = true;
        if (rigBuilder) rigBuilder.enabled = true; // Rigging újraindul az új helyen

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
        else { 
            sensor.AddObservation(Vector3.zero); 
            sensor.AddObservation(0f); 
        }
        if (aimTarget != null)
            sensor.AddObservation(aimTarget.localPosition.y / 5f);

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

        // --- Discrete Actions ---
        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;
        bool shouldJump = actions.DiscreteActions[2] == 1;
        bool shouldSprint = actions.DiscreteActions[3] == 1;


        if (inputs != null)
        {
            inputs.move = new Vector2(moveX, moveZ);
            inputs.look = new Vector2(lookX, lookY);
            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot;
            inputs.jump = shouldJump;
            inputs.sprint = shouldSprint;

            if (!IsHeuristic())
            {
                transform.Rotate(Vector3.up, lookX * 200f * Time.deltaTime);
                inputs.look = Vector2.zero;
            }
        }

        if (aimTarget != null)
        {
            Vector3 lp = aimTarget.localPosition;
            lp.y = Mathf.Clamp(lp.y + (lookY * Time.deltaTime), 0.5f, 4f);
            aimTarget.localPosition = lp;
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
            SimplePlayerAgent targetAgent = hitObject.GetComponentInParent<SimplePlayerAgent>();
            if (targetAgent != null && targetAgent != this)
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
        cont[2] = Input.GetAxis("Mouse X") * 3f;
        cont[3] = Input.GetAxis("Mouse Y") * -3f;

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
        disc[2] = Input.GetKey(KeyCode.Space) ? 1 : 0; // Jump 
        disc[3] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0; // Sprint
    }
}