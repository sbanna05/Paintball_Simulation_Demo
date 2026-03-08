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
    public float moveSpeed = 4f;
    public float sprintMultiplier = 2f;
    public float turnSpeed = 150f;

    [Header("References")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform enemyTarget;
    [SerializeField] private Transform[] agentSpawnPoints;
    [SerializeField] private Transform[] enemySpawnPoints;

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

    private float maxEpisodeTime = 100f;
    private float episodeTimer;
    private string targetTag = "Player";
    private float distance;

    public override void Initialize()
    {
        _controller = GetComponent<CharacterController>();
        _shooter = GetComponent<ShooterController>();
        _anim = GetComponent<Animator>();
        _rigBuilder = GetComponent<RigBuilder>();
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        StartCoroutine(ResetScene());
    }

    private IEnumerator ResetScene()
    {
        _isSpawning = true;
        _controller.enabled = false;
        shotsFired = 0;
        _lookYOffset = 0;

        if (agentSpawnPoints.Length > 0)
        {
            int idx = Random.Range(0, agentSpawnPoints.Length);
            transform.SetPositionAndRotation(agentSpawnPoints[idx].position, agentSpawnPoints[idx].rotation);
        }

        if (enemyTarget && enemySpawnPoints.Length > 0)
            enemyTarget.position = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)].position;

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

        _lookYOffset = Mathf.Clamp(_lookYOffset + (lookYInput * Time.deltaTime * 2f), -1.5f, 1.5f);

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
        sensor.AddObservation(currentVelocity / 10f); // Normalizálva max ~10 m/s-re

        sensor.AddObservation(_shooter.CooldownProgress());

        sensor.AddObservation(_shooter.aimRig != null ? _shooter.aimRig.weight : 0f);

        if (enemyTarget != null)
        {
            Vector3 toEnemy = enemyTarget.position - transform.position;
            distance = toEnemy.magnitude;

            Vector3 localEnemyDir = transform.InverseTransformDirection(toEnemy.normalized);
            sensor.AddObservation(localEnemyDir);

            sensor.AddObservation(Mathf.Clamp(distance / 50f, 0f, 1f));

            Vector3 enemyForward = transform.InverseTransformDirection(enemyTarget.transform.forward);
            sensor.AddObservation(enemyForward);

            if (_shooter != null && _shooter.gunBarrel != null)
            {
                Vector3 gunDir = _shooter.gunBarrel.forward;
                float gunAlignment = Vector3.Dot(gunDir, toEnemy.normalized);
                sensor.AddObservation(gunAlignment);
            }
        }
    }

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (_isSpawning) return;

        if (tag == targetTag)
        {
            AddReward(15f);
            Debug.Log($"<color=green>[{gameObject.name}] DAMAGE!</color> Shots: {shotsFired} ");
            EndEpisode();
        }
        else if (tag == "NearMiss")
        {
            AddReward(0.15f);
        }
        else
        {
            AddReward(-0.05f);
        }
    }

    public void GetHit()
    {
        if (_isSpawning) return;
        AddReward(-20f);
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