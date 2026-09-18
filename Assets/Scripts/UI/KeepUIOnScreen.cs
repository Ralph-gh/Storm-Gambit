using UnityEngine;

[DisallowMultipleComponent]
public class KeepUIOnScreen : MonoBehaviour
{
    [Header("Boundary")]
    [SerializeField] private RectTransform boundary;

    [Header("Padding")]
    [SerializeField] private float leftPadding = 10f;
    [SerializeField] private float rightPadding = 10f;
    [SerializeField] private float topPadding = 10f;
    [SerializeField] private float bottomPadding = 10f;

    private RectTransform target;

    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        target = GetComponent<RectTransform>();

        if (boundary == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
                boundary = canvas.GetComponent<RectTransform>();
        }
    }

    private void OnEnable()
    {
        ClampNow();
    }

    private void LateUpdate()
    {
        if (target == null || boundary == null)
            return;

        bool screenChanged =
            Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight;

        bool rectChanged =
            target.hasChanged ||
            boundary.hasChanged;

        if (screenChanged || rectChanged)
        {
            ClampNow();

            target.hasChanged = false;
            boundary.hasChanged = false;
        }
    }

    public void ClampNow()
    {
        if (target == null || boundary == null)
            return;

        Canvas.ForceUpdateCanvases();

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        Bounds bounds =
            RectTransformUtility.CalculateRelativeRectTransformBounds(
                boundary,
                target
            );

        Rect boundaryRect = boundary.rect;

        float minX =
            boundaryRect.xMin + leftPadding;

        float maxX =
            boundaryRect.xMax - rightPadding;

        float minY =
            boundaryRect.yMin + bottomPadding;

        float maxY =
            boundaryRect.yMax - topPadding;

        float deltaX = 0f;
        float deltaY = 0f;

        // Left side outside
        if (bounds.min.x < minX)
        {
            deltaX += minX - bounds.min.x;
        }

        // Right side outside
        if (bounds.max.x > maxX)
        {
            deltaX -= bounds.max.x - maxX;
        }

        // Bottom outside
        if (bounds.min.y < minY)
        {
            deltaY += minY - bounds.min.y;
        }

        // Top outside
        if (bounds.max.y > maxY)
        {
            deltaY -= bounds.max.y - maxY;
        }

        if (Mathf.Approximately(deltaX, 0f) &&
            Mathf.Approximately(deltaY, 0f))
        {
            return;
        }

        Vector3 localCorrection =
            new Vector3(
                deltaX,
                deltaY,
                0f
            );

        Vector3 worldCorrection =
            boundary.TransformVector(
                localCorrection
            );

        target.position += worldCorrection;
    }
}