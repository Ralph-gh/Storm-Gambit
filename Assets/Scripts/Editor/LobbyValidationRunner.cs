using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Local verification hook for the already-open Editor; no second Unity process is needed.
[InitializeOnLoad]
public static class LobbyValidationRunner
{
    private static TestRunnerApi runner;
    static LobbyValidationRunner()
    {
        EditorApplication.update += CheckRequest;
        if (SessionState.GetBool("StormLobbyChecksRunning", false))
            TestRunnerApi.RegisterTestCallback(new Results());
    }

    private static void CheckRequest()
    {
        if (File.Exists(".utmp/lobby-preview-only.request") && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(".utmp/lobby-preview-only.request");
            Preview();
        }
        const string request = ".utmp/lobby-browser-checks.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        Run();
    }

    [MenuItem("Tools/Storm Gambit/Validate Multiplayer Lobby")]
    public static void Run()
    {
        if (runner != null) return;
        Directory.CreateDirectory(".utmp");
        Preview();
        SessionState.SetBool("StormLobbyChecksRunning", true);
        File.WriteAllText(".utmp/lobby-browser-tests.progress", "Starting lobby browser tests.\n");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new Results());
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            testNames = new[] { "LobbyBrowserTests" }
        }));
    }

    private class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            File.AppendAllText(".utmp/lobby-browser-tests.progress", result.FullName + ": " + result.ResultState + "\n");
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.SetBool("StormLobbyChecksRunning", false);
            TestRunnerApi.SaveResultToFile(result, ".utmp/lobby-browser-tests.xml");
            File.WriteAllText(".utmp/lobby-browser-tests.result",
                $"{result.TestStatus}: {result.PassCount} passed; {result.FailCount} failed.\n{result.Message}");
            Debug.Log($"[Lobby tests] {result.TestStatus}: {result.PassCount} passed; {result.FailCount} failed.");
            TestRunnerApi.UnregisterTestCallback(this);
            if (runner != null) Object.DestroyImmediate(runner);
            runner = null;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
                    EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            };
        }
    }

    [MenuItem("Tools/Storm Gambit/Preview Multiplayer Lobby")]
    public static void Preview()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
        try
        {
            LobbyBrowserUI browser = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<LobbyBrowserUI>(out var found)) browser = found;
            LobbySceneSetup.Validate(browser);
            var cameraObject = new GameObject("Lobby preview camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            var canvas = browser.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            browser.playerNameInput.text = "Storm Mage";
            browser.playerNameLabel.text = "Storm Mage";
            browser.identitySummary.SetActive(true);
            browser.identityEditor.SetActive(false);
            browser.ShowFeedback(null);
            browser.PresentGames(new System.Collections.Generic.List<Unity.Services.Lobbies.Models.Lobby>(), false, true, null);
            Capture(camera, ".utmp/lobby-browser.png");
            browser.identitySummary.SetActive(false);
            browser.identityEditor.SetActive(true);
            browser.playerNameInput.text = "";
            Capture(camera, ".utmp/lobby-name.png");
            browser.identitySummary.SetActive(true);
            browser.identityEditor.SetActive(false);
            browser.playerNameInput.text = "Storm Mage";
            Capture(camera, ".utmp/lobby-browser-tall.png", 1080, 2340);
            browser.PresentGames(new System.Collections.Generic.List<Unity.Services.Lobbies.Models.Lobby>
            {
                PreviewRoom("Storm arena", "Ralph", false),
                PreviewRoom("Mages' duel", "Storm Mage", true)
            }, false, true, null);
            Capture(camera, ".utmp/lobby-populated.png");
            browser.PresentGames(new System.Collections.Generic.List<Unity.Services.Lobbies.Models.Lobby>(), false, true,
                "Could not reach the game lobby. Check your internet and try again.");
            Capture(camera, ".utmp/lobby-error.png");
            browser.PresentGames(new System.Collections.Generic.List<Unity.Services.Lobbies.Models.Lobby>(), false, true, null);
            browser.browsePanel.SetActive(false);
            browser.hostPanel.SetActive(true);
            LobbyUIBuilder.SetTabSelected(browser.browseTab, false);
            LobbyUIBuilder.SetTabSelected(browser.hostTab, true);
            Capture(camera, ".utmp/lobby-host.png");
            browser.hostPanel.SetActive(false);
            browser.waitingPanel.SetActive(true);
            browser.waitingTitle.text = "Storm Mage's game";
            browser.waitingDetails.text = "YOU\nStorm Mage\n\nOPPONENT\nWaiting for an opponent...\n\nPassword protected\nThe match starts when both players connect.";
            Capture(camera, ".utmp/lobby-waiting.png");
            browser.waitingPanel.SetActive(false);
            browser.browsePanel.SetActive(true);
            browser.passwordDialog.SetActive(true);
            browser.dialogTitle.text = "A rival's game";
            Capture(camera, ".utmp/lobby-password.png");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static Unity.Services.Lobbies.Models.Lobby PreviewRoom(string name, string host, bool password)
        => new Unity.Services.Lobbies.Models.Lobby(id: "preview-" + name, name: name, maxPlayers: 2,
            availableSlots: 1, hasPassword: password,
            data: new System.Collections.Generic.Dictionary<string, Unity.Services.Lobbies.Models.DataObject>
            {
                { LobbyMatchmaking.NameKey, new Unity.Services.Lobbies.Models.DataObject(
                    Unity.Services.Lobbies.Models.DataObject.VisibilityOptions.Public, host) }
            });

    private static void Capture(Camera camera, string path, int width = 1080, int height = 1920)
    {
        var texture = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            foreach (var label in camera.GetComponent<Camera>().gameObject.scene.GetRootGameObjects())
                foreach (var text in label.GetComponentsInChildren<TMPro.TMP_Text>()) text.ForceMeshUpdate();
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
        }
    }
}
