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
    public float moveSpeed = 8f;
    public float sprintMultiplier = 3f;
    public float turnSpeed = 120f;

    [Header("Combat Settings")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public AgentMode currentMode = AgentMode.Offensive;

    [Header("Cover Settings")]
    [SerializeField] private LayerMask coverLayerMask;
    public float distance;
    public bool shotFired;

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

    public int stressCounter;
    public int totalNearMisses;
    private bool isUnderFire = false;
    private float underFireTimer = 0f;

    private float maxEpisodeTime = 80f;
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

        /*if (opponentAgent == null && transform.parent != null)
        {
            Player[] agents = transform.parent.GetComponentsInChildren<Player>();
            foreach (var a in agents) if (a != this) { opponentAgent = a; break; }
        }*/
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        CurrentHealth = maxHealth;
        shotsFired = 0;
        stressCounter = 0;
        totalNearMisses = 0;
        shotFired = false;

        isUnderFire = false;
        underFireTimer = 0f;

        if (Random.value > 0.7f)
            currentMode = AgentMode.Defensive;
        else
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

        distance = Vector3.Distance(transform.position, opponentAgent.transform.position);

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float lookX = actions.ContinuousActions[2];
        float lookY = actions.ContinuousActions[3];

        bool isAiming = actions.DiscreteActions[0] == 1;
        bool shootCommand = actions.DiscreteActions[1] == 1;
        bool shouldSprint = actions.DiscreteActions[2] == 1;

        float speed = (shouldSprint && !isAiming) ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 move = transform.forward * moveX + transform.right * moveZ;
        _controller.SimpleMove(move * speed);
        transform.Rotate(Vector3.up, lookX * turnSpeed * Time.deltaTime);

        _lookYOffset = Mathf.Clamp(_lookYOffset + (lookY * Time.deltaTime * 10f), -2f, 4f);

        if (_anim)
        {
            float targetAnimSpeed = move.magnitude * (speed / moveSpeed);
            _anim.SetFloat("Speed", Mathf.Lerp(_anim.GetFloat("Speed"), targetAnimSpeed, Time.deltaTime * 10f));
            _anim.SetFloat("MotionSpeed", 1f);
        }

        _shooter.SetAimState(isAiming, aimTarget, _lookYOffset);

        bool iCanSeeEnemy = CheckLineOfSight();
        if (isAiming && shootCommand)
        {
            shotFired = _shooter.Shoot(aimTarget); 
            if (shotFired)
            {
                AddReward(-0.02f);
                shotsFired++;

            }
        }

        AddReward(-0.0001f);

        if (opponentAgent != null)
        {
            // JAVÍTÁS #1: BODY ALIGNMENT REWARD
            /*float bodyDot = Vector3.Dot(transform.forward, toEnemy);
            if (bodyDot > 0.8f)
            {
                AddReward((bodyDot - 0.8f) * 0.004f);
            }*/

            if (distance < 3f)
            {
                AddReward(-0.02f);
            }

            Vector3 toEnemy = (opponentAgent.transform.position - transform.position).normalized;
            float gunAlignment = Vector3.Dot(_shooter.gunBarrel.forward, toEnemy);

            /*if (distance >= 8f && distance <= 20f)
            {
                float optimalness = 1f - Mathf.Abs(distance - 12f) / 8f;
                AddReward(0.005f * optimalness);
            }
            */
            if (distance >= 3f && distance <= 35f)
            {
                if (currentMode == AgentMode.Defensive)
                {
                    if (!iCanSeeEnemy && IsInCover())
                    {
                        stressCounter = 0;
                        AddReward(0.0001f);
                    }
                    else AddReward(-0.0001f);
                }

                else if (currentMode == AgentMode.Offensive)
                {
                    if (iCanSeeEnemy && gunAlignment > 0.7f)
                        AddReward(0.0001f);
                   // else AddReward(-0.0001f);
                }
                
                /*if (currentMode == AgentMode.Defensive && iCanSeeEnemy && !IsInCover())
                {
                    AddReward(-0.0001f);
                }
                else if (currentMode == AgentMode.Offensive && !iCanSeeEnemy)
                {
                    AddReward(-0.0001f);
                }*/
            }

            if (iCanSeeEnemy && shotFired && gunAlignment > 0.8)
                AddReward(0.02f);

            /*if (isAiming)
                AddReward(0.001f * gunAlignment);*/
        }

        if (episodeTimer >= maxEpisodeTime)
        {
            //AddReward(-1f);
            /*if (currentMode == AgentMode.Defensive) { AddReward(1f); }
            else {AddReward(-1f);}*/

            Debug.Log($"[{gameObject.name}] TIMEOUT - Shots: {shotsFired}/{totalNearMisses} Mode: {currentMode}");
            
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
        sensor.AddObservation(currentMode == AgentMode.Offensive ? 0f : 1f);

        if (opponentAgent != null)
        {
            Vector3 toEnemy = opponentAgent.transform.position - transform.position;

            sensor.AddObservation(transform.InverseTransformDirection(toEnemy.normalized));
            sensor.AddObservation(Mathf.Clamp(distance / 50f, 0f, 1f));
            sensor.AddObservation(transform.InverseTransformDirection(opponentAgent.transform.forward));

            float gunAlignment = Vector3.Dot(_shooter.gunBarrel.forward, toEnemy.normalized);
            sensor.AddObservation(gunAlignment);
        }

        sensor.AddObservation(CheckLineOfSight() ? 1f : 0f);
    }
    
    private void UpdateAgentMode()
    {
        bool lowHealth = CurrentHealth <= maxHealth * 0.5f;
        bool suppressed = isUnderFire && stressCounter > 2;

        if (lowHealth || suppressed)
                currentMode = AgentMode.Defensive;
        else
            currentMode = AgentMode.Offensive;
    }

    public void OnNearMissDetected()
    {
        if (_isSpawning) return;

        isUnderFire = true;
        underFireTimer = 3.0f;
        stressCounter++;
        totalNearMisses++;

        UpdateAgentMode();
        AddReward(-0.05f);
        opponentAgent.AddReward(0.1f);
    }

    public void TakeDamage(float damage, Player attacker)
    {
        if (_isSpawning || CurrentHealth <= 0) return;

        isUnderFire = true;
        underFireTimer = 3.0f;
        stressCounter += 2;

        UpdateAgentMode();

        float baseReward = 3.0f;
        float hitReward = 0f;

        float distanceBonus = Mathf.Clamp((distance - 3f) / 8f, 0f, 8f);
        if (distance > 3f)  hitReward = distanceBonus > 1.0f ? baseReward + 1f * distanceBonus : baseReward;

        attacker.AddReward(hitReward);
        AddReward(-hitReward);

        Debug.Log($"<color=white>[{attacker.gameObject.name}] HIT! (Dist: {distance:F1}m, +{hitReward:F2})</color>");
        CurrentHealth -= damage;
        //AddReward(-0.5f);

        if (CurrentHealth <= 0)
            Die(attacker);
    }

    private void Die(Player killer)
    {
        //AddReward(-4.0f);
        //killer.AddReward(6f);
        Debug.Log($"[{gameObject.name}] DIED. Shots: {shotsFired} / {totalNearMisses}");
        float killReward = 6f;

        if (distance > 3f && distance < 35f)
        {
           // killReward = 2f + (distance / 8f);
            Debug.Log($"<color=red>[{killer.gameObject.name}] KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
        }
        else if (distance >= 35)
        {
           // killReward = 8f;
            Debug.Log($"<color=red>[{killer.gameObject.name}]Far KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
        }
        else
        {
            killReward = -1f;
            Debug.Log($"<color=orange>{killer.gameObject.name}] CLOSE RANGE KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
        }

        killer.AddReward(killReward);
        AddReward(-killReward);

        killer.EndEpisode();

        EndEpisode();
    }

    private bool CheckLineOfSight()
    {
        if (opponentAgent == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = opponentAgent.transform.position + Vector3.up * 1.5f;
        Vector3 dir = target - origin;
        float rayDistance = dir.magnitude;
        dir.Normalize();

        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > 70f)
            return false;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayDistance, coverLayerMask))
        {
            Debug.DrawRay(origin, dir * hit.distance, Color.magenta);
            if (hit.collider.gameObject == opponentAgent.gameObject || hit.collider.transform.IsChildOf(opponentAgent.transform))
            {
                //Debug.Log($"ray to: {hit.collider.gameObject.name}");
                return true;
            }
        }
        return false;
    }

    private bool IsInCover()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 dir = (opponentAgent.transform.position - origin).normalized;

        if (Physics.Raycast(origin, dir, 60f, coverLayerMask))
            return true;

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