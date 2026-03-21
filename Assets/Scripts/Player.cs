using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(CharacterController))]
public class Player : Agent
{
    public enum AgentMode { Offensive, Defensive }

    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 2f;
    public float turnSpeed = 120f;

    [Header("Combat Settings")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public AgentMode currentMode = AgentMode.Offensive;

    [Header("Cover Settings")]
    [SerializeField] private LayerMask coverLayerMask;
    [SerializeField] private float maxRaycastDistance = 50f;

    [Header("References")]
    [SerializeField] private Player opponentAgent;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform[] agentSpawnPoints;

    private CharacterController _controller;
    private ShooterController _shooter;
    private Animator _anim;
    private RigBuilder _rigBuilder;
    private RayPerceptionSensorComponent3D _raySensor;

    private Vector3 previousPosition;
    private Vector3 currentVelocity;
    private bool _isSpawning = false;
    private float _lookYOffset;
    private int shotsFired;
    private int shotsHit;

    public int stressCounter;
    public int totalNearMisses;
    private bool isUnderFire = false;
    private float underFireTimer = 0f;

    private float maxEpisodeTime = 100f;
    private float episodeTimer;
    private string targetTag = "Player";

    public override void Initialize()
    {
        _controller = GetComponent<CharacterController>();
        _shooter = GetComponent<ShooterController>();
        _anim = GetComponent<Animator>();
        _raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        _rigBuilder = GetComponent<RigBuilder>();
        CurrentHealth = maxHealth;

        if (opponentAgent == null && transform.parent != null)
        {
            Player[] agents = transform.parent.GetComponentsInChildren<Player>();
            foreach (var a in agents) if (a != this) { opponentAgent = a; break; }
        }
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        CurrentHealth = maxHealth;
        shotsFired = 0;
        shotsHit = 0;
        stressCounter = 0;
        totalNearMisses = 0;

        isUnderFire = false;
        underFireTimer = 0f;
        currentMode = AgentMode.Offensive;

        StartCoroutine(ResetScene());
    }

    private IEnumerator ResetScene()
    {
        _isSpawning = true;
        _controller.enabled = false;
        _lookYOffset = 0;

        if (agentSpawnPoints != null && agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        yield return new WaitForFixedUpdate();
        Physics.SyncTransforms();

        previousPosition = transform.position;
        _controller.enabled = true;
        _isSpawning = false;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isSpawning) return;

        episodeTimer += Time.fixedDeltaTime;

        if (underFireTimer > 0)
        {
            underFireTimer -= Time.fixedDeltaTime;
            if (underFireTimer <= 0) { isUnderFire = false; stressCounter = 0; }
        }

        float moveForward = actions.ContinuousActions[0];
        float moveSide = actions.ContinuousActions[1];
        float rotateX = actions.ContinuousActions[2];
        float lookYInput = actions.ContinuousActions[3];

        bool isAiming = actions.DiscreteActions[0] == 1;
        bool shootCommand = actions.DiscreteActions[1] == 1;
        bool shouldSprint = actions.DiscreteActions[2] == 1;

        float speed = (shouldSprint && !isAiming) ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 move = transform.forward * moveForward + transform.right * moveSide;
        _controller.SimpleMove(move * speed);
        transform.Rotate(Vector3.up, rotateX * turnSpeed * Time.deltaTime);

        _lookYOffset = Mathf.Clamp(_lookYOffset + (lookYInput * Time.deltaTime * 40f), -2f, 4f);

        if (_anim)
        {
            float targetAnimSpeed = move.magnitude * (speed / moveSpeed);
            _anim.SetFloat("Speed", Mathf.Lerp(_anim.GetFloat("Speed"), targetAnimSpeed, Time.deltaTime * 10f));
            _anim.SetFloat("MotionSpeed", 1f);
        }

        _shooter.SetAimState(isAiming, aimTarget, _lookYOffset);

        if (isAiming && shootCommand)
        {
            if (_shooter.Shoot(aimTarget))
            {
                AddReward(-0.02f);
                shotsFired++;
            }
        }

        UpdateAgentMode();
        AddReward(-0.0002f);

        if (opponentAgent != null)
        {
            float dist = Vector3.Distance(transform.position, opponentAgent.transform.position);
            if (dist < 3f)
            {
                AddReward(-5.0f);
                opponentAgent.AddReward(-5.0f); 
                Debug.Log($"<color=yellow>Too close ({dist:F1}m). Both reset.</color>");

                opponentAgent.EndEpisode();
                EndEpisode();
                return;
            }

            bool iCanSeeEnemy = CheckLineOfSight();

            if (currentMode == AgentMode.Defensive)
            {
                if (!iCanSeeEnemy)
                {
                  AddReward(0.002f);
                  //opponentAgent.AddReward(-0.001f);
                }
                else
                {
                    AddReward(-0.001f);
                    //opponentAgent.AddReward(0.001f);
                }
            }
            else if (currentMode == AgentMode.Offensive)
            {
                if (iCanSeeEnemy)
                {
                    AddReward(0.001f);
                   // opponentAgent.AddReward(-0.001f);
                }
                else
                {
                    AddReward(-0.001f);
                    //opponentAgent.AddReward(0.001f);
                }
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
           // AddReward(-4f);
            Debug.Log($"[{gameObject.name}] TIMEOUT - Shots: {shotsFired}/{shotsHit}");
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.forward);

        currentVelocity = (transform.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = transform.position;
        sensor.AddObservation(currentVelocity.magnitude / 10f);
        sensor.AddObservation(CurrentHealth / maxHealth);

        sensor.AddObservation(_shooter.CooldownProgress());
        sensor.AddObservation(_shooter.aimRig != null ? _shooter.aimRig.weight : 0f);

        if (opponentAgent != null)
        {
            Vector3 toEnemy = opponentAgent.transform.position - transform.position;
            float dist = toEnemy.magnitude;

            sensor.AddObservation(transform.InverseTransformDirection(toEnemy.normalized));
            sensor.AddObservation(Mathf.Clamp(dist / 50f, 0f, 1f));
            sensor.AddObservation(transform.InverseTransformDirection(opponentAgent.transform.forward));

            float gunAlignment = Vector3.Dot(_shooter.gunBarrel.forward, toEnemy.normalized);
            sensor.AddObservation(gunAlignment);
        }

        sensor.AddObservation(CheckLineOfSight() ? 1f : 0f);
    }

    private void UpdateAgentMode()
    {
        bool lowHealth = CurrentHealth <= maxHealth * 0.5f;
        bool suppressed = isUnderFire && stressCounter > 3;

        if (lowHealth || suppressed)
            currentMode = AgentMode.Defensive;
        else
            currentMode = AgentMode.Offensive;
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (_isSpawning || opponentAgent == null) return;

        if (tag == targetTag)
        {
            shotsHit++;
            //Debug.Log($"<color=green>[{gameObject.name}] HIT!</color> Dist: {dist:F1}m");
        }
        else if (tag == "NearMiss")
        {
           AddReward(0.5f);
        }
       /* else 
        {
           AddReward(-0.05f);
        }*/
    }

    public void OnNearMissDetected()
    {
        if (_isSpawning) return;

        isUnderFire = true;
        underFireTimer = 3.0f;
        stressCounter++;
        totalNearMisses++;

        AddReward(-0.25f);
    }

    public void TakeDamage(float damage, Player attacker)
    {
        if (_isSpawning || CurrentHealth <= 0) return;

        isUnderFire = true;
        underFireTimer = 3.0f;
        stressCounter += 2;

        CurrentHealth -= damage;
        float dist = Vector3.Distance(transform.position, attacker.transform.position);
        if (CurrentHealth > 0)
        {
            float hitReward = 1.0f + Mathf.Clamp(dist / 10f, 0f, 3.0f);

            attacker.AddReward(hitReward);
            this.AddReward(-hitReward / 2f);

            Debug.Log($"<color=white>[{attacker.gameObject.name}] HIT! (Dist: {dist:F1}m, +{hitReward:F2} / -0.5)</color>");

        }

        if (CurrentHealth <= 0)
            Die(attacker);
    }

    private void Die(Player killer)
    {
        Debug.Log($"[{gameObject.name}] DIED. Shots: {shotsFired}");
        float dist = Vector3.Distance(transform.position, killer.transform.position);
        float killBonus = 5.0f + Mathf.Clamp(dist / 2f, 0f, 10.0f);

       // float killReward = 1f;

        if (dist >= 4f && dist < 25f)
        {
            //killReward = 10f;
            Debug.Log($"<color=lime>[{killer.gameObject.name}] TACTICAL KILL!</color> ({dist:F1}m, +{killBonus:F1}) Shots {killer.shotsFired} | NM: {killer.totalNearMisses} | Time: {episodeTimer:F1}s");
        }
        else if (dist >= 25f)
        {
            //killReward = 20f;
            Debug.Log($"<color=yellow>[{killer.gameObject.name}] SNIPER KILL!</color> ({dist:F1}m, +{killBonus:F1}) Shots {killer.shotsFired} | NM: {killer.totalNearMisses} | Time: {episodeTimer:F1}s");
        }
        else
        {
            Debug.Log($"<color=orange>[{killer.gameObject.name}] CLOSE KILL</color> ({dist:F1}m, +{killBonus:F1}) Shots {killer.shotsFired} | NM: {killer.totalNearMisses} | Time: {episodeTimer:F1}s");
        }

        if (killer.shotsFired > 0)
        {
            float accuracy = (float)killer.shotsHit / killer.shotsFired;
            if (accuracy > 0.5f)
                killBonus += 5f;
        }

        killer.AddReward(killBonus);
        AddReward(-killBonus / 2f);

        killer.EndEpisode();
        EndEpisode();
    }

    private bool CheckLineOfSight()
    {
        if (opponentAgent == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = opponentAgent.transform.position + Vector3.up * 1.5f;
        Vector3 dir = target - origin;

        int layerMask = ~coverLayerMask;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRaycastDistance, layerMask))
        {
            if (hit.transform.root == opponentAgent.transform.root) return true;
        }
        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        cont[0] = Input.GetAxis("Vertical");
        cont[1] = Input.GetAxis("Horizontal");
        cont[2] = Input.GetAxis("Mouse X");
        cont[3] = Input.GetAxis("Mouse Y");

        var disc = actionsOut.DiscreteActions;
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
        disc[2] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;
    }
}