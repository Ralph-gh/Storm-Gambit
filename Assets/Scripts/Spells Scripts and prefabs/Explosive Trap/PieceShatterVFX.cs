using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime 2D sprite shatter for chess pieces.
/// Attach one instance to BoardManager (or another always-active scene object).
/// </summary>
public class PieceShatterVFX : MonoBehaviour
{
    public static PieceShatterVFX Instance { get; private set; }

    [Header("Shard Grid")]
    [Range(2, 6)] public int columns = 3;
    [Range(2, 6)] public int rows = 3;

    [Header("Motion")]
    public float minForce = 0.65f;
    public float maxForce = 1.35f;
    public float randomForce = 0.35f;
    public float gravityScale = 0.25f;
    public float minAngularVelocity = 180f;
    public float maxAngularVelocity = 520f;

    [Header("Lifetime")]
    public float lifetime = 0.65f;
    public float fadeDuration = 0.22f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Play(ChessPiece piece)
    {
        if (piece == null) return;

        SpriteRenderer sourceRenderer = piece.GetComponent<SpriteRenderer>();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            Debug.LogWarning("[SHATTER] Piece has no SpriteRenderer/Sprite.");
            return;
        }

        Sprite sourceSprite = sourceRenderer.sprite;
        Texture2D texture = sourceSprite.texture;
        Rect sourceRect = sourceSprite.rect;
        float ppu = sourceSprite.pixelsPerUnit;

        GameObject root = new GameObject($"Shatter_{piece.name}");

        var shardRenderers = new List<SpriteRenderer>();
        var runtimeSprites = new List<Sprite>();

        float shardWidth = sourceRect.width / columns;
        float shardHeight = sourceRect.height / rows;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Rect shardRect = new Rect(
                    sourceRect.x + x * shardWidth,
                    sourceRect.y + y * shardHeight,
                    shardWidth,
                    shardHeight
                );

                Sprite shardSprite = Sprite.Create(
                    texture,
                    shardRect,
                    new Vector2(0.5f, 0.5f),
                    ppu,
                    0,
                    SpriteMeshType.FullRect
                );

                runtimeSprites.Add(shardSprite);

                GameObject shard = new GameObject($"Shard_{x}_{y}");
                shard.transform.SetParent(root.transform, true);

                Vector2 shardCenterPixels = new Vector2(
                    x * shardWidth + shardWidth * 0.5f,
                    y * shardHeight + shardHeight * 0.5f
                );

                Vector2 offsetPixels = shardCenterPixels - sourceSprite.pivot;
                Vector3 localOffset = new Vector3(
                    offsetPixels.x / ppu,
                    offsetPixels.y / ppu,
                    0f
                );

                shard.transform.position = piece.transform.TransformPoint(localOffset);
                shard.transform.rotation = piece.transform.rotation;
                shard.transform.localScale = piece.transform.lossyScale;

                SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
                sr.sprite = shardSprite;
                sr.color = sourceRenderer.color;
                sr.flipX = sourceRenderer.flipX;
                sr.flipY = sourceRenderer.flipY;
                sr.sortingLayerID = sourceRenderer.sortingLayerID;
                sr.sortingOrder = sourceRenderer.sortingOrder + 2;
                shardRenderers.Add(sr);

                Rigidbody2D rb = shard.AddComponent<Rigidbody2D>();
                rb.gravityScale = gravityScale;

                Vector2 radial =
                    ((Vector2)shard.transform.position - (Vector2)piece.transform.position);

                if (radial.sqrMagnitude < 0.0001f)
                    radial = Random.insideUnitCircle.normalized;
                else
                    radial.Normalize();

                rb.velocity =
                 radial * Random.Range(minForce, maxForce) +
                 Random.insideUnitCircle * randomForce;

                float spin = Random.Range(minAngularVelocity, maxAngularVelocity);
                rb.angularVelocity = Random.value < 0.5f ? -spin : spin;
            }
        }

        sourceRenderer.enabled = false;

        StartCoroutine(FadeAndDestroy(root, shardRenderers, runtimeSprites));
    }

    private IEnumerator FadeAndDestroy(
        GameObject root,
        List<SpriteRenderer> renderers,
        List<Sprite> runtimeSprites)
    {
        float holdTime = Mathf.Max(0f, lifetime - fadeDuration);
        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alphaMultiplier =
                1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration));

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;

                Color c = sr.color;
                c.a = alphaMultiplier;
                sr.color = c;
            }

            yield return null;
        }

        foreach (Sprite runtimeSprite in runtimeSprites)
        {
            if (runtimeSprite != null)
                Destroy(runtimeSprite);
        }

        if (root != null)
            Destroy(root);
    }
}
