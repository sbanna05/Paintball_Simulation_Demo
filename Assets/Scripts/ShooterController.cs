using UnityEngine;
using StarterAssets;
using UnityEngine.Animations.Rigging;
using Cinemachine;

public class ShooterController : MonoBehaviour
{
    [Header("Rigging & Camera")]
    [SerializeField] private CinemachineVirtualCamera aimCamera;
    [SerializeField] private Rig aimRig;
    [SerializeField] private Transform aimTarget;

    [Header("Shooting")]
    [SerializeField] private LayerMask aimMask;
    [SerializeField] private Transform bulletPrefab;
    [SerializeField] public Transform gunBarrel;
    [SerializeField] private float shootCooldown = 0.3f;

    [Header("Sensitivity")]
    [SerializeField] private float aimSensitivity = 0.5f;

    private StarterAssetsInputs _inputs;
    private SimplePlayerAgent _agent;
    private ThirdPersonController _tpc;
    private Animator _anim;
    private float _lastShootTime;

    private void Awake()
    {
        _inputs = GetComponent<StarterAssetsInputs>();
        _agent = GetComponent<SimplePlayerAgent>();
        _tpc = GetComponent<ThirdPersonController>();
        _anim = GetComponent<Animator>();
    }

    private void LateUpdate()
    {
        if (_inputs == null || aimTarget == null || Camera.main == null) return;

        // 1. Raycast a célzáshoz
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, aimMask))
        {
            aimTarget.position = Vector3.Lerp(aimTarget.position, hit.point, Time.deltaTime * 25f);
        }
        else
        {
            aimTarget.position = Vector3.Lerp(aimTarget.position, ray.origin + ray.direction * 50f, Time.deltaTime * 25f);
        }

        if (_inputs.aim)
        {
            aimCamera.gameObject.SetActive(true);
            _tpc.setRotateOnMove(false);
            _tpc.setSensitivity(aimSensitivity);

            Vector3 worldAimDir = Camera.main.transform.forward;
            worldAimDir.y = 0;
            if (worldAimDir.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, worldAimDir.normalized, Time.deltaTime * 30f);
            }
        }
        else
        {
            aimCamera.gameObject.SetActive(false);
            _tpc.setRotateOnMove(true);
        }

        float targetWeight = _inputs.aim ? 1f : 0f;
        aimRig.weight = Mathf.Lerp(aimRig.weight, targetWeight, Time.deltaTime * 20f);

        _anim.SetBool("canAim", _inputs.aim);
        _anim.SetLayerWeight(1, aimRig.weight);

        if (_inputs.aim && _inputs.shoot && Time.time > _lastShootTime + shootCooldown)
        {
            Shoot();
            _lastShootTime = Time.time;
            _inputs.shoot = false;
        }
    }

    private void Shoot()
    {
        Vector3 dir = (aimTarget.position - gunBarrel.position).normalized;
        Transform b = Instantiate(bulletPrefab, gunBarrel.position, Quaternion.LookRotation(dir));
        if (b.TryGetComponent<Bullet>(out var bullet))
        {
            bullet.SetOwner(_agent);
        }

        _anim.SetTrigger("shoot");
        if (_agent != null) _agent.OnShotFired();
    }
}