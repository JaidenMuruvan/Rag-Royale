using UnityEngine;

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

    [Header("Juice / VFX")]
    [SerializeField]
    private Round3VFX vfx;

    private RoundManager roundManager;
    private PlayerHealth p1Health;
    private PlayerHealth p2Health;

    private bool p1KillerShotFired = false;
    private bool p2KillerShotFired = false;
    private bool roundOver = false;
    private bool playersResolved = false;

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
        {
            throwSystem.OnBothExhausted.AddListener(DecideWinner);
            throwSystem.OnPowerThrowReady.AddListener(OnPowerThrowReady);
            throwSystem.OnPowerThrowUsed.AddListener(OnPowerThrowUsed);
            throwSystem.OnThrowFired.AddListener(OnThrowFired);
        }

        if (killerShotManager != null)
            killerShotManager.OnKillerShotPhaseStarted.AddListener(OnKillerShotPhaseStarted);
    }

    private void Update()
    {
        if (!playersResolved && !roundOver)
            TryResolvePlayerHealth();
    }

    private void OnDestroy()
    {
        if (p1Health != null)
            p1Health.OnKillerShotTriggered.RemoveListener(OnP1KillerShotTriggered);
        if (p2Health != null)
            p2Health.OnKillerShotTriggered.RemoveListener(OnP2KillerShotTriggered);
    }

    private void TryResolvePlayerHealth()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c.PlayerID == 1 && p1Health == null)
            {
                p1Health = c.GetComponent<PlayerHealth>();
                if (p1Health != null)
                    p1Health.OnKillerShotTriggered.AddListener(OnP1KillerShotTriggered);
            }
            else if (c.PlayerID == 2 && p2Health == null)
            {
                p2Health = c.GetComponent<PlayerHealth>();
                if (p2Health != null)
                    p2Health.OnKillerShotTriggered.AddListener(OnP2KillerShotTriggered);
            }
        }

        if (p1Health != null && p2Health != null)
            playersResolved = true;
    }

    private void OnP1KillerShotTriggered()
    {
        if (p1KillerShotFired || roundOver)
            return;
        p1KillerShotFired = true;
        killerShotManager?.ActivateKillerShotPhase_Round3(1);
    }

    private void OnP2KillerShotTriggered()
    {
        if (p2KillerShotFired || roundOver)
            return;
        p2KillerShotFired = true;
        killerShotManager?.ActivateKillerShotPhase_Round3(2);
    }

    private void OnCountdownFinished() => roundTimer?.StartTimer();

    private void OnKillerShotPhaseStarted(int triggeringPlayerID) =>
        vfx?.PlayKillerShotWarning(triggeringPlayerID);

    private void OnPowerThrowReady(int playerID)
    {
        vfx?.PlayPowerThrowReady(playerID);
        Debug.Log($"[Round3] P{playerID} POWER THROW READY");
    }

    private void OnPowerThrowUsed(int playerID) => vfx?.StopPowerThrowReady(playerID);

    private void OnThrowFired(int playerID, bool isPower)
    {
        if (isPower)
            vfx?.PlayPowerThrowLaunch(playerID);
        else
            vfx?.PlayNormalThrowLaunch(playerID);
    }

    public void DecideWinner()
    {
        if (roundOver)
            return;
        roundOver = true;

        roundTimer?.StopTimer();

        if (p1Health == null || p2Health == null)
        {
            Debug.LogWarning("[Round3Manager] Could not find both PlayerHealth components.");
            roundManager.Debug_ForceEndRound(1);
            return;
        }

        float p1Hp = p1Health.CurrentHealth;
        float p2Hp = p2Health.CurrentHealth;

        int winner;
        if (p1Hp > p2Hp)
            winner = 1;
        else if (p2Hp > p1Hp)
            winner = 2;
        else
        {
            winner = Random.Range(0, 2) == 0 ? 1 : 2;
            Debug.Log("[Round3Manager] Health tie — random winner chosen.");
        }

        vfx?.PlayRoundEndFanfare(winner);
        Debug.Log($"[Round3Manager] Round over — P{winner} wins. HP: P1={p1Hp:F1} P2={p2Hp:F1}");
        roundManager.Debug_ForceEndRound(winner);
    }

    private void DisableCombat()
    {
        var combatSystems = FindObjectsByType<CombatSystem>(FindObjectsSortMode.None);
        foreach (var cs in combatSystems)
            cs.SetCombatEnabled(false);
    }
}
