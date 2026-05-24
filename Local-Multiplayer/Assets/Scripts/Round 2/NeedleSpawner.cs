using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeedleSpawner : MonoBehaviour
{
    [Header(" Needles")]
    [SerializeField]
    private List<NeedlePickup> needles = new List<NeedlePickup>();

    [Header("Player Colours")]
    [SerializeField]
    private Color p1Colour = new Color(0.2f, 0.5f, 1f, 1f);

    [SerializeField]
    private Color p2Colour = new Color(1f, 0.3f, 0.2f, 1f);

    [SerializeField]
    private Color pileColour = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Header("Deposit Pile pos")]
    [Tooltip(
        "World position needles arc toward when P1 deposits. "
            + "Point this at a visible stash location on P1's side."
    )]
    [SerializeField]
    private Transform p1DepositPilePoint;

    [Tooltip("Same for P2.")]
    [SerializeField]
    private Transform p2DepositPilePoint;

    [Header("Hold Point Name")]
    [SerializeField]
    private string holdPointName = "NeedleHoldPoint";

    [Header("References")]
    [SerializeField]
    private NeedleManager needleManager;

    private Queue<NeedlePickup> pileQueue = new Queue<NeedlePickup>();

    private NeedlePickup p1HeldNeedle;
    private NeedlePickup p2HeldNeedle;

    private List<NeedlePickup> p1Stash = new List<NeedlePickup>();
    private List<NeedlePickup> p2Stash = new List<NeedlePickup>();

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

    private void OnDestroy()
    {
        if (needleManager != null)
            needleManager.OnNeedleStolen.RemoveListener(OnNeedleStolen);
    }

    // Pile init

    private void InitialisePile()
    {
        pileQueue.Clear();
        foreach (var n in needles)
        {
            if (n == null)
                continue;
            n.ResetToPile(pileColour);
            pileQueue.Enqueue(n);
        }
    }

    // Hold point

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

    private Transform FindChildByName(Transform root, string name)
    {
        var queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t.name == name)
                return t;
            foreach (Transform child in t)
                queue.Enqueue(child);
        }
        Debug.LogWarning($"[NeedleSpawner] '{name}' not found under {root.name}.");
        return null;
    }

    // Called by NeedleManager

    /// <summary>
    /// Try to give the player a physical needle from the pile.
    /// will b false if the player already holds one or the pile is empty.
    /// </summary>
    public bool CollectPhysical(int playerID)
    {
        if (!holdPointsResolved)
            return false;

        // One needle at a time — block if already holding
        if (playerID == 1 && p1HeldNeedle != null)
            return false;
        if (playerID == 2 && p2HeldNeedle != null)
            return false;

        if (pileQueue.Count == 0)
            return false;

        Transform holdPoint = playerID == 1 ? p1HoldPoint : p2HoldPoint;
        Color playerColour = playerID == 1 ? p1Colour : p2Colour;
        if (holdPoint == null)
            return false;

        NeedlePickup needle = pileQueue.Dequeue();

        if (playerID == 1)
            p1HeldNeedle = needle;
        else
            p2HeldNeedle = needle;

        needle.Collect(holdPoint, playerID, playerColour);
        return true;
    }

    public void DepositPhysical(int playerID)
    {
        NeedlePickup needle = playerID == 1 ? p1HeldNeedle : p2HeldNeedle;
        if (needle == null)
            return;

        Transform pilePoint = playerID == 1 ? p1DepositPilePoint : p2DepositPilePoint;
        Vector3 target = pilePoint != null ? pilePoint.position : needle.transform.position;

        needle.Deposit(target);

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

    public void ReturnHeldNeedleToPile(int playerID)
    {
        NeedlePickup needle = playerID == 1 ? p1HeldNeedle : p2HeldNeedle;
        if (needle == null)
            return;

        needle.ReturnToPile(pileColour);
        pileQueue.Enqueue(needle); // back in circulation

        if (playerID == 1)
            p1HeldNeedle = null;
        else
            p2HeldNeedle = null;
    }

    public bool PlayerHoldsNeedle(int playerID) =>
        playerID == 1 ? p1HeldNeedle != null : p2HeldNeedle != null;

    // Killer shot steal

    private void OnNeedleStolen(int winnerID)
    {
        int loserID = winnerID == 1 ? 2 : 1;
        int stealAmount = needleManager != null ? needleManager.GetStealAmount() : 3;

        List<NeedlePickup> loserStash = loserID == 1 ? p1Stash : p2Stash;
        List<NeedlePickup> winnerStash = winnerID == 1 ? p1Stash : p2Stash;
        Transform winnerHold = winnerID == 1 ? p1HoldPoint : p2HoldPoint;
        Transform winnerPile = winnerID == 1 ? p1DepositPilePoint : p2DepositPilePoint;
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

            stolen.Steal(winnerHold, winnerID, winnerColour);
            winnerStash.Add(stolen);

            Vector3 target = winnerPile != null ? winnerPile.position : winnerHold.position;
            StartCoroutine(DelayedDeposit(stolen, target, i * 0.15f));
        }
    }

    private IEnumerator DelayedDeposit(NeedlePickup needle, Vector3 target, float extraDelay)
    {
        yield return new WaitForSeconds(0.5f + extraDelay);
        needle.Deposit(target);
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
}
