using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(CharacterController))]
public class MultiplayerPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 6f;

    [SerializeField]
    private float jumpForce = 5f;

    [SerializeField]
    private float gravity = -18f;

    [Header("Ground Check")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckRadius = 0.2f;

    [SerializeField]
    private LayerMask groundLayer;

    [Header("Character Setup")]
    [SerializeField]
    private Transform characterVisualSlot;

    [SerializeField]
    private GameObject p1CharacterPrefab;

    [SerializeField]
    private GameObject p2CharacterPrefab;

    [Header("Spawn Points")]
    [SerializeField]
    private Transform p1SpawnPoint;

    [SerializeField]
    private Transform p2SpawnPoint;

    [Header("Knockback")]
    [SerializeField]
    private float knockbackDecay = 10f;

    [Header("Animations")]
    private Animator animator;

    //  private
    private CharacterController cc;
    private PlayerInput playerInput;
    private Vector2 moveInput;
    private float verticalVelocity;
    private bool isGrounded;
    private bool jumpQueued;
    private Vector3 knockbackVelocity;

    private PlayerHealth playerHealth;
    private KnockdownManager knockdownManager;

    //  ids
    public int PlayerID { get; private set; }
    public AnimationManager animationScript;

    //  events
    public event Action OnLightAttackEvent;
    public event Action OnHeavyAttackEvent;
    public event Action OnReactionEvent;
    public event Action OnGetUpEvent;
    public event Action OnCollectPressed;

    private bool movementEnabled = false;
    public bool BlockHeld { get; private set; }

    private float facingDirection = 1f;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerHealth = GetComponent<PlayerHealth>();

        knockdownManager = FindFirstObjectByType<KnockdownManager>();

        // IMPORTANT: must be Invoke C# Events for OnCollect (and all other
        // On___ methods) to be called automatically by PlayerInput.
        // If this is wrong, change it in the PlayerInput component Inspector
        // under "Behavior" → "Invoke C# Events".
    }

    private void OnEnable()
    {
        if (playerInput != null)
            PlayerID = playerInput.playerIndex + 1;
    }

    private void Start()
    {
        MoveToSpawnPoint();
        SpawnCharacterVisual();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        CheckGround();
        ApplyGravity();

        if (movementEnabled)
        {
            HandleJump();
            MoveCharacter();
        }
        else if (knockbackVelocity != Vector3.zero)
        {
            cc.Move((Vector3.up * verticalVelocity + knockbackVelocity) * Time.deltaTime);
        }

        ApplyKnockbackDecay();

        //  animations
        if (knockdownManager != null && knockdownManager.CurrentState != KnockdownState.None)
            return;

        CombatSystem combat = GetComponent<CombatSystem>();
        if (combat != null && combat.IsAttacking)
            return;
        if (playerHealth != null && playerHealth.IsReacting)
            return;

        if (!isGrounded)
            animationScript.PlayJump();
        else if (Mathf.Abs(moveInput.x) > 0.1f)
            animationScript.PlayRun();
        else
            animationScript.PlayIdle();
    }

    // Spawn

    public void MoveToSpawnPoint()
    {
        facingDirection = PlayerID == 1 ? 1f : -1f;
        Transform sp = PlayerID == 1 ? p1SpawnPoint : p2SpawnPoint;
        if (sp == null)
            return;

        cc.enabled = false;
        transform.position = sp.position;
        transform.rotation = sp.rotation;
        cc.enabled = true;
    }

    private void SpawnCharacterVisual()
    {
        GameObject prefab = PlayerID == 1 ? p1CharacterPrefab : p2CharacterPrefab;
        if (prefab == null || characterVisualSlot == null)
            return;

        GameObject go = Instantiate(
            prefab,
            characterVisualSlot.position,
            characterVisualSlot.rotation,
            characterVisualSlot
        );
        animationScript = go.GetComponent<AnimationManager>();

        if (PlayerID == 2)
        {
            Vector3 s = characterVisualSlot.localScale;
            characterVisualSlot.localScale = new Vector3(-Mathf.Abs(s.x), s.y, s.z);
        }
    }

    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started && isGrounded)
            jumpQueued = true;
    }

    public void OnLightAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            OnLightAttackEvent?.Invoke();
    }

    public void OnHeavyAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            OnHeavyAttackEvent?.Invoke();
    }

    public void OnBlock(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            BlockHeld = true;
        if (ctx.canceled)
            BlockHeld = false;
    }

    public void OnReactionTrigger(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            OnReactionEvent?.Invoke();
    }

    public void OnGetUp(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            OnGetUpEvent?.Invoke();
    }

    public void OnCollect(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
            OnCollectPressed?.Invoke();
    }

    // Movement

    private void CheckGround()
    {
        Vector3 pos =
            groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.9f;

        isGrounded = Physics.CheckSphere(pos, groundCheckRadius, groundLayer);
    }

    private void ApplyGravity()
    {
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;
    }

    private void HandleJump()
    {
        if (!jumpQueued)
            return;
        verticalVelocity = jumpForce;
        jumpQueued = false;
    }

    private void MoveCharacter()
    {
        Vector3 horizontal = new Vector3(moveInput.x, 0f, 0f);
        if (horizontal.magnitude > 1f)
            horizontal.Normalize();

        Vector3 finalMove =
            horizontal * moveSpeed + Vector3.up * verticalVelocity + knockbackVelocity;
        cc.Move(finalMove * Time.deltaTime);

        // Face the direction of movement — DOESNT FLIPPIN WORK
        if (horizontal.x != 0f && characterVisualSlot != null)
        {
            characterVisualSlot.localScale = new Vector3(
                Mathf.Sign(horizontal.x) * Mathf.Abs(characterVisualSlot.localScale.x),
                characterVisualSlot.localScale.y,
                characterVisualSlot.localScale.z
            );
        }
    }

    // Knockback

    public void ApplyKnockback(Vector3 force) => knockbackVelocity += force;

    private void ApplyKnockbackDecay()
    {
        if (knockbackVelocity == Vector3.zero)
            return;
        knockbackVelocity = Vector3.MoveTowards(
            knockbackVelocity,
            Vector3.zero,
            knockbackDecay * Time.deltaTime
        );
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            moveInput = Vector2.zero;
            verticalVelocity = 0f;
            jumpQueued = false;
        }
    }

    //  helpers

    public Transform GetVisualSlot() => characterVisualSlot;

    public Vector3 GetMoveDirection() => new Vector3(moveInput.x, 0f, 0f).normalized;

    public bool IsGrounded => isGrounded;

    // gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 pos =
            groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.9f;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
}
