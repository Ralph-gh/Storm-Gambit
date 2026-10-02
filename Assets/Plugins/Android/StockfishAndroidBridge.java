package com.rgaming.stormgambit;

import android.content.Context;
import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.util.concurrent.ConcurrentLinkedQueue;

public final class StockfishAndroidBridge
{
    private static Process process;
    private static BufferedWriter writer;
    private static Thread readerThread;
    private static final ConcurrentLinkedQueue<String> outputLines = new ConcurrentLinkedQueue<>();
    private static volatile String lastError = "";
    private static volatile String enginePath = "";

    private StockfishAndroidBridge() { }

    public static synchronized boolean start(Context context)
    {
        if (isAlive()) return true;
        lastError = "";
        outputLines.clear();

        try
        {
            if (context == null)
            {
                lastError = "Android Context is null.";
                return false;
            }

            File nativeDir = new File(context.getApplicationInfo().nativeLibraryDir);
            File engine = new File(nativeDir, "libstockfish.so");
            enginePath = engine.getAbsolutePath();

            if (!engine.exists())
            {
                lastError = "Bundled Stockfish binary was not found at: " + enginePath;
                return false;
            }

            ProcessBuilder builder = new ProcessBuilder(enginePath);
            builder.directory(nativeDir);
            builder.environment().put(
                "LD_LIBRARY_PATH",
                nativeDir.getAbsolutePath()
            );
            builder.redirectErrorStream(true);
            process = builder.start();

            writer = new BufferedWriter(new OutputStreamWriter(process.getOutputStream()));
            final BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));

            readerThread = new Thread(new Runnable()
            {
                @Override
                public void run()
                {
                    try
                    {
                        String line;
                        while ((line = reader.readLine()) != null)
                            outputLines.offer(line);
                    }
                    catch (Exception ex)
                    {
                        lastError = "Stockfish reader failed: " + ex.toString();
                    }
                    finally
                    {
                        try { reader.close(); } catch (Exception ignored) { }
                    }
                }
            }, "StormGambit-StockfishReader");

            readerThread.setDaemon(true);
            readerThread.start();
            return true;
        }
        catch (Exception ex)
        {
            lastError = "Failed to launch Stockfish: " + ex.toString();
            process = null;
            writer = null;
            return false;
        }
    }

    public static synchronized boolean send(String command)
    {
        if (!isAlive() || writer == null)
        {
            lastError = "Cannot send UCI command because Stockfish is not running.";
            return false;
        }

        try
        {
            writer.write(command);
            writer.newLine();
            writer.flush();
            return true;
        }
        catch (Exception ex)
        {
            lastError = "Failed to write UCI command: " + ex.toString();
            return false;
        }
    }

    public static String pollLine()
    {
        return outputLines.poll();
    }

    public static synchronized boolean isAlive()
    {
        if (process == null) return false;

        try
        {
            process.exitValue();
            return false;
        }
        catch (IllegalThreadStateException stillRunning)
        {
            return true;
        }
        catch (Exception ex)
        {
            lastError = "Could not determine Stockfish process state: " + ex.toString();
            return false;
        }
    }

    public static synchronized void stop()
    {
        if (process == null) return;

        try
        {
            if (isAlive() && writer != null)
            {
                writer.write("quit");
                writer.newLine();
                writer.flush();
            }
        }
        catch (Exception ignored) { }

        try { if (writer != null) writer.close(); } catch (Exception ignored) { }
        try { process.destroy(); } catch (Exception ignored) { }

        process = null;
        writer = null;
        readerThread = null;
        outputLines.clear();
    }

    public static String getLastError() { return lastError; }
    public static String getEnginePath() { return enginePath; }
}
