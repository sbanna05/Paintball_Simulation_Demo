using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Bullet : MonoBehaviour
{
    [SerializeField] private Transform vfxHitGreen;
    [SerializeField] private Transform vfxHitRed;
    [SerializeField] private LayerMask hitLayers; // Mire lõhetsz
    private Rigidbody bulletRigidBody;
    private float bulletSpeed = 40f;
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
        // Raycast a pontos találati ponthoz
        RaycastHit hit;
        Vector3 direction = bulletRigidBody.velocity.normalized;
        if (Physics.Raycast(transform.position - direction * 0.5f, direction, out hit, 1f))
        {
            Debug.Log("Raycast HIT at: " + hit.point);
            // Pontos találati pont és felület irány
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;
            // Festék folt forgatása a felület szerint (90 fokkal billentve)
            Quaternion rotation = Quaternion.FromToRotation(Vector3.back, hitNormal);
            // Kicsit kijjebb a felülettõl (z-fighting ellen)
            Vector3 spawnPos = hitPoint + hitNormal * 0.02f;
            // Célpont vagy fal?
            if (other.GetComponent<Target>() != null)
            {
                // Zöld festék - találat
                if (vfxHitGreen != null)
                {
                    Debug.Log("Spawning GREEN splat at: " + spawnPos);
                    Transform splat = Instantiate(vfxHitGreen, spawnPos, rotation);
                    splat.parent = other.transform;
                }
                else
                {
                    Debug.LogError("vfxHitGreen is NULL!");
                }
            }
            else
            {
                // Piros festék - nem célpont
                if (vfxHitRed != null)
                {
                    Debug.Log("Spawning RED splat at: " + spawnPos);
                    Transform splat = Instantiate(vfxHitRed, spawnPos, rotation);
                    splat.parent = other.transform;
                    // Ha túl kicsi a prefab, állítsd nagyobbra
                    if (splat.localScale.magnitude < 0.1f)
                        splat.localScale = Vector3.one * 0.5f;
                }
                else
                {
                    Debug.LogError("vfxHitRed is NULL!");
                }
            }
        }
        else
        {
            Debug.Log("Raycast MISSED - using approximation");
            // Ha raycast nem talált, használj közelítést
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;
            Quaternion rotation = Quaternion.LookRotation(hitNormal);
            if (other.GetComponent<Target>() != null)
            {
                if (vfxHitGreen != null)
                {
                    Transform splat = Instantiate(vfxHitGreen, hitPoint, rotation);
                    splat.parent = other.transform;
                    splat.localScale = Vector3.one * 0.5f;
                }
            }
            else
            {
                if (vfxHitRed != null)
                {
                    Transform splat = Instantiate(vfxHitRed, hitPoint, rotation);
                    splat.parent = other.transform;
                    splat.localScale = Vector3.one * 0.5f;
                }
            }
        }
        // Lövedék törlése
        Destroy(gameObject);
    }
}