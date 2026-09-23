using UnityEngine;

public class LightningBoltVisual : MonoBehaviour
{
    [SerializeField]
    private LineRenderer line;

    [SerializeField]
    private int segments = 8;

    [SerializeField]
    private float horizontalJitter = 0.12f;

    [SerializeField]
    private float lifetime = 0.15f;

    public void FireAt(Vector3 target)
    {
        Vector3 start =
            target +
            new Vector3(
                Random.Range(-1.8f, 1.8f),
                Random.Range(3f, 4.5f),
                0f
            );

        line.positionCount =
            segments + 1;

        for (int i = 0;
             i <= segments;
             i++)
        {
            float t =
                i / (float)segments;

            Vector3 point =
                Vector3.Lerp(
                    start,
                    target,
                    t
                );

            if (i != 0 &&
                i != segments)
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

        Destroy(
            gameObject,
            lifetime
        );
    }
}