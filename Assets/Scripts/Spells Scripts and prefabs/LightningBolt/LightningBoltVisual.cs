using UnityEngine;

public class LightningBoltVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer line;

    [Header("Shape")]
    [SerializeField] private int segments = 10;
    [SerializeField] private float horizontalJitter = 0.10f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 0.15f;

    private void Awake()
    {
        if (line == null)
            line = GetComponent<LineRenderer>();
    }

    public void FireAt(Vector3 target)
    {
        if (line == null)
            return;

        Vector3 start =
            target +
            new Vector3(
                Random.Range(-1.2f, 1.2f),
                Random.Range(5f, 7.5f),
                0f
            );

        line.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            Vector3 point =
                Vector3.Lerp(start, target, t);

            if (i != 0 && i != segments)
            {
                point.x +=
                    Random.Range(
                        -horizontalJitter,
                        horizontalJitter
                    );

                point.y +=
                    Random.Range(
                        -horizontalJitter,
                        horizontalJitter
                    );
            }

            line.SetPosition(i, point);
        }

        Destroy(gameObject, lifetime);
    }
}