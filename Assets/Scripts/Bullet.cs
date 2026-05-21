using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private Transform vfxHitGreen;
    [SerializeField] private Transform vfxHitRed;
    [SerializeField] private LayerMask hitLayers;

    private Rigidbody bulletRigidBody;
    private float bulletSpeed = 80f;
    private float damage = 100f;
    private bool hitOnce;
    private bool nearMissRegistered = false;
    private Player ownerAgent;

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
        Destroy(gameObject, 2f);
        hitOnce = false;
    }

    public void SetOwner(Player agent)
    {
        ownerAgent = agent;
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"BULLET HIT: {other.name} (Tag: {other.tag}) owner:{other.GetComponentInParent<Player>()}");
        if (bulletRigidBody == null) return;

        if (other.transform.IsChildOf(ownerAgent.transform)) return;

        Player victim = other.GetComponentInParent<Player>();
        Vector3 direction = bulletRigidBody.velocity.normalized;

        if (other.CompareTag("NearMiss") && !nearMissRegistered)
        {
            nearMissRegistered = true;
            if (victim != null) victim.OnNearMissDetected();
            return;
        }
        if (other.CompareTag("Player") && !hitOnce)
        {
            if (victim != null) victim.TakeDamage(damage, ownerAgent);
            hitOnce = true;
            Destroy(gameObject);
        }
        else if (!other.isTrigger && !hitOnce)
        {
            Destroy(gameObject);
        }    

        // Spawn visual effect
        RaycastHit hit;
        if (Physics.Raycast(transform.position - direction * 0.5f, direction, out hit, 1f, hitLayers))
        {
           /* SpawnSplat(
                victim ? vfxHitGreen : vfxHitRed,
                hit.point,
                hit.normal,
                other.transform
            );*/
        }
        else
        {
           Vector3 hitPoint = other.ClosestPoint(transform.position);
           Vector3 hitNormal = (transform.position - hitPoint).normalized;
          /* SpawnSplat(
              victim ? vfxHitGreen : vfxHitRed,
              hitPoint,
              hitNormal,
              other.transform
           );*/
        }
    }

    private void SpawnSplat(Transform prefab, Vector3 hitPoint, Vector3 hitNormal, Transform hitTransform)
    {
       Quaternion rotation = Quaternion.identity;
        Vector3 spawnPos = hitPoint;
        Transform splat = Instantiate(prefab, spawnPos, rotation);

        if (hitTransform != null)
            splat.SetParent(hitTransform);

        Destroy(splat.gameObject, 2f);
    }
}