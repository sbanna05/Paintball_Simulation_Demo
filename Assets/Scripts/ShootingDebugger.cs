using UnityEngine;

/// <summary>
/// Attach to Player to visualize shooting mechanics
/// </summary>
public class ShootingDebugger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool enableVisualization = true;
    [SerializeField] private Transform target;
    [SerializeField] private Transform gunBarrel;
    [SerializeField] private LayerMask aimMask;

    [Header("Stats")]
    [SerializeField] private int totalShots = 0;
    [SerializeField] private int shotsOnTarget = 0;
    [SerializeField] private int shotsNearMiss = 0;
    [SerializeField] private int shotsMissed = 0;

    private Vector3 lastAimPoint;
    private Vector3 lastShootDirection;
    private bool justShot = false;
    private float shotTime;

    private void Update()
    {
        if (!enableVisualization || target == null)
            return;

        // Get current aim point (same as ShooterController)
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);

        if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimMask))
        {
            lastAimPoint = hit.point;
        }
        else
        {
            lastAimPoint = transform.position + transform.forward * 20f;
        }

        // Reset shot flag after 0.5s
        if (justShot && Time.time - shotTime > 0.5f)
        {
            justShot = false;
        }
    }

    // Call this when bullet is fired
    public void OnShot(Vector3 shootDirection)
    {
        totalShots++;
        lastShootDirection = shootDirection;
        justShot = true;
        shotTime = Time.time;

        // Check if aiming at target
        if (gunBarrel != null && target != null)
        {
            Vector3 dirToTarget = (target.position - gunBarrel.position).normalized;
            float accuracy = Vector3.Dot(shootDirection, dirToTarget);

            Debug.Log($"<color=cyan>SHOT #{totalShots} - Accuracy: {accuracy:F3} (1.0 = perfect)</color>");
        }
    }

    public void OnHit(string tag)
    {
        if (tag == "Player_2")
            shotsOnTarget++;
        else if (tag == "NearMiss")
            shotsNearMiss++;
        else
            shotsMissed++;
    }

    private void OnDrawGizmos()
    {
        if (!enableVisualization || target == null)
            return;

        // Draw line to target
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, target.position);

        // Draw current aim point
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(lastAimPoint, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, lastAimPoint);

        // Draw last shot direction
        if (justShot && gunBarrel != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(gunBarrel.position, lastShootDirection * 30f);
        }

        // Draw player forward
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward * 5f);
    }

    private void OnGUI()
    {
        if (!enableVisualization)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = 14;

        string stats = $"<b>SHOOTING STATS</b>\n";
        stats += $"Total Shots: {totalShots}\n";
        stats += $"On Target: {shotsOnTarget}\n";
        stats += $"Near Miss: {shotsNearMiss}\n";
        stats += $"Missed: {shotsMissed}\n";

        if (totalShots > 0)
        {
            float accuracy = (shotsOnTarget / (float)totalShots) * 100f;
            stats += $"Accuracy: {accuracy:F1}%";
        }

        GUI.Box(new Rect(Screen.width - 210, 10, 200, 140), stats, style);
    }
}