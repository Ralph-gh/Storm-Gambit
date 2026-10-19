using System.Collections;
using UnityEngine;

public class KingSlaySequenceController : MonoBehaviour
{
    public static KingSlaySequenceController Instance { get; private set; }

    [Header("Art")]
    [SerializeField] private GameObject swordPrefab;

    [Header("Fast Debug")]
    [SerializeField] private bool debugPreviewOnly = true;
    [SerializeField] private KeyCode debugReplayKey = KeyCode.K;

    [Header("Timing")]
    [SerializeField] private float approachDuration = 1.2f;
    [SerializeField] private float swordRevealDelay = 0.25f;
    [SerializeField] private float swingDuration = 0.45f;
    [SerializeField] private float impactDelay = 0.15f;

    [Header("Approach")]
    [SerializeField] private float stopDistance = 0.35f;

    [Header("Sword Placement")]
    [SerializeField]
    private Vector3 swordOffset =
        new Vector3(0f, -0.18f, 0f);

    // Allows us to correct the FBX orientation without changing code.
    [SerializeField]
    private float swordRotationOffset = 0f;

    [Header("Sword Swing")]
    [SerializeField]
    private float swingAngle = 100f;

    [SerializeField]
    private float impactDistance = 0.12f;

    [Header("Debug Test")]
    [SerializeField] private ChessPiece debugAttacker;
    [SerializeField] private ChessPiece debugKing;

    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Update()
    {
#if UNITY_EDITOR

        if (!Application.isPlaying)
            return;

        if (Input.GetKeyDown(debugReplayKey))
        {
            if (isPlaying)
                return;

            if (debugAttacker == null || debugKing == null)
            {
                Debug.LogWarning(
                    "[KING SLAY TEST] Assign Debug Attacker and Debug King."
                );

                return;
            }

            Play(
                debugAttacker,
                debugKing,
                debugKing.currentCell,
                null
            );
        }

#endif
    }
    // =========================================================
    // PUBLIC ENTRY POINT
    // =========================================================

    public void Play(
        ChessPiece attacker,
        ChessPiece king,
        Vector2Int targetSquare,
        PieceType? promotion)
    {
        if (isPlaying)
        {
            Debug.LogWarning(
                "[KING SLAY] Sequence already playing."
            );

            return;
        }

        if (attacker == null || king == null)
        {
            Debug.LogWarning(
                "[KING SLAY] Missing attacker or king."
            );

            return;
        }

        StartCoroutine(
            PlayRoutine(
                attacker,
                king,
                targetSquare,
                promotion
            )
        );
    }

    // =========================================================
    // CINEMATIC
    // =========================================================

    private IEnumerator PlayRoutine(
        ChessPiece attacker,
        ChessPiece king,
        Vector2Int targetSquare,
        PieceType? promotion)
    {
        isPlaying = true;

        GameObject sword = null;
        SpriteRenderer kingRenderer = null;

        Vector3 attackerOriginalPosition =
            attacker.transform.position;

        // =====================================================
        // 1. CALCULATE APPROACH POSITION
        // =====================================================

        Vector3 kingPosition =
            king.transform.position;

        Vector3 direction =
            kingPosition - attackerOriginalPosition;

        direction.z = 0f;

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        Vector3 approachPosition =
            kingPosition - direction * stopDistance;

        approachPosition.z =
            attackerOriginalPosition.z;

        // =====================================================
        // 2. SLOWLY APPROACH KING
        // =====================================================

        float timer = 0f;

        while (timer < approachDuration)
        {
            if (attacker == null || king == null)
            {
                isPlaying = false;
                yield break;
            }

            timer += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    timer / approachDuration
                );

            t = Mathf.SmoothStep(
                0f,
                1f,
                t
            );

            attacker.transform.position =
                Vector3.Lerp(
                    attackerOriginalPosition,
                    approachPosition,
                    t
                );

            yield return null;
        }

        attacker.transform.position =
            approachPosition;

        // =====================================================
        // 3. CREATE SWORD AT ATTACKER
        // =====================================================

        if (swordPrefab != null)
        {
            sword = Instantiate(
                swordPrefab,
                attacker.transform
            );

            sword.transform.localPosition =
                swordOffset;

            sword.transform.localRotation =
                Quaternion.identity;
        }
        else
        {
            Debug.LogWarning(
                "[KING SLAY] Sword prefab not assigned."
            );
        }

        // =====================================================
        // 4. POINT SWORD TOWARD KING
        // =====================================================

        if (sword != null && king != null)
        {
            FaceSwordTowardKing(
                sword.transform,
                king.transform.position
            );
        }

        yield return new WaitForSeconds(
            swordRevealDelay
        );

        // =====================================================
        // 5. FIND SWORD TIP
        // =====================================================

        Transform swordTip = null;

        if (sword != null)
        {
            swordTip =
                sword.transform.Find("SwordTip");

            if (swordTip == null)
            {
                Debug.LogWarning(
                    "[KING SLAY] SwordTip child was not found."
                );
            }
        }

        // =====================================================
        // 6. SWING
        // =====================================================

        bool impactTriggered = false;

        if (sword != null)
        {
            Quaternion startRotation =
                sword.transform.rotation;

            Quaternion endRotation =
                startRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    swingAngle
                );

            timer = 0f;

            while (timer < swingDuration)
            {
                if (attacker == null || king == null)
                {
                    if (sword != null)
                        Destroy(sword);

                    isPlaying = false;
                    yield break;
                }

                timer += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        timer / swingDuration
                    );

                t = Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

                sword.transform.rotation =
                    Quaternion.Slerp(
                        startRotation,
                        endRotation,
                        t
                    );

                // =============================================
                // TIP REACHED KING?
                // =============================================

                if (!impactTriggered &&
                    swordTip != null)
                {
                    Vector3 tipPosition =
                        swordTip.position;

                    Vector3 targetPosition =
                        king.transform.position;

                    // Ignore Z for our 2D board.
                    tipPosition.z = 0f;
                    targetPosition.z = 0f;

                    float distance =
                        Vector3.Distance(
                            tipPosition,
                            targetPosition
                        );

                    if (distance <= impactDistance)
                    {
                        TriggerKingImpact(
                            king,
                            ref kingRenderer,
                            ref impactTriggered
                        );
                    }
                }

                yield return null;
            }
        }

        // =====================================================
        // 7. SAFETY IMPACT
        //
        // If the sword tip misses because the prefab is not
        // tuned yet, still complete the cinematic.
        // =====================================================

        if (!impactTriggered && king != null)
        {
            TriggerKingImpact(
                king,
                ref kingRenderer,
                ref impactTriggered
            );
        }

        yield return new WaitForSeconds(
            impactDelay

        );
        // =====================================================
        // DEBUG PREVIEW - DO NOT ACTUALLY CAPTURE KING
        // =====================================================

        if (debugPreviewOnly)
        {
            yield return new WaitForSeconds(0.35f);

            if (attacker != null) attacker.transform.position = attackerOriginalPosition;
            if (kingRenderer != null) kingRenderer.enabled = true;
            if (sword != null) Destroy(sword);

            isPlaying = false;

            Debug.Log("[KING SLAY TEST] Preview finished. Press K to replay.");

            yield break;
        }
        // =====================================================
        // 8. RESTORE ATTACKER BEFORE REAL BOARD MOVE
        // =====================================================

        if (attacker != null)
        {
            attacker.transform.position =
                attackerOriginalPosition;
        }

        // =====================================================
        // 9. REAL KING CAPTURE
        // =====================================================

        bool accepted = false;

        if (attacker != null)
        {
            accepted =
                attacker.TryExecuteAIMove(
                    targetSquare,
                    promotion
                );
        }

        if (!accepted)
        {
            Debug.LogWarning(
                "[KING SLAY] Final capture was rejected."
            );

            // If the move failed, restore the king visually.
            if (kingRenderer != null)
            {
                kingRenderer.enabled = true;
            }
        }

        // =====================================================
        // 10. CLEANUP
        // =====================================================

        if (sword != null)
        {
            Destroy(sword);
        }

        isPlaying = false;
    }

    // =========================================================
    // AIM SWORD
    // =========================================================

    private void FaceSwordTowardKing(
        Transform swordTransform,
        Vector3 kingPosition)
    {
        Vector3 toKing =
            kingPosition -
            swordTransform.position;

        toKing.z = 0f;

        float angle =
            Mathf.Atan2(
                toKing.y,
                toKing.x
            ) * Mathf.Rad2Deg;

        // Our prefab convention:
        // sword blade points along local +Y.
        angle -= 90f;

        angle += swordRotationOffset;

        swordTransform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );
    }

    // =========================================================
    // IMPACT
    // =========================================================

    private void TriggerKingImpact(ChessPiece king, ref SpriteRenderer kingRenderer, ref bool impactTriggered)
    {
        if (impactTriggered || king == null) return;

        impactTriggered = true;

        Debug.Log("[KING SLAY] Sword reached king. IMPACT.");

        kingRenderer = king.GetComponent<SpriteRenderer>();

        if (PieceShatterVFX.Instance != null)
        {
            PieceShatterVFX.Instance.Play(king);
        }
        else
        {
            Debug.LogWarning("[KING SLAY] PieceShatterVFX is missing from the scene.");

            if (kingRenderer != null) kingRenderer.enabled = false;
        }
    }

    // =========================================================
    // DEBUG
    // =========================================================

    [ContextMenu("TEST King Slay Sequence")]
    private void TestKingSlaySequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[KING SLAY TEST] Enter Play Mode first."
            );

            return;
        }

        if (debugAttacker == null)
        {
            Debug.LogError(
                "[KING SLAY TEST] Assign Debug Attacker."
            );

            return;
        }

        if (debugKing == null)
        {
            Debug.LogError(
                "[KING SLAY TEST] Assign Debug King."
            );

            return;
        }

        Debug.Log(
            $"[KING SLAY TEST] " +
            $"{debugAttacker.team} " +
            $"{debugAttacker.pieceType} " +
            $"will slay {debugKing.team} King."
        );

        Play(debugAttacker, debugKing, debugKing.currentCell, null);
    }
}