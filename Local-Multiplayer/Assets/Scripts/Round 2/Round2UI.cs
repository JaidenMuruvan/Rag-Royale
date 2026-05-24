using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Round2UI : MonoBehaviour
{
    // Inspector — Needle Count Panels

    [Header("P1 Needle Panel (left side)")]
    [SerializeField]
    private Image p1NeedleIcon;

    [SerializeField]
    private TextMeshProUGUI p1NeedleCount;

    [SerializeField]
    private TextMeshProUGUI p1PlayerLabel; // "PLAYER 1"

    [SerializeField]
    private RectTransform p1Panel;

    [Header("P2 Needle Panel (right side)")]
    [SerializeField]
    private Image p2NeedleIcon;

    [SerializeField]
    private TextMeshProUGUI p2NeedleCount;

    [SerializeField]
    private TextMeshProUGUI p2PlayerLabel; // "PLAYER 2"

    [SerializeField]
    private RectTransform p2Panel;

    [Header("Shared Pile")]
    [SerializeField]
    private Image pileNeedleIcon;

    [SerializeField]
    private TextMeshProUGUI pileNeedleCount;

    [SerializeField]
    private TextMeshProUGUI pileLabel; // "PILE"

    // Inspector — Timer

    [Header("Timer")]
    [SerializeField]
    private TextMeshProUGUI timerText;

    [SerializeField]
    private Image timerFill; // optional radial/fill bar

    [SerializeField]
    private float timerDuration = 60f;

    [SerializeField]
    private Color timerNormal = Color.white;

    [SerializeField]
    private Color timerUrgent = new Color(1f, 0.2f, 0.1f);

    [SerializeField]
    private float urgentThreshold = 10f; // seconds

    // Inspector — Control Prompts

    [Header("P1 Control Prompts")]
    [SerializeField]
    private GameObject p1CollectPrompt; // shows when P1 near pile

    [SerializeField]
    private GameObject p1DepositPrompt; // shows when P1 near deposit

    [SerializeField]
    private TextMeshProUGUI p1CollectKey; // "E / RT"

    [SerializeField]
    private TextMeshProUGUI p1DepositKey; // "E / RT"

    [Header("P2 Control Prompts")]
    [SerializeField]
    private GameObject p2CollectPrompt;

    [SerializeField]
    private GameObject p2DepositPrompt;

    [SerializeField]
    private TextMeshProUGUI p2CollectKey;

    [SerializeField]
    private TextMeshProUGUI p2DepositKey;

    [Header("Prompt Labels")]
    [SerializeField]
    private string collectActionLabel = "Hold  to Collect";

    [SerializeField]
    private string depositActionLabel = "Hold  to Deposit";

    [SerializeField]
    private string p1KeyLabel = "E / RT";

    [SerializeField]
    private string p2KeyLabel = "E / RT";

    // Inspector — Killer Shot Banner

    [Header("Killer Shot UI")]
    [SerializeField]
    private GameObject killerShotBanner;

    [SerializeField]
    private TextMeshProUGUI killerShotLabel; // "NEEDLE STEAL CHANCE!"

    [SerializeField]
    private string killerShotText = "NEEDLE STEAL!";

    [SerializeField]
    private float bannerPulseSpeed = 2.5f;

    [SerializeField]
    private Color bannerColorA = new Color(1f, 0.85f, 0f);

    [SerializeField]
    private Color bannerColorB = new Color(1f, 0.2f, 0.1f);

    [Header("Killer Shot Winner Banner")]
    [SerializeField]
    private GameObject stealResultBanner;

    [SerializeField]
    private TextMeshProUGUI stealResultLabel;

    [SerializeField]
    private float stealResultDuration = 2f;

    // Inspector — Floating Needle Steal Animation

    [Header("Needle Steal Float Animation")]
    [SerializeField]
    private GameObject floatingNeedlePrefab; // a UI Image prefab of a needle icon

    [SerializeField]
    private RectTransform floatCanvas; // the canvas rect to spawn floaters on

    [SerializeField]
    private int floatNeedleCount = 3; // how many needles animate

    [SerializeField]
    private float floatDuration = 0.9f;

    [SerializeField]
    private float floatSpread = 60f; // pixels of random spread at source

    [SerializeField]
    private AnimationCurve floatCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField]
    private Color floatNeedleColor = new Color(0.4f, 1f, 0.6f, 1f); // magical green

    // Inspector — References

    [Header("References")]
    [SerializeField]
    private NeedleManager needleManager;

    [SerializeField]
    private RoundTimer roundTimer;

    [SerializeField]
    private KillerShotManager killerShotManager;

    // Private state

    private Coroutine bannerPulseCoroutine;
    private Coroutine stealResultCoroutine;

    // Zone proximity — set by NeedleManager zone callbacks forwarded here
    // or driven directly by CollectZone / DepositZone events
    private bool p1NearPile = false;
    private bool p2NearPile = false;
    private bool p1NearDeposit = false;
    private bool p2NearDeposit = false;

    // Lifecycle

    private void Start()
    {
        InitialisePromptLabels();
        HideAllPrompts();
        HideKillerShotBanner();
        HideStealResult();

        if (needleManager != null)
        {
            needleManager.OnPileCountChanged.AddListener(OnPileChanged);
            needleManager.OnPlayerNeedleCountChanged.AddListener(OnPlayerNeedleChanged);
            needleManager.OnNeedleStolen.AddListener(OnNeedleStolen);
            needleManager.OnKillerShotConditionMet.AddListener(_ => ShowKillerShotBanner());
        }

        if (roundTimer != null)
        {
            roundTimer.OnTimerTick.AddListener(OnTimerTick);
            roundTimer.OnTimerExpired.AddListener(OnTimerExpired);
        }

        if (killerShotManager != null)
        {
            killerShotManager.OnKillerShotPhaseEnded.AddListener(HideKillerShotBanner);
            killerShotManager.OnKillerShotExpired.AddListener(HideKillerShotBanner);
        }

        // Subscribe to zone events for prompt toggling
        // These are set up via the scene's CollectZone and DepositZone objects.
        // Wire them here or let NeedleManager forward its zone events.
        SubscribeToZones();

        // Set initial displays
        UpdateNeedleDisplay(p1NeedleCount, 0);
        UpdateNeedleDisplay(p2NeedleCount, 0);
        UpdateNeedleDisplay(pileNeedleCount, needleManager != null ? needleManager.PileCount : 0);
        UpdateTimer(timerDuration);
    }

    private void OnDestroy()
    {
        if (needleManager != null)
        {
            needleManager.OnPileCountChanged.RemoveListener(OnPileChanged);
            needleManager.OnPlayerNeedleCountChanged.RemoveListener(OnPlayerNeedleChanged);
            needleManager.OnNeedleStolen.RemoveListener(OnNeedleStolen);
        }
    }

    // Prompt initialisation

    private void InitialisePromptLabels()
    {
        if (p1CollectKey != null)
            p1CollectKey.text = p1KeyLabel;
        if (p1DepositKey != null)
            p1DepositKey.text = p1KeyLabel;
        if (p2CollectKey != null)
            p2CollectKey.text = p2KeyLabel;
        if (p2DepositKey != null)
            p2DepositKey.text = p2KeyLabel;
    }

    private void HideAllPrompts()
    {
        SetActive(p1CollectPrompt, false);
        SetActive(p1DepositPrompt, false);
        SetActive(p2CollectPrompt, false);
        SetActive(p2DepositPrompt, false);
    }

    // Zone proximity → prompt toggling

    private void SubscribeToZones()
    {
        // Find all zones in the scene and subscribe
        var collectZones = FindObjectsByType<CollectZone>(FindObjectsSortMode.None);
        foreach (var z in collectZones)
        {
            z.OnPlayerEnter += id => SetNearPile(id, true);
            z.OnPlayerExit += id => SetNearPile(id, false);
        }

        var depositZones = FindObjectsByType<DepositZone>(FindObjectsSortMode.None);
        foreach (var z in depositZones)
        {
            z.OnPlayerEnter += id => SetNearDeposit(id, true);
            z.OnPlayerExit += id => SetNearDeposit(id, false);
        }
    }

    private void SetNearPile(int playerID, bool near)
    {
        if (playerID == 1)
            p1NearPile = near;
        else
            p2NearPile = near;
        RefreshPrompts();
    }

    private void SetNearDeposit(int playerID, bool near)
    {
        if (playerID == 1)
            p1NearDeposit = near;
        else
            p2NearDeposit = near;
        RefreshPrompts();
    }

    private void RefreshPrompts()
    {
        // Collect prompt shows only when near the pile AND not near their deposit
        SetActive(p1CollectPrompt, p1NearPile && !p1NearDeposit);
        SetActive(p1DepositPrompt, p1NearDeposit && !p1NearPile);
        SetActive(p2CollectPrompt, p2NearPile && !p2NearDeposit);
        SetActive(p2DepositPrompt, p2NearDeposit && !p2NearPile);
    }

    // Needle count updates

    private void OnPileChanged(int count) => UpdateNeedleDisplay(pileNeedleCount, count);

    private void OnPlayerNeedleChanged(int playerID, int count)
    {
        UpdateNeedleDisplay(playerID == 1 ? p1NeedleCount : p2NeedleCount, count);
        PulsePanel(playerID == 1 ? p1Panel : p2Panel);
    }

    private void UpdateNeedleDisplay(TextMeshProUGUI text, int count)
    {
        if (text != null)
            text.text = count.ToString();
    }

    private void PulsePanel(RectTransform panel)
    {
        if (panel != null)
            StartCoroutine(PanelPop(panel));
    }

    private IEnumerator PanelPop(RectTransform panel)
    {
        Vector3 original = panel.localScale;
        Vector3 big = original * 1.15f;
        float dur = 0.1f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            panel.localScale = Vector3.Lerp(original, big, t / dur);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            panel.localScale = Vector3.Lerp(big, original, t / dur);
            yield return null;
        }

        panel.localScale = original;
    }

    // Timer

    private void OnTimerTick(float remaining) => UpdateTimer(remaining);

    private void OnTimerExpired() => UpdateTimer(0f);

    private void UpdateTimer(float remaining)
    {
        if (timerText == null)
            return;

        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{minutes:0}:{seconds:00}";

        bool urgent = remaining <= urgentThreshold;
        timerText.color = urgent ? timerUrgent : timerNormal;

        if (timerFill != null)
            timerFill.fillAmount = Mathf.Clamp01(remaining / timerDuration);
    }

    // Killer shot banner

    private void ShowKillerShotBanner()
    {
        SetActive(killerShotBanner, true);
        if (killerShotLabel != null)
            killerShotLabel.text = killerShotText;

        if (bannerPulseCoroutine != null)
            StopCoroutine(bannerPulseCoroutine);
        bannerPulseCoroutine = StartCoroutine(PulseBanner());
    }

    private void HideKillerShotBanner()
    {
        if (bannerPulseCoroutine != null)
        {
            StopCoroutine(bannerPulseCoroutine);
            bannerPulseCoroutine = null;
        }
        SetActive(killerShotBanner, false);
    }

    private IEnumerator PulseBanner()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * bannerPulseSpeed;
            if (killerShotLabel != null)
                killerShotLabel.color = Color.Lerp(
                    bannerColorA,
                    bannerColorB,
                    (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f
                );
            yield return null;
        }
    }

    // Needle steal consequence UI

    private void OnNeedleStolen(int winnerID)
    {
        HideKillerShotBanner();

        // Determine source (loser panel) and destination (winner panel) world-to-screen positions
        RectTransform src = winnerID == 1 ? p2Panel : p1Panel;
        RectTransform dst = winnerID == 1 ? p1Panel : p2Panel;

        // Show result banner
        string winnerName = $"PLAYER {winnerID}";
        int stolen = needleManager != null ? needleManager.GetStealAmount() : 3;
        ShowStealResult($"{winnerName} STEALS {stolen} NEEDLES!");

        // Spawn floating needles
        if (floatingNeedlePrefab != null && floatCanvas != null && src != null && dst != null)
            StartCoroutine(AnimateFloatingNeedles(src, dst, stolen));
    }

    private void ShowStealResult(string message)
    {
        SetActive(stealResultBanner, true);
        if (stealResultLabel != null)
            stealResultLabel.text = message;

        if (stealResultCoroutine != null)
            StopCoroutine(stealResultCoroutine);
        stealResultCoroutine = StartCoroutine(FadeOutStealResult());
    }

    private void HideStealResult()
    {
        SetActive(stealResultBanner, false);
    }

    private IEnumerator FadeOutStealResult()
    {
        yield return new WaitForSeconds(stealResultDuration - 0.4f);

        float t = 0f;
        CanvasGroup cg =
            stealResultBanner != null ? stealResultBanner.GetComponent<CanvasGroup>() : null;

        if (cg != null)
        {
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, t / 0.4f);
                yield return null;
            }
            cg.alpha = 1f; // reset for next time
        }

        SetActive(stealResultBanner, false);
    }

    // Magical floating needle animation

    /// <summary>
    /// Spawns <count> needle icons that float from the loser's panel to the winner's panel
    /// with a slight arc and staggered timing, tinted in the magical colour.
    /// </summary>
    private IEnumerator AnimateFloatingNeedles(RectTransform src, RectTransform dst, int count)
    {
        int spawnCount = Mathf.Min(count, floatNeedleCount);

        for (int i = 0; i < spawnCount; i++)
        {
            StartCoroutine(FloatOnNeedle(src, dst));
            yield return new WaitForSeconds(0.12f); // stagger each needle
        }
    }

    private IEnumerator FloatOnNeedle(RectTransform src, RectTransform dst)
    {
        // Instantiate the needle prefab on the canvas
        GameObject needle = Instantiate(floatingNeedlePrefab, floatCanvas);
        RectTransform rt = needle.GetComponent<RectTransform>();
        Image img = needle.GetComponent<Image>();

        if (img != null)
            img.color = floatNeedleColor;

        // Convert panel anchored positions to local canvas positions
        Vector2 startPos = GetCanvasPos(src) + Random.insideUnitCircle * floatSpread;
        Vector2 endPos = GetCanvasPos(dst);

        // Arc control point — midway but raised (magical arc upward)
        Vector2 midPoint = (startPos + endPos) * 0.5f + Vector2.up * 120f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / floatDuration;
            float curved = floatCurve.Evaluate(t);

            // Quadratic Bezier
            Vector2 pos =
                Mathf.Pow(1 - curved, 2) * startPos
                + 2f * (1 - curved) * curved * midPoint
                + Mathf.Pow(curved, 2) * endPos;

            rt.anchoredPosition = pos;

            // Fade out in last 20%
            if (img != null)
            {
                Color c = img.color;
                c.a = t > 0.8f ? Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f) : 1f;
                img.color = c;
            }

            // Slight rotation for a spinning needle effect
            rt.localRotation = Quaternion.Euler(0f, 0f, curved * 360f);

            yield return null;
        }

        Destroy(needle);
    }

    private Vector2 GetCanvasPos(RectTransform panel)
    {
        // Convert panel's world position to local position within floatCanvas
        Vector3 worldPos = panel.position;
        Vector2 screenPos;
        Camera cam = Camera.main;

        if (floatCanvas.GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay)
        {
            screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
        }
        else
        {
            screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            floatCanvas,
            screenPos,
            cam,
            out Vector2 localPoint
        );
        return localPoint;
    }

    // Utility

    private void SetActive(GameObject go, bool state)
    {
        if (go != null)
            go.SetActive(state);
    }
}
