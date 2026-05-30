using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeedleSpawner : MonoBehaviour
{
    [Header("Pile Needles ")]
    [SerializeField]
    private List<NeedlePickup> needles = new List<NeedlePickup>();

    [Header("Player Colours")]
    [SerializeField]
    private Color p1Colour = new Color(1f, 0.20f, 0.18f, 1f);

    [SerializeField]
    private Color p2Colour = new Color(0.18f, 0.45f, 1f, 1f);

    [SerializeField]
    private Color pileColour = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Header("Deposit Pile Positions")]
    [SerializeField]
    private Transform p1DepositPilePoint;

    [SerializeField]
    private Transform p2DepositPilePoint;

    [Header("Deposit Zone Needle Displays (pincushion)")]
    [Tooltip("Pre-placed needle GameObjects in P1's deposit zone, all initially inactive.")]
    [SerializeField]
    private List<GameObject> p1DepositNeedleSlots = new List<GameObject>();

    [Tooltip("Same for P2.")]
    [SerializeField]
    private List<GameObject> p2DepositNeedleSlots = new List<GameObject>();

    [Header("Hold Point Name")]
    [SerializeField]
    private string holdPointName = "NeedleHoldPoint";

    [Header("References")]
    [SerializeField]
    private NeedleManager needleManager;

    [Header("Juice")]
    [SerializeField]
    private float collectPunchScale = 1.35f;

    [SerializeField]
    private float depositPunchScale = 1.4f;

    [SerializeField]
    private float punchDuration = 0.12f;

    private Queue<NeedlePickup> pileQueue = new Queue<NeedlePickup>();

    private NeedlePickup p1HeldNeedle;
    private NeedlePickup p2HeldNeedle;

    private List<NeedlePickup> p1Stash = new List<NeedlePickup>();
    private List<NeedlePickup> p2Stash = new List<NeedlePickup>();

    private int p1DepositSlotIndex = 0;
    private int p2DepositSlotIndex = 0;

    private Transform p1HoldPoint;
    private Transform p2HoldPoint;
    private bool holdPointsResolved = false;

    private void Start()
    {
        InitialisePile();
        HideAllDepositSlots();

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

    private void HideAllDepositSlots()
    {
        foreach (var go in p1DepositNeedleSlots)
            if (go != null)
                go.SetActive(false);
        foreach (var go in p2DepositNeedleSlots)
            if (go != null)
                go.SetActive(false);
        p1DepositSlotIndex = 0;
        p2DepositSlotIndex = 0;
    }

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

    private Transform FindChildByName(Transform root, string childName)
    {
        var q = new Queue<Transform>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            Transform t = q.Dequeue();
            if (t.name == childName)
                return t;
            foreach (Transform child in t)
                q.Enqueue(child);
        }
        // Debug.LogWarning($"[NeedleSpawner] '{childName}' not found under {root.name}.");
        return null;
    }

    public bool CollectPhysical(int playerID)
    {
        if (!holdPointsResolved)
            return false;
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

        StartCoroutine(PunchScale(needle.transform, collectPunchScale, punchDuration));

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.armDetach, 0.7f, 0.08f);

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

        ActivateNextDepositSlot(playerID);

        GameObject slot = GetLastActivatedSlot(playerID);
        if (slot != null)
            StartCoroutine(PunchScale(slot.transform, depositPunchScale, punchDuration));

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.lightHit, 0.6f, 0.06f);
            int count = (playerID == 1 ? p1Stash.Count : p2Stash.Count) + 1;
            if (count % 5 == 0)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.roundWinChant, 0.4f);
        }

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
        pileQueue.Enqueue(needle);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.menuBack, 0.5f, 0.05f);

        if (playerID == 1)
            p1HeldNeedle = null;
        else
            p2HeldNeedle = null;
    }

    public bool PlayerHoldsNeedle(int playerID) =>
        playerID == 1 ? p1HeldNeedle != null : p2HeldNeedle != null;

    private void ActivateNextDepositSlot(int playerID)
    {
        if (playerID == 1)
        {
            if (p1DepositSlotIndex < p1DepositNeedleSlots.Count)
            {
                p1DepositNeedleSlots[p1DepositSlotIndex]?.SetActive(true);
                p1DepositSlotIndex++;
            }
        }
        else
        {
            if (p2DepositSlotIndex < p2DepositNeedleSlots.Count)
            {
                p2DepositNeedleSlots[p2DepositSlotIndex]?.SetActive(true);
                p2DepositSlotIndex++;
            }
        }
    }

    private GameObject GetLastActivatedSlot(int playerID)
    {
        if (playerID == 1)
        {
            int idx = p1DepositSlotIndex - 1;
            return idx >= 0 && idx < p1DepositNeedleSlots.Count ? p1DepositNeedleSlots[idx] : null;
        }
        else
        {
            int idx = p2DepositSlotIndex - 1;
            return idx >= 0 && idx < p2DepositNeedleSlots.Count ? p2DepositNeedleSlots[idx] : null;
        }
    }

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
        DeactivateDepositSlots(loserID, actualSteal);

        for (int i = 0; i < actualSteal; i++)
        {
            if (loserStash.Count == 0)
                break;

            NeedlePickup stolen = loserStash[loserStash.Count - 1];
            loserStash.RemoveAt(loserStash.Count - 1);
            stolen.Steal(winnerHold, winnerID, winnerColour);
            winnerStash.Add(stolen);

            Vector3 target = winnerPile != null ? winnerPile.position : winnerHold.position;
            StartCoroutine(DelayedDeposit(stolen, target, winnerID, i * 0.15f));
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.killerShotWin, 1f);
    }

    private void DeactivateDepositSlots(int playerID, int count)
    {
        List<GameObject> slots = playerID == 1 ? p1DepositNeedleSlots : p2DepositNeedleSlots;
        ref int idx = ref (playerID == 1 ? ref p1DepositSlotIndex : ref p2DepositSlotIndex);

        for (int i = 0; i < count; i++)
        {
            idx = Mathf.Max(0, idx - 1);
            if (idx < slots.Count)
                slots[idx]?.SetActive(false);
        }
    }

    private IEnumerator DelayedDeposit(
        NeedlePickup needle,
        Vector3 target,
        int playerID,
        float extraDelay
    )
    {
        yield return new WaitForSeconds(0.5f + extraDelay);
        needle.Deposit(target);
        ActivateNextDepositSlot(playerID);
    }

    public NeedlePickup PopNextProjectile(int playerID)
    {
        List<NeedlePickup> stash = playerID == 1 ? p1Stash : p2Stash;
        Transform hold = playerID == 1 ? p1HoldPoint : p2HoldPoint;

        if (stash.Count == 0 || hold == null)
            return null;

        NeedlePickup needle = stash[stash.Count - 1];
        stash.RemoveAt(stash.Count - 1);
        needle.PrepareAsProjectile(hold, playerID);
        return needle;
    }

    public int GetStashCount(int playerID) => playerID == 1 ? p1Stash.Count : p2Stash.Count;

    private IEnumerator PunchScale(Transform t, float peakScale, float duration)
    {
        if (t == null)
            yield break;

        Vector3 original = t.localScale;
        float half = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.LerpUnclamped(original, original * peakScale, elapsed / half);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.LerpUnclamped(original * peakScale, original, elapsed / half);
            yield return null;
        }

        t.localScale = original;
    }
}
