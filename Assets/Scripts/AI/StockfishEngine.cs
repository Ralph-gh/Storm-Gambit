using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Windows/editor UCI bridge for Stockfish.
/// It runs Stockfish invisibly as a child process and exchanges text commands.
/// No Storm Gambit rules live in this class.
/// </summary>
public class StockfishEngine : MonoBehaviour
{
    [Header("Stockfish")]
    [Tooltip("Path relative to Assets/StreamingAssets.")]
    [SerializeField]
    private string engineRelativePath =
        "Stockfish/stockfish-windows-x86-64.exe";

    [Header("Search")]
    [Min(10)]
    [SerializeField]
    private int moveTimeMs = 700;

    private Process process;
    private readonly object inputLock = new object();
    private readonly SemaphoreSlim searchLock = new SemaphoreSlim(1, 1);

    private TaskCompletionSource<bool> uciOkTcs;
    private TaskCompletionSource<bool> readyOkTcs;
    private TaskCompletionSource<string> bestMoveTcs;

    private bool initialized;

    public bool IsReady =>
        initialized &&
        process != null &&
        !process.HasExited;

    public async Task InitializeAsync()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

        if (IsReady)
            return;

        string enginePath =
            Path.Combine(
                Application.streamingAssetsPath,
                engineRelativePath
            );

        if (!File.Exists(enginePath))
        {
            throw new FileNotFoundException(
                "Stockfish executable not found.",
                enginePath
            );
        }

        ProcessStartInfo startInfo =
            new ProcessStartInfo
            {
                FileName = enginePath,
                WorkingDirectory =
                    Path.GetDirectoryName(enginePath),
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
            throw new InvalidOperationException(
                "Stockfish process failed to start."
            );

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // UCI handshake.
        uciOkTcs = NewTcs<bool>();
        SendCommand("uci");

        await WithTimeout(
            uciOkTcs.Task,
            5000,
            "Stockfish did not return uciok."
        );

        // Confirm the engine is ready to receive work.
        readyOkTcs = NewTcs<bool>();
        SendCommand("isready");

        await WithTimeout(
            readyOkTcs.Task,
            5000,
            "Stockfish did not return readyok."
        );

        SendCommand("ucinewgame");

        initialized = true;

        Debug.Log(
            $"[STOCKFISH] Ready: {enginePath}"
        );

#else
        await Task.Yield();

        throw new PlatformNotSupportedException(
            "This first Stockfish bridge is intentionally Windows-only. " +
            "We will add the Android native implementation after the " +
            "desktop UCI pipeline is proven."
        );
#endif
    }

    public async Task<string> GetBestMoveAsync(
        string fen,
        int customMoveTimeMs = -1)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new ArgumentException(
                "FEN cannot be empty.",
                nameof(fen)
            );

        await InitializeAsync();

        await searchLock.WaitAsync();

        try
        {
            int thinkTime =
                customMoveTimeMs > 0
                    ? customMoveTimeMs
                    : moveTimeMs;

            bestMoveTcs = NewTcs<string>();

            SendCommand($"position fen {fen}");
            SendCommand($"go movetime {thinkTime}");

            string move =
                await WithTimeout(
                    bestMoveTcs.Task,
                    thinkTime + 10000,
                    "Stockfish did not return bestmove."
                );

            return move;
        }
        finally
        {
            bestMoveTcs = null;
            searchLock.Release();
        }
    }

    public void StopSearch()
    {
        if (process == null || process.HasExited)
            return;

        SendCommand("stop");
    }

    private void SendCommand(string command)
    {
        if (process == null || process.HasExited)
            throw new InvalidOperationException(
                "Stockfish is not running."
            );

        lock (inputLock)
        {
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
    }

    private void HandleOutput(
        object sender,
        DataReceivedEventArgs e)
    {
        string line = e.Data;

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

        if (line.StartsWith(
            "bestmove ",
            StringComparison.Ordinal))
        {
            string[] parts =
                line.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries
                );

            if (parts.Length >= 2)
                bestMoveTcs?.TrySetResult(parts[1]);
        }
    }

    private void HandleError(
        object sender,
        DataReceivedEventArgs e)
    {
        // Do not call Unity APIs from this process callback thread.
        // We can add a thread-safe diagnostic queue later if needed.
    }

    private static TaskCompletionSource<T> NewTcs<T>()
    {
        return new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    }

    private static async Task<T> WithTimeout<T>(
        Task<T> task,
        int timeoutMs,
        string timeoutMessage)
    {
        Task completed =
            await Task.WhenAny(
                task,
                Task.Delay(timeoutMs)
            );

        if (completed != task)
            throw new TimeoutException(timeoutMessage);

        return await task;
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void Shutdown()
    {
        initialized = false;

        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    SendCommand("quit");
                }
                catch
                {
                    // Ignore shutdown write errors.
                }

                if (!process.WaitForExit(500))
                    process.Kill();
            }
        }
        catch
        {
            // App is shutting down; do not block teardown.
        }
        finally
        {
            process.OutputDataReceived -= HandleOutput;
            process.ErrorDataReceived -= HandleError;
            process.Dispose();
            process = null;
        }
    }
}
