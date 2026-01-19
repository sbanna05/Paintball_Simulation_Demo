using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private Transform vfxHitGreen;
    [SerializeField] private Transform vfxHitRed;
    [SerializeField] private LayerMask hitLayers;

    private Rigidbody bulletRigidBody;
    private float bulletSpeed = 50f;
    private SimplePlayerAgent ownerAgent;

    private void Awake()
    {
        bulletRigidBody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (bulletRigidBody != null)
        {
            bulletRigidBody.velocity = transform.forward * bulletSpeed;
        }
        Destroy(gameObject, 3f);
    }

    public void SetOwner(SimplePlayerAgent agent)
    {
        ownerAgent = agent;
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"BULLET HIT: {other.name} (Tag: {other.tag})");

        if (bulletRigidBody == null)
            return;

        if (other.transform.IsChildOf(ownerAgent.transform)) return;

        //Debug.Log("Ütközés: " + other.name + " Layer: "  + LayerMask.LayerToName(other.gameObject.layer));
        //ownerAgent.RegisterHit(other.tag, other.gameObject);

        Vector3 direction = bulletRigidBody.velocity.normalized;

        // Check what we hit
        bool hitTarget = other.CompareTag("Player_2"); // Direct hit on target
        bool hitNearMiss = other.CompareTag("NearMiss"); // Near miss

        // Near miss
        if (hitNearMiss && ownerAgent != null)
        {
            //Debug.Log("NEAR MISS!");
            ownerAgent.RegisterHit("NearMiss", other.gameObject);
            return;
        }
        else if (hitTarget)
        {

            //Debug.Log("DIRECT HIT ON TARGET!");
            ownerAgent.RegisterHit("Player_2", other.gameObject);
            Destroy(gameObject);
            return;
        }
        else if (!other.isTrigger)
        {
            ownerAgent.RegisterHit(other.tag, other.gameObject);
            Destroy(gameObject);
        }

        // Spawn visual effect
        RaycastHit hit;
        if (Physics.Raycast(transform.position - direction * 0.5f, direction, out hit, 1f, hitLayers))
        {
            /*SpawnSplat(
                hitTarget ? vfxHitGreen : vfxHitRed,
                hit.point,
                hit.normal,
                other.transform
            );*/
        }
        else
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            /*SpawnSplat(
                hitTarget ? vfxHitGreen : vfxHitRed,
                hitPoint,
                hitNormal,
                other.transform
            );*/
        }

    }

    private void SpawnSplat(Transform prefab, Vector3 hitPoint, Vector3 hitNormal, Transform hitTransform)
    {
       Quaternion rotation = Quaternion.LookRotation(-hitNormal);
        Vector3 spawnPos = hitPoint + hitNormal * 0.002f;
        Transform splat = Instantiate(prefab, spawnPos, rotation);

        if (hitTransform != null)
        {
            splat.SetParent(hitTransform);
        }

        Destroy(splat.gameObject, 10f);
    }
}