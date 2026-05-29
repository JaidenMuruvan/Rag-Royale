using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Fired by ThrowSystem. Travels in a straight line on the X axis.
/// On hit: deals damage (PlayerHealth owns the react animation via ReactRoutine),
/// applies knockback to the opponent, spawns VFX, triggers camera shake + hitstop.
/// Static OnProjectileHit lets Round3UI react without inspector wiring.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class NeedleProjectile : MonoBehaviour
{
    // ── Static event ──────────────────────────────────────────────────────────
    public static event Action<int, bool> OnProjectileHit; // (hitPlayerID, isPower)

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Travel")]
    [SerializeField]
    private float maxLifetime = 4f;

    [Header("VFX — Trail")]
    [SerializeField]
    private TrailRenderer trailRenderer;

    [SerializeField]
    private Color normalTrailColor = new Color(1f, 0.9f, 0.5f, 1f);

    [SerializeField]
    private Color powerTrailColor = new Color(1f, 0.2f, 0.05f, 1f);

    [SerializeField]
    private float normalTrailWidth = 0.04f;

    [SerializeField]
    private float powerTrailWidth = 0.12f;

    [Header("VFX — Hit Particles")]
    [SerializeField]
    private GameObject normalHitVFXPrefab;

    [SerializeField]
    private GameObject powerHitVFXPrefab;

    [SerializeField]
    private GameObject missVFXPrefab;

    [Header("Feedback — Normal Hit")]
    [SerializeField]
    private float normalShakeDuration = 0.12f;

    [SerializeField]
    private float normalShakeMagnitude = 0.08f;

    [SerializeField]
    private float normalHitstopDuration = 0.06f;

    [Header("Feedback — Power Hit")]
    [SerializeField]
    private float powerShakeDuration = 0.28f;

    [SerializeField]
    private float powerShakeMagnitude = 0.22f;

    [SerializeField]
    private float powerHitstopDuration = 0.14f;

    [Header("Knockback on Hit")]
    [Tooltip("Horizontal force applied to the opponent on a normal hit.")]
    [SerializeField]
    private float normalKnockbackForce = 5f;

    [Tooltip("Horizontal force applied to the opponent on a power hit.")]
    [SerializeField]
    private float powerKnockbackForce = 12f;

    [Tooltip("Upward component mixed into the knockback direction (0 = pure horizontal).")]
    [SerializeField]
    private float knockbackUpAngle = 0.15f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private int ownerID;
    private int targetPlayerID;
    private Vector3 direction;
    private float speed;
    private float damage;
    private bool isPower;

    private PlayerHealth targetHealth;
    private MultiplayerPlayerController targetController;

    private Rigidbody rb;
    private bool hasHit = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints =
            RigidbodyConstraints.FreezeRotation
            | RigidbodyConstraints.FreezePositionY
            | RigidbodyConstraints.FreezePositionZ;
    }

    // ── Initialise (called by ThrowSystem) ────────────────────────────────────

    public void Initialise(
        int ownerPlayerID,
        Vector3 travelDirection,
        float travelSpeed,
        float dealDamage,
        bool powerThrow,
        PlayerHealth target
    )
    {
        ownerID = ownerPlayerID;
        targetPlayerID = ownerPlayerID == 1 ? 2 : 1;
        direction = travelDirection.normalized;
        speed = travelSpeed;
        damage = dealDamage;
        isPower = powerThrow;
        targetHealth = target;

        // Cache the controller now — no Find needed at impact time.
        if (target != null)
        {
            targetController = target.GetComponent<MultiplayerPlayerController>();

            if (targetController == null)
                Debug.LogWarning(
                    "[NeedleProjectile] Target has no MultiplayerPlayerController — knockback will be skipped."
                );
        }

        rb.linearVelocity = direction * speed;
        SetupTrail();
        StartCoroutine(LifetimeTimeout());
    }

    // ── Trail ─────────────────────────────────────────────────────────────────

    private void SetupTrail()
    {
        if (trailRenderer == null)
            return;
        trailRenderer.startColor = isPower ? powerTrailColor : normalTrailColor;
        trailRenderer.endColor = new Color(0f, 0f, 0f, 0f);
        trailRenderer.startWidth = isPower ? powerTrailWidth : normalTrailWidth;
        trailRenderer.endWidth = 0f;
        trailRenderer.time = isPower ? 0.28f : 0.12f;
    }

    // ── Collision ─────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health != null && health == targetHealth)
        {
            hasHit = true;
            HitPlayer();
            return;
        }

        if (health == null)
        {
            // Ignore the owner's own colliders
            MultiplayerPlayerController ctrl =
                other.GetComponentInParent<MultiplayerPlayerController>();
            if (ctrl != null && ctrl.PlayerID == ownerID)
                return;

            hasHit = true;
            HitEnvironment();
        }
    }

    // ── Hit ───────────────────────────────────────────────────────────────────

    private void HitPlayer()
    {
        // 1. Damage — PlayerHealth.TakeDamage internally starts ReactRoutine,
        //    which calls animationScript.PlayTakeDamage() and sets IsReacting.
        //    We do NOT call PlayTakeDamage() here: ReactRoutine owns that state
        //    and will restore idle after reactDuration. Calling it a second time
        //    here is harmless for the bool but redundant, and would fight the
        //    restore timing during hitstop (WaitForSeconds uses scaled time).
        targetHealth?.TakeDamage(damage);

        if (targetHealth == null)
            Debug.LogWarning("[NeedleProjectile] targetHealth is null at hit time.");

        // 2. Knockback — push opponent in throw direction with a slight upward pop.
        //    AnimationManager.PlayTakeDamage() will have already fired above via
        //    ReactRoutine. The knockback is purely physics.
        if (targetController != null)
        {
            float force = isPower ? powerKnockbackForce : normalKnockbackForce;
            Vector3 knockDir = (direction + Vector3.up * knockbackUpAngle).normalized;
            targetController.ApplyKnockback(knockDir * force);
        }
        else
        {
            Debug.Log(
                "[NeedleProjectile] No MultiplayerPlayerController on target — knockback skipped."
            );
        }

        // 3. VFX at impact point
        GameObject vfxPrefab = isPower ? powerHitVFXPrefab : normalHitVFXPrefab;
        if (vfxPrefab != null)
            Destroy(Instantiate(vfxPrefab, transform.position, Quaternion.identity), 3f);
        else
            Debug.Log(
                $"[NeedleProjectile] No {(isPower ? "power" : "normal")} hit VFX prefab assigned."
            );

        // 4. Screen feedback — hitstop before shake so the shake begins
        //    after time resumes (feels snappier than both at once)
        if (isPower)
        {
            HitStop.Instance?.Freeze(powerHitstopDuration);
            CameraShake.Instance?.Shake(powerShakeDuration, powerShakeMagnitude);
        }
        else
        {
            HitStop.Instance?.Freeze(normalHitstopDuration);
            CameraShake.Instance?.Shake(normalShakeDuration, normalShakeMagnitude);
        }

        // 5. Notify UI
        OnProjectileHit?.Invoke(targetPlayerID, isPower);

        Despawn();
    }

    private void HitEnvironment()
    {
        if (missVFXPrefab != null)
            Destroy(
                Instantiate(missVFXPrefab, transform.position, Quaternion.LookRotation(-direction)),
                2f
            );
        else
            Debug.Log("[NeedleProjectile] No miss VFX prefab assigned.");

        Despawn();
    }

    // ── Despawn ───────────────────────────────────────────────────────────────

    private IEnumerator LifetimeTimeout()
    {
        yield return new WaitForSeconds(maxLifetime);
        if (!hasHit)
            Despawn();
    }

    private void Despawn()
    {
        if (trailRenderer != null)
        {
            trailRenderer.transform.SetParent(null);
            Destroy(trailRenderer.gameObject, trailRenderer.time + 0.1f);
        }
        Destroy(gameObject);
    }
}
