using System.Collections;
using UnityEngine;

public class LightningStrikeVFX : MonoBehaviour
{
    public static LightningStrikeVFX Instance;

    [Header("Lightning")]
    [SerializeField]
    private GameObject lightningBoltPrefab;

    [Header("Screen Flash")]
    [SerializeField]
    private CanvasGroup whiteFlash;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip warningClip;

    [SerializeField]
    private AudioClip thunderClip;

    [Header("Timing")]
    [SerializeField]
    private float warningDelay = 0.12f;

    [SerializeField]
    private float boltToShatterDelay = 0.08f;

    [SerializeField]
    private float shatterDuration = 0.45f;

    private void Awake()
    {
        Instance = this;

        if (whiteFlash != null)
            whiteFlash.alpha = 0f;
    }

    public void PlayStrike(
        ChessPiece target,
        System.Action onComplete)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(
            StrikeRoutine(
                target,
                onComplete
            )
        );
    }

    private IEnumerator StrikeRoutine(
        ChessPiece target,
        System.Action onComplete)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        // =========================
        // 1. WARNING
        // =========================

        if (audioSource != null &&
            warningClip != null)
        {
            audioSource.PlayOneShot(
                warningClip
            );
        }

        if (whiteFlash != null)
            StartCoroutine(FlashScreen());

        yield return new WaitForSeconds(
            warningDelay
        );

        if (target == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        // =========================
        // 2. LIGHTNING BOLT
        // =========================

        if (lightningBoltPrefab != null)
        {
            GameObject bolt =
                Instantiate(
                    lightningBoltPrefab
                );

            LightningBoltVisual visual =
                bolt.GetComponent<
                    LightningBoltVisual>();

            if (visual != null)
            {
                visual.FireAt(
                    target.transform.position
                );
            }
            else
            {
                bolt.transform.position =
                    target.transform.position;
            }
        }

        if (audioSource != null &&
            thunderClip != null)
        {
            audioSource.PlayOneShot(
                thunderClip
            );
        }

        yield return new WaitForSeconds(
            boltToShatterDelay
        );

        // =========================
        // 3. SHATTER
        // =========================

        if (target != null)
        {
            if (PieceShatterVFX.Instance != null)
            {
                PieceShatterVFX.Instance.Play(
                    target
                );
            }
            else
            {
                SpriteRenderer sr =
                    target.GetComponent<
                        SpriteRenderer>();

                if (sr != null)
                    sr.enabled = false;
            }
        }

        yield return new WaitForSeconds(
            shatterDuration
        );

        onComplete?.Invoke();
    }

    private IEnumerator FlashScreen()
    {
        whiteFlash.alpha = 1f;

        yield return new WaitForSeconds(
            0.04f
        );

        float duration = 0.12f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            whiteFlash.alpha =
                Mathf.Lerp(
                    1f,
                    0f,
                    elapsed / duration
                );

            yield return null;
        }

        whiteFlash.alpha = 0f;
    }
}