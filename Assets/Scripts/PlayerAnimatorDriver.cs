using UnityEngine;

namespace KnockoutBalance
{
    [RequireComponent(typeof(Animator), typeof(PlayerController), typeof(PlayerCombat))]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int Jumping = Animator.StringToHash("Jumping");
        private static readonly int Dashing = Animator.StringToHash("Dashing");
        private static readonly int Punch = Animator.StringToHash("Punch");
        private static readonly int Blocking = Animator.StringToHash("Blocking");
        private static readonly int Teetering = Animator.StringToHash("Teetering");
        private static readonly int IsStunned = Animator.StringToHash("IsStunned");
        private static readonly int HurtFrame = Animator.StringToHash("HurtFrame");

        private Animator animator;
        private PlayerController controller;
        private PlayerCombat combat;
        private PlayerInputHandler input;
        private CapsuleCollider2D capsule;
        private SpriteRenderer visual;
        private bool wasAttacking;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            controller = GetComponent<PlayerController>();
            combat = GetComponent<PlayerCombat>();
            input = GetComponent<PlayerInputHandler>();
            capsule = GetComponent<CapsuleCollider2D>();
            visual = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            animator.SetFloat(Speed, controller.Speed);
            animator.SetBool(Grounded, controller.IsGrounded);
            animator.SetBool(Jumping, controller.IsJumping);
            animator.SetBool(Dashing, controller.IsDashing);
            animator.SetBool(Blocking, controller.IsBlocking);
            animator.SetBool(Teetering, controller.IsTeetering);
            animator.SetBool(IsStunned, controller.IsStunned);
            animator.SetInteger(HurtFrame, combat.HurtFrame);
            if (combat.IsAttacking && !wasAttacking) animator.SetTrigger(Punch);
            wasAttacking = combat.IsAttacking;
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            if (visual == null || visual.sprite == null) return;
            bool spriteFacesRight = true;
            if (input != null && !input.IsPlayerOne && visual.sprite.name.StartsWith("default_"))
                spriteFacesRight = false;
            if (visual.sprite.name.StartsWith("hurt_")) spriteFacesRight = false;
            bool wantsToFaceRight = controller.Facing > 0;
            visual.flipX = wantsToFaceRight != spriteFacesRight;
        }

        private void LateUpdate()
        {
            if (capsule == null || visual == null || visual.sprite == null) return;

            // Animation frames have slightly different rectangle heights even
            // though they use centered pivots. Correct the child position after
            // the Animator swaps sprites so the visible bottom remains exactly
            // aligned with the physical capsule bottom on every frame.
            float capsuleBottom = capsule.offset.y - capsule.size.y * 0.5f;
            float spriteBottom = visual.transform.localPosition.y
                + GetVisibleBottom(visual.sprite) * visual.transform.localScale.y;
            Vector3 position = visual.transform.localPosition;
            position.y += capsuleBottom - spriteBottom;
            visual.transform.localPosition = position;
        }

        private static float GetVisibleBottom(Sprite sprite)
        {
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0) return sprite.bounds.min.y;
            float bottom = float.PositiveInfinity;
            foreach (Vector2 vertex in vertices) bottom = Mathf.Min(bottom, vertex.y);
            return float.IsPositiveInfinity(bottom) ? sprite.bounds.min.y : bottom;
        }
    }
}
