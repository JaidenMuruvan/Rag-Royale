using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(RoundManager))]
public class Round2Manager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private NeedleManager needleManager;

    [SerializeField]
    private RoundTimer roundTimer;

    [SerializeField]
    private KillerShotManager killerShotManager;

    [SerializeField]
    private CountdownManager countdownManager;

    private RoundManager roundManager;
    private bool timerPausedForKillerShot = false;

    private void Awake()
    {
        roundManager = GetComponent<RoundManager>();
        if (killerShotManager != null)
            killerShotManager.SetRound(2);
    }

    private void Start()
    {
        DisableCombat();

        if (countdownManager != null)
            countdownManager.OnCountdownFinished.AddListener(OnCountdownFinished);

        if (roundTimer != null)
            roundTimer.OnTimerExpired.AddListener(OnTimerExpired);

        if (needleManager != null)
            needleManager.OnRoundWinner.AddListener(OnNeedleRoundWinner);

        if (killerShotManager != null)
        {
            killerShotManager.OnKillerShotPhaseStarted.AddListener(_ => PauseTimer());
            killerShotManager.OnKillerShotPhaseEnded.AddListener(ResumeTimer);
        }
    }

    private void OnCountdownFinished() => roundTimer?.StartTimer();

    private void OnTimerExpired() => needleManager?.CompareAndDecideWinner();

    private void OnNeedleRoundWinner(int winnerID)
    {
        // Save needle counts for Round 3 — graceful when no MatchData (standalone)
        if (MatchData.Instance != null)
        {
            MatchData.Instance.P1NeedleCount =
                needleManager != null ? needleManager.P1NeedleCount : 0;
            MatchData.Instance.P2NeedleCount =
                needleManager != null ? needleManager.P2NeedleCount : 0;
        }

        // 0 = draw; resolve to P1 for now (extend with overtime if needed)
        roundManager.Debug_ForceEndRound(winnerID == 0 ? 1 : winnerID);
    }

    private void PauseTimer()
    {
        if (roundTimer != null && roundTimer.IsRunning)
        {
            roundTimer.PauseTimer();
            timerPausedForKillerShot = true;
        }
    }

    private void ResumeTimer()
    {
        if (timerPausedForKillerShot && roundTimer != null)
        {
            roundTimer.ResumeTimer();
            timerPausedForKillerShot = false;
        }
    }

    private void DisableCombat()
    {
        var combatSystems = FindObjectsByType<CombatSystem>(FindObjectsSortMode.None);
        foreach (var cs in combatSystems)
            cs.SetCombatEnabled(false);
    }
}
