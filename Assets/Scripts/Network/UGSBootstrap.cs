using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class UGSBootstrap : MonoBehaviour
{
    async void Awake()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        { await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("[UGS] Signed in as " + AuthenticationService.Instance.PlayerId);
        }

    }
}
