using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public class GameController : MonoBehaviour
{
    [Header("Agents")]
    [SerializeField] private PaintballAgent agent1;
    [SerializeField] private PaintballAgent agent2;

    [Header("Episode Settings")]
    [SerializeField] private float maxEpisodeTime = 180f; 
    [SerializeField] private float stalemateWarningTime = 90f; 

    [Header("Statistics")]
    [SerializeField] private bool showDebugInfo = true;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;


    private float episodeStartTime;
    private float currentEpisodeTime;
    private bool episodeActive;

    // Statistics
    private int agent1Wins = 0;
    private int agent2Wins = 0;
    private int stalemates = 0;
    private int totalEpisodes = 0;

    private List<int> availableSpawnIndices = new List<int>();


    private void Start()
    {
        // Validate setup
        if (agent1 == null || agent2 == null)
        {
            Debug.LogError("GameController: Both agents must be assigned!");
            enabled = false;
            return;
        }

        StartNewEpisode();
    }

    private void Update()
    {
        if (!episodeActive) return;

        currentEpisodeTime = Time.time - episodeStartTime;

        // Check for stalemate warning
        if (currentEpisodeTime > stalemateWarningTime)
        {
            // Small penalty for both agents to encourage action
            agent1.AddReward(-0.001f);
            agent2.AddReward(-0.001f);
        }

        // Check for timeout
        if (currentEpisodeTime >= maxEpisodeTime)
        {
            OnTimeout();
        }

        // Debug display
        if (showDebugInfo)
        {
            DisplayDebugInfo();
        }
    }

    private Transform GetUniqueSpawn()
    {
        if (availableSpawnIndices.Count == 0)
        {
            Debug.LogError("No available spawn points!");
            return null;
        }

        int randomListIndex = Random.Range(0, availableSpawnIndices.Count);
        int spawnIndex = availableSpawnIndices[randomListIndex];

        availableSpawnIndices.RemoveAt(randomListIndex);

        return spawnPoints[spawnIndex];
    }


    private void StartNewEpisode()
    {
        episodeStartTime = Time.time;
        currentEpisodeTime = 0f;
        episodeActive = true;

        totalEpisodes++;

        // --- Spawn index lista újratöltése ---
        availableSpawnIndices.Clear();
        for (int i = 0; i < spawnPoints.Length; i++)
            availableSpawnIndices.Add(i);

        // --- Agent 1 spawn ---
        Transform spawn1 = GetUniqueSpawn();
        agent1.ResetAgent(spawn1);

        // --- Agent 2 spawn ---
        Transform spawn2 = GetUniqueSpawn();
        agent2.ResetAgent(spawn2);

        if (showDebugInfo)
        {
            Debug.Log($"=== NEW EPISODE STARTED === (#{totalEpisodes})");
            Debug.Log($"Agent1 spawn: {spawn1.name}");
            Debug.Log($"Agent2 spawn: {spawn2.name}");
        }
    }


    public void OnAgentKilled(PaintballAgent killedAgent)
    {
        if (!episodeActive) return;

        episodeActive = false;

        // Determine winner
        PaintballAgent winner = (killedAgent == agent1) ? agent2 : agent1;

        // Update statistics
        if (winner == agent1)
        {
            agent1Wins++;
            Debug.Log($"<color=green>Agent 1 WINS!</color> (Time: {currentEpisodeTime:F2}s)");
        }
        else
        {
            agent2Wins++;
            Debug.Log($"<color=blue>Agent 2 WINS!</color> (Time: {currentEpisodeTime:F2}s)");
        }

        // Notify agents
        winner.OnVictory();
        // Killed agent already got penalty in TakeDamage

        // Start new episode after delay
        Invoke(nameof(StartNewEpisode), 2f);
    }

    private void OnTimeout()
    {
        if (!episodeActive) return;

        episodeActive = false;
        stalemates++;

        Debug.Log($"<color=yellow>STALEMATE!</color>");

        // Both agents get timeout penalty
        agent1.AddReward(-0.001f);
        agent2.AddReward(-0.001f);

        // Immediate restart for stalemates
        Invoke(nameof(StartNewEpisode), 1f);
    }

    private void DisplayDebugInfo()
    {
        // Display on screen using GUI (optional)
        string info = $"Episode Time: {currentEpisodeTime:F1}s / {maxEpisodeTime}s\n";
        info += $"Agent 1: Health={agent1.GetHealth():F0}, Mode={agent1.GetMode()}, UnderFire={agent1.IsUnderFire()}\n";
        info += $"Agent 2: Health={agent2.GetHealth():F0}, Mode={agent2.GetMode()}, UnderFire={agent2.IsUnderFire()}\n";
        info += $"\nStatistics (Total: {totalEpisodes})\n";
        info += $"Agent 1 Wins: {agent1Wins} ({GetWinRate(agent1Wins):F1}%)\n";
        info += $"Agent 2 Wins: {agent2Wins} ({GetWinRate(agent2Wins):F1}%)\n";
        info += $"Stalemates: {stalemates} ({GetWinRate(stalemates):F1}%)";

        // This would need a UI Text component to display
        // For now, we can use Debug.Log or implement OnGUI
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        string info = $"<b>PAINTBALL SIMULATION</b>\n\n";
        info += $"Episode: {totalEpisodes + 1}\n";
        info += $"Time: {currentEpisodeTime:F1}s / {maxEpisodeTime}s\n\n";

        info += $"<color=lime>AGENT 1</color>\n";
        info += $"  Health: {agent1.GetHealth():F0}/100\n";
        info += $"  Mode: {agent1.GetMode()}\n";
        info += $"  Under Fire: {(agent1.IsUnderFire() ? "YES" : "NO")}\n\n";

        info += $"<color=cyan>AGENT 2</color>\n";
        info += $"  Health: {agent2.GetHealth():F0}/100\n";
        info += $"  Mode: {agent2.GetMode()}\n";
        info += $"  Under Fire: {(agent2.IsUnderFire() ? "YES" : "NO")}\n\n";

        info += $"<b>STATISTICS</b>\n";
        info += $"Total Episodes: {totalEpisodes}\n";
        info += $"Agent 1 Wins: {agent1Wins} ({GetWinRate(agent1Wins):F1}%)\n";
        info += $"Agent 2 Wins: {agent2Wins} ({GetWinRate(agent2Wins):F1}%)\n";
        info += $"Stalemates: {stalemates} ({GetWinRate(stalemates):F1}%)";

        GUI.Box(new Rect(10, 10, 300, 350), info, style);
    }

    private float GetWinRate(int wins)
    {
        if (totalEpisodes == 0) return 0f;
        return (wins / (float)totalEpisodes) * 100f;
    }

    // Public getters for agents
    public float GetCurrentEpisodeTime() => currentEpisodeTime;
    public float GetRemainingTime() => maxEpisodeTime - currentEpisodeTime;
    public bool IsEpisodeActive() => episodeActive;
}