using UnityEngine;

namespace KnockoutBalance
{
    public readonly struct HitData
    {
        public readonly PlayerCombat Attacker;
        public readonly Vector2 Direction;
        public readonly float Damage;
        public readonly float BaseKnockback;
        public readonly float KnockbackScaling;
        public readonly float Hitstun;

        public HitData(PlayerCombat attacker, Vector2 direction, float damage, float baseKnockback, float knockbackScaling, float hitstun)
        {
            Attacker = attacker;
            Direction = direction;
            Damage = damage;
            BaseKnockback = baseKnockback;
            KnockbackScaling = knockbackScaling;
            Hitstun = hitstun;
        }
    }

    public interface IHitReceiver
    {
        void ReceiveHit(HitData hit);
    }
}
