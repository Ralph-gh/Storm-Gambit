using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LastMoveIndicator : MonoBehaviour
{
    public static LastMoveIndicator Instance { get; private set; }

    [Header("Optional Arrow Head (SpriteRenderer child)")]
    [SerializeField] private Transform arrowHead;          // assign a child object (optional)
    [SerializeField] private float startPadding = 0.05f;   // trims line off the piece center
    [SerializeField] private float endPadding = 0.12f;     // trims line so head sits before the destination center

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;

    private LineRenderer lr;

    private void Awake()
    {
        Instance = this;
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.enabled = false;

        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        if (arrowHead) arrowHead.gameObject.SetActive(false);
    }

    /*private void Start()
    {
        ShowMove(new Vector2Int(0, 0), new Vector2Int(7, 7));
    }*/

    public void Hide()
    {
        lr.enabled = false;
        if (arrowHead) arrowHead.gameObject.SetActive(false);
    }

    public void ShowMove(Vector2Int fromCell, Vector2Int toCell)
    {
        if (!BoardInitializer.Instance) return;

        Vector3 a = BoardInitializer.Instance.GetWorldPosition(fromCell);
        Vector3 b = BoardInitializer.Instance.GetWorldPosition(toCell);

        Vector3 dir = (b - a);
        float dist = dir.magnitude;
        if (dist < 0.0001f)
        {
            Hide();
            return;
        }

        Vector3 n = dir / dist;

        // Trim ends so it doesn’t overlap piece sprites too much
        Vector3 start = a + n * startPadding;
        Vector3 end = b - n * endPadding;

        lr.enabled = true;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        // Optional arrow head
        if (arrowHead)
        {
            arrowHead.gameObject.SetActive(true);
            arrowHead.position = end;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrowHead.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
