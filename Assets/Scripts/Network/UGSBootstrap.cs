using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System;
using System.Threading.Tasks;

public class UGSBootstrap : MonoBehaviour
{
    private static Task initialization;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInitialization() => initialization = null;

    public static Task EnsureReadyAsync()
    {
        if (initialization == null || initialization.IsFaulted || initialization.IsCanceled)
            initialization = InitializeAsync();
        return initialization;
    }

    private static async Task InitializeAsync()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async void Awake()
    {
        try { await EnsureReadyAsync(); }
        catch (Exception e) { Debug.LogError("[UGS] Initialization failed: " + e); }
    }
}
