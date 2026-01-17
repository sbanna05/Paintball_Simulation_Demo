using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using StarterAssets;

[RequireComponent(typeof(CharacterController))]
public class Player : Agent
{
    [Header("Refs")]
    [SerializeField] private ShooterController shooter;
    [SerializeField] private Transform enemyTransform;
    [SerializeField] private Transform spawnCenter;
    [SerializeField] private Vector3 spawnSize = new Vector3(20, 0, 20);

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 200f;

    [Header("Rewards & Penalties")]
    [SerializeField] private float hitReward = 10.0f;
    [SerializeField] private float nearMissPenalty = -0.1f;
    [SerializeField] private float stepPenalty = -0.002f;
    [SerializeField] private float timeoutPenalty = -5.0f;

    private CharacterController cc;
    private StarterAssetsInputs inputs;
    private float lastShootTime;
    private float shootCooldown = 0.5f;

    public override void Initialize()
    {
        cc = GetComponent<CharacterController>();
        inputs = GetComponent<StarterAssetsInputs>();
    }

    public override void OnEpisodeBegin()
    {
        // Csak akkor mozgatjuk, ha nem ő a "statikus célpont"
        Vector3 pos = spawnCenter.position + new Vector3(
            Random.Range(-spawnSize.x / 2, spawnSize.x / 2),
            0,
            Random.Range(-spawnSize.z / 2, spawnSize.z / 2)
        );
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformDirection(cc.velocity));
        sensor.AddObservation(transform.forward);

        if (enemyTransform != null)
        {
            Vector3 dirToEnemy = (enemyTransform.position - transform.position).normalized;
            sensor.AddObservation(dirToEnemy);
            sensor.AddObservation(Vector3.Distance(transform.position, enemyTransform.position) / 50f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(stepPenalty);

        if (MaxStep > 0 && StepCount >= MaxStep)
        {
            OnTimeout();
            return;
        }

        // Mozgás Continuous Actions alapokon
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float rotate = actions.ContinuousActions[2];

        Vector3 move = transform.forward * moveZ + transform.right * moveX;
        cc.SimpleMove(move * moveSpeed);
        transform.Rotate(Vector3.up, rotate * rotateSpeed * Time.deltaTime);

        // Discrete Actions: Aim (0/1), Shoot (0/1)
        inputs.aim = actions.DiscreteActions[0] == 1;
        bool shootCommand = actions.DiscreteActions[1] == 1;

        // --- TANULÁSI GYORSÍTÓK (Mivel a célpont áll) ---
        bool canSeeEnemy = CheckLineOfSight();

        if (canSeeEnemy)
        {
            // Kis jutalom, ha rátartja a célkeresztet
            AddReward(0.001f);
        }

        if (shootCommand && Time.time > lastShootTime + shootCooldown)
        {
            inputs.shoot = true;
            lastShootTime = Time.time;

            if (inputs.aim && canSeeEnemy)
                AddReward(0.1f); // Nagyobb jutalom a sikeres "rámutatásért" és lövésért
            else
                AddReward(-0.05f); // Büntetés, ha a semmibe lövöldözik
        }
    }

    private bool CheckLineOfSight()
    {
        if (enemyTransform == null) return false;
        Vector3 direction = enemyTransform.position - (transform.position + Vector3.up);
        if (Physics.Raycast(transform.position + Vector3.up, direction, out RaycastHit hit, 50f))
        {
            // Fontos: a statikus célponton is legyen fent a tag!
            if (hit.transform.CompareTag("Player_2") || hit.transform.IsChildOf(enemyTransform))
                return true;
        }
        return false;
    }

    public void RegisterNearMiss()
    {
        // Ha őt lövik (ő az álló ágens), kap egy kis negatívot, 
        // hogy jelezzük neki: ez nem jó állapot.
        AddReward(nearMissPenalty);
    }

    public void OnTargetHit()
    {
        Debug.Log("<color=green><b>SIKER: Célpont megsemmisítve!</b></color>");
        SetReward(hitReward);
        EndEpisode();
    }

    public void OnTimeout()
    {
        Debug.Log("<color=red>LEJÁRT AZ IDŐ: Stalemate.</color>");
        SetReward(timeoutPenalty);
        EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        var disc = actionsOut.DiscreteActions;
        cont[0] = Input.GetAxis("Horizontal");
        cont[1] = Input.GetAxis("Vertical");
        cont[2] = Input.GetAxis("Mouse X");
        disc[0] = Input.GetMouseButton(1) ? 1 : 0;
        disc[1] = Input.GetMouseButton(0) ? 1 : 0;
    }
}