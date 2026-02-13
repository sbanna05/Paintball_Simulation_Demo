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

    [Header("Training Settings")]
    [SerializeField] private float lookSmoothing = 0.15f;
    [SerializeField] private float moveSmoothing = 0.1f;
    [SerializeField] private bool alwaysSmooth = true;

    private Vector2 smoothLook;
    private Vector2 smoothMove;

    // ÚJ: Velocity tracking
    private Vector3 previousPosition;
    private Vector3 currentVelocity;

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

    private int shotsFired = 0;
    private float lastShotTime = 0f;
    private float shotCooldown = 0.3f;

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        shooterController = GetComponent<ShooterController>();
        rigBuilder = GetComponent<RigBuilder>();
        tpc = GetComponent<ThirdPersonController>();

        previousPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        smoothLook = Vector2.zero;
        smoothMove = Vector2.zero;
        shotsFired = 0;
        lastShotTime = 0f;
        currentVelocity = Vector3.zero;
        previousPosition = transform.position;

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

        previousPosition = transform.position;
        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // === SAJÁT ÁLLAPOT (9) ===

        sensor.AddObservation(transform.forward);

        // 2. Normalizált velocity (3)
        currentVelocity = (transform.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = transform.position;
        sensor.AddObservation(currentVelocity / 10f); // Normalizálva max ~10 m/s-re

        // 3. Jelenlegi aim állapot (1)
        sensor.AddObservation(inputs != null && inputs.aim ? 1f : 0f);

        // 4. Cooldown állapot (1)
        float cooldownProgress = Mathf.Clamp01((Time.time - lastShotTime) / shotCooldown);
        sensor.AddObservation(cooldownProgress);

        // 5. Aimrig weight (1)
        sensor.AddObservation(shooterController.aimRig != null ? shooterController.aimRig.weight : 0f);

        if (opponentAgent != null)
        {
            Vector3 toEnemy = opponentAgent.transform.position - transform.position;
            float distance = toEnemy.magnitude;

            Vector3 localEnemyDir = transform.InverseTransformDirection(toEnemy.normalized);
            sensor.AddObservation(localEnemyDir);

            sensor.AddObservation(Mathf.Clamp(distance / 50f, 0f, 1f)); // Max 30m

            Vector3 enemyForward = transform.InverseTransformDirection(opponentAgent.transform.forward);
            sensor.AddObservation(enemyForward);

            if (shooterController != null && shooterController.gunBarrel != null)
            {
                Vector3 gunDir = shooterController.gunBarrel.forward;
                float gunAlignment = Vector3.Dot(gunDir, toEnemy.normalized);
                sensor.AddObservation(gunAlignment);
            }
            else
            {
                sensor.AddObservation(0f);
            }
        }
        else
        {
            sensor.AddObservation(Vector3.zero);     // localEnemyDir
            sensor.AddObservation(0f);               // distance
            sensor.AddObservation(Vector3.zero);     // enemyForward
            sensor.AddObservation(0f);               // gunAlignment
        }
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

        if (alwaysSmooth || IsHeuristic())
        {
            smoothMove = Vector2.Lerp(smoothMove, new Vector2(rawMoveX, rawMoveZ), 1f - moveSmoothing);
            smoothLook = Vector2.Lerp(smoothLook, new Vector2(rawLookX, rawLookY), 1f - lookSmoothing);
        }
        else
        {
            smoothMove = new Vector2(rawMoveX, rawMoveZ);
            smoothLook = new Vector2(rawLookX, rawLookY);
        }

        if (inputs != null)
        {
            inputs.move = smoothMove;
            inputs.look = smoothLook;
            inputs.aim = shouldAim;
            inputs.shoot = shouldShoot && (Time.time >= lastShotTime + shotCooldown);
            inputs.jump = false;
            inputs.sprint = shouldSprint;
        }

        if (aimTarget != null && !IsHeuristic())
        {
            Vector3 lp = aimTarget.localPosition;
            lp.y = Mathf.Clamp(lp.y + (rawLookY * Time.deltaTime * 3f), 0.5f, 4f);
            aimTarget.localPosition = lp;
        }

        AddReward(-0.0002f);

        if (opponentAgent != null)
        {
            Vector3 toEnemy = opponentAgent.transform.position - transform.position;
            float distance = toEnemy.magnitude;
            Vector3 toEnemyDir = toEnemy.normalized;

            float distanceReward = 0f;
            if (distance < 5f)
            {
                distanceReward = -0.003f;
            }
            else if (distance >= 8f && distance <= 25f)
            {
                float optimalness = 1f - Mathf.Abs(distance - 15.0f) / 7.5f;
                distanceReward = 0.001f * optimalness;
            }

            AddReward(distanceReward);

            float bodyDot = Vector3.Dot(transform.forward, toEnemyDir);
            if (bodyDot > 0.3f)
            {
                AddReward((bodyDot - 0.3f) * 0.002f); // 0 -> 0.0014 ha perfect
            }

            if (shooterController != null && shooterController.gunBarrel != null)
            {
                Vector3 gunDir = shooterController.gunBarrel.forward;
                float aimDot = Vector3.Dot(gunDir, toEnemyDir);

                // Progresszív reward
                if (aimDot > 0.5f)
                {
                    float alignmentReward = (aimDot - 0.5f) * 0.01f; // 0 -> 0.005 ha perfect
                    AddReward(alignmentReward);
                }

                // Extra bonus ha nagyon pontos ÉS aim-el
                if (inputs.aim && aimDot > 0.9f)
                {
                    AddReward(0.002f);
                }
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-5.0f);
            Debug.Log($"[{gameObject.name}] TIMEOUT after {shotsFired} shots");
            EndEpisode();
        }
    }

    public void OnShotFired()
    {
        shotsFired++;
        lastShotTime = Time.time;

        if (!IsEnemyVisible())
        {
            AddReward(-0.1f);
        }
    }

    public void GetHit()
    {
        if (isSpawning) return;
        AddReward(-5.0f);
        Debug.Log($"[{gameObject.name}] GOT HIT after {shotsFired} shots");
        EndEpisode();
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (isSpawning) return;

        if (shooterController == null || shooterController.gunBarrel == null) return;

        float distance = Vector3.Distance(shooterController.gunBarrel.position, hitObject.transform.position);

        if (tag == targetTag)
        {
            float baseReward = 5.0f;

            if (distance < 5.0f)
            {
                AddReward(baseReward * 0.5f);
                Debug.Log($"[{gameObject.name}] <color=red>TOO CLOSE!</color> Dist: {distance:F1}m | Reward: {baseReward * 0.5f:F1}");
            }
            else if (distance >= 5f && distance <= 25f)
            {
                float distanceBonus = Mathf.Clamp((distance - 5f) / 20f, 0.2f, 1f);
                float finalReward = baseReward * (1f + distanceBonus);
                AddReward(finalReward);
                Debug.Log($"<color=green>[{gameObject.name}] HIT!</color> Dist: {distance:F1}m | Reward: {finalReward:F1}");
            }

            EndEpisode();
        }
        else if (tag == "NearMiss")
        {
            if (distance > 8f && distance < 25f)
            {
                AddReward(0.2f);
                Debug.Log("<color=yellow>NEAR MISS (Safe Dist)!</color>");
            }
        }
        else
        {
            AddReward(-0.02f);
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