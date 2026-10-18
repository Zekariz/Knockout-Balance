using UnityEngine;

namespace KnockoutBalance
{
    public sealed class FallBoundary : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && RoundManager.Instance != null) RoundManager.Instance.PlayerFell(player);
        }
    }
}
