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
        shotFired = false;

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

        distance = Vector3.Distance(transform.position, opponentAgent.transform.position);

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

        _lookYOffset = Mathf.Clamp(_lookYOffset + (lookYInput * Time.deltaTime * 10f), -2f, 4f);

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
                AddReward(-0.01f);
                shotsFired++;

                if (!iCanSeeEnemy) AddReward(-0.005f);
            }
        }

        UpdateAgentMode();
        AddReward(-0.0005f);

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
                AddReward(-0.008f);
            }

            Vector3 toEnemy = (opponentAgent.transform.position - transform.position).normalized;
            float gunAlignment = Vector3.Dot(_shooter.gunBarrel.forward, toEnemy);

            /*if (distance >= 8f && distance <= 20f)
            {
                float optimalness = 1f - Mathf.Abs(distance - 12f) / 8f;
                AddReward(0.005f * optimalness);
            }
            */
            if (currentMode == AgentMode.Defensive)
            {
                if (!iCanSeeEnemy && IsInCover()) {
                    stressCounter = 0;
                    AddReward(0.001f);
                }
                else AddReward(-0.0005f);
            }
            
            else if (currentMode == AgentMode.Offensive)
            {
                if (iCanSeeEnemy)
                    AddReward(0.001f);
                else AddReward(-0.0005f);

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
            else AddReward(-1f);*/
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
        sensor.AddObservation(_shooter.aimRig != null ? _shooter.aimRig.weight : 0f);

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

    public void RegisterHit(string tag, GameObject hitObject)
    {
        if (_isSpawning) return;
        if (tag == targetTag)
        {
            if (distance < 3f)
            {
                //AddReward(0.1f);
                Debug.Log($"<color=yellow>[{gameObject.name}] Too Close! ({distance:F1}m) </color>");
            }
            else if (distance >= 3f && distance < 20f)
            {               

                Debug.Log($"<color=green>[{gameObject.name}] HIT!</color> Dist: {distance:F1}m");
            }
            else
            {
                AddReward(5f);
                Debug.Log($"<color=cyan>[{gameObject.name}] Sniper hit! ({distance:F1}m) </color>");
            }
        }
        else if (tag == "NearMiss")
        {
            AddReward(0.1f);
        }
        else
        {
            AddReward(-0.005f);
        }
    }

    public void OnNearMissDetected()
    {
        if (_isSpawning) return;

        isUnderFire = true;
        underFireTimer = 3.0f;
        stressCounter++;
        totalNearMisses++;
        AddReward(-0.05f);
    }

    public void TakeDamage(float damage, Player attacker)
    {
        if (_isSpawning || CurrentHealth <= 0) return;

        isUnderFire = true;
        underFireTimer = 3.0f;
        stressCounter += 2;

        float baseReward = 2.0f;
        float hitReward = 1f;

        float distanceBonus = Mathf.Clamp((distance - 3f) / 8f, 0f, 6f);
        if (distance >3f)  hitReward = distanceBonus > 1.0f ? baseReward + 1f * distanceBonus : baseReward;

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
        AddReward(-2.0f);
        killer.AddReward(4f);
        Debug.Log($"[{gameObject.name}] DIED. Shots: {shotsFired} / {totalNearMisses}");

        
            if (distance > 3f && distance < 20f)
            {
                //killer.AddReward(5f + (distance / 10f));
                Debug.Log($"<color=red>[{killer.gameObject.name}] KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
            }
            else if (distance >= 20)
            {
                //killer.AddReward(10f);
                Debug.Log($"<color=red>[{killer.gameObject.name}]Far KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
            }
            else
            {
                //killer.AddReward(0.5f);
                Debug.Log($"<color=orange>{killer.gameObject.name}] CLOSE RANGE KILL! ({killer.shotsFired} / {killer.totalNearMisses})</color>");
            }

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