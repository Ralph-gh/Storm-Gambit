using UnityEngine;

public class LobbySafeArea : MonoBehaviour
{
    private Rect lastArea;
    private Vector2Int lastSize;
    private void OnEnable() => Update();
    private void Update()
    {
        if (Screen.width == 0 || Screen.height == 0) return;
        var area = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (area == lastArea && size == lastSize) return;
        lastArea = area;
        lastSize = size;
        var rect = (RectTransform)transform;
        rect.anchorMin = area.position / new Vector2(Screen.width, Screen.height);
        rect.anchorMax = (area.position + area.size) / new Vector2(Screen.width, Screen.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
