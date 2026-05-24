using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Core system for Round 2.
/// Tracks the shared needle pile count and each player's stash count.
/// Handles collection (hold near pile), deposit (hold near deposit zone),
/// and triggers the killer shot when the pile drops to threshold.
///
/// STANDALONE TESTING: Works with no MatchData present.
/// All starting values fall back to Inspector fields.
/// </summary>
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
    private float collectRepeatInterval = 0.3f;

    [SerializeField]
    private float depositHoldTime = 0.5f;

    [SerializeField]
    private float depositRepeatInterval = 0.25f;

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

    // --- events ---
    public UnityEvent<int> OnPileCountChanged;
    public UnityEvent<int, int> OnPlayerNeedleCountChanged;
    public UnityEvent<int> OnKillerShotConditionMet;
    public UnityEvent<int> OnNeedleStolen;
    public UnityEvent<int> OnRoundWinner;

    // --- public state ---
    public int PileCount { get; private set; }
    public int P1NeedleCount { get; private set; }
    public int P2NeedleCount { get; private set; }

    // --- private state ---
    private bool killerShotTriggered = false;
    private bool roundOver = false;

    private bool p1CollectHeld = false;
    private bool p2CollectHeld = false;

    private float p1CollectHeldFor = 0f,
        p1CollectRepeat = 0f;
    private float p2CollectHeldFor = 0f,
        p2CollectRepeat = 0f;
    private float p1DepositHeldFor = 0f,
        p1DepositRepeat = 0f;
    private float p2DepositHeldFor = 0f,
        p2DepositRepeat = 0f;

    private bool p1InPile = false,
        p2InPile = false;
    private bool p1InDeposit = false,
        p2InDeposit = false;

    private MultiplayerPlayerController p1Controller;
    private MultiplayerPlayerController p2Controller;
    private bool playersResolved = false;

    private void Start()
    {
        PileCount = startingPileCount;
        OnPileCountChanged?.Invoke(PileCount);
        WireZones();
        WireTimer();
        WireKillerShot();
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

        HandleCollection(
            1,
            p1CollectHeld,
            p1InPile,
            p1InDeposit,
            ref p1CollectHeldFor,
            ref p1CollectRepeat,
            ref p1DepositHeldFor,
            ref p1DepositRepeat
        );
        HandleCollection(
            2,
            p2CollectHeld,
            p2InPile,
            p2InDeposit,
            ref p2CollectHeldFor,
            ref p2CollectRepeat,
            ref p2DepositHeldFor,
            ref p2DepositRepeat
        );

        Debug.Log(
            $"P1 held:{p1CollectHeld} inPile:{p1InPile}  P2 held:{p2CollectHeld} inPile:{p2InPile}"
        );
    }

    private void OnDestroy()
    {
        UnwireZones();
        if (killerShotManager != null)
            killerShotManager.OnKillerShotWinner.RemoveListener(OnKillerShotWon);
    }

    private void WireZones()
    {
        if (pileZone != null)
        {
            pileZone.OnPlayerEnter += OnEnterPile;
            pileZone.OnPlayerExit += OnExitPile;
        }
        if (p1DepositZone != null)
        {
            p1DepositZone.OnPlayerEnter += OnEnterDeposit;
            p1DepositZone.OnPlayerExit += OnExitDeposit;
        }
        if (p2DepositZone != null)
        {
            p2DepositZone.OnPlayerEnter += OnEnterDeposit;
            p2DepositZone.OnPlayerExit += OnExitDeposit;
        }
    }

    private void UnwireZones()
    {
        if (pileZone != null)
        {
            pileZone.OnPlayerEnter -= OnEnterPile;
            pileZone.OnPlayerExit -= OnExitPile;
        }
        if (p1DepositZone != null)
        {
            p1DepositZone.OnPlayerEnter -= OnEnterDeposit;
            p1DepositZone.OnPlayerExit -= OnExitDeposit;
        }
        if (p2DepositZone != null)
        {
            p2DepositZone.OnPlayerEnter -= OnEnterDeposit;
            p2DepositZone.OnPlayerExit -= OnExitDeposit;
        }
    }

    private void WireTimer()
    {
        if (roundTimer != null)
            roundTimer.OnTimerExpired.AddListener(CompareAndDecideWinner);
    }

    private void WireKillerShot()
    {
        if (killerShotManager != null)
            killerShotManager.OnKillerShotWinner.AddListener(OnKillerShotWon);
    }

    private void TryResolvePlayerReferences()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);

        foreach (var c in controllers)
        {
            Debug.Log($"  → name={c.gameObject.name} PlayerID={c.PlayerID}");
            if (c.PlayerID == 1 && p1Controller == null)
            {
                p1Controller = c;
                p1Controller.OnCollectPressed += () => SetCollectHeld(1, true);
                p1Controller.OnCollectReleased += () => SetCollectHeld(1, false);
            }
            else if (c.PlayerID == 2 && p2Controller == null)
            {
                p2Controller = c;
                p2Controller.OnCollectPressed += () => SetCollectHeld(2, true);
                p2Controller.OnCollectReleased += () => SetCollectHeld(2, false);
            }
        }

        // Only mark resolved once BOTH valid IDs are found
        // If PlayerID is still 0 (PlayerInput not ready yet) we just try again next frame
        if (p1Controller != null && p2Controller != null)
            playersResolved = true;
    }

    public void SetCollectHeld(int playerID, bool held)
    {
        if (playerID == 1)
            p1CollectHeld = held;
        else
            p2CollectHeld = held;

        if (!held)
        {
            if (playerID == 1)
            {
                p1CollectHeldFor = 0f;
                p1CollectRepeat = 0f;
                p1DepositHeldFor = 0f;
                p1DepositRepeat = 0f;
            }
            else
            {
                p2CollectHeldFor = 0f;
                p2CollectRepeat = 0f;
                p2DepositHeldFor = 0f;
                p2DepositRepeat = 0f;
            }
        }
    }

    private void OnEnterPile(int id)
    {
        if (id == 1)
            p1InPile = true;
        else
            p2InPile = true;
    }

    private void OnExitPile(int id)
    {
        if (id == 1)
        {
            p1InPile = false;
            p1CollectHeldFor = 0f;
            p1CollectRepeat = 0f;
        }
        else
        {
            p2InPile = false;
            p2CollectHeldFor = 0f;
            p2CollectRepeat = 0f;
        }
    }

    private void OnEnterDeposit(int id)
    {
        if (id == 1)
            p1InDeposit = true;
        else
            p2InDeposit = true;
    }

    private void OnExitDeposit(int id)
    {
        if (id == 1)
        {
            p1InDeposit = false;
            p1DepositHeldFor = 0f;
            p1DepositRepeat = 0f;
        }
        else
        {
            p2InDeposit = false;
            p2DepositHeldFor = 0f;
            p2DepositRepeat = 0f;
        }
    }

    private void HandleCollection(
        int playerID,
        bool holdHeld,
        bool inPile,
        bool inDeposit,
        ref float collectHeld,
        ref float collectRepeat,
        ref float depositHeld,
        ref float depositRepeat
    )
    {
        if (!holdHeld)
            return;

        if (inPile && PileCount > 0)
        {
            collectHeld += Time.deltaTime;
            if (collectHeld >= collectHoldTime)
            {
                collectRepeat += Time.deltaTime;
                if (collectRepeat >= collectRepeatInterval)
                {
                    collectRepeat = 0f;
                    CollectNeedle(playerID);
                }
            }
        }

        if (inDeposit)
        {
            int count = playerID == 1 ? P1NeedleCount : P2NeedleCount;
            if (count > 0)
            {
                depositHeld += Time.deltaTime;
                if (depositHeld >= depositHoldTime)
                {
                    depositRepeat += Time.deltaTime;
                    if (depositRepeat >= depositRepeatInterval)
                    {
                        depositRepeat = 0f;
                        DepositNeedle(playerID);
                    }
                }
            }
        }
    }

    private void CollectNeedle(int playerID)
    {
        if (PileCount <= 0)
            return;
        PileCount--;
        if (playerID == 1)
            P1NeedleCount++;
        else
            P2NeedleCount++;

        needleSpawner?.CollectPhysical(playerID);
        OnPileCountChanged?.Invoke(PileCount);
        OnPlayerNeedleCountChanged?.Invoke(playerID, playerID == 1 ? P1NeedleCount : P2NeedleCount);
        CheckKillerShotCondition();
    }

    private void DepositNeedle(int playerID)
    {
        needleSpawner?.DepositPhysical(playerID);
        OnPlayerNeedleCountChanged?.Invoke(playerID, playerID == 1 ? P1NeedleCount : P2NeedleCount);
    }

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
            return; // standalone — no-op, no crash
        MatchData.Instance.P1NeedleCount = P1NeedleCount;
        MatchData.Instance.P2NeedleCount = P2NeedleCount;
    }

    public int GetStealAmount() => stealAmount;

    public bool KillerShotFired() => killerShotTriggered;
}
