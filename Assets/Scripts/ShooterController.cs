using UnityEngine;
using Cinemachine;
using StarterAssets;
using UnityEngine.Animations.Rigging;

public class ShooterController : MonoBehaviour
{
    [Header("Camera & Sensitivity")]
    [SerializeField] private CinemachineVirtualCamera aimVirtualCamera;
    [SerializeField] private float normalSensitivity = 0.5f;
    [SerializeField] private float aimSensitivity = 0.8f;
    [SerializeField] private LayerMask aimColliderLayerMask;

    [Header("Shooting")]
    [SerializeField] private Transform bullet;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private float shootCooldown = 0.4f;

    [Header("Rigging")]
    [SerializeField] private Rig aimRig;

    private ThirdPersonController thirdPersonController;
    public StarterAssetsInputs starterAssetsInputs;
    private Animator animator;
    private SimplePlayerAgent agent;

    private float aimRigWeight;
    private float lastShootTime;

    private void Awake()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
        agent = GetComponent<SimplePlayerAgent>();
    }

    private void Update()
    {
        // Smooth rig weight
        aimRigWeight = starterAssetsInputs.aim ? 1f : 0f;

        if (aimRig != null)
        {
            aimRig.weight = Mathf.Lerp(aimRig.weight, aimRigWeight, Time.deltaTime * 20f);
        }

        if (animator != null)
        {
            animator.SetLayerWeight(1, aimRig != null ? aimRig.weight : aimRigWeight);
            animator.SetBool("canAim", starterAssetsInputs.aim);
        }

        // Camera
        if (aimVirtualCamera != null)
        {
            aimVirtualCamera.gameObject.SetActive(starterAssetsInputs.aim);
        }

        // ThirdPersonController settings
        if (thirdPersonController != null)
        {
            thirdPersonController.setSensitivity(starterAssetsInputs.aim ? aimSensitivity : normalSensitivity);
            thirdPersonController.setRotateOnMove(!starterAssetsInputs.aim);
        }

        // Get aim point
        Vector3 aimPoint = GetAimPoint();

        // Rotate towards aim when aiming
        if (starterAssetsInputs.aim)
        {
            Vector3 lookDir = aimPoint - transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Lerp(transform.forward, lookDir.normalized, Time.deltaTime * 20f);
            }
        }

        // Shooting with cooldown
        if (starterAssetsInputs.shoot && Time.time >= lastShootTime + shootCooldown)
        {
            Shoot(aimPoint);
            lastShootTime = Time.time;
            starterAssetsInputs.shoot = false; // Reset
        }
    }

    private Vector3 GetAimPoint()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);
        Vector3 defaultPoint = transform.position + transform.forward * 20f;

        if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimColliderLayerMask))
        {
            return hit.point;
        }

        return defaultPoint;
    }

    private void Shoot(Vector3 aimPoint)
    {
        if (bullet == null || spawnpoint == null)
            return;

        Vector3 shootDir = (aimPoint - spawnpoint.position).normalized;
        GameObject bulletInstance = Instantiate(bullet.gameObject, spawnpoint.position, Quaternion.LookRotation(shootDir, Vector3.up));

        // Set owner
        Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
        if (bulletScript != null && agent != null)
        {
            bulletScript.SetOwner(agent);
        }

        if (animator != null)
        {
            animator.SetTrigger("shoot");
        }
    }
}