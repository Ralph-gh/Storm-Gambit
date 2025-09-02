using UnityEngine;

public class TeleportVFX : MonoBehaviour
{
    public static TeleportVFX Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject teleportRingPrefab;

    [Header("Timings")]
    [SerializeField] private float pulseTime = 0.25f;   // scale/alpha anim
    [SerializeField] private float holdTime = 0.05f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayAt(Vector3 worldPos)
    {
        if (!teleportRingPrefab) return;
        var go = Instantiate(teleportRingPrefab, worldPos, Quaternion.identity);
        var pulse = go.AddComponent<TeleportPulse>();
        pulse.Play(pulseTime, holdTime);
    }

    // Convenience for origin + destination bursts
    public void PlayJump(Vector3 origin, Vector3 destination)
    {
        PlayAt(origin);
        PlayAt(destination);
    }
}
