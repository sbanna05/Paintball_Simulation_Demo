using UnityEngine;
using StarterAssets;
using UnityEngine.Animations.Rigging;

public class ShooterController : MonoBehaviour
{
    [Header("Rigging")]
    [SerializeField] private Rig aimRig;
    [SerializeField] private Transform aimTarget;

    [Header("Shooting")]
    [SerializeField] private LayerMask aimMask;
    [SerializeField] private Transform bulletPrefab;
    [SerializeField] private Transform gunBarrel;
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
        if (agent == null || inputs == null || aimTarget == null) return;

        if (agent.IsHeuristic())
        {
            if (Camera.main != null)
            {
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                Ray ray = Camera.main.ScreenPointToRay(screenCenter);
                if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimMask))
                    aimTarget.position = hit.point;
                else
                    aimTarget.position = ray.origin + ray.direction * 50f;
            }
        }
        float targetWeight = inputs.aim ? 1f : 0f;
        aimRig.weight = Mathf.Lerp(aimRig.weight, targetWeight, Time.deltaTime * 20f);

        if (inputs.aim && inputs.shoot && Time.time >= lastShootTime + shootCooldown)
        {
            Shoot(aimTarget.position);
            lastShootTime = Time.time;
            inputs.shoot = false;
        }
    }

    private void Shoot(Vector3 targetWorldPos)
    {
        Vector3 shootDir = (targetWorldPos - gunBarrel.position).normalized;
        if (shootDir == Vector3.zero) shootDir = transform.forward;

        Transform b = Instantiate(bulletPrefab, gunBarrel.position, Quaternion.LookRotation(shootDir));
        if (b.TryGetComponent<Bullet>(out var bulletScript))
            bulletScript.SetOwner(agent);

        agent.OnShotFired();
    }
}