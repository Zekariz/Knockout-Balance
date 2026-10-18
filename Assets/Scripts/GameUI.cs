using UnityEngine;

namespace KnockoutBalance
{
    public sealed class GameUI : MonoBehaviour
    {
        [SerializeField] private int fontSize = 30;
        private GUIStyle leftStyle;
        private GUIStyle rightStyle;
        private GUIStyle centerStyle;

        private void EnsureStyles()
        {
            if (leftStyle != null) return;
            leftStyle = NewStyle(TextAnchor.UpperLeft);
            rightStyle = NewStyle(TextAnchor.UpperRight);
            centerStyle = NewStyle(TextAnchor.UpperCenter);
        }

        private GUIStyle NewStyle(TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = alignment,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            RoundManager manager = RoundManager.Instance != null ? RoundManager.Instance : FindFirstObjectByType<RoundManager>();
            if (manager == null) return;

            float width = Screen.width;
            GUI.Label(new Rect(24f, 20f, width * 0.35f, 42f), "P1 " + Percentage(manager.PlayerOne) + "%", leftStyle);
            GUI.Label(new Rect(width * 0.65f, 20f, width * 0.35f - 24f, 42f), "P2 " + Percentage(manager.PlayerTwo) + "%", rightStyle);
            GUI.Label(new Rect(0f, 62f, width, 32f), "P1 " + manager.PlayerOneWins + "  -  " + manager.PlayerTwoWins + " P2", centerStyle);
            if (!string.IsNullOrEmpty(manager.Message))
                GUI.Label(new Rect(0f, Screen.height * 0.14f, width, 90f), manager.Message, centerStyle);
        }

        private static int Percentage(PlayerController player)
        {
            PlayerCombat combat = player != null ? player.GetComponent<PlayerCombat>() : null;
            return combat == null ? 0 : Mathf.RoundToInt(combat.DamagePercent);
        }
    }
}
