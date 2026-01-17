using UnityEngine;

public class NearMissDetector : MonoBehaviour
{
    [SerializeField] private Player ownerAgent; // Húzd be az ügynököt az inspektorban

    /*private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Bullet>())
        {
            ownerAgent.OnNearMiss();
        }
    }*/
}