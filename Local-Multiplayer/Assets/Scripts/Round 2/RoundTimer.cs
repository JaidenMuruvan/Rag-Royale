using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A simple reusable round timer.
/// Start it by calling StartTimer(). It fires OnTimerExpired when it hits zero
/// and ticks OnTimerTick every second so the UI can update.
/// </summary>
public class RoundTimer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private float duration = 60f; // seconds

    // --- events ---
    public UnityEvent<float> OnTimerTick; // fires every frame with time remaining
    public UnityEvent OnTimerExpired; // fires once at zero

    public float TimeRemaining { get; private set; }
    public bool IsRunning { get; private set; }

    public void StartTimer()
    {
        if (IsRunning)
            return;
        TimeRemaining = duration;
        IsRunning = true;
        StartCoroutine(TimerRoutine());
    }

    public void StopTimer()
    {
        IsRunning = false;
        StopAllCoroutines();
    }

    public void PauseTimer() => IsRunning = false;

    public void ResumeTimer() => IsRunning = true;

    private IEnumerator TimerRoutine()
    {
        while (TimeRemaining > 0f)
        {
            if (IsRunning)
            {
                TimeRemaining -= Time.deltaTime;
                if (TimeRemaining < 0f)
                    TimeRemaining = 0f;
                OnTimerTick?.Invoke(TimeRemaining);
            }
            yield return null;
        }

        IsRunning = false;
        OnTimerExpired?.Invoke();
    }
}
