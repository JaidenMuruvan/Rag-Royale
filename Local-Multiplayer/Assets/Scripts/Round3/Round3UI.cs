using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Round 3 HUD. Displays:
///   • Per-player ammo count + ammo pip row
///   • HP bars (polled from PlayerHealth)
///   • Power-throw "CHARGED!" indicator with pulse animation
///   • Hit flash on the side of the player who was hit
///   • Killer shot reaction-window countdown bar
///
/// Wired up entirely in Start() via events — no per-frame polling except for HP bars and
/// the killer shot countdown (which only runs while the phase is active).
/// </summary>
public class Round3UI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField]
    private ThrowSystem throwSystem;

    [SerializeField]
    private KillerShotManager killerShotManager;

    [Header("Ammo — P1")]
    [SerializeField]
    private TextMeshProUGUI p1AmmoLabel;

    [SerializeField]
    private Transform p1AmmoPipRoot; // parent of ammo pip images

    [SerializeField]
    private GameObject ammoPipPrefab; // small filled circle prefab

    [Header("Ammo — P2")]
    [SerializeField]
    private TextMeshProUGUI p2AmmoLabel;

    [SerializeField]
    private Transform p2AmmoPipRoot;

    [Header("HP Bars")]
    [SerializeField]
    private Slider p1HpSlider;

    [SerializeField]
    private Slider p2HpSlider;

    [SerializeField]
    private Image p1HpFill;

    [SerializeField]
    private Image p2HpFill;

    [SerializeField]
    private Color hpHealthyColor = new Color(0.2f, 0.85f, 0.3f);

    [SerializeField]
    private Color hpDangerColor = new Color(0.95f, 0.2f, 0.15f);

    [SerializeField, Range(0f, 1f)]
    private float hpDangerThreshold = 0.35f;

    [Header("Power Throw Indicators")]
    [SerializeField]
    private GameObject p1PowerThrowPanel;

    [SerializeField]
    private GameObject p2PowerThrowPanel;

    [SerializeField]
    private float powerPulseSpeed = 3f;

    [Header("Hit Flash Overlays")]
    [SerializeField]
    private Image p1HitFlash; // left-side overlay (P1 took damage)

    [SerializeField]
    private Image p2HitFlash; // right-side overlay (P2 took damage)

    [SerializeField]
    private Color normalHitFlashColor = new Color(1f, 0.1f, 0.1f, 0.45f);

    [SerializeField]
    private Color powerHitFlashColor = new Color(1f, 0.5f, 0f, 0.70f);

    [SerializeField]
    private float hitFlashDuration = 0.25f;

    [Header("Killer Shot HUD")]
    [SerializeField]
    private GameObject killerShotPanel;

    [SerializeField]
    private Slider killerShotTimerSlider;

    [SerializeField]
    private TextMeshProUGUI killerShotLabel;

    [SerializeField]
    private Image killerShotFill;

    [SerializeField]
    private Color killerShotReadyColor = new Color(1f, 0.85f, 0f);

    [SerializeField]
    private Color killerShotUrgentColor = new Color(1f, 0.15f, 0.1f);

    [Header("Exhausted Banner")]
    [SerializeField]
    private GameObject p1ExhaustedBanner;

    [SerializeField]
    private GameObject p2ExhaustedBanner;

    // ── Private ───────────────────────────────────────────────────────────────

    private PlayerHealth p1Health;
    private PlayerHealth p2Health;
    private bool playersResolved = false;

    private Coroutine p1FlashCoroutine;
    private Coroutine p2FlashCoroutine;
    private bool killerShotPhaseActive = false;

    // Pip pools
    private Image[] p1Pips;
    private Image[] p2Pips;
    private int p1MaxAmmo;
    private int p2MaxAmmo;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Hide panels that only appear on demand
        SetActive(killerShotPanel, false);
        SetActive(p1PowerThrowPanel, false);
        SetActive(p2PowerThrowPanel, false);
        SetActive(p1ExhaustedBanner, false);
        SetActive(p2ExhaustedBanner, false);
        SetAlpha(p1HitFlash, 0f);
        SetAlpha(p2HitFlash, 0f);

        // Wire throw system events
        if (throwSystem != null)
        {
            throwSystem.OnAmmoChanged.AddListener(OnAmmoChanged);
            throwSystem.OnPowerThrowReady.AddListener(OnPowerThrowReady);
            throwSystem.OnPowerThrowUsed.AddListener(OnPowerThrowUsed);
            throwSystem.OnNeedlesExhausted.AddListener(OnPlayerExhausted);
        }

        // Wire killer shot events
        if (killerShotManager != null)
        {
            killerShotManager.OnKillerShotPhaseStarted.AddListener(OnKillerShotStarted);
            killerShotManager.OnKillerShotPhaseEnded.AddListener(OnKillerShotEnded);
            killerShotManager.OnKillerShotWinner.AddListener(OnKillerShotWinner);
            killerShotManager.OnEarlyPress.AddListener(OnEarlyPress);
        }

        // Subscribe to static hit event
        NeedleProjectile.OnProjectileHit += OnProjectileHit;

        // Initialise ammo display once ThrowSystem has loaded from MatchData
        // (ThrowSystem.Start runs first if ordered correctly; safe either way)
        StartCoroutine(InitAmmoNextFrame());
    }

    private void OnDestroy()
    {
        NeedleProjectile.OnProjectileHit -= OnProjectileHit;

        if (throwSystem != null)
        {
            throwSystem.OnAmmoChanged.RemoveListener(OnAmmoChanged);
            throwSystem.OnPowerThrowReady.RemoveListener(OnPowerThrowReady);
            throwSystem.OnPowerThrowUsed.RemoveListener(OnPowerThrowUsed);
            throwSystem.OnNeedlesExhausted.RemoveListener(OnPlayerExhausted);
        }

        if (killerShotManager != null)
        {
            killerShotManager.OnKillerShotPhaseStarted.RemoveListener(OnKillerShotStarted);
            killerShotManager.OnKillerShotPhaseEnded.RemoveListener(OnKillerShotEnded);
            killerShotManager.OnKillerShotWinner.RemoveListener(OnKillerShotWinner);
            killerShotManager.OnEarlyPress.RemoveListener(OnEarlyPress);
        }
    }

    private void Update()
    {
        // Resolve player health references lazily
        if (!playersResolved)
            TryResolveHealth();

        // Poll HP bars every frame (cheap slider update)
        if (playersResolved)
            UpdateHpBars();

        // Pulse power throw indicators
        PulsePowerIndicator(p1PowerThrowPanel);
        PulsePowerIndicator(p2PowerThrowPanel);

        // Update killer shot countdown slider
        if (killerShotPhaseActive && killerShotManager != null && killerShotTimerSlider != null)
        {
            float t = Mathf.Clamp01(
                killerShotManager.GetWindowTimeRemaining() / killerShotManager.GetWindowDuration()
            );
            killerShotTimerSlider.value = t;

            if (killerShotFill != null)
                killerShotFill.color = Color.Lerp(killerShotUrgentColor, killerShotReadyColor, t);
        }
    }

    // ── Ammo ─────────────────────────────────────────────────────────────────

    private IEnumerator InitAmmoNextFrame()
    {
        yield return null; // wait one frame so ThrowSystem.Start() has run

        if (throwSystem == null)
            yield break;

        p1MaxAmmo = throwSystem.P1Ammo;
        p2MaxAmmo = throwSystem.P2Ammo;

        BuildPips(p1AmmoPipRoot, p1MaxAmmo, out p1Pips);
        BuildPips(p2AmmoPipRoot, p2MaxAmmo, out p2Pips);

        RefreshAmmoLabel(1, throwSystem.P1Ammo);
        RefreshAmmoLabel(2, throwSystem.P2Ammo);
    }

    private void BuildPips(Transform root, int count, out Image[] pips)
    {
        pips = new Image[0];
        if (root == null || ammoPipPrefab == null)
            return;

        // Clear existing children
        foreach (Transform child in root)
            Destroy(child.gameObject);

        pips = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(ammoPipPrefab, root);
            pips[i] = go.GetComponent<Image>();
        }
    }

    private void OnAmmoChanged(int playerID, int newAmmo)
    {
        RefreshAmmoLabel(playerID, newAmmo);
        RefreshPips(playerID, newAmmo);
    }

    private void RefreshAmmoLabel(int playerID, int ammo)
    {
        TextMeshProUGUI label = playerID == 1 ? p1AmmoLabel : p2AmmoLabel;
        if (label != null)
            label.text = ammo.ToString();
    }

    private void RefreshPips(int playerID, int ammo)
    {
        Image[] pips = playerID == 1 ? p1Pips : p2Pips;
        int maxAmt = playerID == 1 ? p1MaxAmmo : p2MaxAmmo;
        if (pips == null)
            return;

        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] == null)
                continue;
            // Pips filled from left; remaining pips dim/grey
            pips[i].color = i < ammo ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.4f);
        }
    }

    // ── HP Bars ───────────────────────────────────────────────────────────────

    private void TryResolveHealth()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c.PlayerID == 1 && p1Health == null)
                p1Health = c.GetComponent<PlayerHealth>();
            else if (c.PlayerID == 2 && p2Health == null)
                p2Health = c.GetComponent<PlayerHealth>();
        }
        if (p1Health != null && p2Health != null)
            playersResolved = true;
    }

    private void UpdateHpBars()
    {
        UpdateSingleHpBar(p1HpSlider, p1HpFill, p1Health);
        UpdateSingleHpBar(p2HpSlider, p2HpFill, p2Health);
    }

    private void UpdateSingleHpBar(Slider slider, Image fill, PlayerHealth health)
    {
        if (slider == null || health == null)
            return;
        float ratio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
        slider.value = ratio;
        if (fill != null)
            fill.color = Color.Lerp(
                hpDangerColor,
                hpHealthyColor,
                Mathf.InverseLerp(0f, hpDangerThreshold, ratio)
                    * (ratio > hpDangerThreshold ? 1f : ratio / hpDangerThreshold)
            );
    }

    // ── Hit Flash ────────────────────────────────────────────────────────────

    private void OnProjectileHit(int hitPlayerID, bool isPower)
    {
        Image overlay = hitPlayerID == 1 ? p1HitFlash : p2HitFlash;
        Color color = isPower ? powerHitFlashColor : normalHitFlashColor;
        ref Coroutine routine = ref (
            hitPlayerID == 1 ? ref p1FlashCoroutine : ref p2FlashCoroutine
        );

        if (overlay == null)
            return;
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(HitFlashRoutine(overlay, color));
    }

    private IEnumerator HitFlashRoutine(Image overlay, Color color)
    {
        overlay.color = color;
        overlay.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < hitFlashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / hitFlashDuration;
            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, t * t);
            overlay.color = c;
            yield return null;
        }

        overlay.gameObject.SetActive(false);
    }

    // ── Power Throw Indicator ─────────────────────────────────────────────────

    private void OnPowerThrowReady(int playerID)
    {
        GameObject panel = playerID == 1 ? p1PowerThrowPanel : p2PowerThrowPanel;
        SetActive(panel, true);
    }

    private void OnPowerThrowUsed(int playerID)
    {
        GameObject panel = playerID == 1 ? p1PowerThrowPanel : p2PowerThrowPanel;
        SetActive(panel, false);
    }

    private void PulsePowerIndicator(GameObject panel)
    {
        if (panel == null || !panel.activeSelf)
            return;
        float s = 1f + Mathf.Sin(Time.unscaledTime * powerPulseSpeed) * 0.08f;
        panel.transform.localScale = Vector3.one * s;
    }

    // ── Exhausted Banner ──────────────────────────────────────────────────────

    private void OnPlayerExhausted(int playerID)
    {
        GameObject banner = playerID == 1 ? p1ExhaustedBanner : p2ExhaustedBanner;
        SetActive(banner, true);
    }

    // ── Killer Shot HUD ───────────────────────────────────────────────────────

    private void OnKillerShotStarted(int triggeringPlayerID)
    {
        killerShotPhaseActive = true;
        SetActive(killerShotPanel, true);

        if (killerShotTimerSlider != null)
            killerShotTimerSlider.value = 1f;
        if (killerShotLabel != null)
        {
            killerShotLabel.text =
                triggeringPlayerID == 0 ? "KILLER SHOT!" : $"P{triggeringPlayerID} KILLER SHOT!";
        }
    }

    private void OnKillerShotEnded()
    {
        killerShotPhaseActive = false;
        SetActive(killerShotPanel, false);
    }

    private void OnKillerShotWinner(int winnerID)
    {
        // Panel is hidden via OnKillerShotEnded — just log for now.
        Debug.Log($"[Round3UI] P{winnerID} won the killer shot reaction.");
    }

    private void OnEarlyPress(int playerID)
    {
        // Optional: flash the label red or show an "EARLY!" badge
        Debug.Log($"[Round3UI] P{playerID} pressed EARLY.");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    private static void SetAlpha(Image img, float alpha)
    {
        if (img == null)
            return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}
