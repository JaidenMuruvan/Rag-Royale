using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class RoundManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private KillerShotManager killerShotManager;

    [SerializeField]
    public KnockdownManager knockdownManager;

    [SerializeField]
    private CountdownManager countdownManager;

    [SerializeField]
    private SceneTransitionManager sceneTransition;

    [Header("Round Settings")]
    [SerializeField]
    private int roundsToWin = 2;

    [SerializeField]
    private float roundEndDelay = 2f;

    public int CurrentRound { get; private set; }
    public int P1RoundWins { get; private set; }
    public int P2RoundWins { get; private set; }

    private bool roundOver = false;
    private bool playersResolved = false;
    private bool roundStarted = false;

    private PlayerHealth p1Health;
    private PlayerHealth p2Health;
    private MultiplayerPlayerController p1Controller;
    private MultiplayerPlayerController p2Controller;

    //  events
    public UnityEvent<int> OnRoundStarted;
    public UnityEvent<int> OnRoundWon;
    public UnityEvent<int> OnMatchWon;
    public UnityEvent<int, int> OnScoreUpdated;

    private void Start()
    {
        //  persisted state from MatchData so round wins are righttt
        // even after a scene load.
        LoadFromMatchData();
    }

    private void Update()
    {
        if (!playersResolved)
        {
            TryResolvePlayerReferences();
            return;
        }

        if (!roundStarted)
        {
            roundStarted = true;
            StartCoroutine(BeginRound());
        }
    }

    // matchData integration stuff

    private void LoadFromMatchData()
    {
        if (MatchData.Instance == null)
        {
            Debug.LogWarning("[RoundManager] No MatchData instance found — using defaults.");
            CurrentRound = 1;
            P1RoundWins = 0;
            P2RoundWins = 0;
            roundsToWin = 2;
            return;
        }

        CurrentRound = MatchData.Instance.CurrentRound;
        P1RoundWins = MatchData.Instance.P1RoundWins;
        P2RoundWins = MatchData.Instance.P2RoundWins;
        roundsToWin = MatchData.Instance.RoundsToWin;
    }

    private void SaveToMatchData()
    {
        if (MatchData.Instance == null)
            return;

        MatchData.Instance.P1RoundWins = P1RoundWins;
        MatchData.Instance.P2RoundWins = P2RoundWins;
        MatchData.Instance.CurrentRound = CurrentRound;
    }

    private void TryResolvePlayerReferences()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);

        PlayerHealth found1 = null;
        PlayerHealth found2 = null;

        foreach (var c in controllers)
        {
            if (c.PlayerID == 1)
            {
                found1 = c.GetComponent<PlayerHealth>();
                p1Controller = c;
            }
            else if (c.PlayerID == 2)
            {
                found2 = c.GetComponent<PlayerHealth>();
                p2Controller = c;
            }
        }

        if (found1 == null || found2 == null)
            return;

        p1Health = found1;
        p2Health = found2;

        p1Health.OnPlayerDefeated.AddListener(() => OnPlayerDefeated(1));
        p2Health.OnPlayerDefeated.AddListener(() => OnPlayerDefeated(2));

        playersResolved = true;
    }

    // round flow

    private IEnumerator BeginRound()
    {
        OnScoreUpdated?.Invoke(P1RoundWins, P2RoundWins);
        OnRoundStarted?.Invoke(CurrentRound);

        if (countdownManager != null)
        {
            countdownManager.StartCountdown();

            bool countdownDone = false;
            countdownManager.OnCountdownFinished.AddListener(() => countdownDone = true);
            yield return new WaitUntil(() => countdownDone);
        }

        roundOver = false;
    }

    private void OnPlayerDefeated(int playerID)
    {
        if (roundOver)
            return;
        EndRound(winnerID: playerID == 1 ? 2 : 1);
    }

    private void EndRound(int winnerID)
    {
        if (roundOver)
            return;
        roundOver = true;

        if (winnerID == 1)
            P1RoundWins++;
        else
            P2RoundWins++;

        SaveToMatchData();

        OnRoundWon?.Invoke(winnerID);
        OnScoreUpdated?.Invoke(P1RoundWins, P2RoundWins);

        bool matchOver = P1RoundWins >= roundsToWin || P2RoundWins >= roundsToWin;

        if (matchOver)
        {
            StartCoroutine(EndMatch(winnerID));
        }
        else
        {
            CurrentRound++;
            MatchData.Instance.CurrentRound = CurrentRound;

            StartCoroutine(TransitionToNextRound());
        }
    }

    private IEnumerator TransitionToNextRound()
    {
        yield return new WaitForSeconds(roundEndDelay);

        if (sceneTransition != null)
        {
            sceneTransition.LoadNextRoundScene(CurrentRound);
        }
        else
        {
            // Fallback: load directly if no SceneTransitionManager is wired.
            Debug.LogWarning(
                "[RoundManager] SceneTransitionManager not assigned — loading scene directly."
            );
            if (CurrentRound < MatchData.RoundScenes.Length)
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    MatchData.RoundScenes[CurrentRound]
                );
        }
    }

    private IEnumerator EndMatch(int winnerID)
    {
        yield return new WaitForSeconds(roundEndDelay);
        OnMatchWon?.Invoke(winnerID);
    }

    public void Debug_ForceEndRound(int winnerID) => EndRound(winnerID);
}
