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
    public enum AgentMode { Offensive, Defensive }

    [Header("References")]
    [SerializeField] private SimplePlayerAgent opponentAgent;
    [SerializeField] private Transform aimTarget;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] agentSpawnPoints;

    [Header("Combat Settings")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }

    [Header("Training Settings")]
    [SerializeField] private float lookSmoothing = 0.15f;
    [SerializeField] private float moveSmoothing = 0.1f;

    // Components
    private StarterAssetsInputs inputs;
    private CharacterController characterController;
    private RayPerceptionSensorComponent3D raySensor;
    private ShooterController shooterController;
    private RigBuilder rigBuilder;
    private ThirdPersonController tpc;

    // State
    private float maxEpisodeTime = 120f;
    private float episodeTimer;
    private bool isSpawning;
    private string targetTag = "Player";
    private AgentMode currentMode = AgentMode.Offensive;

    private bool isUnderFire = false;
    private float underFireTimer = 0f;
    private int nearMissCount = 0;     
    private int shotsFired = 0;
    private Vector2 smoothLook;
    private Vector2 smoothMove;

    // Anti-Camp
    private Vector3 lastPosition;
    private float campTimer;

    public override void Initialize()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        characterController = GetComponent<CharacterController>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        shooterController = GetComponent<ShooterController>();
        rigBuilder = GetComponent<RigBuilder>();
        tpc = GetComponent<ThirdPersonController>();

        CurrentHealth = maxHealth;
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        CurrentHealth = maxHealth;
        currentMode = AgentMode.Offensive;

        // Reset Suppression
        isUnderFire = false;
        underFireTimer = 0f;
        nearMissCount = 0;
        shotsFired = 0;

        campTimer = 0f;
        lastPosition = transform.position;

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

        if (aimTarget) aimTarget.localPosition = new Vector3(0, 1.5f, 10f);

        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.1f);

        if (characterController) characterController.enabled = true;
        if (shooterController) shooterController.enabled = true;
        if (rigBuilder) rigBuilder.enabled = true;
        if (tpc) tpc.enabled = true;

        lastPosition = transform.position;
        isSpawning = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(CurrentHealth / maxHealth);
        sensor.AddObservation(shooterController.CooldownProgress());
        sensor.AddObservation((float)currentMode); // 0 = Offensive, 1 = Defensive

        // 2. Saját irány
        sensor.AddObservation(transform.forward);

        if (opponentAgent != null)
        {
            Vector3 toEnemy = opponentAgent.transform.position - transform.position;
            float distance = toEnemy.magnitude;

            // 3. Ellenség infók
            sensor.AddObservation(toEnemy.normalized);
            sensor.AddObservation(Mathf.Clamp(distance / 50f, 0f, 1f));
            sensor.AddObservation(opponentAgent.transform.forward);

            // 4. Látom-e?
            bool canSee = CheckLineOfSight();
            sensor.AddObservation(canSee ? 1f : 0f);

            // 5. Célzás
            if (shooterController.gunBarrel != null)
            {
                float dot = Vector3.Dot(shooterController.gunBarrel.forward, toEnemy.normalized);
                sensor.AddObservation(dot);
            }
            else { sensor.AddObservation(0f); }

            // 6. Ellenség HP
            sensor.AddObservation(opponentAgent.CurrentHealth / maxHealth);

            float suppressionLevel = isUnderFire ? Mathf.Clamp01(nearMissCount / 5f) + 0.1f : 0f;
            sensor.AddObservation(suppressionLevel);
        }
        else
        {
            sensor.AddObservation(new float[10]);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isSpawning) return;
        episodeTimer += Time.fixedDeltaTime;

        // --- SUPPRESSION LOGIKA ---
        if (underFireTimer > 0)
        {
            underFireTimer -= Time.fixedDeltaTime;
            if (underFireTimer <= 0)
            {
                isUnderFire = false;
                nearMissCount = 0;
            }
        }
        UpdateAgentMode();

        float rawMoveX = actions.ContinuousActions[0];
        float rawMoveZ = actions.ContinuousActions[1];
        float rawLookX = actions.ContinuousActions[2];
        float rawLookY = actions.ContinuousActions[3];

        bool shouldAim = actions.DiscreteActions[0] == 1;
        bool shouldShoot = actions.DiscreteActions[1] == 1;
        bool shouldJump = actions.DiscreteActions[2] == 1;
        bool shouldSprint = actions.DiscreteActions[3] == 1;

        if (IsHeuristic())
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

            bool canShoot = shooterController.IsCooldownReady();
            if (shouldShoot && canShoot)
                inputs.shoot = true;
            else
                inputs.shoot = false;

            inputs.jump = false;
            inputs.sprint = shouldSprint;
        }               

        if (aimTarget != null && !IsHeuristic())
        {
            Vector3 lp = aimTarget.localPosition;
            lp.y = Mathf.Clamp(lp.y + (rawLookY * Time.deltaTime * 3f), 0.5f, 4f);
            aimTarget.localPosition = lp;
        }

        AddReward(-0.0005f);

        // Anti-Camp (Csak ha OFFENSIVE módban van!)
        // Ha védekezik (mert lőnek rá), akkor NEM büntetjük az egyhelyben állást (fedezék)!
        if (currentMode == AgentMode.Offensive)
        {
            if (Vector3.Distance(transform.position, lastPosition) < 0.5f)
            {
                campTimer += Time.fixedDeltaTime;
                if (campTimer > 3.0f) AddReward(-0.005f);
            }
            else
            {
                campTimer = 0f;
                lastPosition = transform.position;
            }
        }

        if (opponentAgent != null)
        {
            if (CheckLineOfSight())
            {
                Vector3 toEnemy = (opponentAgent.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toEnemy);
                if (dot > 0.7f) AddReward(0.001f);
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-2f);
            Debug.Log($"[{gameObject.name}] TIMEOUT after {shotsFired} shots");
            EndEpisode();
        }
    }

    private void UpdateAgentMode()
    {
        bool lowHealth = CurrentHealth < maxHealth * 0.4f;

        //Tűz alatt van -> Védekezés
        bool suppressed = isUnderFire && nearMissCount > 3;

        if (lowHealth || suppressed)
        {
            if (currentMode != AgentMode.Defensive)
            {
                currentMode = AgentMode.Defensive;
                // AddReward(0.05f); // Dicséret a váltásért (opcionális)
            }
        }
        else
        {
            currentMode = AgentMode.Offensive;
        }

        // Defensive logika: Jutalmazzuk, ha elbújik!
        if (currentMode == AgentMode.Defensive)
        {
            if (!CheckLineOfSight() && isUnderFire)
            {
                float safetyReward = 0.002f;
                AddReward(safetyReward);
            }
        }
    }

    public void OnShotFired()
    {
        shotsFired++;
        AddReward(-0.01f);
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (isSpawning) return;

        if (shooterController == null || shooterController.gunBarrel == null) return;

        float distance = Vector3.Distance(shooterController.gunBarrel.position, hitObject.transform.position);

        if (tag == targetTag)
        {
            float baseReward = 6.0f;

            float distanceBonus = Mathf.Clamp((distance - 5f) / 15f, 0.5f, 1.5f);
            float finalReward = baseReward * (1f + distanceBonus);
            AddReward(finalReward);
            Debug.Log($"<color=green>[{gameObject.name}] HIT!</color> Dist: {distance:F1}m | Reward: {finalReward:F1}");
        }
        else if(tag == "NearMiss")
        {
            AddReward(0.08f);
            // Debug.Log("<color=yellow>NEAR MISS (Safe Dist)!</color>");
        }    
        else
        {
            AddReward(-0.01f);
        }
    }

    public void OnNearMissDetected()
    {
        isUnderFire = true;
        underFireTimer = 2.0f; 
        nearMissCount++;

        AddReward(-0.05f);
    }

    // Bullet hívja, ha ENGEM találtak el
    public void TakeDamage(float damage, SimplePlayerAgent attacker)
    {
        if (isSpawning) return;

        CurrentHealth -= damage;
        AddReward(-0.5f);

        isUnderFire = true;
        underFireTimer = 3.0f;

        if (CurrentHealth <= 0)
        {
            Die(attacker);
        }
    }

    private void Die(SimplePlayerAgent killer)
    {
        AddReward(-2.0f);
        Debug.Log($"[{gameObject.name}] DIED.");

        if (killer != null)
        {
            killer.AddReward(2.0f);
            killer.EndEpisode();
        }

        EndEpisode();
    }

    private bool CheckLineOfSight()
    {
        if (opponentAgent == null) return false;
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = opponentAgent.transform.position + Vector3.up * 1.5f;
        Vector3 dir = target - origin;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, 60f))
        {
            if (hit.transform.root == opponentAgent.transform.root) return true;
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