using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(RoundManager))]
public class Round3Manager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ThrowSystem throwSystem;

    [SerializeField]
    private RoundTimer roundTimer;

    [SerializeField]
    private KillerShotManager killerShotManager;

    [SerializeField]
    private CountdownManager countdownManager;

    private RoundManager roundManager;

    private void Awake()
    {
        roundManager = GetComponent<RoundManager>();
        if (killerShotManager != null)
            killerShotManager.SetRound(3);
    }

    private void Start()
    {
        DisableCombat();

        if (countdownManager != null)
            countdownManager.OnCountdownFinished.AddListener(OnCountdownFinished);

        if (roundTimer != null)
            roundTimer.OnTimerExpired.AddListener(DecideWinner);

        if (throwSystem != null)
            throwSystem.OnBothExhausted.AddListener(DecideWinner);
    }

    private void OnCountdownFinished()
    {
        roundTimer?.StartTimer();
    }

    private void DecideWinner()
    {
        roundTimer?.StopTimer();

        PlayerHealth p1Health = null;
        PlayerHealth p2Health = null;

        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c.PlayerID == 1)
                p1Health = c.GetComponent<PlayerHealth>();
            else if (c.PlayerID == 2)
                p2Health = c.GetComponent<PlayerHealth>();
        }

        if (p1Health == null || p2Health == null)
        {
            Debug.LogWarning("[Round3Manager] Could not find both PlayerHealth components.");
            roundManager.Debug_ForceEndRound(1);
            return;
        }

        int winner;
        if (p1Health.CurrentHealth > p2Health.CurrentHealth)
            winner = 1;
        else if (p2Health.CurrentHealth > p1Health.CurrentHealth)
            winner = 2;
        else
        {
            // Exact tie — whoever threw last / random; extend if needed
            winner = Random.Range(0, 2) == 0 ? 1 : 2;
            Debug.Log("[Round3Manager] health tie — random winner picked.");
        }

        roundManager.Debug_ForceEndRound(winner);
    }

    private void DisableCombat()
    {
        var combatSystems = FindObjectsByType<CombatSystem>(FindObjectsSortMode.None);
        foreach (var cs in combatSystems)
            cs.SetCombatEnabled(false);
    }
}
