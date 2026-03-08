using UnityEngine;
using UnityEngine.Animations.Rigging;
using Cinemachine;

public class ShooterController : MonoBehaviour
{
    [Header("Rigging & Camera")]
    [SerializeField] private CinemachineVirtualCamera aimCamera;
    [SerializeField] private LayerMask aimMask;
    public Rig aimRig;

    [Header("Shooting")]
    public Transform gunBarrel;
    public Transform bulletPrefab;
    public float shootCooldown = 0.3f;
    private float _lastShootTime;

    private Animator _anim;
    private Player _agent;
    private Camera _mainCamera;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _agent = GetComponent<Player>();
        _mainCamera = Camera.main;
    }

    public void SetAimState(bool isAiming, Transform aimTargetTransform, float lookYInput)
    {
        if (aimCamera) aimCamera.gameObject.SetActive(isAiming);

        if (isAiming)
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = _mainCamera.ScreenPointToRay(screenCenter);

            Vector3 targetPosition;
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, aimMask))
            {
                targetPosition = hit.point;
            }
            else
            {
                targetPosition = ray.origin + ray.direction * 50f;
            }

            targetPosition.y += lookYInput * 2f;

            aimTargetTransform.position = Vector3.Lerp(aimTargetTransform.position, targetPosition, Time.deltaTime * 30f);
        }
        else
        {
            // Alaphelyzet: a karakter előtt egy fix pontban, lookY eltolással
            Vector3 defaultPos = transform.position + transform.forward * 5f + transform.up * (1.5f + lookYInput);
            aimTargetTransform.position = Vector3.Lerp(aimTargetTransform.position, defaultPos, Time.deltaTime * 10f);
        }

        // Rig és Animátor frissítés
        float targetWeight = isAiming ? 1f : 0f;
        aimRig.weight = Mathf.Lerp(aimRig.weight, targetWeight, Time.deltaTime * 15f);

        if (_anim)
        {
            _anim.SetLayerWeight(1, aimRig.weight);
            _anim.SetBool("canAim", isAiming);
        }
    }

    public bool Shoot(Transform aimTarget)
    {
        if (Time.time < _lastShootTime + shootCooldown) return false;

        Vector3 shootDir = (aimTarget.position - gunBarrel.position).normalized;
        Transform b = Instantiate(bulletPrefab, gunBarrel.position, Quaternion.LookRotation(shootDir));

        if (b.TryGetComponent<Bullet>(out var bullet))
            bullet.SetOwner(_agent);

        if (_anim) _anim.SetTrigger("shoot");
        _lastShootTime = Time.time;
        return true;
    }

    public float CooldownProgress() => Mathf.Clamp01((Time.time - _lastShootTime) / shootCooldown);
}