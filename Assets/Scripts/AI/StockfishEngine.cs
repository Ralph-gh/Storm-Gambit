using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Diagnostics;
#endif

/// <summary>
/// Cross-platform UCI bridge for Stockfish.
/// Windows uses the normal executable process.
/// Android uses StockfishAndroidBridge.java plus an ARM64 Stockfish binary
/// packaged as libstockfish.so in the app's native library directory.
/// </summary>
public class StockfishEngine : MonoBehaviour
{
    [Header("Windows Stockfish")]
    [Tooltip("Path relative to Assets/StreamingAssets. Used only on Windows.")]
    [SerializeField]
    private string engineRelativePath =
        "Stockfish/stockfish-windows-x86-64.exe";

    [Header("Search")]
    [Min(10)]
    [SerializeField]
    private int moveTimeMs = 700;

    private readonly object inputLock = new object();
    private readonly SemaphoreSlim searchLock = new SemaphoreSlim(1, 1);

    private TaskCompletionSource<bool> uciOkTcs;
    private TaskCompletionSource<bool> readyOkTcs;
    private TaskCompletionSource<string> bestMoveTcs;

    private bool initialized;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private Process process;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass androidBridge;
    private AndroidJavaObject androidActivity;
    private bool androidStarted;
#endif

    public bool IsReady
    {
        get
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return initialized && process != null && !process.HasExited;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return initialized && androidStarted && androidBridge != null;
#else
            return false;
#endif
        }
    }

    public async Task InitializeAsync()
    {
        if (IsReady)
            return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        await InitializeWindowsAsync();
#elif UNITY_ANDROID && !UNITY_EDITOR
        await InitializeAndroidAsync();
#else
        await Task.Yield();
        throw new PlatformNotSupportedException(
            $"Stockfish is not configured for {Application.platform}."
        );
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private async Task InitializeWindowsAsync()
    {
        string enginePath = Path.Combine(
            Application.streamingAssetsPath,
            engineRelativePath
        );

        if (!File.Exists(enginePath))
            throw new FileNotFoundException("Stockfish executable not found.", enginePath);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = enginePath,
            WorkingDirectory = Path.GetDirectoryName(enginePath),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += HandleOutput;
        process.ErrorDataReceived += HandleError;

        if (!process.Start())
            throw new InvalidOperationException("Stockfish process failed to start.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await PerformUciHandshakeAsync();
        initialized = true;
        Debug.Log($"[STOCKFISH/WINDOWS] Ready: {enginePath}");
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private async Task InitializeAndroidAsync()
    {
        Debug.Log($"[STOCKFISH/ANDROID] Initializing on {SystemInfo.deviceModel}.");

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            androidActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        if (androidActivity == null)
            throw new InvalidOperationException("Could not get Android currentActivity.");

        androidBridge = new AndroidJavaClass(
            "com.rgaming.stormgambit.StockfishAndroidBridge"
        );

        androidStarted = androidBridge.CallStatic<bool>("start", androidActivity);

        if (!androidStarted)
        {
            string error = androidBridge.CallStatic<string>("getLastError");
            throw new InvalidOperationException(
                "[STOCKFISH/ANDROID] Could not start Stockfish. " + error
            );
        }

        await PerformUciHandshakeAsync();
        initialized = true;

        string enginePath = androidBridge.CallStatic<string>("getEnginePath");
        Debug.Log($"[STOCKFISH/ANDROID] Ready: {enginePath}");
    }
#endif

    private async Task PerformUciHandshakeAsync()
    {
        uciOkTcs = NewTcs<bool>();
        SendCommand("uci");

        await WithTimeout(
            uciOkTcs.Task,
            5000,
            "Stockfish did not return uciok."
        );

        readyOkTcs = NewTcs<bool>();
        SendCommand("isready");

        await WithTimeout(
            readyOkTcs.Task,
            5000,
            "Stockfish did not return readyok."
        );

        SendCommand("ucinewgame");
    }

    public async Task<string> GetBestMoveAsync(string fen, int customMoveTimeMs = -1)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new ArgumentException("FEN cannot be empty.", nameof(fen));

        await InitializeAsync();
        await searchLock.WaitAsync();

        try
        {
            int thinkTime = customMoveTimeMs > 0 ? customMoveTimeMs : moveTimeMs;
            bestMoveTcs = NewTcs<string>();

            SendCommand($"position fen {fen}");
            SendCommand($"go movetime {thinkTime}");

            return await WithTimeout(
                bestMoveTcs.Task,
                thinkTime + 10000,
                "Stockfish did not return bestmove."
            );
        }
        finally
        {
            bestMoveTcs = null;
            searchLock.Release();
        }
    }

    public void StopSearch()
    {
        if (!IsReady)
            return;

        try { SendCommand("stop"); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[STOCKFISH] stop failed: {ex.Message}");
        }
    }

    private void SendCommand(string command)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (process == null || process.HasExited)
            throw new InvalidOperationException("Stockfish is not running.");

        lock (inputLock)
        {
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (!androidStarted || androidBridge == null)
            throw new InvalidOperationException("Android Stockfish is not running.");

        bool sent = androidBridge.CallStatic<bool>("send", command);

        if (!sent)
        {
            string error = androidBridge.CallStatic<string>("getLastError");
            throw new IOException(
                "Failed to send command to Android Stockfish. " + error
            );
        }
#else
        throw new PlatformNotSupportedException(
            $"Stockfish command sending is not supported on {Application.platform}."
        );
#endif
    }

    private void HandleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (line == "uciok")
        {
            uciOkTcs?.TrySetResult(true);
            return;
        }

        if (line == "readyok")
        {
            readyOkTcs?.TrySetResult(true);
            return;
        }

        if (line.StartsWith("bestmove ", StringComparison.Ordinal))
        {
            string[] parts = line.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (parts.Length >= 2)
                bestMoveTcs?.TrySetResult(parts[1]);
        }
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private void HandleOutput(object sender, DataReceivedEventArgs e)
    {
        HandleLine(e.Data);
    }

    private void HandleError(object sender, DataReceivedEventArgs e)
    {
        // Avoid Unity API calls from this callback thread.
    }
#endif

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!androidStarted || androidBridge == null)
            return;

        for (int i = 0; i < 128; i++)
        {
            string line = androidBridge.CallStatic<string>("pollLine");
            if (string.IsNullOrEmpty(line))
                break;

            HandleLine(line);
        }

        bool alive = androidBridge.CallStatic<bool>("isAlive");
        if (initialized && !alive)
        {
            string error = androidBridge.CallStatic<string>("getLastError");
            Debug.LogError(
                "[STOCKFISH/ANDROID] Engine stopped unexpectedly. " + error
            );
            initialized = false;
            androidStarted = false;
        }
#endif
    }

    private static TaskCompletionSource<T> NewTcs<T>()
    {
        return new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs, string timeoutMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        if (completed != task)
            throw new TimeoutException(timeoutMessage);

        return await task;
    }

    private void OnDestroy() => Shutdown();
    private void OnApplicationQuit() => Shutdown();

    private void Shutdown()
    {
        initialized = false;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
            {
                try { SendCommand("quit"); } catch { }
                if (!process.WaitForExit(500))
                    process.Kill();
            }
        }
        catch { }
        finally
        {
            process.OutputDataReceived -= HandleOutput;
            process.ErrorDataReceived -= HandleError;
            process.Dispose();
            process = null;
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (androidBridge != null)
        {
            try { androidBridge.CallStatic("stop"); } catch { }
            androidBridge.Dispose();
            androidBridge = null;
        }

        if (androidActivity != null)
        {
            androidActivity.Dispose();
            androidActivity = null;
        }

        androidStarted = false;
#endif
    }
}
