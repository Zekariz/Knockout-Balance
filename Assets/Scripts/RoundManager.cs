using System.Collections;
using UnityEngine;

namespace KnockoutBalance
{
    public sealed class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [SerializeField] private int winsRequired = 2;
        [SerializeField] private PlayerController playerOne;
        [SerializeField] private PlayerController playerTwo;
        [SerializeField] private SeesawController seesaw;
        private int playerOneWins;
        private int playerTwoWins;
        private bool roundResolving;
        private string message = "";
        private int roundNumber;
        private bool roundStarted;

        public PlayerController PlayerOne => playerOne;
        public PlayerController PlayerTwo => playerTwo;
        public int PlayerOneWins => playerOneWins;
        public int PlayerTwoWins => playerTwoWins;
        public int RoundNumber => roundNumber;
        public string Message => message;
        public bool IsRoundResolving => roundResolving;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            FindMissingReferences();
        }

        private void Start()
        {
            FindMissingReferences();
            if (!roundStarted && HasRequiredReferences())
            {
                roundStarted = true;
                roundResolving = true;
                StartCoroutine(StartRound());
            }
        }

        public void Initialize(PlayerController p1, PlayerController p2, SeesawController board)
        {
            playerOne = p1;
            playerTwo = p2;
            seesaw = board;
        }

        private bool HasRequiredReferences() => playerOne != null && playerTwo != null && seesaw != null;

        private void FindMissingReferences()
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController player in players)
            {
                PlayerInputHandler input = player.GetComponent<PlayerInputHandler>();
                if (input != null && input.IsPlayerOne) playerOne ??= player;
                else playerTwo ??= player;
            }

            if (playerOne == null || playerTwo == null)
            {
                foreach (PlayerController player in players)
                {
                    if (player == playerOne || player == playerTwo) continue;
                    if (playerOne == null) playerOne = player;
                    else if (playerTwo == null) playerTwo = player;
                }
            }
            seesaw ??= FindFirstObjectByType<SeesawController>();
        }

        public void PlayerFell(PlayerController fallen)
        {
            if (roundResolving || fallen == null || !HasRequiredReferences()) return;
            roundResolving = true;
            PlayerController winner = fallen == playerOne ? playerTwo : playerOne;
            if (winner == playerOne) playerOneWins++; else playerTwoWins++;
            StartCoroutine(ResolveRound(winner));
        }

        private IEnumerator ResolveRound(PlayerController winner)
        {
            if (!HasRequiredReferences()) yield break;
            playerOne.LockRound();
            playerTwo.LockRound();
            message = winner == playerOne ? "Player 1 wins the round!" : "Player 2 wins the round!";
            yield return new WaitForSeconds(1.5f);
            if (playerOneWins >= winsRequired || playerTwoWins >= winsRequired)
            {
                message = playerOneWins >= winsRequired ? "Player 1 wins the match! Press R to replay" : "Player 2 wins the match! Press R to replay";
                yield break;
            }
            yield return StartCoroutine(StartRound());
        }

        private IEnumerator StartRound()
        {
            FindMissingReferences();
            if (!HasRequiredReferences())
            {
                message = "Round setup is missing players or seesaw.";
                roundResolving = false;
                yield break;
            }
            roundNumber++;
            seesaw.ResetPhysics();
            playerOne.ResetForRound();
            playerTwo.ResetForRound();
            for (int countdown = 3; countdown > 0; countdown--)
            {
                message = "ROUND " + roundNumber + "\n" + countdown;
                yield return new WaitForSeconds(0.7f);
            }
            message = "Fight!";
            playerOne.UnlockRound();
            playerTwo.UnlockRound();
            roundResolving = false;
            yield return new WaitForSeconds(0.5f);
            if (!roundResolving) message = "";
        }

        private void Update()
        {
            bool restart = false;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                restart = true;
            }
            #if ENABLE_LEGACY_INPUT_MANAGER
            if (!restart)
            {
                try { restart = Input.GetKeyDown(KeyCode.R); }
                catch (System.InvalidOperationException) { }
            }
            #endif

            if ((playerOneWins >= winsRequired || playerTwoWins >= winsRequired) && restart)
            {
                playerOneWins = 0;
                playerTwoWins = 0;
                roundResolving = true;
                StartCoroutine(StartRound());
            }
        }

    }
}
