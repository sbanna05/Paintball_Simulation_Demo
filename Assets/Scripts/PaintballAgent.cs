using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using StarterAssets;
using Cinemachine;
using UnityEngine.InputSystem;


public class PaintballAgent : Agent
{
    [Header("Agent References")]
    [SerializeField] private Transform enemyTransform;
    // GameController automatikusan megkeresve
    private GameController gameController;
    [SerializeField] private bool isMLControlled = true;


    [Header("Detection Settings")]
    [SerializeField] private float maxRaycastDistance = 50f;
    // raycastDirections ELTÁVOLÍTVA - Ray Perception Sensor használja
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private LayerMask coverLayerMask;

    // MEGJEGYZÉS: Ray Perception Sensor automatikusan hozzáadja:
    // - 7 ray * (1 hit/ray + 4 detectable tags) = ~35-50 observations

    [Header("Combat Settings")]
    [SerializeField] private float shootCooldown = 0.5f;
    [SerializeField] private float maxHealth = 100f;

    [Header("Near Miss Collider")]
    [SerializeField] private Collider nearMissCollider; // A nagyobb collider a közelihalálérzékeléshez

    // Components
    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private ShooterController shooterController;
    private Animator animator;

    // Agent State
    private float currentHealth;
    private float lastShootTime;
    private bool isUnderFire = false;
    private float underFireTimer = 0f;
    private AgentMode currentMode = AgentMode.Offensive;

    // Observations cache
    private float distanceToEnemy;
    private bool enemyVisible;
    private Vector3 directionToEnemy;

    public enum AgentMode
    {
        Offensive,
        Defensive
    }

    public override void Initialize()
    {
        // Get components
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        shooterController = GetComponent<ShooterController>();
        animator = GetComponent<Animator>();

        currentHealth = maxHealth;

        // Setup near miss detection
        if (nearMissCollider != null)
        {
            nearMissCollider.isTrigger = true;
        }

        gameController = FindObjectOfType<GameController>();
        if (gameController == null)
        {
            Debug.LogError("GameController NOT FOUND in scene!");
        }

        if (isMLControlled)
        {
            starterAssetsInputs.enablePlayerInput = false;
        }


    }

    public override void OnEpisodeBegin()
    {

    }

    public void ResetAgent(Transform spawnPoint)
    {
        currentHealth = maxHealth;
        isUnderFire = false;
        underFireTimer = 0f;
        currentMode = AgentMode.Offensive;
        lastShootTime = -shootCooldown;

        starterAssetsInputs.move = Vector2.zero;
        starterAssetsInputs.look = Vector2.zero;
        starterAssetsInputs.aim = false;
        starterAssetsInputs.shoot = false;
        starterAssetsInputs.jump = false;

        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Update enemy info
        if (enemyTransform != null)
        {
            directionToEnemy = (enemyTransform.position - transform.position);
            distanceToEnemy = directionToEnemy.magnitude;
            directionToEnemy.Normalize();

            // Check line of sight
            enemyVisible = CheckLineOfSight(enemyTransform.position);
        }

        // 1. Self state (7 observations)
        sensor.AddObservation(transform.localPosition); // 3
        sensor.AddObservation(transform.forward); // 3
        sensor.AddObservation(currentHealth / maxHealth); // 1 (normalized)

        // 2. Enemy information (5 observations)
        if (enemyTransform != null)
        {
            sensor.AddObservation(directionToEnemy); // 3
            sensor.AddObservation(distanceToEnemy / maxRaycastDistance); // 1 (normalized)
            sensor.AddObservation(enemyVisible ? 1f : 0f); // 1
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // 3
            sensor.AddObservation(0f); // 1
            sensor.AddObservation(0f); // 1
        }

        // 3. Ray Perception Sensor automatically adds environmental observations!
        // Nincs szükség manuális raycast-okra itt

        // 4. Combat state (3 observations)
        sensor.AddObservation(isUnderFire ? 1f : 0f); // 1
        sensor.AddObservation(starterAssetsInputs.aim ? 1f : 0f); // 1
        sensor.AddObservation((int)currentMode / 1f); // 1 (0=Offensive, 1=Defensive)

        // 5. Closest cover distance (1 observation)
        float closestCoverDistance = FindClosestCover();
        sensor.AddObservation(closestCoverDistance / maxRaycastDistance); // 1

        // Total MANUAL observations: 7 + 5 + 0 + 3 + 1 = 16
        // Ray Perception Sensor adds automatically: ~35-50 (depending on settings)
        // TOTAL: ~51-66 observations
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Debug: First action to verify ML is controlling
        if (Time.frameCount % 100 == 0) // Log every 100 frames
        {
            //Debug.Log($"{gameObject.name} - ML Action: Move={actions.DiscreteActions[0]}, Rotate={actions.DiscreteActions[1]}");
        }

        // Discrete actions
        int moveAction = actions.DiscreteActions[0]; // 0-4: forward, back, left, right, none
        int rotateAction = actions.DiscreteActions[1]; // 0-2: left, none, right
        int shootAction = actions.DiscreteActions[2]; // 0-1: no, yes
        int aimAction = actions.DiscreteActions[3]; // 0-1: normal, aim

        // Or Continuous actions (alternative)
        // float moveX = actions.ContinuousActions[0]; // -1 to 1
        // float moveZ = actions.ContinuousActions[1]; // -1 to 1
        // float rotate = actions.ContinuousActions[2]; // -1 to 1

        // Apply movement
        Vector2 moveInput = Vector2.zero;
        switch (moveAction)
        {
            case 0: moveInput = new Vector2(0, 1); break; // Forward
            case 1: moveInput = new Vector2(0, -1); break; // Back
            case 2: moveInput = new Vector2(-1, 0); break; // Left
            case 3: moveInput = new Vector2(1, 0); break; // Right
            case 4: moveInput = Vector2.zero; break; // None
        }
        starterAssetsInputs.move = moveInput;

        // Apply rotation
        Vector2 lookInput = Vector2.zero;
        switch (rotateAction)
        {
            case 0: lookInput = new Vector2(-1, 0); break; // Turn left
            case 1: lookInput = Vector2.zero; break; // No rotation
            case 2: lookInput = new Vector2(1, 0); break; // Turn right
        }
        starterAssetsInputs.look = lookInput;

        // Apply aim
        starterAssetsInputs.aim = (aimAction == 1);

        // Apply shoot (with cooldown)
        if (shootAction == 1 && Time.time > lastShootTime + shootCooldown)
        {
            starterAssetsInputs.shoot = true;
            lastShootTime = Time.time;

            // Small penalty for shooting without aim or without seeing enemy
            if (!starterAssetsInputs.aim || !enemyVisible)
            {
                AddReward(-0.02f);
            }
            else
            {
                AddReward(0.05f); // Reward for good positioning
            }
        }

        // Update agent mode based on situation
        UpdateAgentMode();

        // Time penalty to encourage faster resolution
        AddReward(-0.001f);

        // Reward for maintaining appropriate distance
        if (enemyTransform != null)
        {
            float optimalDistance = (currentMode == AgentMode.Offensive) ? 15f : 25f;
            float distanceDifference = Mathf.Abs(distanceToEnemy - optimalDistance);
            AddReward(-distanceDifference * 0.0001f);
        }

        // Update under fire timer
        if (isUnderFire)
        {
            underFireTimer += Time.fixedDeltaTime;

            // If still in open after near miss, penalty
            if (underFireTimer > 1f && !IsInCover())
            {
                AddReward(-0.05f);
            }

            // Reset after some time
            if (underFireTimer > 3f)
            {
                isUnderFire = false;
                underFireTimer = 0f;
            }
        }
    }

    private void UpdateAgentMode()
    {
        // Switch to defensive if:
        // - Low health
        // - Under fire
        // - Enemy has advantage

        if (currentHealth < maxHealth * 0.4f || isUnderFire)
        {
            if (currentMode != AgentMode.Defensive)
            {
                currentMode = AgentMode.Defensive;
                AddReward(0.02f); // Reward for smart mode switch
            }
        }
        else if (currentHealth > maxHealth * 0.7f && enemyVisible)
        {
            if (currentMode != AgentMode.Offensive)
            {
                currentMode = AgentMode.Offensive;
                AddReward(0.01f);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual control for testing
        var discreteActions = actionsOut.DiscreteActions;

        // Movement
        if (Input.GetKey(KeyCode.W))
            discreteActions[0] = 0; // Forward
        else if (Input.GetKey(KeyCode.S))
            discreteActions[0] = 1; // Back
        else if (Input.GetKey(KeyCode.A))
            discreteActions[0] = 2; // Left
        else if (Input.GetKey(KeyCode.D))
            discreteActions[0] = 3; // Right
        else
            discreteActions[0] = 4; // None

        // Rotation
        float mouseX = Input.GetAxisRaw("Mouse X");
        if (mouseX < -0.1f)
            discreteActions[1] = 0; // Balra forgás
        else if (mouseX > 0.1f)
            discreteActions[1] = 2; // Jobbra forgás
        else
            discreteActions[1] = 1; // Nincs forgás

        // Shoot
        discreteActions[2] = Input.GetMouseButton(0) ? 1 : 0;

        // Aim
        discreteActions[3] = Input.GetMouseButton(1) ? 1 : 0;
    }

    // Called by Bullet when hit
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        AddReward(-0.3f); // Penalty for taking damage

        if (currentHealth <= 0)
        {
            // Fatal hit
            AddReward(-1.0f);
            gameController.OnAgentKilled(this);
            EndEpisode();
        }
    }

    // Called by GameController when this agent wins
    public void OnVictory()
    {
        AddReward(1.0f);
        EndEpisode();
    }

    // Called by GameController on timeout
    public void OnTimeout()
    {
        AddReward(-0.5f); // Penalty for stalemate
        EndEpisode();
    }

    // Trigger for near miss detection
    private void OnTriggerEnter(Collider other)
    {
        // Check if bullet passed nearby (hit the near miss collider but not body)
        if (other.CompareTag("Bullet") || other.GetComponent<Bullet>() != null)
        {
            // Only trigger if bullet didn't hit main body
            Collider[] bodyColliders = GetComponentsInChildren<Collider>();
            bool hitMainBody = false;

            foreach (Collider col in bodyColliders)
            {
                if (col == nearMissCollider) continue;

                if (col.bounds.Contains(other.transform.position))
                {
                    hitMainBody = true;
                    break;
                }
            }

            if (!hitMainBody)
            {
                // Near miss!
                isUnderFire = true;
                underFireTimer = 0f;
                AddReward(-0.1f); // Warning penalty

                Debug.Log($"{gameObject.name} - Near miss detected!");
            }
        }
    }

    // Helper methods
    private bool CheckLineOfSight(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - (transform.position + Vector3.up);

        if (Physics.Raycast(transform.position + Vector3.up, direction, out RaycastHit hit, maxRaycastDistance))
        {
            // Check if we hit the enemy (not obstacle)
            if (hit.transform == enemyTransform || hit.transform.IsChildOf(enemyTransform))
            {
                return true;
            }
        }

        return false;
    }

    private float FindClosestCover()
    {
        Collider[] covers = Physics.OverlapSphere(transform.position, maxRaycastDistance, coverLayerMask);

        if (covers.Length == 0)
            return maxRaycastDistance;

        float closestDistance = maxRaycastDistance;

        foreach (Collider cover in covers)
        {
            float distance = Vector3.Distance(transform.position, cover.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        return closestDistance;
    }

    private bool IsInCover()
    {
        // Simple cover check: is there obstacle between agent and enemy?
        if (enemyTransform == null) return false;

        Vector3 dirToEnemy = enemyTransform.position - transform.position;

        if (Physics.Raycast(transform.position + Vector3.up, dirToEnemy, out RaycastHit hit, distanceToEnemy, coverLayerMask))
        {
            return true; // Something is blocking
        }

        return false;
    }

    // Getters
    public float GetHealth() => currentHealth;
    public AgentMode GetMode() => currentMode;
    public bool IsUnderFire() => isUnderFire;
}