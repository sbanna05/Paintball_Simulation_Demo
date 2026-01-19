using UnityEngine;
using Cinemachine;
using StarterAssets;
using UnityEngine.Animations.Rigging;

public class ShooterController : MonoBehaviour
{
    [Header("Rigging & Aim")]
    [SerializeField] private Rig aimRig;
    [SerializeField] private Transform aimTargetTransform;
    [SerializeField] private LayerMask aimColliderLayerMask;

    [Header("Shooting")]
    [SerializeField] private Transform bulletPrefab;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float shootCooldown = 0.3f;

    private StarterAssetsInputs inputs;
    private SimplePlayerAgent agent;
    private float lastShootTime;

    private void Awake()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        agent = GetComponent<SimplePlayerAgent>();
    }

    private void LateUpdate()
    {
        Vector3 targetPoint;

        if (agent.StepCount > 0 && !agent.IsHeuristic())
        {
            targetPoint = transform.position + transform.forward * 50f;
        }
        else
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = Camera.main.ScreenPointToRay(screenCenter);
            targetPoint = ray.origin + ray.direction * 50f;

            if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimColliderLayerMask))
            {
                targetPoint = hit.point;
            }
        }

        // Rigging célpont frissítése
        aimTargetTransform.position = Vector3.Lerp(aimTargetTransform.position, targetPoint, Time.deltaTime * 25f);

        float targetWeight = inputs.aim ? 1f : 0f;
        aimRig.weight = Mathf.Lerp(aimRig.weight, targetWeight, Time.deltaTime * 10f);

        if (inputs.aim && inputs.shoot && Time.time >= lastShootTime + shootCooldown)
        {
            Shoot(targetPoint);
            lastShootTime = Time.time;
            inputs.shoot = false;
        }
    }

    private void Shoot(Vector3 targetPoint)
    {
        // A lövés iránya a fegyver csövétõl a Rigging célpontja felé
        Vector3 shootDir = (targetPoint - spawnpoint.position).normalized;
        Transform b = Instantiate(bulletPrefab, spawnpoint.position, Quaternion.LookRotation(shootDir));

        if (b.TryGetComponent<Bullet>(out var bulletScript))
        {
            bulletScript.SetOwner(agent);
        }
    }
}