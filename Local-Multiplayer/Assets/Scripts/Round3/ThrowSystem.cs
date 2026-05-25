using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ThrowSystem : MonoBehaviour
{
    // Inspector

    [Header("Throw Settings")]
    [SerializeField]
    private float throwSpeed = 18f;

    [SerializeField]
    private float normalDamage = 10f;

    [SerializeField]
    private float powerDamage = 15f;

    [SerializeField]
    private float throwCooldown = 0.4f; // seconds between throws

    [Header("Standalone Test Defaults")]
    [SerializeField]
    private int defaultP1Needles = 10;

    [SerializeField]
    private int defaultP2Needles = 10;

    [Header("Projectile")]
    [SerializeField]
    private GameObject needleProjectilePrefab;

    [Header("Launch Points")]
    [SerializeField]
    private string launchPointName = "NeedleLaunchPoint";

    [Header("References")]
    [SerializeField]
    private KillerShotManager killerShotManager;

    public UnityEvent<int, int> OnAmmoChanged;
    public UnityEvent<int> OnNeedlesExhausted;
    public UnityEvent OnBothExhausted;
    public UnityEvent<int> OnPowerThrowReady;
    public UnityEvent<int> OnPowerThrowUsed;
    public UnityEvent<int, bool> OnThrowFired;

    public int P1Ammo { get; private set; }
    public int P2Ammo { get; private set; }
    public bool P1PowerThrow { get; private set; }
    public bool P2PowerThrow { get; private set; }

    private bool p1Exhausted = false;
    private bool p2Exhausted = false;

    private float p1Cooldown = 0f;
    private float p2Cooldown = 0f;

    private Transform p1LaunchPoint;
    private Transform p2LaunchPoint;

    private MultiplayerPlayerController p1Controller;
    private MultiplayerPlayerController p2Controller;
    private PlayerHealth p1Health;
    private PlayerHealth p2Health;

    private bool playersResolved = false;

    private void Start()
    {
        LoadAmmoFromMatchData();

        if (killerShotManager != null)
            killerShotManager.OnKillerShotWinner.AddListener(OnKillerShotWon);
    }

    private void Update()
    {
        if (!playersResolved)
        {
            TryResolvePlayers();
            return;
        }

        if (p1Cooldown > 0f)
            p1Cooldown -= Time.deltaTime;
        if (p2Cooldown > 0f)
            p2Cooldown -= Time.deltaTime;
    }

    private void OnDestroy()
    {
        if (killerShotManager != null)
            killerShotManager.OnKillerShotWinner.RemoveListener(OnKillerShotWon);

        if (p1Controller != null)
            p1Controller.OnThrowEvent -= () => TryThrow(1);
        if (p2Controller != null)
            p2Controller.OnThrowEvent -= () => TryThrow(2);
    }

    private void LoadAmmoFromMatchData()
    {
        if (MatchData.Instance != null)
        {
            P1Ammo = MatchData.Instance.P1NeedleCount;
            P2Ammo = MatchData.Instance.P2NeedleCount;
        }
        else
        {
            P1Ammo = defaultP1Needles;
            P2Ammo = defaultP2Needles;
        }

        OnAmmoChanged?.Invoke(1, P1Ammo);
        OnAmmoChanged?.Invoke(2, P2Ammo);

        CheckExhaustion(1);
        CheckExhaustion(2);
    }

    private void TryResolvePlayers()
    {
        var controllers = FindObjectsByType<MultiplayerPlayerController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c.PlayerID == 1 && p1Controller == null)
            {
                p1Controller = c;
                p1Health = c.GetComponent<PlayerHealth>();
                p1LaunchPoint = FindChildByName(c.transform, launchPointName);
                p1Controller.OnThrowEvent += () => TryThrow(1);
            }
            else if (c.PlayerID == 2 && p2Controller == null)
            {
                p2Controller = c;
                p2Health = c.GetComponent<PlayerHealth>();
                p2LaunchPoint = FindChildByName(c.transform, launchPointName);
                p2Controller.OnThrowEvent += () => TryThrow(2);
            }
        }

        if (p1Controller != null && p2Controller != null)
            playersResolved = true;
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        var queue = new System.Collections.Generic.Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t.name == targetName)
                return t;
            foreach (Transform child in t)
                queue.Enqueue(child);
        }
        Debug.LogWarning($"[ThrowSystem] '{targetName}' not found under {root.name}.");
        return null;
    }

    public void TryThrow(int playerID)
    {
        if (!playersResolved)
            return;

        int ammo = playerID == 1 ? P1Ammo : P2Ammo;
        float cooldown = playerID == 1 ? p1Cooldown : p2Cooldown;
        bool exhausted = playerID == 1 ? p1Exhausted : p2Exhausted;

        if (exhausted || cooldown > 0f)
            return;

        bool isPower = playerID == 1 ? P1PowerThrow : P2PowerThrow;

        // Power throw does NOT cost an extra needle
        if (!isPower)
        {
            if (ammo <= 0)
                return;
            if (playerID == 1)
                P1Ammo--;
            else
                P2Ammo--;
        }

        if (playerID == 1)
            p1Cooldown = throwCooldown;
        else
            p2Cooldown = throwCooldown;

        if (isPower)
        {
            if (playerID == 1)
                P1PowerThrow = false;
            else
                P2PowerThrow = false;
            OnPowerThrowUsed?.Invoke(playerID);
        }

        OnAmmoChanged?.Invoke(playerID, playerID == 1 ? P1Ammo : P2Ammo);
        OnThrowFired?.Invoke(playerID, isPower);

        LaunchProjectile(playerID, isPower);
        CheckExhaustion(playerID);
    }

    private void LaunchProjectile(int playerID, bool isPower)
    {
        Transform launchPoint = playerID == 1 ? p1LaunchPoint : p2LaunchPoint;
        PlayerHealth targetHealth = playerID == 1 ? p2Health : p1Health;
        MultiplayerPlayerController opponent = playerID == 1 ? p2Controller : p1Controller;

        if (launchPoint == null || needleProjectilePrefab == null)
            return;

        // Direction: toward the opponent, locked on X axis (side-scroller)
        Vector3 direction = Vector3.right * (playerID == 1 ? 1f : -1f);
        if (opponent != null)
        {
            float diff = opponent.transform.position.x - launchPoint.position.x;
            direction = Vector3.right * Mathf.Sign(diff);
        }

        GameObject proj = Instantiate(
            needleProjectilePrefab,
            launchPoint.position,
            Quaternion.LookRotation(direction)
        );
        NeedleProjectile np = proj.GetComponent<NeedleProjectile>();

        if (np != null)
            np.Initialise(
                playerID,
                direction,
                throwSpeed,
                isPower ? powerDamage : normalDamage,
                isPower,
                targetHealth
            );
    }

    private void CheckExhaustion(int playerID)
    {
        int ammo = playerID == 1 ? P1Ammo : P2Ammo;
        if (ammo > 0)
            return;

        if (playerID == 1 && !p1Exhausted)
        {
            p1Exhausted = true;
            OnNeedlesExhausted?.Invoke(1);
        }
        else if (playerID == 2 && !p2Exhausted)
        {
            p2Exhausted = true;
            OnNeedlesExhausted?.Invoke(2);
        }

        if (p1Exhausted && p2Exhausted)
            OnBothExhausted?.Invoke();
    }

    private void OnKillerShotWon(int winnerID)
    {
        if (winnerID == 1)
            P1PowerThrow = true;
        else
            P2PowerThrow = true;
        OnPowerThrowReady?.Invoke(winnerID);
    }
}
