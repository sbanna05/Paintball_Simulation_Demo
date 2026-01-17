using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float bulletSpeed = 50f;
    private Rigidbody rb;
    private Player ownerAgent;
    private Collider ownerCol;

    private void Awake() => rb = GetComponent<Rigidbody>();

    private void Start()
    {
        rb.velocity = transform.forward * bulletSpeed;
        Destroy(gameObject, 3f);
    }

    public void SetOwner(Player owner, Collider col)
    {
        this.ownerAgent = owner;
        this.ownerCol = col;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == ownerCol) return;

        // Near Miss (Közelítés)
        if (other.CompareTag("NearMiss"))
        {
            Player victim = other.GetComponentInParent<Player>();
            if (victim != null && victim != ownerAgent)
            {
                victim.RegisterNearMiss();
            }
            return; // A golyó menjen tovább a test felé!
        }

        // Célpont eltalálása (Statikus vagy mozgó)
        if (other.CompareTag("Player_2"))
        {
            ownerAgent.OnTargetHit();
            Destroy(gameObject);
            return;
        }

        // Fal vagy föld
        Destroy(gameObject);
    }
}