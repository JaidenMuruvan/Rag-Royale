using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// No longer spawns anything.
/// You place all needle GameObjects in the scene yourself and assign them
/// to the 'needles' list in the Inspector.
///
/// Manages the pool of pre-placed needles:
///   - Tracks which are in the pile / held / deposited
///   - Tints needles to the collecting player's colour on collect
///   - Retints on steal
///   - Resets colour to pileColour on round reset
///   - Finds player hold points by searching for a child named holdPointName
/// </summary>
public class NeedleSpawner : MonoBehaviour
{
    // Inspector

    [Header("Pre-placed Needles")]
    [Tooltip("Assign every needle GameObject from the scene here. Order doesn't matter.")]
    [SerializeField]
    private List<NeedlePickup> needles = new List<NeedlePickup>();

    [Header("Player Colours")]
    [SerializeField]
    private Color p1Colour = new Color(0.2f, 0.5f, 1f, 1f); // blue-ish

    [SerializeField]
    private Color p2Colour = new Color(1f, 0.3f, 0.2f, 1f); // red-ish

    [SerializeField]
    private Color pileColour = new Color(0.85f, 0.85f, 0.85f, 1f); // neutral grey

    [Header("Hold Point Name")]
    [SerializeField]
    private string holdPointName = "NeedleHoldPoint";

    [Header("References")]
    [SerializeField]
    private NeedleManager needleManager;

    // Runtime state

    // Needles available in the pile (not yet collected)
    private Queue<NeedlePickup> pileQueue = new Queue<NeedlePickup>();

    // Currently held needle per player (one at a time)
    private NeedlePickup p1HeldNeedle;
    private NeedlePickup p2HeldNeedle;

    // Deposited stashes
    private List<NeedlePickup> p1Stash = new List<NeedlePickup>();
    private List<NeedlePickup> p2Stash = new List<NeedlePickup>();

    // Hold point transforms found at runtime
    private Transform p1HoldPoint;
    private Transform p2HoldPoint;
    private bool holdPointsResolved = false;

    private void Start()
    {
        InitialisePile();

        if (needleManager != null)
            needleManager.OnNeedleStolen.AddListener(OnNeedleStolen);
    }

    private void Update()
    {
        if (!holdPointsResolved)
            TryResolveHoldPoints();
    }

    // Pile initialisation — reset all pre-placed needles to pile colour

    private void InitialisePile()
    {
        pileQueue.Clear();

        foreach (var needle in needles)
        {
            if (needle == null)
                continue;
            needle.ResetToPile(pileColour);
            pileQueue.Enqueue(needle);
        }
    }

    // Hold point resolution — BFS by child name on each player instance

    private void TryResolveHoldPoints()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);

        foreach (var c in controllers)
        {
            if (c.PlayerID == 1 && p1HoldPoint == null)
                p1HoldPoint = FindChildByName(c.transform, holdPointName);

            if (c.PlayerID == 2 && p2HoldPoint == null)
                p2HoldPoint = FindChildByName(c.transform, holdPointName);
        }

        if (p1HoldPoint != null && p2HoldPoint != null)
            holdPointsResolved = true;
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t.name == targetName)
                return t;
            foreach (Transform child in t)
                queue.Enqueue(child);
        }

        Debug.LogWarning($"[NeedleSpawner] '{targetName}' not found under {root.name}.");
        return null;
    }

    // Called by NeedleManager

    public void CollectPhysical(int playerID)
    {
        if (!holdPointsResolved || pileQueue.Count == 0)
            return;

        Transform holdPoint = playerID == 1 ? p1HoldPoint : p2HoldPoint;
        Color colour = playerID == 1 ? p1Colour : p2Colour;
        if (holdPoint == null)
            return;

        NeedlePickup needle = pileQueue.Dequeue();

        // Auto-deposit the previously held needle before picking up a new one
        if (playerID == 1)
        {
            if (p1HeldNeedle != null)
                DepositPhysical(1);
            p1HeldNeedle = needle;
        }
        else
        {
            if (p2HeldNeedle != null)
                DepositPhysical(2);
            p2HeldNeedle = needle;
        }

        needle.Collect(holdPoint, playerID, colour);
    }

    public void DepositPhysical(int playerID)
    {
        NeedlePickup needle = playerID == 1 ? p1HeldNeedle : p2HeldNeedle;
        if (needle == null)
            return;

        needle.Deposit();

        if (playerID == 1)
        {
            p1Stash.Add(needle);
            p1HeldNeedle = null;
        }
        else
        {
            p2Stash.Add(needle);
            p2HeldNeedle = null;
        }
    }

    // Killer shot steal

    private void OnNeedleStolen(int winnerID)
    {
        int loserID = winnerID == 1 ? 2 : 1;
        int stealAmount = needleManager != null ? needleManager.GetStealAmount() : 3;

        List<NeedlePickup> loserStash = loserID == 1 ? p1Stash : p2Stash;
        List<NeedlePickup> winnerStash = winnerID == 1 ? p1Stash : p2Stash;
        Transform winnerHold = winnerID == 1 ? p1HoldPoint : p2HoldPoint;
        Color winnerColour = winnerID == 1 ? p1Colour : p2Colour;

        if (winnerHold == null)
            return;

        int actualSteal = Mathf.Min(stealAmount, loserStash.Count);

        for (int i = 0; i < actualSteal; i++)
        {
            if (loserStash.Count == 0)
                break;

            NeedlePickup stolen = loserStash[loserStash.Count - 1];
            loserStash.RemoveAt(loserStash.Count - 1);

            stolen.Steal(winnerHold, winnerID, winnerColour); // retint to winner colour
            winnerStash.Add(stolen);

            // After it snaps to the winner's hand, deposit it into their stash
            StartCoroutine(DelayedDeposit(stolen, i * 0.15f));
        }
    }

    private IEnumerator DelayedDeposit(NeedlePickup needle, float extraDelay)
    {
        yield return new WaitForSeconds(0.5f + extraDelay);
        needle.Deposit();
    }

    // Round 3 handoff

    public NeedlePickup PopNextProjectile(int playerID)
    {
        List<NeedlePickup> stash = playerID == 1 ? p1Stash : p2Stash;
        Transform holdPoint = playerID == 1 ? p1HoldPoint : p2HoldPoint;

        if (stash.Count == 0 || holdPoint == null)
            return null;

        NeedlePickup needle = stash[stash.Count - 1];
        stash.RemoveAt(stash.Count - 1);
        needle.PrepareAsProjectile(holdPoint, playerID);
        return needle;
    }

    public int GetStashCount(int playerID) => playerID == 1 ? p1Stash.Count : p2Stash.Count;

    // Round 3 standalone testing

    public void Debug_FillStashesForRound3(int p1Count, int p2Count)
    {
        // Re-use the first N pile needles as pre-filled stash entries
        var tempList = new List<NeedlePickup>(pileQueue);

        FillFromList(tempList, p1Count, p1Stash, p1HoldPoint, 1, p1Colour);
        FillFromList(tempList, p2Count, p2Stash, p2HoldPoint, 2, p2Colour);
    }

    private void FillFromList(
        List<NeedlePickup> source,
        int count,
        List<NeedlePickup> stash,
        Transform holdPoint,
        int playerID,
        Color colour
    )
    {
        int taken = 0;
        for (int i = source.Count - 1; i >= 0 && taken < count; i--)
        {
            NeedlePickup np = source[i];
            source.RemoveAt(i);
            np.Collect(holdPoint, playerID, colour);
            np.Deposit();
            stash.Add(np);
            taken++;
        }
    }
}
