using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WrestleGame
{
    public class WrestlingGameManager : MonoBehaviour
    {
        public static WrestlingGameManager Instance { get; private set; }

        [Header("Match Setup")]
        public GameMode gameMode = GameMode.PlayerVsCpu;
        public GameState gameState = GameState.MainMenu;
        public AIDifficulty aiDifficulty = AIDifficulty.Normal;
        public float matchDuration = 90f;
        public float matchTimer = 90f;

        [Header("Fighters")]
        public WrestlingFighter fighter1;
        public WrestlingFighter fighter2;

        [Header("Announcer Callouts")]
        public string calloutText = "";
        public float calloutTimer = 0f;
        public Color calloutColor = Color.yellow;

        // Pinfall referee sequence variables
        private WrestlingFighter activePinner;
        private WrestlingFighter activePinned;
        private Coroutine pinfallCoroutine;
        public int currentPinCount = 0;

        // UI & Menus
        private bool showControlsModal = false;
        private bool showHudHints = true;
        private float matchElapsed = 0f;
        private WrestlingFighter matchWinner;
        private string winReason = "";

        // Textures & Styles
        private Texture2D whiteTex;
        private GUIStyle customTextStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Application.targetFrameRate = 60;
            whiteTex = Texture2D.whiteTexture;

            InitializeFighters();
        }

        private void Start()
        {
            EnsureManagerSetup();
        }

        public void InitializeFighters()
        {
            if (fighter1 == null)
            {
                var go = GameObject.Find("Wrestler1");
                if (go != null) fighter1 = go.GetComponent<WrestlingFighter>();
            }
            if (fighter2 == null)
            {
                var go = GameObject.Find("Wrestler2");
                if (go != null) fighter2 = go.GetComponent<WrestlingFighter>();
            }

            if (fighter1 != null && fighter2 != null)
            {
                fighter1.fighterName = "TITAN (P1)";
                fighter2.fighterName = "VIPER (CPU)";
                fighter1.opponent = fighter2;
                fighter2.opponent = fighter1;
            }
        }

        private void EnsureManagerSetup()
        {
            if (WrestlingRing.Instance == null)
            {
                var ringGo = GameObject.Find("Ring");
                if (ringGo != null && ringGo.GetComponent<WrestlingRing>() == null)
                {
                    ringGo.AddComponent<WrestlingRing>();
                }
            }

            if (WrestlingAudio.Instance == null)
            {
                gameObject.AddComponent<WrestlingAudio>();
            }

            if (WrestlingVFX.Instance == null)
            {
                gameObject.AddComponent<WrestlingVFX>();
            }

            if (Camera.main != null && Camera.main.GetComponent<WrestlingCamera>() == null)
            {
                Camera.main.gameObject.AddComponent<WrestlingCamera>();
            }
        }

        public void ShowCallout(string text, Color col, float duration = 1.4f)
        {
            calloutText = text;
            calloutColor = col;
            calloutTimer = duration;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (calloutTimer > 0f) calloutTimer -= dt;

            // Global Shortcuts
            if (Keyboard.current != null)
            {
                // Instant Start from Main Menu with Space or Enter
                if (gameState == GameState.MainMenu)
                {
                    if (Keyboard.current.enterKey.wasPressedThisFrame ||
                        Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                        Keyboard.current.spaceKey.wasPressedThisFrame ||
                        Keyboard.current.jKey.wasPressedThisFrame)
                    {
                        StartMatch();
                        return;
                    }
                }

                // Pause with Escape key only (so 'P' is reserved purely for Pinning)
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    if (gameState == GameState.Playing)
                    {
                        gameState = GameState.Paused;
                    }
                    else if (gameState == GameState.Paused)
                    {
                        gameState = GameState.Playing;
                    }
                }

                if (Keyboard.current.hKey.wasPressedThisFrame)
                {
                    showHudHints = !showHudHints;
                }

                if (gameState == GameState.MatchOver && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                {
                    StartMatch();
                }
            }

            if (gameState == GameState.Playing)
            {
                matchElapsed += dt;
                matchTimer = Mathf.Max(0f, matchTimer - dt);

                if (matchTimer <= 0f)
                {
                    TimeOutFinish();
                }
            }
        }

        #region Match Flow & Modes

        public void StartMatch()
        {
            InitializeFighters();

            if (fighter1 == null || fighter2 == null) return;

            // Configure Fighters based on GameMode
            switch (gameMode)
            {
                case GameMode.PlayerVsCpu:
                    fighter1.fighterName = "TITAN (P1)";
                    fighter1.controlType = ControlType.Player1;
                    fighter2.fighterName = "VIPER (CPU)";
                    fighter2.controlType = ControlType.AI;
                    fighter2.aiDifficulty = aiDifficulty;
                    break;

                case GameMode.PlayerVsPlayer:
                    fighter1.fighterName = "TITAN (P1)";
                    fighter1.controlType = ControlType.Player1;
                    fighter2.fighterName = "VIPER (P2)";
                    fighter2.controlType = ControlType.Player2;
                    break;

                case GameMode.SpectatorCpuVsCpu:
                    fighter1.fighterName = "TITAN (AI)";
                    fighter1.controlType = ControlType.AI;
                    fighter1.aiDifficulty = aiDifficulty;
                    fighter2.fighterName = "VIPER (AI)";
                    fighter2.controlType = ControlType.AI;
                    fighter2.aiDifficulty = aiDifficulty;
                    break;
            }

            // Spawn Positions inside ring
            Vector3 p1Spawn = WrestlingRing.Instance != null ? WrestlingRing.Instance.GetSpawnPosition(true) : new Vector3(3.25f, 1.12f, -0.09f);
            Vector3 p2Spawn = WrestlingRing.Instance != null ? WrestlingRing.Instance.GetSpawnPosition(false) : new Vector3(5.85f, 1.12f, -0.09f);

            fighter1.ResetFighter(p1Spawn, Quaternion.LookRotation(Vector3.right, Vector3.up));
            fighter2.ResetFighter(p2Spawn, Quaternion.LookRotation(Vector3.left, Vector3.up));

            matchTimer = matchDuration;
            matchElapsed = 0f;
            currentPinCount = 0;
            matchWinner = null;
            winReason = "";
            showControlsModal = false;
            gameState = GameState.Playing;

            if (WrestlingAudio.Instance != null)
            {
                WrestlingAudio.Instance.PlayBell();
                WrestlingAudio.Instance.PlayCrowdCheer(0.8f);
            }

            ShowCallout("BELL RINGS - FIGHT!", Color.yellow, 1.8f);
        }

        public void StartPinfallSequence(WrestlingFighter pinner, WrestlingFighter pinned)
        {
            if (gameState != GameState.Playing) return;

            gameState = GameState.PinningCount;
            activePinner = pinner;
            activePinned = pinned;
            currentPinCount = 0;

            if (pinfallCoroutine != null) StopCoroutine(pinfallCoroutine);
            pinfallCoroutine = StartCoroutine(PinfallRefereeRoutine());
        }

        private IEnumerator PinfallRefereeRoutine()
        {
            ShowCallout("PIN ATTEMPT!", Color.red, 1.0f);

            yield return new WaitForSeconds(0.6f);

            for (int count = 1; count <= 3; count++)
            {
                currentPinCount = count;

                if (WrestlingAudio.Instance != null)
                {
                    WrestlingAudio.Instance.PlayRefCount(count);
                }

                if (WrestlingCamera.Instance != null)
                {
                    WrestlingCamera.Instance.TriggerShake(0.25f * count);
                }

                ShowCallout("COUNT " + count + "!", new Color(1f, 0.3f, 0.3f), 0.9f);

                yield return new WaitForSeconds(1.0f);

                if (gameState != GameState.PinningCount) yield break;
            }

            EndPinfallSequence(true, activePinner);
        }

        public void EndPinfallSequence(bool success, WrestlingFighter winner)
        {
            if (pinfallCoroutine != null)
            {
                StopCoroutine(pinfallCoroutine);
                pinfallCoroutine = null;
            }

            if (success && winner != null)
            {
                EndMatch(winner, "PINFALL (3-COUNT)");
            }
            else
            {
                currentPinCount = 0;
                gameState = GameState.Playing;
                ShowCallout("KICKOUT!", Color.green, 1.2f);
            }
        }

        public void EndMatch(WrestlingFighter winner, string reason)
        {
            if (gameState == GameState.MatchOver) return;

            if (pinfallCoroutine != null)
            {
                StopCoroutine(pinfallCoroutine);
                pinfallCoroutine = null;
            }

            gameState = GameState.MatchOver;
            matchWinner = winner;
            winReason = reason;

            WrestlingFighter loser = (winner == fighter1) ? fighter2 : fighter1;
            if (winner != null) winner.state = FighterState.Victory;
            if (loser != null) loser.state = FighterState.Defeat;

            if (WrestlingAudio.Instance != null)
            {
                WrestlingAudio.Instance.PlayBell();
                WrestlingAudio.Instance.PlayVictory();
            }

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.TriggerScreenFlash(new Color(1f, 0.85f, 0.2f), 0.75f);
            }

            string winMsg = (winner != null) ? "★ " + winner.fighterName + " WINS! ★" : "MATCH OVER";
            ShowCallout(winMsg, Color.yellow, 5.0f);
        }

        private void TimeOutFinish()
        {
            if (fighter1.stamina > fighter2.stamina)
            {
                EndMatch(fighter1, "TIME LIMIT DECISION");
            }
            else if (fighter2.stamina > fighter1.stamina)
            {
                EndMatch(fighter2, "TIME LIMIT DECISION");
            }
            else
            {
                gameState = GameState.MatchOver;
                matchWinner = null;
                winReason = "TIME LIMIT DRAW";
                if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayBell();
                ShowCallout("TIME LIMIT DRAW!", Color.yellow, 4.0f);
            }
        }

        #endregion

        #region Custom UI Drawing & Styling Engine

        private void EnsureTextStyle()
        {
            if (customTextStyle == null)
            {
                customTextStyle = new GUIStyle();
            }
        }

        private void DrawText(Rect rect, string text, Color color, int fontSize, TextAnchor anchor, bool bold = true, bool shadow = true)
        {
            EnsureTextStyle();
            customTextStyle.fontSize = fontSize;
            customTextStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            customTextStyle.alignment = anchor;
            customTextStyle.wordWrap = true;

            if (shadow)
            {
                customTextStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, customTextStyle);
            }

            customTextStyle.normal.textColor = color;
            GUI.Label(rect, text, customTextStyle);
        }

        private bool DrawCustomButton(Rect rect, string text, Color fillColor, Color borderColor, Color textColor, int fontSize = 16, bool isHighlighted = false)
        {
            Vector2 mousePos = Event.current.mousePosition;
            bool isHover = rect.Contains(mousePos);

            Color currentFill = isHover ? Color.Lerp(fillColor, Color.white, 0.22f) : fillColor;
            Color currentBorder = isHighlighted ? Color.yellow : (isHover ? Color.white : borderColor);
            Color currentText = isHover ? Color.white : textColor;

            // Draw outer border
            DrawRect(rect, currentBorder);
            // Draw inner fill
            int borderWidth = isHighlighted || isHover ? 3 : 2;
            DrawRect(new Rect(rect.x + borderWidth, rect.y + borderWidth, rect.width - (borderWidth * 2), rect.height - (borderWidth * 2)), currentFill);

            // Draw Button Label
            DrawText(rect, text, currentText, fontSize, TextAnchor.MiddleCenter, true, true);

            // Handle Click
            bool clicked = false;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                clicked = true;
                if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayUiClick();
            }

            return clicked;
        }

        private void DrawCard(Rect rect, Color fillColor, Color borderColor, int borderWidth = 3)
        {
            DrawRect(rect, borderColor);
            DrawRect(new Rect(rect.x + borderWidth, rect.y + borderWidth, rect.width - (borderWidth * 2), rect.height - (borderWidth * 2)), fillColor);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex != null ? whiteTex : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            switch (gameState)
            {
                case GameState.MainMenu:
                    DrawMainMenu();
                    break;

                case GameState.Playing:
                case GameState.PinningCount:
                    DrawInGameHUD();
                    break;

                case GameState.Paused:
                    DrawInGameHUD();
                    DrawPauseMenu();
                    break;

                case GameState.MatchOver:
                    DrawInGameHUD();
                    DrawMatchOverScreen();
                    break;
            }

            if (showControlsModal)
            {
                DrawControlsModal();
            }
        }

        private void DrawMainMenu()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // Dark vignette backdrop
            DrawRect(new Rect(0, 0, sw, sh), new Color(0.02f, 0.04f, 0.08f, 0.85f));

            // Top Title Banner
            DrawCard(new Rect(0, 0, sw, 85), new Color(0.06f, 0.09f, 0.18f, 0.98f), new Color(1f, 0.8f, 0.15f), 2);
            DrawText(new Rect(0, 8, sw, 40), "★ WRESTLE FIGHT: 3D ARENA ★", new Color(1f, 0.88f, 0.2f), 28, TextAnchor.MiddleCenter, true);
            
            float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
            Color promptColor = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.3f), pulse);
            DrawText(new Rect(0, 50, sw, 25), "PRESS [SPACE] OR [ENTER] TO START MATCH NOW!", promptColor, 15, TextAnchor.MiddleCenter, true);

            // Center Menu Card
            float cardW = Mathf.Clamp(sw * 0.75f, 420f, 560f);
            float cardH = Mathf.Clamp(sh * 0.72f, 320f, 440f);
            float cardX = (sw - cardW) * 0.5f;
            float cardY = (sh - cardH) * 0.5f + 25f;

            DrawCard(new Rect(cardX, cardY, cardW, cardH), new Color(0.05f, 0.08f, 0.15f, 0.96f), new Color(0.2f, 0.65f, 1f), 3);

            float contentY = cardY + 15;
            DrawText(new Rect(cardX, contentY, cardW, 24), "CHOOSE GAME MODE & PLAY", Color.cyan, 16, TextAnchor.MiddleCenter, true);
            contentY += 32;

            // 1. Game Mode Tabs
            float modeBtnH = 38;
            float modeSpacing = 8;
            
            bool isP1Cpu = (gameMode == GameMode.PlayerVsCpu);
            if (DrawCustomButton(
                new Rect(cardX + 25, contentY, cardW - 50, modeBtnH),
                isP1Cpu ? "✔ 1P vs CPU (Solo Match)" : "▶ 1P vs CPU (Solo Match)",
                isP1Cpu ? new Color(0.1f, 0.55f, 0.3f) : new Color(0.12f, 0.18f, 0.3f),
                isP1Cpu ? Color.green : new Color(0.3f, 0.5f, 0.8f),
                Color.white, 15, isP1Cpu))
            {
                gameMode = GameMode.PlayerVsCpu;
            }
            contentY += modeBtnH + modeSpacing;

            bool isP1P2 = (gameMode == GameMode.PlayerVsPlayer);
            if (DrawCustomButton(
                new Rect(cardX + 25, contentY, cardW - 50, modeBtnH),
                isP1P2 ? "✔ 1P vs 2P (Local 2-Player Versus)" : "⚔ 1P vs 2P (Local 2-Player Versus)",
                isP1P2 ? new Color(0.1f, 0.55f, 0.3f) : new Color(0.12f, 0.18f, 0.3f),
                isP1P2 ? Color.green : new Color(0.3f, 0.5f, 0.8f),
                Color.white, 15, isP1P2))
            {
                gameMode = GameMode.PlayerVsPlayer;
            }
            contentY += modeBtnH + modeSpacing;

            bool isSpec = (gameMode == GameMode.SpectatorCpuVsCpu);
            if (DrawCustomButton(
                new Rect(cardX + 25, contentY, cardW - 50, modeBtnH),
                isSpec ? "✔ AI vs AI (Spectator Showdown)" : "👁 AI vs AI (Spectator Showdown)",
                isSpec ? new Color(0.1f, 0.55f, 0.3f) : new Color(0.12f, 0.18f, 0.3f),
                isSpec ? Color.green : new Color(0.3f, 0.5f, 0.8f),
                Color.white, 15, isSpec))
            {
                gameMode = GameMode.SpectatorCpuVsCpu;
            }
            contentY += modeBtnH + 12;

            // 2. Difficulty & Controls Row
            float subBtnW = (cardW - 60) * 0.5f;
            Color diffColor = (aiDifficulty == AIDifficulty.Easy) ? Color.green : (aiDifficulty == AIDifficulty.Normal ? Color.yellow : Color.red);
            if (DrawCustomButton(
                new Rect(cardX + 25, contentY, subBtnW, 36),
                "AI: " + aiDifficulty.ToString().ToUpper(),
                new Color(0.15f, 0.22f, 0.35f),
                diffColor,
                diffColor,
                14))
            {
                aiDifficulty = (AIDifficulty)(((int)aiDifficulty + 1) % 4);
            }

            if (DrawCustomButton(
                new Rect(cardX + 35 + subBtnW, contentY, subBtnW, 36),
                "📖 Move List Guide",
                new Color(0.15f, 0.22f, 0.35f),
                Color.cyan,
                Color.white,
                14))
            {
                showControlsModal = true;
            }
            contentY += 46;

            // 3. Huge Start Fight Button
            if (DrawCustomButton(
                new Rect(cardX + 25, contentY, cardW - 50, 50),
                "★ START MATCH (PRESS SPACE / ENTER) ★",
                new Color(0.85f, 0.55f, 0.05f),
                new Color(1f, 0.95f, 0.3f),
                Color.white,
                17, true))
            {
                StartMatch();
            }

            // Bottom bar quick shortcut guide
            DrawCard(new Rect(0, sh - 32, sw, 32), new Color(0.04f, 0.06f, 0.12f, 0.95f), new Color(0.3f, 0.4f, 0.6f), 1);
            DrawText(new Rect(0, sh - 28, sw, 24), "CONTROLS: [WASD] Move   [J/Space] Strike   [K] Kick   [L] Body Slam   [Shift] Block   [U] Finisher   [C] Camera Mode", new Color(0.85f, 0.9f, 1f), 13, TextAnchor.MiddleCenter, false);
        }

        private void DrawInGameHUD()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // Top HUD Bar
            DrawCard(new Rect(0, 0, sw, 92), new Color(0.04f, 0.06f, 0.12f, 0.9f), new Color(0.25f, 0.35f, 0.55f), 2);

            float cardW = Mathf.Clamp(sw * 0.36f, 220f, 420f);

            // Fighter 1 (Left)
            if (fighter1 != null)
            {
                DrawText(new Rect(20, 8, cardW, 22), fighter1.fighterName, new Color(0.3f, 0.9f, 1f), 16, TextAnchor.MiddleLeft, true);
                // Stamina / Momentum Bar
                Color stmColor = (fighter1.staminaFreezeTimer > 0f) ? new Color(0.4f, 0.4f, 0.4f) : Color.Lerp(new Color(0.95f, 0.2f, 0.2f), new Color(0.15f, 0.85f, 0.35f), fighter1.stamina / 100f);
                string stmText = (fighter1.staminaFreezeTimer > 0f) ? "STUNNED (0%)" : "STAMINA " + Mathf.CeilToInt(fighter1.stamina) + "%";
                DrawProgressBar(new Rect(20, 32, cardW, 26), fighter1.stamina, fighter1.maxStamina, stmColor, stmText);
                // Special Bar
                DrawSpecialMeter(new Rect(20, 63, cardW, 16), fighter1.specialMeter);
            }

            // Match Timer (Center)
            float timerW = 100;
            DrawCard(new Rect((sw - timerW) * 0.5f, 10, timerW, 46), new Color(0.08f, 0.12f, 0.22f, 0.98f), new Color(1f, 0.8f, 0.15f), 2);
            int mins = Mathf.FloorToInt(matchTimer / 60f);
            int secs = Mathf.FloorToInt(matchTimer % 60f);
            DrawText(new Rect((sw - timerW) * 0.5f, 16, timerW, 34), string.Format("{0}:{1:00}", mins, secs), Color.white, 22, TextAnchor.MiddleCenter, true);

            // Fighter 2 (Right)
            if (fighter2 != null)
            {
                float f2X = sw - cardW - 20;
                DrawText(new Rect(f2X, 8, cardW, 22), fighter2.fighterName, new Color(1f, 0.45f, 0.45f), 16, TextAnchor.MiddleRight, true);
                // Stamina / Momentum Bar
                Color stmColor = (fighter2.staminaFreezeTimer > 0f) ? new Color(0.4f, 0.4f, 0.4f) : Color.Lerp(new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.75f, 0.15f), fighter2.stamina / 100f);
                string stmText = (fighter2.staminaFreezeTimer > 0f) ? "STUNNED (0%)" : "STAMINA " + Mathf.CeilToInt(fighter2.stamina) + "%";
                DrawProgressBar(new Rect(f2X, 32, cardW, 26), fighter2.stamina, fighter2.maxStamina, stmColor, stmText);
                // Special Bar
                DrawSpecialMeter(new Rect(f2X, 63, cardW, 16), fighter2.specialMeter);
            }

            // Bottom Hints Bar & Camera Indicator
            if (showHudHints)
            {
                DrawCard(new Rect(0, sh - 34, sw, 34), new Color(0.04f, 0.06f, 0.12f, 0.92f), new Color(0.25f, 0.35f, 0.55f), 1);
                string camName = (WrestlingCamera.Instance != null) ? WrestlingCamera.Instance.currentMode.ToString() : "Dynamic";
                string hintText = "[WASD] Move   [J/Space] Strike   [K] Kick   [L] Body Slam   [Shift] Block/Reversal   [U] Finisher   [C] Camera: " + camName + "   [Esc] Pause";
                DrawText(new Rect(0, sh - 30, sw, 26), hintText, new Color(0.9f, 0.95f, 1f), 13, TextAnchor.MiddleCenter, false);
            }

            // Center Big Callout Banner
            if (calloutTimer > 0f)
            {
                DrawText(new Rect(0, sh * 0.24f, sw, 48), calloutText, calloutColor, 34, TextAnchor.MiddleCenter, true);
            }

            // Pinfall Referee Overlay
            if (gameState == GameState.PinningCount)
            {
                DrawPinfallOverlay(sw, sh);
            }
        }

        private void DrawPinfallOverlay(float sw, float sh)
        {
            float overlayW = Mathf.Clamp(sw * 0.65f, 340f, 440f);
            float overlayH = 170;
            float ox = (sw - overlayW) * 0.5f;
            float oy = sh * 0.35f;

            DrawCard(new Rect(ox, oy, overlayW, overlayH), new Color(0.05f, 0.07f, 0.15f, 0.98f), Color.red, 3);
            DrawText(new Rect(ox, oy + 8, overlayW, 26), "★ REFEREE PIN COUNT ★", Color.yellow, 18, TextAnchor.MiddleCenter, true);

            if (currentPinCount > 0)
            {
                DrawText(new Rect(ox, oy + 32, overlayW, 65), currentPinCount.ToString(), new Color(1f, 0.2f, 0.2f), 56, TextAnchor.MiddleCenter, true);
            }

            if (activePinned != null)
            {
                float kickout = activePinned.pinKickoutProgress;
                DrawText(new Rect(ox, oy + 102, overlayW, 22), "MASH ATTACK KEYS (SPACE / J) TO KICK OUT!", Color.white, 13, TextAnchor.MiddleCenter, true);
                DrawProgressBar(new Rect(ox + 25, oy + 128, overlayW - 50, 22), kickout, 100f, Color.green, string.Format("KICKOUT {0}%", Mathf.RoundToInt(kickout)));
            }
        }

        private void DrawPauseMenu()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            DrawRect(new Rect(0, 0, sw, sh), new Color(0, 0, 0, 0.7f));

            float boxW = Mathf.Clamp(sw * 0.6f, 340f, 440f);
            float boxH = 350;
            float bx = (sw - boxW) * 0.5f;
            float by = (sh - boxH) * 0.5f;

            DrawCard(new Rect(bx, by, boxW, boxH), new Color(0.06f, 0.09f, 0.18f, 0.98f), new Color(0.2f, 0.65f, 1f), 3);
            DrawText(new Rect(bx, by + 15, boxW, 32), "MATCH PAUSED", Color.yellow, 24, TextAnchor.MiddleCenter, true);

            float btnY = by + 60;
            float btnH = 40;
            float spacing = 12;

            if (DrawCustomButton(new Rect(bx + 30, btnY, boxW - 60, btnH), "▶ Resume Match", new Color(0.12f, 0.5f, 0.25f), Color.green, Color.white, 15))
            {
                gameState = GameState.Playing;
            }
            btnY += btnH + spacing;

            if (DrawCustomButton(new Rect(bx + 30, btnY, boxW - 60, btnH), "🔄 Restart Match", new Color(0.15f, 0.3f, 0.5f), Color.cyan, Color.white, 15))
            {
                StartMatch();
            }
            btnY += btnH + spacing;

            string camName = (WrestlingCamera.Instance != null) ? WrestlingCamera.Instance.currentMode.ToString() : "Dynamic";
            if (DrawCustomButton(new Rect(bx + 30, btnY, boxW - 60, btnH), "🎥 Camera Mode: " + camName, new Color(0.15f, 0.3f, 0.5f), Color.cyan, Color.white, 15))
            {
                if (WrestlingCamera.Instance != null) WrestlingCamera.Instance.NextCameraMode();
            }
            btnY += btnH + spacing;

            if (DrawCustomButton(new Rect(bx + 30, btnY, boxW - 60, btnH), "📖 Move List Guide", new Color(0.15f, 0.3f, 0.5f), Color.cyan, Color.white, 15))
            {
                showControlsModal = true;
            }
            btnY += btnH + spacing;

            if (DrawCustomButton(new Rect(bx + 30, btnY, boxW - 60, btnH), "⏏ Main Menu", new Color(0.65f, 0.15f, 0.15f), Color.red, Color.white, 15))
            {
                gameState = GameState.MainMenu;
            }
        }

        private void DrawMatchOverScreen()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            DrawRect(new Rect(0, 0, sw, sh), new Color(0, 0, 0, 0.78f));

            float boxW = Mathf.Clamp(sw * 0.7f, 400f, 520f);
            float boxH = 430;
            float bx = (sw - boxW) * 0.5f;
            float by = (sh - boxH) * 0.5f;

            DrawCard(new Rect(bx, by, boxW, boxH), new Color(0.06f, 0.09f, 0.18f, 0.98f), new Color(1f, 0.85f, 0.2f), 3);

            string winTitle = (matchWinner != null) ? matchWinner.fighterName + " WINS!" : "TIME LIMIT DRAW";
            DrawText(new Rect(bx, by + 12, boxW, 36), winTitle, Color.yellow, 26, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(bx, by + 48, boxW, 22), "VICTORY VIA " + winReason, Color.white, 14, TextAnchor.MiddleCenter, true);

            // Stats Card
            float statY = by + 78;
            DrawCard(new Rect(bx + 20, statY, boxW - 40, 240), new Color(0.03f, 0.05f, 0.12f, 0.95f), new Color(0.2f, 0.4f, 0.7f), 2);

            statY += 8;
            DrawText(new Rect(bx + 30, statY, boxW - 60, 22), "MATCH STATISTICS", Color.cyan, 14, TextAnchor.MiddleCenter, true);
            statY += 26;

            int p1Dmg = fighter1 != null ? Mathf.RoundToInt(fighter1.totalDamageDealt) : 0;
            int p2Dmg = fighter2 != null ? Mathf.RoundToInt(fighter2.totalDamageDealt) : 0;
            int p1Strikes = fighter1 != null ? fighter1.strikesLanded : 0;
            int p2Strikes = fighter2 != null ? fighter2.strikesLanded : 0;
            int p1Slams = fighter1 != null ? fighter1.slamsExecuted : 0;
            int p2Slams = fighter2 != null ? fighter2.slamsExecuted : 0;
            int p1Rev = fighter1 != null ? fighter1.reversalsCount : 0;
            int p2Rev = fighter2 != null ? fighter2.reversalsCount : 0;

            DrawStatLine(bx + 35, statY, boxW - 70, "Total Match Time", string.Format("{0:0.0}s", matchElapsed)); statY += 24;
            DrawStatLine(bx + 35, statY, boxW - 70, "Damage Dealt (P1 / P2)", p1Dmg + "  /  " + p2Dmg); statY += 24;
            DrawStatLine(bx + 35, statY, boxW - 70, "Strikes Landed (P1 / P2)", p1Strikes + "  /  " + p2Strikes); statY += 24;
            DrawStatLine(bx + 35, statY, boxW - 70, "Body Slams (P1 / P2)", p1Slams + "  /  " + p2Slams); statY += 24;
            DrawStatLine(bx + 35, statY, boxW - 70, "Counter Reversals (P1 / P2)", p1Rev + "  /  " + p2Rev); statY += 34;

            // Buttons
            float btnW = (boxW - 60) * 0.5f;
            if (DrawCustomButton(new Rect(bx + 25, by + 340, btnW, 46), "★ REMATCH (R)", new Color(0.85f, 0.55f, 0.05f), Color.yellow, Color.white, 16, true))
            {
                StartMatch();
            }

            if (DrawCustomButton(new Rect(bx + 35 + btnW, by + 340, btnW, 46), "⏏ Main Menu", new Color(0.2f, 0.3f, 0.5f), Color.cyan, Color.white, 15))
            {
                gameState = GameState.MainMenu;
            }
        }

        private void DrawStatLine(float x, float y, float w, string label, string val)
        {
            DrawText(new Rect(x, y, w * 0.55f, 22), label, Color.white, 13, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(x + w * 0.5f, y, w * 0.5f, 22), val, Color.yellow, 13, TextAnchor.MiddleRight, true);
        }

        private void DrawControlsModal()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            DrawRect(new Rect(0, 0, sw, sh), new Color(0, 0, 0, 0.8f));

            float modalW = Mathf.Clamp(sw * 0.85f, 440f, 620f);
            float modalH = Mathf.Clamp(sh * 0.85f, 380f, 500f);
            float mx = (sw - modalW) * 0.5f;
            float my = (sh - modalH) * 0.5f;

            DrawCard(new Rect(mx, my, modalW, modalH), new Color(0.06f, 0.09f, 0.18f, 0.98f), new Color(0.2f, 0.65f, 1f), 3);
            DrawText(new Rect(mx, my + 10, modalW, 30), "WRESTLE FIGHT - MOVE LIST & CONTROLS", Color.yellow, 18, TextAnchor.MiddleCenter, true);

            float contentY = my + 45;

            // Player 1
            DrawCard(new Rect(mx + 15, contentY, modalW - 30, 155), new Color(0.03f, 0.05f, 0.12f, 0.95f), Color.cyan, 2);
            DrawText(new Rect(mx + 25, contentY + 6, modalW - 50, 20), "PLAYER 1 CONTROLS (Solo / 1P):", Color.cyan, 13, TextAnchor.MiddleLeft, true);
            DrawText(new Rect(mx + 25, contentY + 28, modalW - 50, 18), "• Move in 3D: [ W / A / S / D ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 48, modalW - 50, 18), "• Light Strike Combo (1-2-3): [ J ] or [ Space ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 68, modalW - 50, 18), "• Heavy Strike / Kick: [ K ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 88, modalW - 50, 18), "• Body Slam / Grapple: [ L ] (Hold near opponent)", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 108, modalW - 50, 18), "• Block / Counter Reversal: [ Left Shift ] or [ I ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 128, modalW - 50, 18), "• Super Finisher (100% Bar): [ U ]   •   Pin Downed Foe: [ P ]", Color.yellow, 12, TextAnchor.MiddleLeft, true);

            contentY += 165;

            // Player 2
            DrawCard(new Rect(mx + 15, contentY, modalW - 30, 125), new Color(0.03f, 0.05f, 0.12f, 0.95f), new Color(1f, 0.45f, 0.45f), 2);
            DrawText(new Rect(mx + 25, contentY + 6, modalW - 50, 20), "PLAYER 2 CONTROLS (2-Player Local):", new Color(1f, 0.45f, 0.45f), 13, TextAnchor.MiddleLeft, true);
            DrawText(new Rect(mx + 25, contentY + 28, modalW - 50, 18), "• Move in 3D: [ Arrow Keys ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 48, modalW - 50, 18), "• Strike / Kick: [ Numpad 1 / 2 ] or [ [ / ] ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 68, modalW - 50, 18), "• Body Slam: [ Numpad 3 ] or [ \\ ]", Color.white, 12, TextAnchor.MiddleLeft, false);
            DrawText(new Rect(mx + 25, contentY + 88, modalW - 50, 18), "• Block: [ Right Shift ]   •   Finisher: [ Numpad 5 ]", Color.white, 12, TextAnchor.MiddleLeft, false);

            if (DrawCustomButton(new Rect(mx + (modalW - 160) * 0.5f, my + modalH - 45, 160, 36), "CLOSE GUIDE", new Color(0.85f, 0.55f, 0.05f), Color.yellow, Color.white, 14))
            {
                showControlsModal = false;
            }
        }

        private void DrawProgressBar(Rect r, float val, float maxVal, Color fill, string text = "")
        {
            // Background
            DrawRect(r, new Color(0.03f, 0.05f, 0.12f, 0.98f));
            // Fill
            float pct = Mathf.Clamp01(val / maxVal);
            DrawRect(new Rect(r.x + 2, r.y + 2, (r.width - 4) * pct, r.height - 4), fill);

            if (!string.IsNullOrEmpty(text))
            {
                DrawText(r, text, Color.white, 11, TextAnchor.MiddleCenter, true, true);
            }
        }

        private void DrawSpecialMeter(Rect r, float specialVal)
        {
            DrawRect(r, new Color(0.03f, 0.05f, 0.12f, 0.98f));
            float pct = Mathf.Clamp01(specialVal / 100f);
            Color fill = (pct >= 1f) ? (Mathf.Sin(Time.time * 12f) > 0 ? Color.yellow : Color.magenta) : new Color(0.85f, 0.35f, 0.95f);
            DrawRect(new Rect(r.x + 1, r.y + 1, (r.width - 2) * pct, r.height - 2), fill);

            if (pct >= 1f)
            {
                DrawText(r, "★ SUPER FINISHER READY [U] ★", Color.white, 9, TextAnchor.MiddleCenter, true, true);
            }
        }

        #endregion
    }
}

