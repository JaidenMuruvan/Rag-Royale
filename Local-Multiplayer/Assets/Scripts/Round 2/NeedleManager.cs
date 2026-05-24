using UnityEngine;
using UnityEngine.Events;

public class NeedleManager : MonoBehaviour
{
    [Header("Pile Settings")]
    [SerializeField]
    private int startingPileCount = 20;

    [SerializeField]
    private int killerShotThreshold = 5;

    [SerializeField]
    private int stealAmount = 3;

    [Header("Collection Settings")]
    [SerializeField]
    private float collectHoldTime = 0.6f;

    [SerializeField]
    private float depositHoldTime = 0.4f;

    [Header("Zone References")]
    [SerializeField]
    private CollectZone pileZone;

    [SerializeField]
    private DepositZone p1DepositZone;

    [SerializeField]
    private DepositZone p2DepositZone;

    [Header("References")]
    [SerializeField]
    private RoundTimer roundTimer;

    [SerializeField]
    private KillerShotManager killerShotManager;

    [SerializeField]
    private NeedleSpawner needleSpawner;

    //  events
    public UnityEvent<int> OnPileCountChanged;
    public UnityEvent<int, int> OnPlayerNeedleCountChanged;
    public UnityEvent<int> OnKillerShotConditionMet;
    public UnityEvent<int> OnNeedleStolen;
    public UnityEvent<int> OnRoundWinner;

    //  public state
    public int PileCount { get; private set; }
    public int P1NeedleCount { get; private set; }
    public int P2NeedleCount { get; private set; }

    // private state
    private bool killerShotTriggered = false;
    private bool roundOver = false;

    private bool p1CollectToggle = false;
    private bool p2CollectToggle = false;
    private bool p1CollectHeld = false;
    private bool p2CollectHeld = false;

    private bool p1Carrying = false;
    private bool p2Carrying = false;

    // Hold timers
    private float p1CollectTimer = 0f;
    private float p1DepositTimer = 0f;
    private float p2CollectTimer = 0f;
    private float p2DepositTimer = 0f;

    private bool p1InPile = false;
    private bool p2InPile = false;
    private bool p1InDeposit = false;
    private bool p2InDeposit = false;

    private MultiplayerPlayerController p1Controller;
    private MultiplayerPlayerController p2Controller;
    private bool playersResolved = false;

    private void Start()
    {
        PileCount = startingPileCount;
        OnPileCountChanged?.Invoke(PileCount);
        WireZones();
        if (roundTimer != null)
            roundTimer.OnTimerExpired.AddListener(CompareAndDecideWinner);
        if (killerShotManager != null)
            killerShotManager.OnKillerShotWinner.AddListener(OnKillerShotWon);
    }

    private void Update()
    {
        if (!playersResolved)
        {
            TryResolvePlayerReferences();
            return;
        }
        if (roundOver)
            return;

        HandlePlayer(
            1,
            p1CollectHeld,
            p1InPile,
            p1InDeposit,
            p1Carrying,
            ref p1CollectTimer,
            ref p1DepositTimer
        );
        HandlePlayer(
            2,
            p2CollectHeld,
            p2InPile,
            p2InDeposit,
            p2Carrying,
            ref p2CollectTimer,
            ref p2DepositTimer
        );
    }

    private void OnDestroy()
    {
        UnwireZones();
        if (killerShotManager != null)
            killerShotManager.OnKillerShotWinner.RemoveListener(OnKillerShotWon);
    }

    private void HandlePlayer(
        int id,
        bool held,
        bool inPile,
        bool inDeposit,
        bool carrying,
        ref float collectTimer,
        ref float depositTimer
    )
    {
        if (held)
        {
            // Not carrying yet — try to collect from pile
            if (!carrying && inPile && PileCount > 0)
            {
                collectTimer += Time.deltaTime;
                if (collectTimer >= collectHoldTime)
                {
                    collectTimer = 0f;
                    TryCollect(id);
                }
            }
            // caarrrying and standing in deposit zone — deposit
            else if (carrying && inDeposit)
            {
                depositTimer += Time.deltaTime;
                if (depositTimer >= depositHoldTime)
                {
                    depositTimer = 0f;
                    DoDeposit(id);

                    SetToggle(id, false);
                }
            }
        }
        else
        {
            collectTimer = 0f;
            depositTimer = 0f;
        }
    }

    public void ToggleCollect(int playerID)
    {
        bool currentToggle = playerID == 1 ? p1CollectToggle : p2CollectToggle;
        bool carrying = playerID == 1 ? p1Carrying : p2Carrying;
        bool inDeposit = playerID == 1 ? p1InDeposit : p2InDeposit;

        if (!currentToggle)
        {
            SetToggle(playerID, true);
        }
        else
        {
            SetToggle(playerID, false);

            if (carrying)
            {
                if (inDeposit)
                    DoDeposit(playerID);
                else
                    ReturnToPile(playerID);
            }
        }
    }

    private void SetToggle(int playerID, bool value)
    {
        if (playerID == 1)
        {
            p1CollectToggle = value;
            p1CollectHeld = value;
        }
        else
        {
            p2CollectToggle = value;
            p2CollectHeld = value;
        }
    }

    private void TryCollect(int playerID)
    {
        bool alreadyCarrying = playerID == 1 ? p1Carrying : p2Carrying;
        if (alreadyCarrying)
            return;

        bool success = needleSpawner == null || needleSpawner.CollectPhysical(playerID);
        if (!success)
            return;

        PileCount--;
        if (playerID == 1)
        {
            P1NeedleCount++;
            p1Carrying = true;
        }
        else
        {
            P2NeedleCount++;
            p2Carrying = true;
        }

        OnPileCountChanged?.Invoke(PileCount);
        OnPlayerNeedleCountChanged?.Invoke(playerID, playerID == 1 ? P1NeedleCount : P2NeedleCount);
        CheckKillerShotCondition();
    }

    private void DoDeposit(int playerID)
    {
        bool carrying = playerID == 1 ? p1Carrying : p2Carrying;
        if (!carrying)
            return;

        needleSpawner?.DepositPhysical(playerID);

        if (playerID == 1)
            p1Carrying = false;
        else
            p2Carrying = false;

        OnPlayerNeedleCountChanged?.Invoke(playerID, playerID == 1 ? P1NeedleCount : P2NeedleCount);
    }

    private void ReturnToPile(int playerID)
    {
        bool carrying = playerID == 1 ? p1Carrying : p2Carrying;
        if (!carrying)
            return;

        needleSpawner?.ReturnHeldNeedleToPile(playerID);

        PileCount++;
        if (playerID == 1)
        {
            P1NeedleCount--;
            p1Carrying = false;
        }
        else
        {
            P2NeedleCount--;
            p2Carrying = false;
        }

        OnPileCountChanged?.Invoke(PileCount);
        OnPlayerNeedleCountChanged?.Invoke(playerID, playerID == 1 ? P1NeedleCount : P2NeedleCount);
    }

    // Killer shot

    private void CheckKillerShotCondition()
    {
        if (killerShotTriggered || PileCount > killerShotThreshold)
            return;
        killerShotTriggered = true;
        killerShotManager?.ActivateKillerShotForRound2();
        OnKillerShotConditionMet?.Invoke(0);
    }

    private void OnKillerShotWon(int winnerID)
    {
        if (!killerShotTriggered)
            return;
        int loserID = winnerID == 1 ? 2 : 1;
        int loserCount = loserID == 1 ? P1NeedleCount : P2NeedleCount;
        int actualSteal = Mathf.Min(stealAmount, loserCount);
        if (actualSteal <= 0)
            return;

        if (loserID == 1)
            P1NeedleCount -= actualSteal;
        else
            P2NeedleCount -= actualSteal;
        if (winnerID == 1)
            P1NeedleCount += actualSteal;
        else
            P2NeedleCount += actualSteal;

        OnPlayerNeedleCountChanged?.Invoke(winnerID, winnerID == 1 ? P1NeedleCount : P2NeedleCount);
        OnPlayerNeedleCountChanged?.Invoke(loserID, loserID == 1 ? P1NeedleCount : P2NeedleCount);
        OnNeedleStolen?.Invoke(winnerID);
        SaveToMatchData();
    }

    // Zone wiring

    private void WireZones()
    {
        if (pileZone != null)
        {
            pileZone.OnPlayerEnter += id => SetInPile(id, true);
            pileZone.OnPlayerExit += id => SetInPile(id, false);
        }
        if (p1DepositZone != null)
        {
            p1DepositZone.OnPlayerEnter += id => SetInDeposit(id, true);
            p1DepositZone.OnPlayerExit += id => SetInDeposit(id, false);
        }
        if (p2DepositZone != null)
        {
            p2DepositZone.OnPlayerEnter += id => SetInDeposit(id, true);
            p2DepositZone.OnPlayerExit += id => SetInDeposit(id, false);
        }
    }

    private void UnwireZones()
    {
        if (pileZone != null)
        {
            pileZone.OnPlayerEnter -= id => SetInPile(id, true);
            pileZone.OnPlayerExit -= id => SetInPile(id, false);
        }
        if (p1DepositZone != null)
        {
            p1DepositZone.OnPlayerEnter -= id => SetInDeposit(id, true);
            p1DepositZone.OnPlayerExit -= id => SetInDeposit(id, false);
        }
        if (p2DepositZone != null)
        {
            p2DepositZone.OnPlayerEnter -= id => SetInDeposit(id, true);
            p2DepositZone.OnPlayerExit -= id => SetInDeposit(id, false);
        }
    }

    private void SetInPile(int id, bool v)
    {
        if (id == 1)
            p1InPile = v;
        else
            p2InPile = v;
    }

    private void SetInDeposit(int id, bool v)
    {
        if (id == 1)
            p1InDeposit = v;
        else
            p2InDeposit = v;
    }

    // Player reference resolution

    private void TryResolvePlayerReferences()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c.PlayerID == 1 && p1Controller == null)
            {
                p1Controller = c;
                p1Controller.OnCollectPressed += () => ToggleCollect(1);
            }
            else if (c.PlayerID == 2 && p2Controller == null)
            {
                p2Controller = c;
                p2Controller.OnCollectPressed += () => ToggleCollect(2);
            }
        }
        if (p1Controller != null && p2Controller != null)
            playersResolved = true;
    }

    // Winner + persistence

    public void CompareAndDecideWinner()
    {
        if (roundOver)
            return;
        roundOver = true;
        SaveToMatchData();
        int winner =
            P1NeedleCount > P2NeedleCount ? 1
            : P2NeedleCount > P1NeedleCount ? 2
            : 0;
        OnRoundWinner?.Invoke(winner);
    }

    private void SaveToMatchData()
    {
        if (MatchData.Instance == null)
            return;
        MatchData.Instance.P1NeedleCount = P1NeedleCount;
        MatchData.Instance.P2NeedleCount = P2NeedleCount;
    }

    public int GetStealAmount() => stealAmount;

    public bool KillerShotFired() => killerShotTriggered;
}
