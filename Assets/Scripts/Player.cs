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
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 2f;
    public float turnSpeed = 120f;

    [Header("Combat Settings")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }

    [Header("References")]
    [SerializeField] private Player opponentAgent;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform[] agentSpawnPoints;

    private CharacterController _controller;
    private ShooterController _shooter;
    private Animator _anim;
    private RigBuilder _rigBuilder;

    private Vector3 previousPosition;
    private Vector3 currentVelocity;
    private bool _isSpawning = false;
    private float _currentAnimSpeed;
    private float _lookYOffset;
    private int shotsFired;
    public int stressCounter = 0;
    public int totalNearMisses = 0;

    private float maxEpisodeTime = 100f;
    private float episodeTimer;
    private string targetTag = "Player";

    public override void Initialize()
    {
        _controller = GetComponent<CharacterController>();
        _shooter = GetComponent<ShooterController>();
        _anim = GetComponent<Animator>();
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
        StartCoroutine(ResetScene());
    }

    private IEnumerator ResetScene()
    {
        _isSpawning = true;
        _controller.enabled = false;
        shotsFired = 0;
        stressCounter = 0;
        totalNearMisses = 0;
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

        float moveForward = actions.ContinuousActions[0];
        float moveSide = actions.ContinuousActions[1];
        float rotateX = actions.ContinuousActions[2];
        float lookYInput = actions.ContinuousActions[3];

        bool isAiming = actions.DiscreteActions[0] == 1;
        bool shootCommand = actions.DiscreteActions[1] == 1;
        bool shouldSprint = actions.DiscreteActions[2] == 1;

        // --- MOZGÁS ---
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
                AddReward(-0.05f);
                shotsFired++;
            }
        }

        AddReward(-0.0001f);

        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-5f);
            Debug.Log($"[{gameObject.name}] TIMEOUT after {shotsFired} shots");
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.forward);

        currentVelocity = (transform.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = transform.position;
        sensor.AddObservation(currentVelocity / 10f);

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
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (_isSpawning) return;
        float distance = Vector3.Distance(_shooter.gunBarrel.position, hitObject.transform.position);

        if (tag == targetTag)
        {
            if (distance > 4f)
            {
                AddReward(3.0f);
                Debug.Log($"<color=green>[{gameObject.name}] DAMAGE!</color>");
            }
            else
            {
                Debug.Log($"<color=red>[{gameObject.name}] too close!</color>");
            }
        }
        else if (tag == "NearMiss")
        {
            AddReward(0.08f);
        }
        else
        {
            AddReward(-0.05f);
        }
    }

    public void OnNearMissDetected()
    {
        if (_isSpawning) return;
        totalNearMisses++;
        AddReward(-0.05f);
    }

    public void TakeDamage(float damage, Player attacker)
    {
        if (_isSpawning || CurrentHealth <= 0) return;

        CurrentHealth -= damage;
        AddReward(-0.4f);
        Debug.Log($"[{gameObject.name}] TOOK DAMAGE: {damage} (HP: {CurrentHealth})");

        if (CurrentHealth <= 0)
            Die(attacker);
    }

    private void Die(Player killer)
    {
        AddReward(-3f);
        Debug.Log($"[{gameObject.name}] DIED. Shots: {shotsFired} / {totalNearMisses}");

        if (killer != null)
        {
            killer.AddReward(5f);
            Debug.Log($"<color=red>[{killer.gameObject.name}] KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
            killer.EndEpisode();
        }

        EndEpisode();
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