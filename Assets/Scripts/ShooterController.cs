using UnityEngine;
using Cinemachine;
using StarterAssets;
using UnityEngine.Animations.Rigging;

public class ShooterController : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera aimCam;
    [SerializeField] private LayerMask aimMask;
    [SerializeField] private Transform bulletPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float fireCooldown = 0.4f;
    [SerializeField] private Rig aimRig;

    private StarterAssetsInputs inputs;
    private ThirdPersonController tpc;
    private Animator animator;
    private Player agent;

    private float lastFire;
    private float rigWeight;

    private void Awake()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        tpc = GetComponent<ThirdPersonController>();
        animator = GetComponent<Animator>();
        agent = GetComponent<Player>();
    }

    private void Update()
    {
        aimRig.weight = Mathf.Lerp(aimRig.weight, rigWeight, Time.deltaTime * 20f);
        animator.SetLayerWeight(1, aimRig.weight);

        Ray ray = Camera.main.ScreenPointToRay(
            new Vector2(Screen.width / 2f, Screen.height / 2f)
        );

        Vector3 aimPoint = transform.forward * 10f;
        if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimMask))
            aimPoint = hit.point;

        if (inputs.aim)
        {
            aimCam.gameObject.SetActive(true);
            tpc.setRotateOnMove(false);
            rigWeight = 1f;

            Vector3 look = aimPoint - transform.position;
            look.y = 0;
            transform.forward = Vector3.Lerp(transform.forward, look.normalized, Time.deltaTime * 20f);

            if (inputs.shoot && Time.time - lastFire > fireCooldown)
            {
                Fire(aimPoint);
                lastFire = Time.time;
                inputs.shoot = false;
            }
        }
        else
        {
            rigWeight = 0f;
            aimCam.gameObject.SetActive(false);
            tpc.setRotateOnMove(true);
        }
    }

    private void Fire(Vector3 aimPoint)
    {
        Vector3 dir = (aimPoint - spawnPoint.position).normalized;
        Transform b = Instantiate(bulletPrefab, spawnPoint.position, Quaternion.LookRotation(dir));

        Bullet bullet = b.GetComponent<Bullet>();
        bullet.SetOwner(agent, GetComponent<Collider>());

        animator.SetTrigger("shoot");
    }
}
