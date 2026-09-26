using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WrestleGame
{
    public class WrestlingFighter : MonoBehaviour
    {
        [Header("Identity & Control")]
        public string fighterName = "Wrestler 1";
        public ControlType controlType = ControlType.Player1;
        public AIDifficulty aiDifficulty = AIDifficulty.Normal;

        [Header("Stamina / Momentum Bar")]
        public float maxStamina = 100f;
        public float stamina = 50f;
        public float specialMeter = 0f;
        public float staminaFreezeTimer = 0f;

        [Header("Movement")]
        public float moveSpeed = 2.2f;
        public float dashSpeed = 3.6f;
        public float turnSpeed = 12f;

        [Header("Match References")]
        public WrestlingFighter opponent;
        public WrestlingRing ring;

        [Header("Current State")]
        public FighterState state = FighterState.Idle;

        // Combat Timers & Variables
        private float stateTimer = 0f;
        private float attackCooldown = 0f;
        private int comboStep = 0;
        private float comboResetTimer = 0f;
        private float blockCounterWindow = 0f;

        // Pinfall variables
        public float pinKickoutProgress = 0f;
        public int refCount = 0;

        // Procedural Animation & Smooth Physics Variables
        private Vector3 initialScale = new Vector3(0.75f, 0.75f, -0.75f);
        private float animTime = 0f;
        private Vector3 currentMoveVelocity;
        private Vector3 knockbackVelocity;
        private Vector3 reboundVelocity;
        private float reboundTime = 0f;

        // Procedural attack & hit animation offsets
        private Vector3 attackLungeOffset = Vector3.zero;
        private Vector3 attackPunchStretch = Vector3.zero;
        private Vector3 hitShakeOffset = Vector3.zero;
        private float attackTorsoTwist = 0f;

        // AI variables
        private float aiDecisionTimer = 0f;
        private Vector3 aiTargetPos;
        private float aiActionCooldown = 0f;

        // Match Statistics
        public int strikesLanded = 0;
        public int slamsExecuted = 0;
        public int reversalsCount = 0;
        public float totalDamageDealt = 0f;

        private void Awake()
        {
            initialScale = new Vector3(0.75f, 0.75f, -0.75f);
            transform.localScale = initialScale;
            stamina = 50f;
            specialMeter = 0f;
        }

        private void Start()
        {
            if (ring == null) ring = WrestlingRing.Instance;
            FindOpponentIfNull();
        }

        public void FindOpponentIfNull()
        {
            if (opponent == null)
            {
                var fighters = FindObjectsByType<WrestlingFighter>(FindObjectsSortMode.None);
                foreach (var f in fighters)
                {
                    if (f != this)
                    {
                        opponent = f;
                        break;
                    }
                }
            }
        }

        public void ResetFighter(Vector3 spawnPos, Quaternion spawnRot)
        {
            StopAllCoroutines();
            transform.position = spawnPos;
            transform.rotation = spawnRot;
            initialScale = new Vector3(0.75f, 0.75f, -0.75f);
            transform.localScale = initialScale;

            stamina = 50f;
            specialMeter = 0f;
            staminaFreezeTimer = 0f;
            state = FighterState.Idle;
            stateTimer = 0f;
            attackCooldown = 0f;
            comboStep = 0;
            reboundTime = 0f;
            pinKickoutProgress = 0f;
            refCount = 0;
            strikesLanded = 0;
            slamsExecuted = 0;
            reversalsCount = 0;
            totalDamageDealt = 0f;
            currentMoveVelocity = Vector3.zero;
            knockbackVelocity = Vector3.zero;
            reboundVelocity = Vector3.zero;
            attackLungeOffset = Vector3.zero;
            attackPunchStretch = Vector3.zero;
            hitShakeOffset = Vector3.zero;
            attackTorsoTwist = 0f;
        }

        private void Update()
        {
            if (WrestlingGameManager.Instance != null && WrestlingGameManager.Instance.gameState == GameState.MatchOver)
            {
                if (state == FighterState.Victory)
                {
                    AnimateVictory(Time.deltaTime);
                }
                else
                {
                    AnimateDefeat(Time.deltaTime);
                }
                ClampToRingMat();
                return;
            }

            if (WrestlingGameManager.Instance != null && WrestlingGameManager.Instance.gameState != GameState.Playing && WrestlingGameManager.Instance.gameState != GameState.PinningCount)
            {
                AnimateIdle(Time.deltaTime);
                ClampToRingMat();
                return;
            }

            float dt = Time.deltaTime;
            animTime += dt;

            // Cooldowns
            if (attackCooldown > 0f) attackCooldown -= dt;
            if (comboResetTimer > 0f)
            {
                comboResetTimer -= dt;
                if (comboResetTimer <= 0f) comboStep = 0;
            }
            if (blockCounterWindow > 0f) blockCounterWindow -= dt;

            // Finisher Stamina Freeze
            if (staminaFreezeTimer > 0f)
            {
                staminaFreezeTimer -= dt;
                stamina = 0f;
            }
            else
            {
                // Slow baseline passive drift towards 40% if idle/moving
                if (state == FighterState.Idle || state == FighterState.Moving)
                {
                    if (stamina < 40f)
                    {
                        stamina = Mathf.Min(40f, stamina + 4f * dt);
                    }
                }
            }

            // Smooth decay of knockback impulse
            if (knockbackVelocity.sqrMagnitude > 0.001f)
            {
                transform.position += knockbackVelocity * dt;
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, dt * 10f);
            }

            // Smooth decay of attack / hit offsets
            attackLungeOffset = Vector3.Lerp(attackLungeOffset, Vector3.zero, dt * 14f);
            attackPunchStretch = Vector3.Lerp(attackPunchStretch, Vector3.zero, dt * 16f);
            hitShakeOffset = Vector3.Lerp(hitShakeOffset, Vector3.zero, dt * 20f);
            attackTorsoTwist = Mathf.Lerp(attackTorsoTwist, 0f, dt * 14f);

            // State Machine
            switch (state)
            {
                case FighterState.Idle:
                case FighterState.Moving:
                case FighterState.Blocking:
                    HandleActiveControl(dt);
                    AnimateLocomotion(dt);
                    break;

                case FighterState.Dashing:
                    HandleDashing(dt);
                    break;

                case FighterState.RopeBouncing:
                    HandleRopeRebound(dt);
                    break;

                case FighterState.LightAttack:
                case FighterState.HeavyAttack:
                case FighterState.SpecialFinisher:
                    HandleAttacking(dt);
                    break;

                case FighterState.Grappling:
                    break;

                case FighterState.BeingGrappled:
                    break;

                case FighterState.HitStun:
                    HandleHitStun(dt);
                    break;

                case FighterState.KnockedDown:
                    HandleKnockedDown(dt);
                    break;

                case FighterState.GettingUp:
                    HandleGettingUp(dt);
                    break;

                case FighterState.Pinning:
                    HandlePinningAttacker(dt);
                    break;

                case FighterState.BeingPinned:
                    HandleBeingPinnedVictim(dt);
                    break;

                case FighterState.Victory:
                    AnimateVictory(dt);
                    break;

                case FighterState.Defeat:
                    AnimateDefeat(dt);
                    break;
            }

            ClampToRingMat();
        }

        private void ClampToRingMat()
        {
            bool lockToGround = (state != FighterState.BeingGrappled && state != FighterState.SpecialFinisher);
            if (ring != null)
            {
                transform.position = ring.ClampPosition(transform.position, 0.25f, lockToGround);
            }
        }

        #region Input & Control Handling

        private void HandleActiveControl(float dt)
        {
            Vector3 inputDir = Vector3.zero;
            bool attack1 = false;
            bool attack2 = false;
            bool grapple = false;
            bool special = false;
            bool block = false;
            bool pin = false;

            if (controlType == ControlType.Player1)
            {
                ReadPlayer1Input(out inputDir, out attack1, out attack2, out grapple, out special, out block, out pin);
            }
            else if (controlType == ControlType.Player2)
            {
                ReadPlayer2Input(out inputDir, out attack1, out attack2, out grapple, out special, out block, out pin);
            }
            else
            {
                ReadAIInput(dt, out inputDir, out attack1, out attack2, out grapple, out special, out block, out pin);
            }

            // Block State
            if (block && stamina > 5f)
            {
                if (state != FighterState.Blocking)
                {
                    blockCounterWindow = 0.28f;
                }
                state = FighterState.Blocking;
                stamina = Mathf.Max(0f, stamina - 6f * dt);
                FaceOpponent(dt * 0.5f);
                return;
            }
            else if (state == FighterState.Blocking)
            {
                state = FighterState.Idle;
            }

            // Pin Attempt (Dedicated 'P' / 'Enter' button)
            if (pin && opponent != null && opponent.state == FighterState.KnockedDown)
            {
                float dist = Vector3.Distance(transform.position, opponent.transform.position);
                if (dist < 1.6f)
                {
                    StartPin();
                    return;
                }
            }

            // Special Finisher
            if (special && specialMeter >= 100f && attackCooldown <= 0f)
            {
                if (opponent != null && Vector3.Distance(transform.position, opponent.transform.position) < 1.8f)
                {
                    ExecuteSpecialFinisher();
                    return;
                }
            }

            // Grapple / Slam
            if (grapple && attackCooldown <= 0f && stamina >= 12f)
            {
                ExecuteGrappleOrSlam();
                return;
            }

            // Heavy Strike / Kick
            if (attack2 && attackCooldown <= 0f && stamina >= 7f)
            {
                ExecuteHeavyStrike();
                return;
            }

            // Light Strike Combo
            if (attack1 && attackCooldown <= 0f && stamina >= 3f)
            {
                ExecuteLightStrike();
                return;
            }

            // 3D Movement with smooth velocity blending
            if (inputDir.sqrMagnitude > 0.01f)
            {
                state = FighterState.Moving;
                float currentSpeed = moveSpeed;

                Vector3 targetMove = inputDir.normalized * currentSpeed;
                currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, targetMove, dt * 14f);
                transform.position += currentMoveVelocity * dt;

                Quaternion targetRot = Quaternion.LookRotation(inputDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * turnSpeed);

                // Check Rope Rebound
                if (ring != null && ring.CheckRopeCollision(transform.position, inputDir, 0.35f, out Vector3 bounceDir))
                {
                    TriggerRopeRebound(bounceDir);
                }
            }
            else
            {
                state = FighterState.Idle;
                currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, Vector3.zero, dt * 12f);
                FaceOpponent(dt);
            }
        }

        private void ReadPlayer1Input(out Vector3 move, out bool a1, out bool a2, out bool grp, out bool spc, out bool blk, out bool pin)
        {
            move = Vector3.zero;
            a1 = a2 = grp = spc = blk = pin = false;
            if (Keyboard.current == null) return;

            var kb = Keyboard.current;
            if (kb.wKey.isPressed) move.z += 1;
            if (kb.sKey.isPressed) move.z -= 1;
            if (kb.aKey.isPressed) move.x -= 1;
            if (kb.dKey.isPressed) move.x += 1;

            a1 = kb.jKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            a2 = kb.kKey.wasPressedThisFrame;
            grp = kb.lKey.wasPressedThisFrame;
            spc = kb.uKey.wasPressedThisFrame;
            blk = kb.leftShiftKey.isPressed || kb.iKey.isPressed;
            pin = kb.pKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
        }

        private void ReadPlayer2Input(out Vector3 move, out bool a1, out bool a2, out bool grp, out bool spc, out bool blk, out bool pin)
        {
            move = Vector3.zero;
            a1 = a2 = grp = spc = blk = pin = false;
            if (Keyboard.current == null) return;

            var kb = Keyboard.current;
            if (kb.upArrowKey.isPressed) move.z += 1;
            if (kb.downArrowKey.isPressed) move.z -= 1;
            if (kb.leftArrowKey.isPressed) move.x -= 1;
            if (kb.rightArrowKey.isPressed) move.x += 1;

            a1 = kb.numpad1Key.wasPressedThisFrame || kb.leftBracketKey.wasPressedThisFrame;
            a2 = kb.numpad2Key.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame;
            grp = kb.numpad3Key.wasPressedThisFrame || kb.backslashKey.wasPressedThisFrame;
            spc = kb.numpad5Key.wasPressedThisFrame;
            blk = kb.rightShiftKey.isPressed || kb.numpad0Key.isPressed || kb.slashKey.isPressed;
            pin = kb.numpadEnterKey.wasPressedThisFrame || kb.semicolonKey.wasPressedThisFrame;
        }

        private void ReadAIInput(float dt, out Vector3 move, out bool a1, out bool a2, out bool grp, out bool spc, out bool blk, out bool pin)
        {
            move = Vector3.zero;
            a1 = a2 = grp = spc = blk = pin = false;

            if (opponent == null) return;

            aiDecisionTimer -= dt;
            aiActionCooldown -= dt;

            float dist = Vector3.Distance(transform.position, opponent.transform.position);
            Vector3 dirToOpponent = (opponent.transform.position - transform.position).normalized;
            dirToOpponent.y = 0;

            // Pin downed opponent
            if (opponent.state == FighterState.KnockedDown)
            {
                if (dist < 1.4f)
                {
                    pin = true;
                    return;
                }
                else
                {
                    move = dirToOpponent * 0.75f;
                    return;
                }
            }

            // React to opponent attack with Block / Counter
            if ((opponent.state == FighterState.LightAttack || opponent.state == FighterState.HeavyAttack) && dist < 1.5f)
            {
                float blockChance = aiDifficulty == AIDifficulty.Hard ? 0.45f : (aiDifficulty == AIDifficulty.Normal ? 0.25f : 0.1f);
                if (Random.value < blockChance)
                {
                    blk = true;
                    return;
                }
            }

            // AI Decision interval
            if (aiDecisionTimer <= 0f)
            {
                aiDecisionTimer = Random.Range(0.35f, 0.75f);

                if (dist > 1.8f)
                {
                    aiTargetPos = opponent.transform.position + Random.insideUnitSphere * 0.35f;
                    aiTargetPos.y = transform.position.y;
                }
                else if (dist < 0.65f)
                {
                    Vector3 circleDir = Vector3.Cross(dirToOpponent, Vector3.up);
                    aiTargetPos = transform.position - dirToOpponent * 0.45f + circleDir * (Random.value > 0.5f ? 0.6f : -0.6f);
                }
                else
                {
                    if (aiActionCooldown <= 0f)
                    {
                        aiActionCooldown = Random.Range(0.6f, 1.2f);

                        if (specialMeter >= 100f && Random.value < 0.75f)
                        {
                            spc = true;
                        }
                        else if (Random.value < 0.3f && stamina > 15f)
                        {
                            grp = true;
                        }
                        else if (Random.value < 0.35f && stamina > 10f)
                        {
                            a2 = true;
                        }
                        else if (stamina > 5f)
                        {
                            a1 = true;
                        }
                    }
                }
            }

            if (aiTargetPos != Vector3.zero)
            {
                Vector3 moveDelta = (aiTargetPos - transform.position);
                moveDelta.y = 0;
                if (moveDelta.sqrMagnitude > 0.08f)
                {
                    move = moveDelta.normalized * 0.75f;
                }
            }
        }

        private void FaceOpponent(float dt)
        {
            if (opponent == null) return;
            Vector3 dir = (opponent.transform.position - transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 8f);
            }
        }

        #endregion

        #region Combat Actions & Strike Animations

        private void ExecuteLightStrike()
        {
            state = FighterState.LightAttack;
            stateTimer = 0.22f;
            attackCooldown = 0.28f;
            stamina -= 3f;
            comboStep = (comboStep % 3) + 1;
            comboResetTimer = 0.9f;

            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayPunch();

            float punchSide = (comboStep % 2 == 1) ? 1f : -1f;
            attackLungeOffset = transform.forward * 0.32f;
            attackPunchStretch = new Vector3(-0.06f, 0.04f, 0.22f);
            attackTorsoTwist = 18f * punchSide;

            CheckStrikeHit(
                damage: 8f + (comboStep * 2f),
                range: 1.15f,
                knockback: 0.18f * comboStep,
                specialGain: 12f,
                isHeavy: comboStep == 3
            );
        }

        private void ExecuteHeavyStrike()
        {
            state = FighterState.HeavyAttack;
            stateTimer = 0.35f;
            attackCooldown = 0.42f;
            stamina -= 7f;

            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayHeavyHit();

            attackLungeOffset = transform.forward * 0.48f;
            attackPunchStretch = new Vector3(0.08f, -0.1f, 0.35f);
            attackTorsoTwist = -24f;

            CheckStrikeHit(
                damage: 18f,
                range: 1.35f,
                knockback: 0.75f,
                specialGain: 20f,
                isHeavy: true
            );
        }

        private void CheckStrikeHit(float damage, float range, float knockback, float specialGain, bool isHeavy)
        {
            if (opponent == null) return;

            float dist = Vector3.Distance(transform.position, opponent.transform.position);
            if (dist <= range)
            {
                if (opponent.state == FighterState.Blocking && opponent.blockCounterWindow > 0f)
                {
                    opponent.ExecuteReversalCounter(this);
                    return;
                }

                bool blocked = opponent.state == FighterState.Blocking;
                opponent.TakeDamage(damage, (opponent.transform.position - transform.position).normalized, knockback, isHeavy, blocked, this);

                strikesLanded++;
                totalDamageDealt += damage * (blocked ? 0.2f : 1f);
                specialMeter = Mathf.Min(100f, specialMeter + specialGain);

                if (WrestlingVFX.Instance != null)
                {
                    Vector3 hitPoint = (transform.position + opponent.transform.position) * 0.5f + Vector3.up * 0.6f;
                    Color sparkCol = isHeavy ? new Color(1f, 0.4f, 0.1f) : new Color(1f, 0.9f, 0.2f);
                    WrestlingVFX.Instance.SpawnHitSparks(hitPoint, sparkCol, isHeavy ? 18 : 10);
                }

                if (WrestlingCamera.Instance != null && isHeavy)
                {
                    WrestlingCamera.Instance.TriggerShake(0.25f);
                }
            }
        }

        private void ExecuteGrappleOrSlam()
        {
            if (opponent == null) return;
            float dist = Vector3.Distance(transform.position, opponent.transform.position);

            if (dist > 1.35f)
            {
                state = FighterState.LightAttack;
                stateTimer = 0.25f;
                attackCooldown = 0.35f;
                stamina -= 5f;
                attackLungeOffset = transform.forward * 0.25f;
                return;
            }

            stamina -= 12f;
            StartCoroutine(BodySlamRoutine());
        }

        private IEnumerator BodySlamRoutine()
        {
            state = FighterState.Grappling;
            opponent.state = FighterState.BeingGrappled;

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnCombatText("BODY SLAM!", transform.position + Vector3.up * 1.5f, Color.yellow, true, "Slam");
            }

            Vector3 startAttackerPos = transform.position;
            float matY = ring != null ? ring.matStandingY : 1.12f;

            // Phase 1: Lift victim up overhead (0.45s)
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float p = t / 0.45f;

                transform.position = new Vector3(startAttackerPos.x, matY + (Mathf.Sin(p * Mathf.PI) * 0.15f), startAttackerPos.z);
                Vector3 liftPos = transform.position + Vector3.up * Mathf.Lerp(0.5f, 1.35f, p) + transform.forward * Mathf.Lerp(0.4f, 0f, p);
                opponent.transform.position = liftPos;
                opponent.transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 90f * p);

                yield return null;
            }

            // Phase 2: Rotate and Slam onto canvas (0.35s)
            t = 0f;
            Vector3 slamTargetPos = transform.position + transform.forward * 0.85f;
            slamTargetPos.y = matY;

            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float p = t / 0.35f;
                float easeIn = p * p;

                Vector3 slamPos = Vector3.Lerp(opponent.transform.position, slamTargetPos, easeIn);
                opponent.transform.position = slamPos;
                opponent.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0);

                yield return null;
            }

            // Phase 3: Impact!
            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlaySlam();

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnSlamDust(slamTargetPos, 2.2f);
            }

            if (WrestlingCamera.Instance != null) WrestlingCamera.Instance.TriggerShake(0.45f);

            float slamDamage = 26f;
            opponent.TakeDamage(slamDamage, transform.forward, 0.4f, true, false, this);

            slamsExecuted++;
            totalDamageDealt += slamDamage;
            specialMeter = Mathf.Min(100f, specialMeter + 25f);

            yield return new WaitForSeconds(0.2f);
            if (state == FighterState.Grappling)
            {
                state = FighterState.Idle;
                attackCooldown = 0.35f;
            }
        }

        private void ExecuteSpecialFinisher()
        {
            if (opponent == null) return;
            specialMeter = 0f;
            StartCoroutine(SpecialFinisherRoutine());
        }

        private IEnumerator SpecialFinisherRoutine()
        {
            state = FighterState.SpecialFinisher;
            opponent.state = FighterState.BeingFinished;

            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayFinisher();

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.TriggerScreenFlash(new Color(1f, 0.9f, 0.3f), 0.7f);
                WrestlingVFX.Instance.SpawnCombatText("★ SUPER FINISHER! ★", transform.position + Vector3.up * 1.8f, Color.magenta, true, "Finisher");
            }

            if (WrestlingCamera.Instance != null) WrestlingCamera.Instance.TriggerShake(0.6f);

            Time.timeScale = 0.5f;

            float t = 0f;
            Vector3 mid = (transform.position + opponent.transform.position) * 0.5f;
            float matY = ring != null ? ring.matStandingY : 1.12f;

            while (t < 0.8f)
            {
                t += Time.unscaledDeltaTime;
                float p = t / 0.8f;

                transform.position = new Vector3(mid.x, matY + (Mathf.Sin(p * Mathf.PI) * 1.2f), mid.z);
                opponent.transform.position = transform.position + Vector3.up * 1.4f;
                opponent.transform.Rotate(0, 720f * Time.unscaledDeltaTime, 0);

                yield return null;
            }

            Time.timeScale = 1.0f;

            Vector3 slamPos = transform.position + transform.forward * 0.8f;
            slamPos.y = matY;
            opponent.transform.position = slamPos;
            opponent.transform.rotation = Quaternion.Euler(90f, 0, 0);

            if (WrestlingAudio.Instance != null)
            {
                WrestlingAudio.Instance.PlaySlam();
                WrestlingAudio.Instance.PlayCrowdCheer(1.0f);
            }

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnSlamDust(slamPos, 3.5f);
            }

            if (WrestlingCamera.Instance != null) WrestlingCamera.Instance.TriggerShake(0.7f);

            float finDamage = 40f;
            opponent.TakeDamage(finDamage, transform.forward, 0.5f, true, false, this);

            slamsExecuted++;
            totalDamageDealt += finDamage;

            yield return new WaitForSeconds(0.3f);
            if (state == FighterState.SpecialFinisher)
            {
                state = FighterState.Idle;
                attackCooldown = 0.4f;
            }
        }

        public void ExecuteReversalCounter(WrestlingFighter attacker)
        {
            state = FighterState.LightAttack;
            stateTimer = 0.3f;
            reversalsCount++;

            attackLungeOffset = transform.forward * 0.4f;
            attackTorsoTwist = 25f;

            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayReversal();

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnCombatText("COUNTER REVERSAL!", transform.position + Vector3.up * 1.2f, Color.cyan, true, "Reversal");
            }

            attacker.TakeDamage(14f, (attacker.transform.position - transform.position).normalized, 0.7f, true, false, this);
            specialMeter = Mathf.Min(100f, specialMeter + 30f);
        }

        #endregion

        #region Damage, Knockdown & Pinning

        public void TakeDamage(float damage, Vector3 knockbackDir, float knockbackDist, bool heavy, bool blocked, WrestlingFighter attacker)
        {
            float staminaLoss = blocked ? (damage * 0.3f) : damage;
            stamina = Mathf.Max(0f, stamina - staminaLoss);

            // Smooth knockback impulse
            knockbackVelocity = knockbackDir * (knockbackDist * (blocked ? 3f : 7f));
            hitShakeOffset = -transform.forward * (heavy ? 0.25f : 0.12f) + Vector3.up * 0.06f;

            string categoryId = "Dmg_" + fighterName;

            if (blocked)
            {
                if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayBlock();
                if (WrestlingVFX.Instance != null)
                {
                    WrestlingVFX.Instance.SpawnCombatText("BLOCKED", transform.position + Vector3.up * 1.0f, Color.gray, false, categoryId);
                }
                return;
            }

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnCombatText("-" + Mathf.RoundToInt(staminaLoss) + " STM", transform.position + Vector3.up * 0.8f, heavy ? Color.red : Color.white, heavy, categoryId);
            }

            // Heavy strike knockdown or hitstun (NO KO: game ends exclusively by pinfall)
            if (heavy && (stamina <= 0f || Random.value < 0.45f))
            {
                EnterKnockdown();
            }
            else
            {
                state = FighterState.HitStun;
                stateTimer = 0.12f;
            }
        }

        public void EnterKnockdown()
        {
            state = FighterState.KnockedDown;
            float downDuration = Mathf.Lerp(3.8f, 1.5f, stamina / maxStamina);
            stateTimer = downDuration;
            transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0);

            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayCrowdGasp();
        }

        public void StartPin()
        {
            if (opponent == null || opponent.state != FighterState.KnockedDown) return;

            state = FighterState.Pinning;
            opponent.state = FighterState.BeingPinned;
            refCount = 0;
            pinKickoutProgress = 0f;

            if (WrestlingGameManager.Instance != null)
            {
                WrestlingGameManager.Instance.StartPinfallSequence(this, opponent);
            }
        }

        private void HandlePinningAttacker(float dt)
        {
            if (opponent != null)
            {
                float matY = ring != null ? ring.matStandingY : 1.12f;
                transform.position = new Vector3(opponent.transform.position.x, matY + 0.18f, opponent.transform.position.z);
                transform.rotation = Quaternion.Euler(30f, opponent.transform.eulerAngles.y, 0);
            }
        }

        private void HandleBeingPinnedVictim(float dt)
        {
            bool mash = false;

            if (controlType == ControlType.Player1)
            {
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.jKey.wasPressedThisFrame || Keyboard.current.kKey.wasPressedThisFrame))
                {
                    mash = true;
                }
            }
            else if (controlType == ControlType.Player2)
            {
                if (Keyboard.current != null && (Keyboard.current.numpad1Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame || Keyboard.current.rightBracketKey.wasPressedThisFrame))
                {
                    mash = true;
                }
            }
            else
            {
                // AI kickout mash based on remaining stamina
                float mashChance = (stamina / maxStamina) * 0.85f + 0.1f;
                if (Random.value < mashChance * dt * 15f)
                {
                    mash = true;
                }
            }

            if (mash)
            {
                float strength = Mathf.Lerp(8f, 32f, stamina / maxStamina);
                pinKickoutProgress += strength;

                if (pinKickoutProgress >= 100f)
                {
                    KickoutSuccess();
                }
            }
        }

        public void KickoutSuccess()
        {
            if (WrestlingAudio.Instance != null)
            {
                WrestlingAudio.Instance.PlayCrowdCheer(1.0f);
                WrestlingAudio.Instance.PlayReversal();
            }

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnCombatText("KICK OUT!", transform.position + Vector3.up * 1.2f, Color.green, true, "Kickout");
            }

            if (WrestlingGameManager.Instance != null)
            {
                WrestlingGameManager.Instance.EndPinfallSequence(false, null);
            }

            state = FighterState.GettingUp;
            stateTimer = 0.4f;

            if (opponent != null)
            {
                opponent.state = FighterState.Idle;
                opponent.transform.position += -opponent.transform.forward * 0.8f;
            }
        }

        #endregion

        #region Rope Rebound & Timed States

        public void TriggerRopeRebound(Vector3 bounceDir)
        {
            state = FighterState.RopeBouncing;
            reboundVelocity = bounceDir * (ring != null ? ring.ropeBounceForce : 4.2f);
            reboundTime = 0.45f;

            if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayRopeBounce();

            if (WrestlingVFX.Instance != null)
            {
                WrestlingVFX.Instance.SpawnCombatText("ROPE REBOUND!", transform.position + Vector3.up * 1.0f, Color.yellow, true, "Rope_" + fighterName);
            }

            transform.rotation = Quaternion.LookRotation(bounceDir, Vector3.up);
        }

        private void HandleRopeRebound(float dt)
        {
            reboundTime -= dt;
            transform.position += reboundVelocity * dt;
            reboundVelocity = Vector3.Lerp(reboundVelocity, Vector3.zero, dt * 5f);

            if (opponent != null && Vector3.Distance(transform.position, opponent.transform.position) < 1.25f)
            {
                if (WrestlingAudio.Instance != null) WrestlingAudio.Instance.PlayHeavyHit();
                opponent.TakeDamage(20f, reboundVelocity.normalized, 0.9f, true, false, this);
                state = FighterState.Idle;
                attackCooldown = 0.35f;
                return;
            }

            if (reboundTime <= 0f)
            {
                state = FighterState.Idle;
            }
        }

        private void HandleDashing(float dt)
        {
            stateTimer -= dt;
            transform.position += transform.forward * dashSpeed * dt;
            if (stateTimer <= 0f) state = FighterState.Idle;
        }

        private void HandleAttacking(float dt)
        {
            stateTimer -= dt;
            transform.position += attackLungeOffset * (dt * 5f);
            transform.localScale = initialScale + attackPunchStretch;
            transform.localRotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y + attackTorsoTwist, transform.eulerAngles.z);

            if (stateTimer <= 0f)
            {
                transform.localScale = initialScale;
                state = FighterState.Idle;
            }
        }

        private void HandleHitStun(float dt)
        {
            stateTimer -= dt;
            float shudder = Mathf.Sin(Time.time * 50f) * 0.08f;
            transform.localScale = new Vector3(initialScale.x + shudder, initialScale.y - Mathf.Abs(shudder * 0.5f), initialScale.z + shudder);
            transform.position += hitShakeOffset * (dt * 10f);

            if (stateTimer <= 0f)
            {
                transform.localScale = initialScale;
                state = FighterState.Idle;
            }
        }

        private void HandleKnockedDown(float dt)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f)
            {
                state = FighterState.GettingUp;
                stateTimer = 0.4f;
            }
        }

        private void HandleGettingUp(float dt)
        {
            stateTimer -= dt;
            float progress = 1f - Mathf.Clamp01(stateTimer / 0.4f);
            transform.rotation = Quaternion.Euler(Mathf.Lerp(90f, 0f, progress), transform.eulerAngles.y, 0);
            if (stateTimer <= 0f)
            {
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                transform.localScale = initialScale;
                state = FighterState.Idle;
            }
        }

        #endregion

        #region Procedural Locomotion & Animation Engine

        private void AnimateIdle(float dt)
        {
            float breath = Mathf.Sin(animTime * 3.5f) * 0.04f;
            float idleSway = Mathf.Sin(animTime * 2.2f) * 2.5f;

            transform.localScale = new Vector3(initialScale.x * (1f + breath * 0.6f), initialScale.y * (1f - breath), initialScale.z * (1f + breath * 0.6f));
            transform.localRotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, idleSway);
        }

        private void AnimateLocomotion(float dt)
        {
            if (state == FighterState.Moving)
            {
                float stepCycle = animTime * 11f;
                float stepBob = Mathf.Abs(Mathf.Sin(stepCycle)) * 0.08f;
                float strideTilt = Mathf.Sin(stepCycle) * 6.5f;
                float forwardLean = Mathf.Clamp(currentMoveVelocity.magnitude * 2.5f, 0f, 8f);

                transform.localScale = new Vector3(
                    initialScale.x * (1f - stepBob * 0.3f),
                    initialScale.y * (1f + stepBob),
                    initialScale.z * (1f - stepBob * 0.3f)
                );

                transform.localRotation = Quaternion.Euler(forwardLean, transform.eulerAngles.y, strideTilt);
            }
            else if (state == FighterState.Blocking)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(initialScale.x * 1.08f, initialScale.y * 0.92f, initialScale.z * 1.08f), dt * 15f);
                transform.localRotation = Quaternion.Euler(10f, transform.eulerAngles.y, 0);
            }
            else if (state == FighterState.Idle)
            {
                AnimateIdle(dt);
            }
        }

        private void AnimateVictory(float dt)
        {
            float hop = Mathf.Abs(Mathf.Sin(animTime * 7f)) * 0.28f;
            float matY = ring != null ? ring.matStandingY : 1.12f;
            transform.position = new Vector3(transform.position.x, matY + hop, transform.position.z);
            transform.Rotate(0, 110f * dt, 0);
            transform.localScale = new Vector3(initialScale.x * 1.05f, initialScale.y * 1.15f, initialScale.z * 1.05f);
        }

        private void AnimateDefeat(float dt)
        {
            transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0);
            transform.localScale = initialScale;
        }

        #endregion
    }
}

