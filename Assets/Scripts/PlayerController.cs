using System.Collections;
using UnityEngine;

namespace KnockoutBalance
{
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputHandler), typeof(PlayerCombat))]
    public sealed class PlayerController : MonoBehaviour
    {
        private enum State { Idle, Walk, Run, Airborne, Attack, Block, Dash, Hitstun, Teeter, RoundLocked }

        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private float groundAcceleration = 45f;
        [SerializeField] private float jumpVelocity = 8.5f;
        [SerializeField] private float runDelay = 0.35f;
        [SerializeField] private float dashSpeed = 10f;
        [SerializeField] private float dashDuration = 0.28f;
        [SerializeField] private float dashCooldown = 0.65f;
        [SerializeField] private float teeterEdgeDistance = 0.55f;
        [SerializeField] private float groundProbeDistance = 0.18f;

        private Rigidbody2D body;
        private IPlayerInput input;
        private PlayerCombat combat;
        private CapsuleCollider2D capsule;
        private SpriteRenderer visual;
        private State state = State.Airborne;
        private SeesawController seesaw;
        private Vector2 groundNormal = Vector2.up;
        private bool grounded;
        private float lastGroundedTime = -100f;
        private float runHeldTime;
        private float lastMoveSign;
        private float dashAvailableTime;
        private float invulnerableUntil;
        private int facing = 1;
        private Vector2 spawnPoint;
        private float ignoreGroundUntil;

        public int Facing => facing;
        public bool IsGrounded => grounded;
        public bool IsJumping => !grounded && body != null && body.linearVelocity.y > 0.05f;
        public bool IsDashing => state == State.Dash;
        public bool IsTeetering => state == State.Teeter;
        public bool IsHitstunned => state == State.Hitstun || (combat != null && combat.IsStunned);
        public bool IsStunned { get; private set; }
        public float Speed => body == null ? 0f : Mathf.Abs(Vector2.Dot(body.linearVelocity, seesaw != null ? (Vector2)seesaw.transform.right : Vector2.right));
        public bool IsBlocking => state == State.Block;
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public bool IsRoundLocked => state == State.RoundLocked;
        public bool CanStartAttack => grounded && !IsRoundLocked && state != State.Dash && state != State.Hitstun && state != State.Teeter && state != State.Block;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputHandler>();
            combat = GetComponent<PlayerCombat>();
            capsule = GetComponent<CapsuleCollider2D>();
            visual = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            spawnPoint = body.position;
        }

        private void Start()
        {
            IgnoreOtherPlayerBodies();
        }

        private void IgnoreOtherPlayerBodies()
        {
            if (capsule == null) return;
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController other in players)
            {
                if (other == this) continue;
                CapsuleCollider2D otherCapsule = other.GetComponent<CapsuleCollider2D>();
                if (otherCapsule != null) Physics2D.IgnoreCollision(capsule, otherCapsule, true);
            }
        }

        private void Update()
        {
            if (IsRoundLocked) return;
            if (combat.IsStunned)
            {
                state = State.Hitstun;
                return;
            }
            if (input.JumpPressed && CanJump()) Jump();
            if (input.JumpReleased && body.linearVelocity.y > 0f) body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * 0.55f);
            if (state != State.Hitstun && state != State.Dash && state != State.Attack && state != State.Teeter)
            {
                if (grounded && input.BlockHeld) state = State.Block;
                else if (state == State.Block && !input.BlockHeld) state = State.Idle;
                if (input.PunchPressed && combat.BeginPunch()) return;
                if (input.DashPressed && grounded && Time.time >= dashAvailableTime) StartCoroutine(DashRoutine());
            }
        }

        private void FixedUpdate()
        {
            if (IsRoundLocked) return;
            RefreshGrounding();
            if (body.linearVelocity.y < -14f) body.linearVelocity = new Vector2(body.linearVelocity.x, -14f);
            if (combat.IsStunned)
            {
                state = State.Hitstun;
                Vector2 stunTangent = grounded ? GroundTangent() : Vector2.right;
                body.linearVelocity -= stunTangent * Vector2.Dot(body.linearVelocity, stunTangent);
                return;
            }
            if (!grounded) { if (state != State.Hitstun && state != State.Dash && state != State.Attack) state = State.Airborne; return; }
            if (state == State.Hitstun || state == State.Dash || state == State.Attack || state == State.Block) return;

            bool teetering = UpdateTeeter();
            float direction = input.MoveInput;

            if (Mathf.Abs(direction) > 0.01f)
            {
                if (Mathf.Sign(direction) != lastMoveSign) runHeldTime = 0f;
                runHeldTime += Time.fixedDeltaTime;
                lastMoveSign = Mathf.Sign(direction);
                if (!IsStunned) facing = (int)Mathf.Sign(direction);
            }
            else { runHeldTime = 0f; lastMoveSign = 0f; }

            float speed = runHeldTime >= runDelay ? runSpeed : walkSpeed;
            Vector2 tangent = GroundTangent();
            float currentAlong = Vector2.Dot(body.linearVelocity, tangent);
            float desiredAlong = direction * speed;
            float delta = Mathf.MoveTowards(currentAlong, desiredAlong, groundAcceleration * Time.fixedDeltaTime) - currentAlong;
            body.linearVelocity += tangent * delta;
            state = teetering ? State.Teeter : (Mathf.Abs(direction) < 0.01f ? State.Idle : (runHeldTime >= runDelay ? State.Run : State.Walk));
        }

        private bool CanJump() => Time.time - lastGroundedTime <= 0.10f && state != State.Hitstun && state != State.Attack && state != State.Dash && state != State.Block;

        private void Jump()
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
            grounded = false;
            ignoreGroundUntil = Time.time + 0.10f;
            state = State.Airborne;
        }

        private IEnumerator DashRoutine()
        {
            state = State.Dash;
            dashAvailableTime = Time.time + dashCooldown;
            invulnerableUntil = Time.time + 0.20f;
            float direction = Mathf.Abs(input.MoveInput) > 0.01f ? Mathf.Sign(input.MoveInput) : facing;
            Vector2 tangent = GroundTangent();
            body.linearVelocity = tangent * direction * dashSpeed;
            yield return new WaitForSeconds(dashDuration);
            if (!IsRoundLocked) state = grounded ? State.Idle : State.Airborne;
        }

        private bool UpdateTeeter()
        {
            if (seesaw == null) return false;
            float localX = seesaw.transform.InverseTransformPoint(body.position).x;
            bool nearEdge = Mathf.Abs(localX) >= seesaw.HalfLength - teeterEdgeDistance;
            bool steepDownhill = Mathf.Abs(seesaw.Angle) >= 14f && Mathf.Sign(localX) == Mathf.Sign(seesaw.Angle);
            return nearEdge || steepDownhill;
        }

        private void RefreshGrounding()
        {
            if (capsule == null || Time.time < ignoreGroundUntil)
            {
                grounded = false;
                return;
            }

            Vector3 scale = transform.lossyScale;
            Vector2 probeSize = new Vector2(
                capsule.size.x * Mathf.Abs(scale.x) * 0.92f,
                capsule.size.y * Mathf.Abs(scale.y) + groundProbeDistance);
            Vector2 origin = (Vector2)transform.TransformPoint(capsule.offset) + Vector2.down * (groundProbeDistance * 0.5f);
            float angle = transform.eulerAngles.z;
            int groundMask = LayerMask.GetMask("Ground");
            if (groundMask == 0) groundMask = Physics2D.AllLayers;

            Collider2D[] overlaps = Physics2D.OverlapCapsuleAll(
                origin,
                probeSize,
                capsule.direction,
                angle,
                groundMask);
            SeesawController candidate = null;
            foreach (Collider2D overlap in overlaps)
            {
                candidate = overlap.GetComponent<SeesawController>();
                if (candidate != null) break;
            }
            if (candidate == null)
            {
                grounded = false;
                return;
            }

            grounded = true;
            seesaw = candidate;
            groundNormal = candidate.transform.up;
            lastGroundedTime = Time.time;
        }

        private Vector2 GroundTangent()
        {
            Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x).normalized;
            if (tangent.sqrMagnitude < 0.01f)
                tangent = seesaw != null ? (Vector2)seesaw.transform.right : Vector2.right;
            if (tangent.x < 0f) tangent = -tangent;
            return tangent;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            SeesawController candidate = collision.collider.GetComponent<SeesawController>();
            if (candidate == null) return;
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.3f)
                {
                    grounded = true;
                    seesaw = candidate;
                    groundNormal = contact.normal;
                    lastGroundedTime = Time.time;
                    return;
                }
            }
        }

        public void BeginAttack() => state = State.Attack;
        public void EndAttack() { if (!IsRoundLocked) state = grounded ? State.Idle : State.Airborne; }
        public void BeginComboStun()
        {
            StopAllCoroutines();
            state = State.Hitstun;
            Vector2 tangent = grounded ? GroundTangent() : Vector2.right;
            body.linearVelocity -= tangent * Vector2.Dot(body.linearVelocity, tangent);
        }

        public void EndComboStun()
        {
            if (!IsRoundLocked && state == State.Hitstun)
                state = grounded ? State.Idle : State.Airborne;
        }

        public void ApplyKnockback(Vector2 force, float stun)
        {
            StopAllCoroutines();
            body.linearVelocity = force;
            if (stun > 0f) StartCoroutine(HitstunRoutine(stun));
        }

        private IEnumerator HitstunRoutine(float duration)
        {
            state = State.Hitstun;
            yield return new WaitForSeconds(duration);
            if (!IsRoundLocked) state = grounded ? State.Idle : State.Airborne;
        }

        public void SetSpawn(Vector2 position) => spawnPoint = position;
        public void SetFacing(int direction)
        {
            if (!IsStunned) facing = direction < 0 ? -1 : 1;
        }

        public void FaceAttacker(bool attackerIsToMyRight)
        {
            facing = attackerIsToMyRight ? 1 : -1;
            if (visual != null) visual.flipX = !attackerIsToMyRight;
        }

        public void SetStunned(bool value) => IsStunned = value;

        private void LateUpdate()
        {
            // PlayerAnimatorDriver may swap the sprite during Update. Reassert
            // the locked facing afterward so a cross-up cannot flip a held
            // single-frame Hurt pose.
            if (IsStunned && visual != null) visual.flipX = facing < 0;
        }

        public void ResetForRound()
        {
            StopAllCoroutines();
            body.position = spawnPoint;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            combat.ResetCombat();
            IsStunned = false;
            grounded = false;
            state = State.RoundLocked;
        }

        public void UnlockRound() => state = State.Airborne;
        public void LockRound() { StopAllCoroutines(); state = State.RoundLocked; body.linearVelocity = Vector2.zero; }
    }
}
