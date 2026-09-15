using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
public class BoardViewportFitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform boardSlot;
    [SerializeField] private Tilemap boardTilemap;

    [Header("Board Fit")]
    [SerializeField] private float padding = 1.02f;

    private Camera boardCamera;

    private int lastScreenWidth;
    private int lastScreenHeight;

    private Quaternion lastCameraRotation;

    private void Awake()
    {
        boardCamera = GetComponent<Camera>();

        // Chessboard camera must remain orthographic.
        boardCamera.orthographic = true;

        // IMPORTANT:
        // Keep Main Camera rendering the entire screen.
        boardCamera.rect = new Rect(0f, 0f, 1f, 1f);

        lastCameraRotation = transform.rotation;
    }

    private void Start()
    {
        ApplyLayout();
        lastCameraRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (boardSlot == null || boardTilemap == null)
            return;

        bool screenChanged =
            Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight;

        bool slotChanged =
            boardSlot.hasChanged;

        bool cameraFlipped =
            transform.rotation != lastCameraRotation;

        if (screenChanged ||
            slotChanged ||
            cameraFlipped)
        {
            ApplyLayout();

            boardSlot.hasChanged = false;
            lastCameraRotation = transform.rotation;
        }
    }

    private void ApplyLayout()
    {
        if (boardSlot == null || boardTilemap == null)
            return;

        Canvas.ForceUpdateCanvases();

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        // =========================================================
        // 1. ALWAYS KEEP CAMERA FULL SCREEN
        // =========================================================

        boardCamera.rect = new Rect(
            0f,
            0f,
            1f,
            1f
        );

        // =========================================================
        // 2. GET BOARDSLOT POSITION IN SCREEN PIXELS
        // =========================================================

        Vector3[] corners = new Vector3[4];
        boardSlot.GetWorldCorners(corners);

        Canvas canvas =
            boardSlot.GetComponentInParent<Canvas>();

        Camera uiCamera = null;

        // Screen Space Overlay does not need a camera.
        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        Vector2 bottomLeft =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                corners[0]
            );

        Vector2 topRight =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                corners[2]
            );

        float slotWidth =
            topRight.x - bottomLeft.x;

        float slotHeight =
            topRight.y - bottomLeft.y;

        if (slotWidth <= 0f || slotHeight <= 0f)
            return;

        Vector2 slotCenter =
            (bottomLeft + topRight) * 0.5f;

        // =========================================================
        // 3. GET EXACT 8x8 CHESSBOARD SIZE
        // =========================================================

        // Your actual chess pieces also use Tilemap cell centers,
        // so use the exact same coordinate system here.

        Vector3 cell00 =
            boardTilemap.GetCellCenterWorld(
                new Vector3Int(0, 0, 0)
            );

        Vector3 cell10 =
            boardTilemap.GetCellCenterWorld(
                new Vector3Int(1, 0, 0)
            );

        Vector3 cell01 =
            boardTilemap.GetCellCenterWorld(
                new Vector3Int(0, 1, 0)
            );

        Vector3 cell77 =
            boardTilemap.GetCellCenterWorld(
                new Vector3Int(7, 7, 0)
            );

        float cellWidth =
            Mathf.Abs(cell10.x - cell00.x);

        float cellHeight =
            Mathf.Abs(cell01.y - cell00.y);

        float boardWidth =
            cellWidth * 8f;

        float boardHeight =
            cellHeight * 8f;

        Vector3 boardCenter =
            (cell00 + cell77) * 0.5f;

        // =========================================================
        // 4. CALCULATE CAMERA ZOOM
        // =========================================================

        // How much Orthographic Size is required for the board
        // to fit inside BoardSlot vertically?
        float sizeFromHeight =
            boardHeight *
            Screen.height /
            (2f * slotHeight);

        // How much is required horizontally?
        float sizeFromWidth =
            boardWidth *
            Screen.height /
            (2f * slotWidth);

        boardCamera.orthographicSize =
            Mathf.Max(
                sizeFromHeight,
                sizeFromWidth
            ) * padding;

        // =========================================================
        // 5. FIND WHERE BOARDSLOT IS RELATIVE TO SCREEN CENTER
        // =========================================================

        Vector2 screenCenter =
            new Vector2(
                Screen.width * 0.5f,
                Screen.height * 0.5f
            );

        Vector2 pixelOffset =
            slotCenter - screenCenter;

        float visibleWorldHeight =
            boardCamera.orthographicSize * 2f;

        float worldUnitsPerPixel =
            visibleWorldHeight /
            Screen.height;

        // =========================================================
        // 6. ROTATION-AWARE OFFSET
        // =========================================================

        // THIS fixes the flip bug.
        //
        // At 0 degrees:
        // transform.right = screen right
        // transform.up    = screen up
        //
        // At 180 degrees:
        // both directions reverse.
        //
        // Therefore BoardSlot remains in the same screen location
        // even though the camera flips.

        Vector3 worldOffset =
            transform.right *
            (pixelOffset.x * worldUnitsPerPixel)
            +
            transform.up *
            (pixelOffset.y * worldUnitsPerPixel);

        // =========================================================
        // 7. POSITION CAMERA
        // =========================================================

        transform.position =
            new Vector3(
                boardCenter.x - worldOffset.x,
                boardCenter.y - worldOffset.y,
                transform.position.z
            );
    }

    // Optional:
    // Allows another script such as BoardFlipController to force
    // an immediate refresh if we ever need it.
    public void RefreshNow()
    {
        ApplyLayout();
        lastCameraRotation = transform.rotation;
    }
}