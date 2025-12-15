using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    [SerializeField] private Transform vfxHitGreen;
    [SerializeField] private Transform vfxHitRed;
    // LayerMask-ot megtartjuk, ha szükséged van rá:
    [SerializeField] private LayerMask hitLayers;

    private Rigidbody bulletRigidBody;
    private float bulletSpeed = 50f;

    private void Awake()
    {
        bulletRigidBody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        bulletRigidBody.velocity = transform.forward * bulletSpeed;
        Destroy(gameObject, 3f);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("BULLET TRIGGERED: " + other.name);

        Vector3 direction = bulletRigidBody.velocity.normalized;

        // Target keresése a collideren vagy szülõjén
        Target target = other.GetComponentInParent<Target>();

        RaycastHit hit;
        // Raycast a pontos találati ponthoz
        // A hitLayers LayerMask-ot is beiktattuk
        if (Physics.Raycast(transform.position - direction, direction, out hit, 1f, hitLayers))
        {
            SpawnSplat(
                target ? vfxHitGreen : vfxHitRed,
                hit.point,
                hit.normal,
                other.transform
            );
        }
        else
        {
            // Raycast MISSED ág (Ha a Raycast valamiért nem talált, a Collider találatból próbálunk közelíteni)
            // Debug.Log("Raycast MISSED - using approximation");

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            SpawnSplat(
                target ? vfxHitGreen : vfxHitRed,
                hitPoint,
                hitNormal,
                other.transform
            );
        }

        Destroy(gameObject);
    }

    private void SpawnSplat(Transform prefab, Vector3 hitPoint, Vector3 hitNormal, Transform hitTransform)
    {
        if (!prefab)
        {
            Debug.LogError("Splat Prefab is NULL!");
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, hitNormal);
        Debug.Log("rotation: " + rotation);
        Vector3 spawnPos = hitPoint + hitNormal * 0.02f;
        
        Transform splat = Instantiate(prefab, spawnPos, rotation);

        // RÖGZÍTÉS A FALHOZ
        splat.SetParent(hitTransform);

        
    }
}