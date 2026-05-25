using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Round3UI : MonoBehaviour
{
    [Header("P1 Ammo Panel")]
    [SerializeField]
    private Image p1NeedleIcon;

    [SerializeField]
    private TextMeshProUGUI p1AmmoCount;

    [SerializeField]
    private RectTransform p1Panel;

    [Header("P2 Ammo Panel")]
    [SerializeField]
    private Image p2NeedleIcon;

    [SerializeField]
    private TextMeshProUGUI p2AmmoCount;

    [SerializeField]
    private RectTransform p2Panel;

    [Header("Power Throw Banners")]
    [SerializeField]
    private GameObject p1PowerBanner;

    [SerializeField]
    private TextMeshProUGUI p1PowerLabel;

    [SerializeField]
    private GameObject p2PowerBanner;

    [SerializeField]
    private TextMeshProUGUI p2PowerLabel;

    [SerializeField]
    private string powerThrowText = "POWER THROW!";

    [SerializeField]
    private Color powerColorA = new Color(1f, 0.85f, 0f);

    [SerializeField]
    private Color powerColorB = new Color(1f, 0.2f, 0.1f);

    [SerializeField]
    private float powerPulseSpeed = 3f;

    [Header("Timer")]
    [SerializeField]
    private TextMeshProUGUI timerText;

    [SerializeField]
    private Image timerFill;

    [SerializeField]
    private float timerDuration = 60f;

    [SerializeField]
    private Color timerNormal = Color.white;

    [SerializeField]
    private Color timerUrgent = new Color(1f, 0.2f, 0.1f);

    [SerializeField]
    private float urgentThreshold = 10f;

    [Header("Hit Flash")]
    [SerializeField]
    private Image p1HitFlash;

    [SerializeField]
    private Image p2HitFlash;

    [SerializeField]
    private Image p1PowerHitFlash;

    [SerializeField]
    private Image p2PowerHitFlash;

    [SerializeField]
    private float normalFlashDuration = 0.15f;

    [SerializeField]
    private float powerFlashDuration = 0.35f;

    [SerializeField]
    private Color normalFlashColor = new Color(1f, 0.1f, 0.1f, 0.55f);

    [SerializeField]
    private Color powerFlashColor = new Color(1f, 0.35f, 0f, 0.85f);

    [Header("Out of Ammo")]
    [SerializeField]
    private GameObject p1OutOfAmmoBanner;

    [SerializeField]
    private GameObject p2OutOfAmmoBanner;

    [SerializeField]
    private TextMeshProUGUI p1OutOfAmmoLabel;

    [SerializeField]
    private TextMeshProUGUI p2OutOfAmmoLabel;

    [SerializeField]
    private string outOfAmmoText = "OUT OF AMMO";

    [SerializeField]
    private float outOfAmmoDuration = 2.5f;

    [Header("References")]
    [SerializeField]
    private ThrowSystem throwSystem;

    [SerializeField]
    private RoundTimer roundTimer;

    private Coroutine p1PowerPulse;
    private Coroutine p2PowerPulse;

    // Lifecycle

    private void OnEnable()
    {
        NeedleProjectile.OnProjectileHit += OnProjectileHit;
    }

    private void OnDisable()
    {
        NeedleProjectile.OnProjectileHit -= OnProjectileHit;
    }

    private void Start()
    {
        HideAll();

        if (throwSystem != null)
        {
            throwSystem.OnAmmoChanged.AddListener(OnAmmoChanged);
            throwSystem.OnPowerThrowReady.AddListener(ShowPowerBanner);
            throwSystem.OnPowerThrowUsed.AddListener(HidePowerBanner);
            throwSystem.OnNeedlesExhausted.AddListener(OnNeedlesExhausted);

            UpdateAmmoDisplay(p1AmmoCount, p1Panel, throwSystem.P1Ammo);
            UpdateAmmoDisplay(p2AmmoCount, p2Panel, throwSystem.P2Ammo);
        }

        if (roundTimer != null)
        {
            roundTimer.OnTimerTick.AddListener(t => UpdateTimer(t));
            roundTimer.OnTimerExpired.AddListener(() => UpdateTimer(0f));
        }

        UpdateTimer(timerDuration);
    }

    private void HideAll()
    {
        SetActive(p1PowerBanner, false);
        SetActive(p2PowerBanner, false);
        SetActive(p1OutOfAmmoBanner, false);
        SetActive(p2OutOfAmmoBanner, false);
        SetAlpha(p1HitFlash, 0f);
        SetAlpha(p2HitFlash, 0f);
        SetAlpha(p1PowerHitFlash, 0f);
        SetAlpha(p2PowerHitFlash, 0f);
    }

    private void OnAmmoChanged(int playerID, int count)
    {
        if (playerID == 1)
            UpdateAmmoDisplay(p1AmmoCount, p1Panel, count);
        else
            UpdateAmmoDisplay(p2AmmoCount, p2Panel, count);
    }

    private void UpdateAmmoDisplay(TextMeshProUGUI label, RectTransform panel, int count)
    {
        if (label != null)
            label.text = count.ToString();
        if (panel != null)
            StartCoroutine(PanelPop(panel));
    }

    private IEnumerator PanelPop(RectTransform panel)
    {
        Vector3 orig = panel.localScale;
        Vector3 big = orig * 1.18f;
        float dur = 0.08f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            panel.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            panel.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }
        panel.localScale = orig;
    }

    private void ShowPowerBanner(int playerID)
    {
        if (playerID == 1)
        {
            SetActive(p1PowerBanner, true);
            if (p1PowerLabel != null)
                p1PowerLabel.text = powerThrowText;
            if (p1PowerPulse != null)
                StopCoroutine(p1PowerPulse);
            p1PowerPulse = StartCoroutine(PulseBanner(p1PowerLabel));
        }
        else
        {
            SetActive(p2PowerBanner, true);
            if (p2PowerLabel != null)
                p2PowerLabel.text = powerThrowText;
            if (p2PowerPulse != null)
                StopCoroutine(p2PowerPulse);
            p2PowerPulse = StartCoroutine(PulseBanner(p2PowerLabel));
        }
    }

    private void HidePowerBanner(int playerID)
    {
        if (playerID == 1)
        {
            if (p1PowerPulse != null)
            {
                StopCoroutine(p1PowerPulse);
                p1PowerPulse = null;
            }
            SetActive(p1PowerBanner, false);
        }
        else
        {
            if (p2PowerPulse != null)
            {
                StopCoroutine(p2PowerPulse);
                p2PowerPulse = null;
            }
            SetActive(p2PowerBanner, false);
        }
    }

    private IEnumerator PulseBanner(TextMeshProUGUI label)
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * powerPulseSpeed;
            if (label != null)
                label.color = Color.Lerp(
                    powerColorA,
                    powerColorB,
                    (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f
                );
            yield return null;
        }
    }

    private void OnProjectileHit(int hitPlayerID, bool isPower)
    {
        Image flash =
            hitPlayerID == 1
                ? (isPower ? p1PowerHitFlash : p1HitFlash)
                : (isPower ? p2PowerHitFlash : p2HitFlash);

        Color color = isPower ? powerFlashColor : normalFlashColor;
        float duration = isPower ? powerFlashDuration : normalFlashDuration;

        if (flash != null)
            StartCoroutine(FlashImage(flash, color, duration));
    }

    private IEnumerator FlashImage(Image img, Color color, float duration)
    {
        Color c = color;
        img.color = c;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(color.a, 0f, t / duration);
            img.color = c;
            yield return null;
        }

        SetAlpha(img, 0f);
    }

    private void OnNeedlesExhausted(int playerID)
    {
        GameObject banner = playerID == 1 ? p1OutOfAmmoBanner : p2OutOfAmmoBanner;
        TextMeshProUGUI label = playerID == 1 ? p1OutOfAmmoLabel : p2OutOfAmmoLabel;
        RectTransform panel = playerID == 1 ? p1Panel : p2Panel;

        if (label != null)
            label.text = outOfAmmoText;
        StartCoroutine(ShowThenHide(banner, outOfAmmoDuration));

        // Grey out the whole ammo panel to show it's spent
        if (panel != null)
        {
            foreach (var img in panel.GetComponentsInChildren<Image>())
                img.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            foreach (var txt in panel.GetComponentsInChildren<TextMeshProUGUI>())
                txt.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        }
    }

    private IEnumerator ShowThenHide(GameObject go, float duration)
    {
        SetActive(go, true);
        yield return new WaitForSeconds(duration);
        SetActive(go, false);
    }

    private void UpdateTimer(float remaining)
    {
        if (timerText == null)
            return;
        int mins = Mathf.FloorToInt(remaining / 60f);
        int secs = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{mins:0}:{secs:00}";
        timerText.color = remaining <= urgentThreshold ? timerUrgent : timerNormal;
        if (timerFill != null)
            timerFill.fillAmount = Mathf.Clamp01(remaining / timerDuration);
    }

    private void SetActive(GameObject go, bool state)
    {
        if (go != null)
            go.SetActive(state);
    }

    private void SetAlpha(Image img, float a)
    {
        if (img == null)
            return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}
