using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NeedlePickup : MonoBehaviour
{
    [Header("Hold Snap")]
    [SerializeField]
    private float snapSpeed = 18f;

    [SerializeField]
    private Vector3 holdOffset = Vector3.zero;

    [Header("Drop Animation")]
    [SerializeField]
    private float dropDuration = 0.35f;

    [Header("sphere Renderer")]
    [SerializeField]
    private Renderer sphereRenderer;

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

    private Vector3 pileRestPosition;
    private Coroutine dropCoroutine;

    private void Awake()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (sphereRenderer == null)
            sphereRenderer = GetComponentInChildren<Renderer>();

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

    public void Collect(Transform playerHoldPoint, int playerID, Color playerColour)
    {
        if (CurrentState != NeedleState.InPile)
            return;

        CurrentState = NeedleState.HeldByPlayer;
        HeldByPlayerID = playerID;
        holdPoint = playerHoldPoint;
        pileRestPosition = transform.position;

        transform.SetParent(null);
        col.enabled = false;
        SetColour(playerColour);
    }

    public void ReturnToPile(Color pileColour)
    {
        if (CurrentState != NeedleState.HeldByPlayer)
            return;

        CurrentState = NeedleState.InPile;
        HeldByPlayerID = 0;
        holdPoint = null;

        SetColour(pileColour);

        if (dropCoroutine != null)
            StopCoroutine(dropCoroutine);
        dropCoroutine = StartCoroutine(
            ArcTo(
                pileRestPosition,
                () =>
                {
                    col.enabled = true;
                }
            )
        );
    }

    public void Deposit(Vector3 depositPilePosition)
    {
        if (CurrentState != NeedleState.HeldByPlayer)
            return;

        CurrentState = NeedleState.Deposited;
        HeldByPlayerID = 0;
        holdPoint = null;
        col.enabled = false;

        if (dropCoroutine != null)
            StopCoroutine(dropCoroutine);
        dropCoroutine = StartCoroutine(
            ArcTo(
                depositPilePosition,
                () =>
                {
                    gameObject.SetActive(false);
                }
            )
        );
    }

    public void Steal(Transform winnerHoldPoint, int winnerID, Color winnerColour)
    {
        if (dropCoroutine != null)
        {
            StopCoroutine(dropCoroutine);
            dropCoroutine = null;
        }

        gameObject.SetActive(true);
        CurrentState = NeedleState.HeldByPlayer;
        HeldByPlayerID = winnerID;
        holdPoint = winnerHoldPoint;
        col.enabled = false;

        transform.SetParent(null);
        SetColour(winnerColour);
    }

    public void ResetToPile(Color pileColour)
    {
        if (dropCoroutine != null)
        {
            StopCoroutine(dropCoroutine);
            dropCoroutine = null;
        }

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

    public void PrepareAsProjectile(Transform playerHoldPoint, int playerID)
    {
        if (dropCoroutine != null)
        {
            StopCoroutine(dropCoroutine);
            dropCoroutine = null;
        }

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

    private IEnumerator ArcTo(Vector3 target, System.Action onComplete)
    {
        Vector3 start = transform.position;
        Vector3 mid = (start + target) * 0.5f + Vector3.up * 0.3f;
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            float et = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            transform.position =
                Mathf.Pow(1 - et, 2) * start + 2f * (1 - et) * et * mid + Mathf.Pow(et, 2) * target;

            yield return null;
        }

        transform.position = target;
        onComplete?.Invoke();
        dropCoroutine = null;
    }

    private void SetColour(Color colour)
    {
        if (sphereRenderer != null)
            sphereRenderer.material.color = colour;
    }
}
