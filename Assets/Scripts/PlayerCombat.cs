using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnockoutBalance
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCombat : MonoBehaviour, IHitReceiver
    {
        [SerializeField] private float punchStartup = 0.12f;
        [SerializeField] private float punchActive = 0.10f;
        [SerializeField] private float punchRecovery = 0.28f;
        [SerializeField] private float damagePerPunch = 10f;
        [SerializeField] private float baseKnockback = 5.5f;
        [SerializeField] private float knockbackScaling = 0.035f;
        [SerializeField] private float punchCooldown = 0.25f;
        [SerializeField] private float comboResetDelay = 2f;
        [SerializeField] private float comboStunDuration = 1f;

        private PlayerController controller;
        private Animator animator;
        private BoxCollider2D punchHitbox;
        private readonly HashSet<PlayerCombat> hitTargets = new HashSet<PlayerCombat>();
        private Coroutine attackRoutine;
        private float punchCooldownTimer;
        private float comboResetTimer;
        private float stunTimer;
        private float hurtDisplayTimer;
        private int hitComboCount;
        private int hurtStage;

        public float DamagePercent { get; private set; }
        public bool IsAttacking => attackRoutine != null;
        public bool IsStunned => stunTimer > 0f;
        public int HitComboCount => hitComboCount;
        public int HurtFrame => hurtStage;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            animator = GetComponent<Animator>();
            GameObject hitboxObject = new GameObject("PunchHitbox");
            hitboxObject.transform.SetParent(transform);
            hitboxObject.transform.localPosition = new Vector3(0.78f, 0.12f, 0f);
            punchHitbox = hitboxObject.AddComponent<BoxCollider2D>();
            punchHitbox.isTrigger = true;
            punchHitbox.size = new Vector2(0.85f, 0.7f);
            hitboxObject.AddComponent<PunchHitbox>().Initialize(this);
            punchHitbox.enabled = false;
        }

        private void Update()
        {
            punchCooldownTimer = Mathf.Max(0f, punchCooldownTimer - Time.deltaTime);

            if (hurtDisplayTimer > 0f)
            {
                hurtDisplayTimer -= Time.deltaTime;
                if (hurtDisplayTimer <= 0f)
                {
                    controller.SetStunned(false);
                    if (hitComboCount == 0) hurtStage = 0;
                }
            }

            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                if (stunTimer <= 0f)
                {
                    stunTimer = 0f;
                    ResetCombo();
                    controller.EndComboStun();
                }
                return;
            }

            if (hitComboCount > 0)
            {
                comboResetTimer -= Time.deltaTime;
                if (comboResetTimer <= 0f) ResetCombo();
            }
        }

        public bool BeginPunch()
        {
            if (punchCooldownTimer > 0f || !controller.CanStartAttack) return false;

            // A new accepted input owns the punch immediately. Do not wait for
            // the previous coroutine or animation to finish recovering.
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            punchCooldownTimer = punchCooldown;
            hitTargets.Clear();
            punchHitbox.enabled = false;
            attackRoutine = StartCoroutine(PunchRoutine());
            if (animator != null)
            {
                animator.ResetTrigger("Punch");
                animator.ResetTrigger("PunchHit");
                // Use the state's short name. The generated controllers can
                // have a renamed root state machine, so hard-coding a
                // "Base Layer.Punch" path is not reliable.
                animator.Play("Punch", -1, 0f);
                animator.Update(0f);
            }
            return true;
        }

        private IEnumerator PunchRoutine()
        {
            // Keep startup/active/recovery proportions, but fit the complete
            // swing inside the configured cooldown so that the attackRoutine
            // guard does not silently impose a longer cooldown.
            float authoredDuration = punchStartup + punchActive + punchRecovery;
            float timingScale = punchCooldown / Mathf.Max(0.01f, authoredDuration);
            controller.BeginAttack();
            yield return new WaitForSeconds(punchStartup * timingScale);
            hitTargets.Clear();
            punchHitbox.transform.localPosition = new Vector3(controller.Facing * 0.78f, 0.12f, 0f);
            punchHitbox.enabled = true;
            yield return new WaitForSeconds(punchActive * timingScale);
            punchHitbox.enabled = false;
            yield return new WaitForSeconds(punchRecovery * timingScale);
            attackRoutine = null;
            controller.EndAttack();
        }

        public void TryHit(PlayerCombat target)
        {
            if (target == this || !IsAttacking || hitTargets.Contains(target)) return;
            hitTargets.Add(target);
            Vector2 direction = new Vector2(controller.Facing, 0.22f).normalized;
            target.ReceiveHit(new HitData(this, direction, damagePerPunch, baseKnockback, knockbackScaling, 0.18f));
        }

        public void ReceiveHit(HitData hit)
        {
            if (controller.IsRoundLocked || controller.IsInvulnerable) return;
            float magnitude = hit.BaseKnockback + DamagePercent * hit.KnockbackScaling;
            // Only the combo opener can be blocked. Once hit 1 connects, hits
            // 2-4 continue the sequence even if Block is still being held.
            if (hitComboCount == 0 && controller.IsBlocking && IsAttackerInFront(hit.Attacker))
            {
                return;
            }

            if (hit.Attacker != null) hit.Attacker.PlayPunchConnect();
            DamagePercent += hit.Damage;
            if (hit.Attacker != null)
            {
                bool attackerIsToMyRight = hit.Attacker.transform.position.x > transform.position.x;
                controller.FaceAttacker(attackerIsToMyRight);
            }
            hitComboCount++;
            comboResetTimer = comboResetDelay;
            hurtStage = hitComboCount;
            controller.SetStunned(true);

            switch (hitComboCount)
            {
                case 1:
                    // Visual reaction only: movement and blocking remain available.
                    hurtDisplayTimer = comboStunDuration;
                    break;
                case 2:
                    BeginOrRefreshComboStun();
                    break;
                case 3:
                    BeginOrRefreshComboStun();
                    break;
                default:
                    // Preserve the fourth hurt pose briefly while immediately
                    // ending stun and applying the only knockback in the combo.
                    hitComboCount = 0;
                    comboResetTimer = 0f;
                    stunTimer = 0f;
                    hurtDisplayTimer = punchCooldown;
                    controller.EndComboStun();
                    controller.ApplyKnockback(hit.Direction * magnitude, 0f);
                    break;
            }
        }

        private void PlayPunchConnect()
        {
            if (animator != null) animator.SetTrigger("PunchHit");
        }

        private void BeginOrRefreshComboStun()
        {
            hurtDisplayTimer = 0f;
            stunTimer = comboStunDuration;
            CancelPunch();
            controller.BeginComboStun();
        }

        private void CancelPunch()
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            if (punchHitbox != null) punchHitbox.enabled = false;
            controller.EndAttack();
        }

        private void ResetCombo()
        {
            hitComboCount = 0;
            comboResetTimer = 0f;
            hurtStage = 0;
            hurtDisplayTimer = 0f;
            controller.SetStunned(false);
        }

        private bool IsAttackerInFront(PlayerCombat attacker)
        {
            if (attacker == null) return false;
            float horizontalOffset = attacker.transform.position.x - transform.position.x;
            return Mathf.Abs(horizontalOffset) > 0.01f && Mathf.Sign(horizontalOffset) == controller.Facing;
        }

        public void ResetCombat()
        {
            StopAllCoroutines();
            attackRoutine = null;
            punchHitbox.enabled = false;
            punchCooldownTimer = 0f;
            stunTimer = 0f;
            ResetCombo();
            DamagePercent = 0f;
        }
    }

    public sealed class PunchHitbox : MonoBehaviour
    {
        private PlayerCombat owner;
        public void Initialize(PlayerCombat combat) => owner = combat;

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerCombat target = other.GetComponent<PlayerCombat>();
            if (target != null) owner.TryHit(target);
        }
    }
}
