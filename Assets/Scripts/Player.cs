using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using Unity.MLAgents.Policies;
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
    private float _currentAnimSpeed;
    private float _lookYOffset;
    private int shotsFired;

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

        _lookYOffset = Mathf.Clamp(_lookYOffset + (lookYInput * Time.deltaTime * 2f), -2f, 4f);

        if (_anim)
        {
            float targetAnimSpeed = move.magnitude * (speed / moveSpeed);
            _currentAnimSpeed = Mathf.Lerp(_currentAnimSpeed, targetAnimSpeed, Time.deltaTime * 10f);
            _anim.SetFloat("Speed", _currentAnimSpeed);
            _anim.SetFloat("MotionSpeed", 1f);
        }

        _shooter.SetAimState(isAiming, aimTarget, _lookYOffset);

        if (isAiming && shootCommand)
        {
            if (_shooter.Shoot(aimTarget))
            {
                AddReward(-0.01f);
                shotsFired++;
            }
        }

        UpdateAgentMode();
        AddReward(-0.0001f);
        if (opponentAgent != null && IsEnemyVisible())
        {
            float dist = Vector3.Distance(transform.position, opponentAgent.transform.position);
            Vector3 toEnemy = (opponentAgent.transform.position - transform.position).normalized;

            // JAVÍTÁS #1: BODY ALIGNMENT REWARD
            /*float bodyDot = Vector3.Dot(transform.forward, toEnemy);
            if (bodyDot > 0.8f)
            {
                AddReward((bodyDot - 0.8f) * 0.004f);
            }*/

            if (dist < 6f)
            {
                float proximityPenalty = Mathf.Pow((6f - dist) / 6f, 2);
                AddReward(-0.02f * proximityPenalty);
            }

            if (dist >= 8f && dist <= 20f)
            {
                float optimalness = 1f - Mathf.Abs(dist - 12f) / 8f;
                AddReward(0.005f * optimalness);
            }
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-4.0f);
            Debug.Log($"[{gameObject.name}] TIMEOUT after {shotsFired} shots");
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
        sensor.AddObservation(IsEnemyVisible() ? 1f : 0f);
    }

    private void UpdateAgentMode()
    {
        bool lowHealth = CurrentHealth <= maxHealth * 0.5f;
        bool suppressed = isUnderFire && stressCounter > 2;

        if (lowHealth || suppressed)
        {
            if (currentMode != AgentMode.Defensive)
            {
                currentMode = AgentMode.Defensive;
                AddReward(0.05f);
            }
        }
        else
            currentMode = AgentMode.Offensive;

    }


    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (_isSpawning) return;
        float dist = Vector3.Distance(_shooter.gunBarrel.position, hitObject.transform.position);

        if (tag == targetTag)
        {
            if (dist <= 5f)
            {
                AddReward(0.1f);
                Debug.Log($"<color=yellow>[{gameObject.name}] Too Close! ({dist:F1}m) </color>");
            }
            else
            {
                float baseReward = 2.0f;
                /* float distanceMultiplier = dist / 10f;
                 float finalReward = baseReward * distanceMultiplier;*/

                float distanceBonus = Mathf.Clamp((dist - 8f) / 10f, 0f, 5f);
                float finalReward = baseReward + distanceBonus;

                Debug.Log($"<color=green>[{gameObject.name}] HIT!</color> Dist: {dist:F1}m Reward: {finalReward:F2}");
            }
        }
        else if (tag == "NearMiss")
        {
            AddReward(0.1f);
        }
        else
        {
            AddReward(-0.05f);
        }
    }

    public void OnNearMissDetected()
    {
        if (_isSpawning) return;
        isUnderFire = true;
        underFireTimer = 3.0f;

        stressCounter++;
        totalNearMisses++;
        AddReward(-0.03f);
    }

    public void TakeDamage(float damage, Player attacker)
    {
        if (_isSpawning || CurrentHealth <= 0) return;

        isUnderFire = true;
        underFireTimer = 3.0f;

        CurrentHealth -= damage;
        AddReward(-0.5f);

        if (CurrentHealth <= 0)
            Die(attacker);
    }

    private void Die(Player killer)
    {
        AddReward(-2.0f);
        Debug.Log($"[{gameObject.name}] DIED. Shots: {shotsFired} / {totalNearMisses}");

        if (killer != null)
        {
            killer.AddReward(3f);
            Debug.Log($"<color=red>[{killer.gameObject.name}] KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
            killer.EndEpisode();
        }

        EndEpisode();
    }

    private bool IsEnemyVisible()
    {
        if (_raySensor == null) return false;

        var rayOutputs = RayPerceptionSensor.Perceive(_raySensor.GetRayPerceptionInput()).RayOutputs;
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

    private bool CheckLineOfSight()
    {
        if (opponentAgent == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = opponentAgent.transform.position + Vector3.up * 1.5f;
        Vector3 dir = target - origin;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, 60f))
        {
            if (hit.transform.root == opponentAgent.transform.root)
                return true;
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