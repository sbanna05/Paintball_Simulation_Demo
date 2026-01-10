using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    [SerializeField] private Transform vfxHitGreen;
    [SerializeField] private Transform vfxHitRed;
    [SerializeField] private float damage = 100f; // Fatal shot

    [SerializeField] private LayerMask hitLayers;

    private Rigidbody bulletRigidBody;
    private float bulletSpeed = 50f;
    private PaintballAgent ownerAgent; // Who shot this bullet

    private void Awake()
    {
        bulletRigidBody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        bulletRigidBody.velocity = transform.forward * bulletSpeed;
        Destroy(gameObject, 3f);
    }

    // Set the agent who fired this bullet
    public void SetOwner(PaintballAgent owner)
    {
        ownerAgent = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore collision with owner
        if (ownerAgent != null && other.transform.IsChildOf(ownerAgent.transform))
        {
            return;
        }

        Debug.Log("BULLET TRIGGERED: " + other.name);

        Vector3 direction = bulletRigidBody.velocity.normalized;

        // Check if we hit an agent
        PaintballAgent hitAgent = other.GetComponentInParent<PaintballAgent>();

        // Check if this is the near miss collider or actual body
        bool isNearMissCollider = other.gameObject.name.Contains("NearMiss") ||
                                   other.gameObject.CompareTag("NearMiss");

        if (hitAgent != null && !isNearMissCollider)
        {
            // Direct hit on agent body!
            hitAgent.TakeDamage(damage);
            Debug.Log($"<color=red>DIRECT HIT!</color> {hitAgent.name} took {damage} damage!");
        }

        // Target keresése (for target practice)
        Target target = other.GetComponentInParent<Target>();

        // Raycast a pontos találati ponthoz
        RaycastHit hit;
        if (Physics.Raycast(transform.position - direction, direction, out hit, 1f, hitLayers))
        {
            SpawnSplat(
                (target != null || hitAgent != null) ? vfxHitGreen : vfxHitRed,
                hit.point,
                hit.normal,
                other.transform
            );
        }
        else
        {
            // Raycast MISSED - using approximation
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            SpawnSplat(
                (target != null || hitAgent != null) ? vfxHitGreen : vfxHitRed,
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
            Debug.LogWarning("Splat Prefab is NULL!");
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, -hitNormal);
        Vector3 spawnPos = hitPoint + hitNormal * 0.002f;

        Transform splat = Instantiate(prefab, spawnPos, rotation);

        // RÖGZÍTÉS A FALHOZ/KARAKTERHEZ
        splat.SetParent(hitTransform);

        // Auto-destroy splat after some time (optional)
        Destroy(splat.gameObject, 10f);
    }
}