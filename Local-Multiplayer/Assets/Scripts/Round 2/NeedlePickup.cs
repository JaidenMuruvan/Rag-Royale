using UnityEngine;

/// <summary>
/// Sits on each pre-placed needle GameObject in the scene.
/// No spawning — assign all needles directly in NeedleManager's Inspector list.
///
/// On collect: tints the needle's Renderer to the collecting player's colour.
/// On deposit: colour stays (it's now owned by that player's stash).
/// On steal:   retints to the winner's colour.
/// On reset:   returns to the neutral pile colour.
///
/// States:
///   InPile       — sitting in pile, neutral colour, waiting
///   HeldByPlayer — snapping to player's hold point, tinted to player colour
///   Deposited    — hidden (deactivated), counted in stash, ready for Round 3
///   Projectile   — Round 3: physics launched
/// </summary>
[RequireComponent(typeof(Collider))]
public class NeedlePickup : MonoBehaviour
{
    [Header("Hold Snap")]
    [SerializeField]
    private float snapSpeed = 18f;

    [SerializeField]
    private Vector3 holdOffset = Vector3.zero;

    public enum NeedleState
    {
        InPile,
        HeldByPlayer,
        Deposited,
        Projectile,
    }

    public NeedleState CurrentState { get; private set; } = NeedleState.InPile;
    public int HeldByPlayerID { get; private set; } = 0;

    private Transform holdPoint;
    private Collider col;
    private Rigidbody rb;
    private Renderer rend;

    private void Awake()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void Update()
    {
        if (CurrentState == NeedleState.HeldByPlayer)
            SnapToHoldPoint();
    }

    // State transitions

    /// <summary>Player collects this needle — snap to their hold point and tint it.</summary>
    public void Collect(Transform playerHoldPoint, int playerID, Color playerColour)
    {
        if (CurrentState != NeedleState.InPile)
            return;

        CurrentState = NeedleState.HeldByPlayer;
        HeldByPlayerID = playerID;
        holdPoint = playerHoldPoint;

        transform.SetParent(null);
        col.enabled = false;

        SetColour(playerColour);
    }

    /// <summary>Player deposits — hide it, keep the colour in case of steal.</summary>
    public void Deposit()
    {
        if (CurrentState != NeedleState.HeldByPlayer)
            return;

        CurrentState = NeedleState.Deposited;
        HeldByPlayerID = 0;
        holdPoint = null;

        gameObject.SetActive(false);
    }

    /// <summary>Killer shot steal — reactivate, retint to winner's colour, snap to their hold point.</summary>
    public void Steal(Transform winnerHoldPoint, int winnerID, Color winnerColour)
    {
        gameObject.SetActive(true);
        CurrentState = NeedleState.HeldByPlayer;
        HeldByPlayerID = winnerID;
        holdPoint = winnerHoldPoint;
        col.enabled = false;

        transform.SetParent(null);
        SetColour(winnerColour);
    }

    /// <summary>Return to pile — reset colour and re-enable.</summary>
    public void ResetToPile(Color pileColour)
    {
        gameObject.SetActive(true);
        CurrentState = NeedleState.InPile;
        HeldByPlayerID = 0;
        holdPoint = null;
        col.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }
        SetColour(pileColour);
    }

    /// <summary>Round 3 — bring a deposited needle back as a throwable.</summary>
    public void PrepareAsProjectile(Transform playerHoldPoint, int playerID)
    {
        gameObject.SetActive(true);
        CurrentState = NeedleState.HeldByPlayer;
        HeldByPlayerID = playerID;
        holdPoint = playerHoldPoint;
        col.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    /// <summary>Round 3 — launch in direction.</summary>
    public void Launch(Vector3 direction, float speed)
    {
        if (CurrentState == NeedleState.Projectile)
            return;

        CurrentState = NeedleState.Projectile;
        holdPoint = null;
        transform.SetParent(null);
        col.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearVelocity = direction.normalized * speed;
        }
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void SnapToHoldPoint()
    {
        if (holdPoint == null)
            return;
        Vector3 target = holdPoint.position + holdPoint.TransformDirection(holdOffset);
        transform.position = Vector3.Lerp(transform.position, target, snapSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            holdPoint.rotation,
            snapSpeed * Time.deltaTime
        );
    }

    private void SetColour(Color colour)
    {
        if (rend == null)
            return;
        // Works with standard materials — creates a material instance automatically
        rend.material.color = colour;
    }
}
