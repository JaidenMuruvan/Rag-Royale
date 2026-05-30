using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the killer shot reaction window for all rounds.
///
/// Round 1: winner → KnockdownManager.StartKnockdown
/// Round 2: winner → OnKillerShotWinner (NeedleManager handles the steal)
/// Round 3: winner → OnKillerShotWinner (ThrowSystem grants a power throw)
///
/// Set CurrentRound before the round starts via SetRound(), or wire it in the Inspector.
/// </summary>
public class KillerShotManager : MonoBehaviour
{
    // ── Inspector

    [Header("Killer Shot Settings")]
    [SerializeField]
    private float minReactionWindow = 3f;

    [SerializeField]
    private float maxReactionWindow = 6f;

    [SerializeField]
    private float perfectTimingWindow = 0.4f;

    [SerializeField]
    private float hapticLowFreq = 0.3f;

    [SerializeField]
    private float hapticHighFreq = 0.8f;

    [Header("References")]
    [SerializeField]
    private KnockdownManager knockdownManager; // Round 1 only

    [Header("Round Context")]
    [SerializeField]
    private int currentRound = 1;

    // ── Events ────

    /// <summary>Fired when the phase begins. Arg = triggeringPlayerID (0 = both / round 2).</summary>
    public UnityEvent<int> OnKillerShotPhaseStarted;
    public UnityEvent OnKillerShotPhaseEnded;

    /// <summary>Fired when a player wins the reaction window (all rounds).</summary>
    public UnityEvent<int> OnKillerShotWinner;

    public UnityEvent OnKillerShotExpired;
    public UnityEvent<int> OnEarlyPress;
    public UnityEvent<int> OnPerfectPress;
    public UnityEvent<int> OnTooLate;

    // ── Private state ─────────────────────────────────────────────────────────

    private PlayerHealth p1Health;
    private PlayerHealth p2Health;
    private MultiplayerPlayerController p1Controller;
    private MultiplayerPlayerController p2Controller;
    private CombatSystem p1Combat;
    private CombatSystem p2Combat;
    private bool playersResolved = false;

    private bool killerShotActive = false;
    private bool reactionWindowOpen = false;
    private float reactionWindowDuration;
    private float windowTimer;
    private Coroutine reactionCoroutine;

    private bool p1PressedEarly = false;
    private bool p2PressedEarly = false;
    private bool p1ReactionPressed = false;
    private bool p2ReactionPressed = false;
    private bool p1ReactedInWindow = false;
    private bool p2ReactedInWindow = false;
    private int triggeringPlayerID = 0;

    private Gamepad p1Gamepad;
    private Gamepad p2Gamepad;

    // ── Public getters (for UI) ───────────────────────────────────────────────

    public float GetWindowTimeRemaining() => windowTimer;

    public float GetWindowDuration() => reactionWindowDuration;

    public int GetTriggeringPlayerID() => triggeringPlayerID;

    public bool IsActive => killerShotActive;

    // ── Lifecycle

    private void Update()
    {
        if (!playersResolved)
        {
            TryResolvePlayerReferences();
            return;
        }
        if (!killerShotActive)
            return;

        bool p1Pressed = p1ReactionPressed;
        bool p2Pressed = p2ReactionPressed;
        p1ReactionPressed = false;
        p2ReactionPressed = false;

        if (!reactionWindowOpen)
        {
            if (p1Pressed && !p1PressedEarly)
            {
                p1PressedEarly = true;
                OnEarlyPress?.Invoke(1);
            }
            if (p2Pressed && !p2PressedEarly)
            {
                p2PressedEarly = true;
                OnEarlyPress?.Invoke(2);
            }
            return;
        }

        bool p1Valid = p1Pressed && !p1PressedEarly;
        bool p2Valid = p2Pressed && !p2PressedEarly;

        if (p1Valid)
            p1ReactedInWindow = true;
        if (p2Valid)
            p2ReactedInWindow = true;

        if (p1Valid && p2Valid)
            ResolveKillerShot(1); // simultaneous → P1 wins
        else if (p1Valid)
        {
            CheckPerfect(1);
            ResolveKillerShot(1);
        }
        else if (p2Valid)
        {
            CheckPerfect(2);
            ResolveKillerShot(2);
        }
    }

    private void OnDestroy()
    {
        if (p1Controller != null)
            p1Controller.OnReactionEvent -= OnP1Reaction;
        if (p2Controller != null)
            p2Controller.OnReactionEvent -= OnP2Reaction;
        StopHaptics();
    }

    // ── Round context ─────────────────────────────────────────────────────────

    /// <summary>Call from a RoundManager (or set in Inspector) before the round starts.</summary>
    public void SetRound(int round) => currentRound = round;

    // ── Activation — internal (HP threshold path, Rounds 1 & 3) ──────────────

    private void ActivateKillerShotPhase(int playerID)
    {
        if (killerShotActive)
            return;
        BeginPhase(playerID);
    }

    // ── Activation — Round 2 (NeedleManager pile threshold) ──────────────────

    /// <summary>Called by NeedleManager when the pile drops below its threshold.</summary>
    public void ActivateKillerShotForRound2()
    {
        if (killerShotActive)
            return;
        BeginPhase(0); // 0 = both players prompted
    }

    // ── Activation — Round 3 (HP threshold, called by Round3Manager) ─────────

    /// <summary>
    /// Called by Round3Manager when a player's HP drops to or below the configured threshold.
    /// Identical to the internal HP path but exposed publicly so Round3Manager owns the
    /// threshold check (keeping the serializable value there, not here).
    /// </summary>
    public void ActivateKillerShotPhase_Round3(int triggeringPlayer)
    {
        if (killerShotActive)
            return;
        if (currentRound != 3)
        {
            Debug.LogWarning(
                "[KillerShotManager] ActivateKillerShotPhase_Round3 called but currentRound != 3."
            );
            return;
        }
        BeginPhase(triggeringPlayer);
    }

    // ── Shared begin logic ────────────────────────────────────────────────────

    private void BeginPhase(int playerID)
    {
        killerShotActive = true;
        reactionWindowOpen = false;
        triggeringPlayerID = playerID;

        p1PressedEarly = false;
        p2PressedEarly = false;
        p1ReactionPressed = false;
        p2ReactionPressed = false;
        p1ReactedInWindow = false;
        p2ReactedInWindow = false;

        reactionWindowDuration = Random.Range(minReactionWindow, maxReactionWindow);
        windowTimer = reactionWindowDuration;

        // In Round 3 there is no melee combat to disable, but the CombatSystem references
        // will simply be null so the null-conditional calls are safe.
        p1Combat?.SetCombatEnabled(false);
        p2Combat?.SetCombatEnabled(false);

        OnKillerShotPhaseStarted?.Invoke(triggeringPlayerID);
        StartHaptics();

        reactionCoroutine = StartCoroutine(ReactionWindowRoutine());
    }

    // ── Reaction window ───────────────────────────────────────────────────────

    private IEnumerator ReactionWindowRoutine()
    {
        yield return new WaitForSeconds(0.5f); // short grace period before window opens

        reactionWindowOpen = true;

        float elapsed = 0f;
        while (elapsed < reactionWindowDuration && killerShotActive)
        {
            elapsed += Time.deltaTime;
            windowTimer = reactionWindowDuration - elapsed;
            yield return null;
        }

        if (killerShotActive)
        {
            killerShotActive = false;
            reactionWindowOpen = false;
            StopHaptics();
            ReenableCombat();

            if (!p1ReactedInWindow && !p1PressedEarly)
                OnTooLate?.Invoke(1);
            if (!p2ReactedInWindow && !p2PressedEarly)
                OnTooLate?.Invoke(2);

            OnKillerShotExpired?.Invoke();
            OnKillerShotPhaseEnded?.Invoke();
        }
    }

    private void CheckPerfect(int playerID)
    {
        if (windowTimer <= perfectTimingWindow)
            OnPerfectPress?.Invoke(playerID);
    }

    // ── Resolution — round-branching consequence ──────────────────────────────

    private void ResolveKillerShot(int winnerID)
    {
        if (!killerShotActive)
            return;

        killerShotActive = false;
        reactionWindowOpen = false;
        StopHaptics();

        if (reactionCoroutine != null)
        {
            StopCoroutine(reactionCoroutine);
            reactionCoroutine = null;
        }

        switch (currentRound)
        {
            case 1:
                int loserID = winnerID == 1 ? 2 : 1;
                knockdownManager?.StartKnockdown(loserID);
                break;

            case 2:
                // NeedleManager subscribes to OnKillerShotWinner and handles the steal.
                break;

            case 3:
                // ThrowSystem subscribes to OnKillerShotWinner and grants a power throw.
                break;
        }

        ReenableCombat();
        OnKillerShotWinner?.Invoke(winnerID);
        OnKillerShotPhaseEnded?.Invoke();

        Debug.Log($"[KillerShot] Round {currentRound} — P{winnerID} wins reaction window.");
    }

    // ── Combat re-enable ──────────────────────────────────────────────────────

    private void ReenableCombat()
    {
        // Only re-enable melee combat in rounds that use it.
        // Round 3 uses ThrowSystem — no melee — but the null-safe calls are harmless.
        if (currentRound == 1)
        {
            p1Combat?.SetCombatEnabled(true);
            p2Combat?.SetCombatEnabled(true);
        }
        // Round 2 & 3: external systems manage their own enable state.
    }

    // ── Public reset ──────────────────────────────────────────────────────────

    public void ResetKillerShot()
    {
        killerShotActive = false;
        reactionWindowOpen = false;
        triggeringPlayerID = 0;
        StopHaptics();
        ReenableCombat();

        if (reactionCoroutine != null)
        {
            StopCoroutine(reactionCoroutine);
            reactionCoroutine = null;
        }

        OnKillerShotPhaseEnded?.Invoke();
    }

    // ── Player reference resolution ───────────────────────────────────────────

    private void TryResolvePlayerReferences()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);
        p1Controller = null;
        p2Controller = null;

        foreach (var c in controllers)
        {
            if (c.PlayerID == 1)
            {
                p1Controller = c;
                p1Health = c.GetComponent<PlayerHealth>();
                p1Combat = c.GetComponent<CombatSystem>();
            }
            else if (c.PlayerID == 2)
            {
                p2Controller = c;
                p2Health = c.GetComponent<PlayerHealth>();
                p2Combat = c.GetComponent<CombatSystem>();
            }
        }

        if (p1Controller == null || p2Controller == null)
            return;

        // Rounds 1 & 3 use the HP threshold path baked into PlayerHealth.
        // Round 3 Manager ALSO calls ActivateKillerShotPhase_Round3() independently,
        // so we only subscribe PlayerHealth.OnKillerShotTriggered for Round 1 here to
        // avoid double-firing in Round 3.
        if (currentRound == 1)
        {
            if (p1Health != null)
                p1Health.OnKillerShotTriggered.AddListener(() => ActivateKillerShotPhase(1));
            if (p2Health != null)
                p2Health.OnKillerShotTriggered.AddListener(() => ActivateKillerShotPhase(2));
        }
        // Round 2: NeedleManager calls ActivateKillerShotForRound2() directly.
        // Round 3: Round3Manager calls ActivateKillerShotPhase_Round3() directly.

        p1Controller.OnReactionEvent += OnP1Reaction;
        p2Controller.OnReactionEvent += OnP2Reaction;

        playersResolved = true;
        RefreshGamepads();
    }

    private void OnP1Reaction() => p1ReactionPressed = true;

    private void OnP2Reaction() => p2ReactionPressed = true;

    // ── Haptics ───

    private void StartHaptics()
    {
        RefreshGamepads();
        p1Gamepad?.SetMotorSpeeds(hapticLowFreq, hapticHighFreq);
        p2Gamepad?.SetMotorSpeeds(hapticLowFreq, hapticHighFreq);
    }

    private void StopHaptics()
    {
        p1Gamepad?.ResetHaptics();
        p2Gamepad?.ResetHaptics();
    }

    private void RefreshGamepads()
    {
        var pads = Gamepad.all;
        p1Gamepad = pads.Count > 0 ? pads[0] : null;
        p2Gamepad = pads.Count > 1 ? pads[1] : null;
    }
}
