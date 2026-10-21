
using UnityEngine;
using Unity.Netcode;

[DefaultExecutionOrder(-10000)]
public class NetworkManagerDuplicateGuard : MonoBehaviour
{
    private static NetworkManagerDuplicateGuard activeInstance;

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.Log("[NETWORK] Duplicate Network Manager removed.");
            Destroy(gameObject);
            return;
        }

        activeInstance = this;

        Debug.Log("[NETWORK] Original Network Manager retained.");
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }
}
