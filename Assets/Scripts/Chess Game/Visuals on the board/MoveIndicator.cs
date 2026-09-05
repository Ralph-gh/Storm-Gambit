using UnityEngine;

public class MoveIndicator : MonoBehaviour
{
    public static MoveIndicator Instance { get; private set; }

    [SerializeField] private GameObject indicatorPrefab;

    private GameObject fromIndicator;
    private GameObject toIndicator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowMove(Vector2Int from, Vector2Int to)
    {
        if (BoardInitializer.Instance == null)
            return;

        EnsureIndicators();

        PlaceIndicator(fromIndicator, from);
        PlaceIndicator(toIndicator, to);
    }

    private void EnsureIndicators()
    {
        if (fromIndicator == null)
            fromIndicator = Instantiate(indicatorPrefab, transform);

        if (toIndicator == null)
            toIndicator = Instantiate(indicatorPrefab, transform);
    }

    private void PlaceIndicator(GameObject indicator, Vector2Int cell)
    {
        indicator.transform.position =
            BoardInitializer.Instance.GetWorldPosition(cell);

        indicator.SetActive(true);
    }

    public void Clear()
    {
        if (fromIndicator != null)
            fromIndicator.SetActive(false);

        if (toIndicator != null)
            toIndicator.SetActive(false);
    }
}