using KnockoutBalance;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KnockoutBalance.Editor
{
    public static class SceneGameplayInstaller
    {
        private const string PlatformName = "platform_0";
        private const string PlayerOneName = "default_7";
        private const string PlayerTwoName = "default_0";
        private const string PlayerLayerName = "Player";
        private const string GroundLayerName = "Ground";
        private const string RunningSheetPath = "Assets/Sprite/running.png";
        private const string ControllerActionsPath = "Assets/Resources/KnockoutBalanceControls.inputactions";
        private const float SpawnClearance = 0.005f;

        [MenuItem("Knockout Balance/Install Gameplay Components")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before running Install Gameplay Components. Physics setup performed during Play Mode is temporary.");
                return;
            }

            GameObject platform = Require(PlatformName);
            GameObject playerOne = Require(PlayerOneName);
            GameObject playerTwo = Require(PlayerTwoName);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int groundLayer = EnsureLayer(GroundLayerName);
            Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);

            SeesawController seesaw = ConfigurePlatform(platform, groundLayer, out BoxCollider2D platformCollider);
            PlayerController p1 = ConfigurePlayer(playerOne, true, playerLayer, platform, platformCollider);
            PlayerController p2 = ConfigurePlayer(playerTwo, false, playerLayer, platform, platformCollider);
            Physics2D.IgnoreCollision(
                playerOne.GetComponent<CapsuleCollider2D>(),
                playerTwo.GetComponent<CapsuleCollider2D>(),
                true);
            ConfigureFallBoundaries();
            Physics2D.SyncTransforms();

            GameObject managerObject = FindOrCreate("RoundManager");
            RoundManager manager = GetOrAdd<RoundManager>(managerObject);
            manager.Initialize(p1, p2, seesaw);
            ConfigureAnimations(playerOne, true);
            ConfigureAnimations(playerTwo, false);
            ConfigureDynamicCamera(playerOne, playerTwo);
            GetOrAdd<GameUI>(FindOrCreate("GameUI"));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(platform.scene);
            Selection.activeGameObject = platform;
            Debug.Log("Knockout Balance gameplay components installed on platform_0, default_7, and default_0.");
        }

        private static void ConfigureDynamicCamera(GameObject playerOne, GameObject playerTwo)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null)
                throw new System.InvalidOperationException("The scene needs a Main Camera for DynamicCamera.");

            mainCamera.orthographic = true;
            DynamicCamera dynamicCamera = GetOrAdd<DynamicCamera>(mainCamera.gameObject);

            GameObject backgroundObject = GameObject.Find("Background_Fixed");
            if (backgroundObject == null) backgroundObject = GameObject.Find("backgroundd_0");
            SpriteRenderer background = backgroundObject != null
                ? backgroundObject.GetComponent<SpriteRenderer>()
                : mainCamera.GetComponentInChildren<SpriteRenderer>(true);
            GameObject borderObject = GameObject.Find("Border_Fixed");
            if (borderObject == null) borderObject = GameObject.Find("border_0");
            SpriteRenderer border = borderObject != null ? borderObject.GetComponent<SpriteRenderer>() : null;

            dynamicCamera.Configure(playerOne.transform, playerTwo.transform, background, border);
            SerializedObject serializedCamera = new SerializedObject(dynamicCamera);
            serializedCamera.FindProperty("player1").objectReferenceValue = playerOne.transform;
            serializedCamera.FindProperty("player2").objectReferenceValue = playerTwo.transform;
            serializedCamera.FindProperty("minZoom").floatValue = 5f;
            serializedCamera.FindProperty("maxZoom").floatValue = 8f;
            serializedCamera.FindProperty("zoomLimiter").floatValue = 10f;
            serializedCamera.FindProperty("smoothTime").floatValue = 0.2f;
            serializedCamera.FindProperty("offset").vector3Value = new Vector3(0f, 1f, -10f);
            serializedCamera.FindProperty("deathYThreshold").floatValue = -5f;
            serializedCamera.FindProperty("fixedBackground").objectReferenceValue = background;
            serializedCamera.FindProperty("backgroundPadding").floatValue = 1.05f;
            serializedCamera.FindProperty("displayBorder").objectReferenceValue = border;
            serializedCamera.FindProperty("borderOverscan").floatValue = 1.04f;
            serializedCamera.FindProperty("borderVerticalScale").floatValue = 1.03f;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();

            if (background == null)
            {
                Debug.LogWarning("DynamicCamera could not find Background_Fixed or backgroundd_0; camera tracking is installed, but background coverage was not configured.");
                return;
            }

            Transform backgroundTransform = background.transform;
            float backgroundDepth = Mathf.Abs(backgroundTransform.position.z - mainCamera.transform.position.z);
            if (backgroundDepth < 1f) backgroundDepth = 100f;
            backgroundTransform.SetParent(mainCamera.transform, false);
            backgroundTransform.localPosition = new Vector3(0f, 0f, backgroundDepth);
            backgroundTransform.localRotation = Quaternion.identity;

            Vector2 spriteSize = background.sprite.bounds.size;
            float requiredHeight = 8f * 2f * 1.05f;
            float requiredWidth = requiredHeight * mainCamera.aspect;
            float backgroundScale = Mathf.Max(
                requiredWidth / Mathf.Max(0.01f, spriteSize.x),
                requiredHeight / Mathf.Max(0.01f, spriteSize.y));
            backgroundTransform.localScale = new Vector3(backgroundScale, backgroundScale, 1f);

            if (border == null)
            {
                Debug.LogWarning("DynamicCamera could not find Border_Fixed or border_0; the display border was not configured.");
                return;
            }

            Transform borderTransform = border.transform;
            borderTransform.SetParent(mainCamera.transform, false);
            borderTransform.localRotation = Quaternion.identity;
            Vector2 borderSize = border.sprite.bounds.size;
            float viewHeight = mainCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * mainCamera.aspect;
            float borderScaleX = viewWidth / Mathf.Max(0.01f, borderSize.x) * 1.04f;
            float borderScaleY = viewHeight / Mathf.Max(0.01f, borderSize.y) * 1.04f * 1.03f;
            borderTransform.localScale = new Vector3(borderScaleX, borderScaleY, 1f);
            Vector2 borderCenter = border.sprite.bounds.center;
            borderTransform.localPosition = new Vector3(
                -borderCenter.x * borderScaleX,
                -borderCenter.y * borderScaleY,
                1f);
            border.sortingOrder = 1000;
        }

        private static SeesawController ConfigurePlatform(GameObject platform, int groundLayer, out BoxCollider2D collider)
        {
            SpriteRenderer renderer = platform.GetComponent<SpriteRenderer>();
            if (renderer == null) throw new System.InvalidOperationException("platform_0 needs a SpriteRenderer.");
            SetLayerRecursively(platform, groundLayer);
            Rigidbody2D body = GetOrAdd<Rigidbody2D>(platform);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = true;
            body.rotation = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 1f;
            body.linearDamping = 0.4f;
            body.angularDamping = 1.5f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            collider = GetOrAdd<BoxCollider2D>(platform);
            // Only the top deck is solid. The wheel in the artwork must not make
            // the collision surface extend above the visible plank.
            Bounds spriteBounds = renderer.sprite.bounds;
            float deckHeight = spriteBounds.size.y * 0.16f;
            float visibleDeckTop = FindVisibleSpriteTop(renderer.sprite);
            collider.size = new Vector2(spriteBounds.size.x * 0.98f, deckHeight);
            collider.offset = new Vector2(
                spriteBounds.center.x,
                visibleDeckTop - deckHeight * 0.5f);
            collider.isTrigger = false;
            collider.usedByEffector = false;
            body.useAutoMass = true;
            collider.sharedMaterial = GetOrCreatePhysicsMaterial("PlankMaterial");
            float scaledArea = collider.size.x * collider.size.y
                * Mathf.Abs(platform.transform.lossyScale.x * platform.transform.lossyScale.y);
            collider.density = 5f / Mathf.Max(0.01f, scaledArea);
            body.centerOfMass = Vector2.zero;

            // The hinge and rigidbody centre both use the plank's local origin.
            // This prevents an empty plank from behaving like a pendulum.
            Vector2 pivotPosition = platform.transform.TransformPoint(Vector2.zero);
            GameObject pivot = FindOrCreate("SeesawPivotAnchor");
            pivot.transform.position = pivotPosition;
            RemoveAllColliders(pivot);
            GameObject pivotVisual = GameObject.Find("PivotVisual");
            if (pivotVisual != null) RemoveAllColliders(pivotVisual);
            Rigidbody2D pivotBody = GetOrAdd<Rigidbody2D>(pivot);
            pivotBody.bodyType = RigidbodyType2D.Static;
            pivotBody.simulated = true;
            pivot.layer = groundLayer;

            HingeJoint2D hinge = GetOrAdd<HingeJoint2D>(platform);
            hinge.enabled = false;
            hinge.connectedBody = pivotBody;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = Vector2.zero;
            hinge.useLimits = true;
            // HingeJoint2D angular limits do not expose a bounciness setting;
            // their hard angular stop is non-springy by default.
            hinge.limits = new JointAngleLimits2D { min = -12f, max = 12f };
            hinge.enabled = true;
            SeesawController seesaw = GetOrAdd<SeesawController>(platform);
            seesaw.ConfigureHalfLength(collider.size.x * 0.5f);
            return seesaw;
        }

        private static PlayerController ConfigurePlayer(GameObject player, bool isPlayerOne, int playerLayer, GameObject platform, BoxCollider2D platformCollider)
        {
            SpriteRenderer renderer = GetPlayerRenderer(player, out _);
            if (renderer == null) throw new System.InvalidOperationException(player.name + " needs a SpriteRenderer.");
            SetLayerRecursively(player, playerLayer);
            HingeJoint2D accidentalHinge = player.GetComponent<HingeJoint2D>();
            if (accidentalHinge != null) Object.DestroyImmediate(accidentalHinge);
            Rigidbody2D body = GetOrAdd<Rigidbody2D>(player);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = true;
            body.mass = 1f;
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = GetOrAdd<CapsuleCollider2D>(player);
            // Align the capsule's bottom with the sprite feet instead of its
            // bounding-box centre, so a physical landing looks like a landing.
            Bounds spriteBounds = renderer.sprite.bounds;
            Vector2 colliderSize = new Vector2(spriteBounds.size.x * 0.48f, spriteBounds.size.y * 0.58f);
            collider.size = colliderSize;
            collider.offset = new Vector2(spriteBounds.center.x, spriteBounds.min.y + colliderSize.y * 0.5f);
            collider.isTrigger = false;
            collider.usedByEffector = false;
            collider.sharedMaterial = GetOrCreatePhysicsMaterial("PlayerMaterial");

            // sprite.bounds is expressed relative to the sprite pivot. Therefore
            // visualPosition + spriteBounds.min.y is the drawn bottom/feet.
            // Make that value exactly equal the physical capsule bottom.
            Vector3 visualOffset = renderer.transform.localPosition;
            float colliderBottom = collider.offset.y - collider.size.y * 0.5f;
            visualOffset.y = colliderBottom - spriteBounds.min.y;
            renderer.transform.localPosition = visualOffset;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;

            PlayerInputHandler input = GetOrAdd<PlayerInputHandler>(player);
            input.Configure(isPlayerOne);
            UnityEngine.InputSystem.InputActionAsset controllerActions =
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ControllerActionsPath);
            if (controllerActions == null)
                throw new System.InvalidOperationException("Could not load controller actions at '" + ControllerActionsPath + "'.");
            SerializedObject serializedInput = new SerializedObject(input);
            serializedInput.FindProperty("controllerActions").objectReferenceValue = controllerActions;
            serializedInput.ApplyModifiedPropertiesWithoutUndo();
            PlayerController controller = GetOrAdd<PlayerController>(player);
            controller.SetFacing(isPlayerOne ? 1 : -1);
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("groundAcceleration").floatValue = 45f;
            serializedController.FindProperty("groundProbeDistance").floatValue = 0.18f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            PlayerCombat combat = GetOrAdd<PlayerCombat>(player);
            SerializedObject serializedCombat = new SerializedObject(combat);
            serializedCombat.FindProperty("punchCooldown").floatValue = 0.25f;
            serializedCombat.ApplyModifiedPropertiesWithoutUndo();
            PlacePlayerAbovePlatform(body, collider, platform.transform, platformCollider);
            controller.SetSpawn(body.position);
            return controller;
        }

        private static void PlacePlayerAbovePlatform(Rigidbody2D body, CapsuleCollider2D playerCollider, Transform platform, BoxCollider2D platformCollider)
        {
            // Find the deck surface directly from the collider, then place the
            // *bottom* of the player's capsule a tiny distance above that line.
            // This avoids starting inside the deck and prevents tunnelling on
            // the very first physics tick.
            Vector3 current = body.position;
            Vector3 platformLocal = platform.InverseTransformPoint(current);
            float deckTopLocal = platformCollider.offset.y + platformCollider.size.y * 0.5f;
            Vector3 deckPoint = platform.TransformPoint(new Vector3(platformLocal.x, deckTopLocal, 0f));
            Physics2D.SyncTransforms();
            float targetCapsuleBottom = deckPoint.y + SpawnClearance;
            current.y += targetCapsuleBottom - playerCollider.bounds.min.y;
            body.position = current;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            Physics2D.SyncTransforms();
        }

        private static void ConfigureAnimations(GameObject player, bool isPlayerOne)
        {
            EnsureFolder("Assets", "Animations");
            EnsureRunningSheetIsSliced();
            string prefix = isPlayerOne ? "Player1" : "Player2";
            string actionPrefix = isPlayerOne ? "player1" : "player2";
            string actionPath = "Assets/Sprite/" + actionPrefix + ".png";
            Sprite fallback = GetPlayerRenderer(player, out _).sprite;
            Sprite[] idleFrames = LoadSpritesByIndices("Assets/Sprite/default.png", "default", isPlayerOne ? new[] { 4, 5, 6, 7 } : new[] { 0, 1, 2, 3 }, fallback);
            Sprite[] oldRunFrames = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 0, 1, 2, 3 }, fallback);
            string runningPrefix = isPlayerOne ? "running_p1" : "running_p2";
            Sprite[] runFrames = LoadSpritesByIndices(RunningSheetPath, runningPrefix, new[] { 0, 1, 2, 3 }, fallback);
            Sprite[] jumpFrames = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 4, 5, 6, 7 }, fallback);
            Sprite[] dashFrames = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 8, 9, 10, 11 }, fallback);
            Sprite[] punchFrames = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 12, 13, 14, 15 }, fallback);
            Sprite[] punchConnectFrame = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 13 }, fallback);
            Sprite[] teeterFrames = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 17, 18, 20, 23 }, fallback);
            Sprite[] blockFrames = LoadSpritesByIndices(actionPath, actionPrefix, new[] { 25 }, fallback);
            int[] hurtIndices = isPlayerOne ? new[] { 12, 11, 8, 9 } : new[] { 5, 4, 0, 2 };
            Sprite[][] hurtFrames = new Sprite[4][];
            for (int i = 0; i < hurtFrames.Length; i++)
                hurtFrames[i] = LoadSpritesByIndices("Assets/Sprite/hurt.png", "hurt", new[] { hurtIndices[i] }, fallback);
            // The action sheets use smaller source cells than the standalone
            // idle sheet. Keep their apparent character height consistent so
            // changing state does not make the fighter suddenly shrink.
            float actionScale = AverageSpriteHeight(idleFrames) / Mathf.Max(0.01f, AverageSpriteHeight(oldRunFrames));
            float runScale = AverageSpriteHeight(idleFrames) / Mathf.Max(0.01f, AverageSpriteHeight(runFrames));

            CreateOrUpdateClip(ClipPath(prefix, "Idle"), idleFrames, true, 1f);
            CreateOrUpdateClip(ClipPath(prefix, "Run"), runFrames, true, runScale);
            CreateOrUpdateClip(ClipPath(prefix, "Jump"), jumpFrames, false, actionScale);
            CreateOrUpdateClip(ClipPath(prefix, "Dash"), dashFrames, false, actionScale);
            CreateOrUpdateClip(ClipPath(prefix, "Punch"), punchFrames, false, actionScale);
            CreateOrUpdateClip(ClipPath(prefix, "PunchConnect"), punchConnectFrame, false, actionScale);
            CreateOrUpdateClip(ClipPath(prefix, "Block"), blockFrames, true, actionScale);
            CreateOrUpdateClip(ClipPath(prefix, "Teeter"), teeterFrames, true, actionScale);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(prefix, "Hitstun")) != null)
                AssetDatabase.DeleteAsset(ClipPath(prefix, "Hitstun"));
            for (int i = 0; i < hurtFrames.Length; i++)
            {
                float hurtScale = AverageSpriteHeight(idleFrames) / Mathf.Max(0.01f, AverageSpriteHeight(hurtFrames[i]));
                CreateOrUpdateClip(ClipPath(prefix, "Hurt" + (i + 1)), hurtFrames[i], false, hurtScale);
            }

            string oldControllerPath = "Assets/Animations/" + prefix + "Controller.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(oldControllerPath) != null)
                AssetDatabase.DeleteAsset(oldControllerPath);
            string controllerPath = "Assets/Animations/" + prefix + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            controller.parameters = new[]
            {
                new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float },
                new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "Jumping", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "Dashing", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "Punch", type = AnimatorControllerParameterType.Trigger },
                new AnimatorControllerParameter { name = "PunchHit", type = AnimatorControllerParameterType.Trigger },
                new AnimatorControllerParameter { name = "Blocking", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "Teetering", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "IsStunned", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "HurtFrame", type = AnimatorControllerParameterType.Int }
            };

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.name = prefix + " State Machine";
            machine.states = System.Array.Empty<ChildAnimatorState>();
            machine.anyStateTransitions = System.Array.Empty<AnimatorStateTransition>();
            AnimatorState idle = AddState(machine, "Idle", prefix);
            AnimatorState run = AddState(machine, "Run", prefix);
            AnimatorState jump = AddState(machine, "Jump", prefix);
            AnimatorState dash = AddState(machine, "Dash", prefix);
            AnimatorState punch = AddState(machine, "Punch", prefix);
            AnimatorState punchConnect = AddState(machine, "PunchConnect", prefix);
            AnimatorState block = AddState(machine, "Block", prefix);
            AnimatorState teeter = AddState(machine, "Teeter", prefix);
            AnimatorState[] hurtStates = new AnimatorState[4];
            for (int i = 0; i < hurtStates.Length; i++)
                hurtStates[i] = AddState(machine, "Hurt" + (i + 1), prefix);
            machine.defaultState = idle;
            for (int i = 0; i < hurtStates.Length; i++)
            {
                AnimatorStateTransition hurtTransition = machine.AddAnyStateTransition(hurtStates[i]);
                hurtTransition.hasExitTime = false;
                hurtTransition.duration = 0f;
                hurtTransition.canTransitionToSelf = false;
                hurtTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsStunned");
                hurtTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "HurtFrame");
                AddHurtReturn(hurtStates[i], idle, run);
            }
            AnimatorStateTransition punchHitTransition = machine.AddAnyStateTransition(punchConnect);
            punchHitTransition.hasExitTime = false;
            punchHitTransition.duration = 0f;
            punchHitTransition.canTransitionToSelf = false;
            punchHitTransition.AddCondition(AnimatorConditionMode.If, 0f, "PunchHit");
            AddAnyTransition(machine, dash, "Dashing", AnimatorConditionMode.If, 0f);
            AddAnyTransition(machine, block, "Blocking", AnimatorConditionMode.If, 0f);
            AddAnyTransition(machine, teeter, "Teetering", AnimatorConditionMode.If, 0f);
            AddAnyTransition(machine, jump, "Jumping", AnimatorConditionMode.If, 0f);
            AnimatorStateTransition punchTransition = machine.AddAnyStateTransition(punch);
            punchTransition.hasExitTime = false;
            punchTransition.duration = 0f;
            punchTransition.canTransitionToSelf = false;
            punchTransition.AddCondition(AnimatorConditionMode.If, 0f, "Punch");

            AddTransition(idle, run, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddTransition(run, idle, "Speed", AnimatorConditionMode.Less, 0.1f);
            AddGroundedReturn(jump, idle, run, "Grounded");
            AddBooleanReturn(dash, idle, run, "Dashing");
            AddBooleanReturn(block, idle, run, "Blocking");
            AddBooleanReturn(teeter, idle, run, "Teetering");
            AddPunchReturns(punch, idle, run);
            AddPunchConnectReturns(punchConnect, idle, run);
            EditorUtility.SetDirty(controller);

            Animator animator = GetOrAdd<Animator>(player);
            animator.runtimeAnimatorController = controller;
            GetOrAdd<PlayerAnimatorDriver>(player);
        }

        private static string ClipPath(string prefix, string stateName) => "Assets/Animations/" + prefix + "_" + stateName + ".anim";

        private static AnimatorState AddState(AnimatorStateMachine machine, string stateName, string prefix)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(prefix, stateName));
            return state;
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddAnyTransition(AnimatorStateMachine machine, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddGroundedReturn(AnimatorState from, AnimatorState idle, AnimatorState run, string parameter)
        {
            AnimatorStateTransition toRun = AddInstantTransition(from, run);
            toRun.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            AnimatorStateTransition toIdle = AddInstantTransition(from, idle);
            toIdle.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.11f, "Speed");
        }

        private static void AddBooleanReturn(AnimatorState from, AnimatorState idle, AnimatorState run, string parameter)
        {
            AnimatorStateTransition toRun = AddInstantTransition(from, run);
            toRun.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            AnimatorStateTransition toIdle = AddInstantTransition(from, idle);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.11f, "Speed");
        }

        private static void AddHurtReturn(AnimatorState from, AnimatorState idle, AnimatorState run)
        {
            AnimatorStateTransition toRun = AddInstantTransition(from, run);
            toRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsStunned");
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            AnimatorStateTransition toIdle = AddInstantTransition(from, idle);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsStunned");
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.11f, "Speed");
        }

        private static void AddPunchReturns(AnimatorState punch, AnimatorState idle, AnimatorState run)
        {
            AnimatorStateTransition toRun = punch.AddTransition(run);
            toRun.hasExitTime = true;
            toRun.exitTime = 0.8f;
            toRun.duration = 0f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            AnimatorStateTransition toIdle = punch.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.8f;
            toIdle.duration = 0f;
        }

        private static void AddPunchConnectReturns(AnimatorState punchConnect, AnimatorState idle, AnimatorState run)
        {
            AnimatorStateTransition toRun = punchConnect.AddTransition(run);
            toRun.hasExitTime = true;
            toRun.exitTime = 0.15f;
            toRun.duration = 0f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            AnimatorStateTransition toIdle = punchConnect.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.15f;
            toIdle.duration = 0f;
        }

        private static AnimatorStateTransition AddInstantTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            return transition;
        }

        private static void CreateOrUpdateClip(string path, Sprite[] sprites, bool loop, float visualScale)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            const float frameRate = 12f;
            clip.frameRate = frameRate;
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[Mathf.Max(2, sprites.Length + 1)];
            for (int i = 0; i < keys.Length; i++)
            {
                int spriteIndex = i < sprites.Length ? i : (loop ? 0 : sprites.Length - 1);
                keys[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = sprites[spriteIndex] };
            }
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("Visual", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationCurve scaleCurve = AnimationCurve.Constant(0f, keys[keys.Length - 1].time, visualScale);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), "m_LocalScale.x"), scaleCurve);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), "m_LocalScale.y"), scaleCurve);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visual", typeof(Transform), "m_LocalScale.z"), AnimationCurve.Constant(0f, keys[keys.Length - 1].time, 1f));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static float AverageSpriteHeight(Sprite[] sprites)
        {
            float total = 0f;
            foreach (Sprite sprite in sprites) total += VisibleSpriteHeight(sprite);
            return sprites.Length > 0 ? total / sprites.Length : 1f;
        }

        private static float VisibleSpriteHeight(Sprite sprite)
        {
            // Sprite.bounds measures the whole grid cell, including transparent
            // padding. Tight-mesh vertices follow the actual painted pixels and
            // therefore give a stable apparent character height across sheets.
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0) return sprite.bounds.size.y;

            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            foreach (Vector2 vertex in vertices)
            {
                bottom = Mathf.Min(bottom, vertex.y);
                top = Mathf.Max(top, vertex.y);
            }
            float height = top - bottom;
            return height > 0.001f ? height : sprite.bounds.size.y;
        }

#pragma warning disable 0618 // SpriteMetaData is still the supported importer path for automated grid slicing here.
        private static void EnsureRunningSheetIsSliced()
        {
            TextureImporter importer = AssetImporter.GetAtPath(RunningSheetPath) as TextureImporter;
            if (importer == null)
                throw new System.InvalidOperationException("Could not import the running animation sheet at '" + RunningSheetPath + "'.");

            Sprite[] existing = LoadAllSprites(RunningSheetPath);
            bool alreadySliced = existing.Length == 8;
            if (alreadySliced)
            {
                foreach (Sprite sprite in existing)
                {
                    if (!sprite.name.StartsWith("running_p1_") && !sprite.name.StartsWith("running_p2_"))
                    {
                        alreadySliced = false;
                        break;
                    }
                }
            }
            if (alreadySliced) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;

            importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
            int cellWidth = textureWidth / 4;
            int cellHeight = textureHeight / 2;
            SpriteMetaData[] slices = new SpriteMetaData[8];
            for (int playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    int index = playerIndex * 4 + frame;
                    slices[index] = new SpriteMetaData
                    {
                        name = "running_p" + (playerIndex + 1) + "_" + frame,
                        rect = new Rect(frame * cellWidth, playerIndex == 0 ? cellHeight : 0, cellWidth, cellHeight),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                }
            }
            importer.spritesheet = slices;
            importer.SaveAndReimport();
        }
#pragma warning restore 0618

        private static Sprite[] LoadAllSprites(string assetPath)
        {
            System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset is Sprite sprite) sprites.Add(sprite);
            return sprites.ToArray();
        }

        private static Sprite[] LoadSpritesByIndices(string assetPath, string namePrefix, int[] indices, Sprite fallback)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            System.Collections.Generic.Dictionary<int, Sprite> available = new System.Collections.Generic.Dictionary<int, Sprite>();
            foreach (Object asset in assets)
            {
                Sprite sprite = asset as Sprite;
                string prefix = namePrefix + "_";
                if (sprite == null || !sprite.name.StartsWith(prefix)) continue;
                if (int.TryParse(sprite.name.Substring(prefix.Length), out int index)) available[index] = sprite;
            }

            System.Collections.Generic.List<Sprite> result = new System.Collections.Generic.List<Sprite>();
            foreach (int index in indices)
            {
                if (available.TryGetValue(index, out Sprite sprite)) result.Add(sprite);
                else Debug.LogWarning("Missing sliced sprite '" + namePrefix + "_" + index + "' in " + assetPath + ".");
            }
            if (result.Count == 0) result.Add(fallback);
            return result.ToArray();
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static PhysicsMaterial2D GetOrCreatePhysicsMaterial(string materialName)
        {
            EnsureFolder("Assets", "Physics");
            string path = "Assets/Physics/" + materialName + ".physicsMaterial2D";
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null)
            {
                material = new PhysicsMaterial2D(materialName);
                AssetDatabase.CreateAsset(material, path);
            }
            material.friction = 1f;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static float FindVisibleSpriteTop(Sprite sprite)
        {
            // sprite.bounds contains the full rectangular slice, including the
            // large transparent area above this platform. Tight-mesh vertices
            // follow the actual opaque artwork, so their maximum Y is the real
            // top surface of the wooden plank.
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0) return sprite.bounds.max.y;
            float top = float.NegativeInfinity;
            foreach (Vector2 vertex in vertices) top = Mathf.Max(top, vertex.y);
            return float.IsNegativeInfinity(top) ? sprite.bounds.max.y : top;
        }

        private static SpriteRenderer GetPlayerRenderer(GameObject player, out bool movedRendererToChild)
        {
            SpriteRenderer existing = player.GetComponent<SpriteRenderer>();
            if (existing == null)
            {
                movedRendererToChild = false;
                return player.GetComponentInChildren<SpriteRenderer>();
            }

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            SpriteRenderer copy = visual.AddComponent<SpriteRenderer>();
            copy.sprite = existing.sprite;
            copy.color = existing.color;
            copy.flipX = existing.flipX;
            copy.flipY = existing.flipY;
            copy.sortingLayerID = existing.sortingLayerID;
            copy.sortingOrder = existing.sortingOrder;
            copy.maskInteraction = existing.maskInteraction;
            copy.sharedMaterial = existing.sharedMaterial;
            Object.DestroyImmediate(existing);
            movedRendererToChild = true;
            return copy;
        }

        private static void ConfigureFallBoundaries()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic) return;
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            Vector2 center = camera.transform.position;
            ConfigureBoundary("FallBoundaryBottom", center + Vector2.down * (height * 0.65f), new Vector2(width * 1.5f, 1f));
            ConfigureBoundary("FallBoundaryLeft", center + Vector2.left * (width * 0.65f), new Vector2(1f, height * 1.5f));
            ConfigureBoundary("FallBoundaryRight", center + Vector2.right * (width * 0.65f), new Vector2(1f, height * 1.5f));
        }

        private static void ConfigureBoundary(string name, Vector2 position, Vector2 size)
        {
            GameObject boundary = FindOrCreate(name);
            boundary.transform.position = position;
            BoxCollider2D collider = GetOrAdd<BoxCollider2D>(boundary);
            collider.isTrigger = true;
            collider.size = size;
            GetOrAdd<FallBoundary>(boundary);
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int index = 8; index < 32; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return index;
            }
            throw new System.InvalidOperationException("No free User Layer slot is available for '" + layerName + "'.");
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void RemoveAllColliders(GameObject gameObject)
        {
            Collider2D[] colliders = gameObject.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D collider in colliders)
                Object.DestroyImmediate(collider);
        }

        private static GameObject Require(string name)
        {
            GameObject result = GameObject.Find(name);
            if (result == null) throw new System.InvalidOperationException("Could not find required scene object '" + name + "'.");
            return result;
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject result = GameObject.Find(name);
            return result != null ? result : new GameObject(name);
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
