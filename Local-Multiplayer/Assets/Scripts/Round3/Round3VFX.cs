// Round3VFX.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Round3VFX : MonoBehaviour
{
    // ── Particles & UI ───────────────────────────────────────────────
    [Header("Power Throw — Readiness Particles")]
    [SerializeField]
    private ParticleSystem p1PowerReadyParticles;

    [SerializeField]
    private ParticleSystem p2PowerReadyParticles;

    [Header("Power Throw — Launch Burst")]
    [SerializeField]
    private ParticleSystem p1PowerLaunchBurst;

    [SerializeField]
    private ParticleSystem p2PowerLaunchBurst;

    [Header("Normal Throw — Launch Puff")]
    [SerializeField]
    private ParticleSystem p1NormalLaunchPuff;

    [SerializeField]
    private ParticleSystem p2NormalLaunchPuff;

    [Header("Killer Shot Warning")]
    [SerializeField]
    private Image killerShotWarningOverlay;

    [SerializeField]
    private float killerShotFlashDuration = 0.6f;

    [SerializeField]
    private Color killerShotFlashColor = new Color(1f, 0.1f, 0.05f, 0.55f);

    [SerializeField]
    private ParticleSystem killerShotBurstParticles;

    [Header("Round End Fanfare")]
    [SerializeField]
    private ParticleSystem winnerConfetti;

    [SerializeField]
    private Image roundEndFlashOverlay;

    [SerializeField]
    private float roundEndFlashDuration = 0.4f;

    [SerializeField]
    private Color roundEndFlashColor = new Color(1f, 0.85f, 0.1f, 0.45f);

    // ── Audio Clips ──────────────────────────────────────────────────
    [Header("Audio Clips")]
    [SerializeField]
    private AudioClip powerThrowReadyClip;

    [SerializeField]
    private AudioClip powerThrowLaunchClip;

    [SerializeField]
    private AudioClip killerShotWarningClip;

    [SerializeField]
    private AudioClip roundEndFanfareClip;

    // ── Private ──────────────────────────────────────────────────────
    private Coroutine overlayCoroutine;

    // ── Power Throw Ready ────────────────────────────────────────────
    public void PlayPowerThrowReady(int playerID)
    {
        ParticleSystem ps = playerID == 1 ? p1PowerReadyParticles : p2PowerReadyParticles;
        if (ps != null && !ps.isPlaying)
            ps.Play();

        AudioManager.Instance?.Play(powerThrowReadyClip, 1f);
    }

    public void StopPowerThrowReady(int playerID)
    {
        ParticleSystem ps = playerID == 1 ? p1PowerReadyParticles : p2PowerReadyParticles;
        ps?.Stop();
    }

    // ── Throw Launch ─────────────────────────────────────────────────
    public void PlayPowerThrowLaunch(int playerID)
    {
        ParticleSystem ps = playerID == 1 ? p1PowerLaunchBurst : p2PowerLaunchBurst;
        ps?.Play();

        AudioManager.Instance?.Play(powerThrowLaunchClip, 1.1f);
        CameraShake.Instance?.Shake(0.08f, 0.06f);
    }

    public void PlayNormalThrowLaunch(int playerID)
    {
        ParticleSystem ps = playerID == 1 ? p1NormalLaunchPuff : p2NormalLaunchPuff;
        ps?.Play();
    }

    // ── Killer Shot Warning ──────────────────────────────────────────
    public void PlayKillerShotWarning(int triggeringPlayerID)
    {
        killerShotBurstParticles?.Play();
        AudioManager.Instance?.Play(killerShotWarningClip, 1f);
        FlashOverlay(killerShotWarningOverlay, killerShotFlashColor, killerShotFlashDuration);
    }

    // ── Round End Fanfare ────────────────────────────────────────────
    public void PlayRoundEndFanfare(int winnerID)
    {
        winnerConfetti?.Play();
        AudioManager.Instance?.Play(roundEndFanfareClip, 1f);
        FlashOverlay(roundEndFlashOverlay, roundEndFlashColor, roundEndFlashDuration);
    }

    // ── Internal Helpers ─────────────────────────────────────────────
    private void FlashOverlay(Image overlay, Color color, float duration)
    {
        if (overlay == null)
            return;
        if (overlayCoroutine != null)
            StopCoroutine(overlayCoroutine);
        overlayCoroutine = StartCoroutine(FlashRoutine(overlay, color, duration));
    }

    private IEnumerator FlashRoutine(Image overlay, Color targetColor, float duration)
    {
        overlay.color = targetColor;
        overlay.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            Color c = targetColor;
            c.a = Mathf.Lerp(targetColor.a, 0f, t);
            overlay.color = c;
            yield return null;
        }

        overlay.gameObject.SetActive(false);
        overlayCoroutine = null;
    }
}
