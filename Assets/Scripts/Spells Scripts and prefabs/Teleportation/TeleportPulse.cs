using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class TeleportPulse : MonoBehaviour
{
    private SpriteRenderer sr;

    public void Play(float pulseTime, float holdTime)
    {
        sr = GetComponent<SpriteRenderer>();
        StartCoroutine(Run(pulseTime, holdTime));
    }

    private IEnumerator Run(float pulseTime, float holdTime)
    {
        // start small & bright
        transform.localScale = Vector3.zero;
        Color baseC = sr.color;
        sr.color = new Color(baseC.r, baseC.g, baseC.b, 1f);

        // scale up + slight fade
        float t = 0f;
        while (t < pulseTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / pulseTime);
            transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, Mathf.SmoothStep(0, 1, k));
            sr.color = new Color(baseC.r, baseC.g, baseC.b, Mathf.Lerp(1f, 0.6f, k));
            yield return null;
        }

        if (holdTime > 0f) yield return new WaitForSeconds(holdTime);

        // quick fade out
        t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(0.6f, 0f, t / 0.15f);
            sr.color = new Color(baseC.r, baseC.g, baseC.b, a);
            yield return null;
        }

        Destroy(gameObject);
    }
}
