using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Fired by ThrowSystem. Travels in a straight line on the X axis.
/// On hit: deals damage, spawns hit VFX, camera shake, hitstop.
/// Fires static OnProjectileHit so Round3UI can show hit flash without needing a direct reference.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class NeedleProjectile : MonoBehaviour
{
    // Static event — Round3UI subscribes to this, no inspector wiring needed
    public static event Action<int, bool> OnProjectileHit; // (hitPlayerID, isPower)

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

    // Runtime — set by ThrowSystem.Initialise()

    private int ownerID;
    private int targetPlayerID;
    private Vector3 direction;
    private float speed;
    private float damage;
    private bool isPower;
    private PlayerHealth targetHealth;
    private Rigidbody rb;
    private bool hasHit = false;

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

        rb.linearVelocity = direction * speed;

        SetupTrail();
        StartCoroutine(LifetimeTimeout());
    }

    // Trail

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

    // Collision

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health != null && health == targetHealth)
        {
            hasHit = true;
            HitPlayer();
        }
        else if (health == null)
        {
            hasHit = true;
            HitEnvironment();
        }
        // Ignore owner collider
    }

    // Hit

    private void HitPlayer()
    {
        targetHealth?.TakeDamage(damage);

        // Notify UI (static — no inspector wiring needed)
        OnProjectileHit?.Invoke(targetPlayerID, isPower);

        // VFX
        GameObject vfxPrefab = isPower ? powerHitVFXPrefab : normalHitVFXPrefab;
        if (vfxPrefab != null)
            Destroy(Instantiate(vfxPrefab, transform.position, Quaternion.identity), 3f);

        // Feedback
        if (isPower)
        {
            CameraShake.Instance?.Shake(powerShakeDuration, powerShakeMagnitude);
            HitStop.Instance?.Freeze(powerHitstopDuration);
        }
        else
        {
            CameraShake.Instance?.Shake(normalShakeDuration, normalShakeMagnitude);
            HitStop.Instance?.Freeze(normalHitstopDuration);
        }

        Despawn();
    }

    private void HitEnvironment()
    {
        if (missVFXPrefab != null)
            Destroy(
                Instantiate(missVFXPrefab, transform.position, Quaternion.LookRotation(-direction)),
                2f
            );
        Despawn();
    }

    // Despawn

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
