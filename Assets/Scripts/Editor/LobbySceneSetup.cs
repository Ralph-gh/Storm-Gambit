using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;
using UnityEditor.Events;

[InitializeOnLoad]
public static class LobbySceneSetup
{
    private const string ScenePath = "Assets/Scenes/LobbyScene.unity";

    static LobbySceneSetup()
    {
        EditorApplication.delayCall += InstallIfNeeded;
        EditorApplication.update += CheckThemeRequest;
    }

    private static void CheckThemeRequest()
    {
        const string request = ".utmp/lobby-theme-bake.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try
        {
            Bake();
            LobbyValidationRunner.Preview();
            File.WriteAllText(".utmp/lobby-bake.result", "Lobby UI saved and previews rendered.");
        }
        catch (Exception error)
        {
            File.WriteAllText(".utmp/lobby-bake.result", error.ToString());
            Debug.LogException(error);
        }
    }

    private static void InstallIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += InstallIfNeeded;
            return;
        }
        if (File.Exists(ScenePath) && !File.ReadAllText(ScenePath).Contains("m_Name: Multiplayer Lobby UI"))
            Bake();
    }

    [MenuItem("Tools/Storm Gambit/Rebuild Multiplayer Lobby UI")]
    public static void Bake()
    {
        EnsureThemeAssets();
        var original = SceneManager.GetActiveScene();
        var lobbyScene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !lobbyScene.isLoaded;
        if (!opened && lobbyScene.isDirty)
        {
            Debug.LogWarning("[Lobby UI] Save LobbyScene before rebuilding its UI.");
            return;
        }
        if (opened) lobbyScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(lobbyScene);
            LobbySceneController controller = null;
            foreach (var root in lobbyScene.GetRootGameObjects())
            {
                if (root.name == "Multiplayer Lobby UI") { UnityEngine.Object.DestroyImmediate(root); continue; }
                if (root.TryGetComponent<Canvas>(out _)) root.SetActive(false);
                if (root.TryGetComponent<LobbySceneController>(out var found)) controller = found;
            }
            if (controller == null) throw new InvalidOperationException("LobbySceneController is missing from LobbyScene.");
            var art = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Lobby of the storm 1.png");
            var browser = LobbyUIBuilder.Build(controller, art);
            IntegrateNavigation(lobbyScene, browser);
            Validate(browser);
            EditorSceneManager.MarkSceneDirty(lobbyScene);
            EditorSceneManager.SaveScene(lobbyScene);
            Debug.Log("[Lobby UI] Installed editable game browser, hosting form, waiting room and password dialog into LobbyScene.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(lobbyScene, true);
        }
    }

    private static void EnsureThemeAssets()
    {
        const string longFramePath = "Assets/Resources/LobbyLongButtonFrame.asset";
        if (AssetDatabase.LoadAssetAtPath<Sprite>(longFramePath) == null)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/UI ART/Lobby Long Button v3 - No Pieces.png")
                .OfType<Sprite>().Single(sprite => sprite.name == "LobbyLongButtonV3");
            var frame = Sprite.Create(source.texture, source.rect, new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(170, 24, 170, 24));
            frame.name = "LobbyLongButtonFrame";
            AssetDatabase.CreateAsset(frame, longFramePath);
        }
        const string framePath = "Assets/Resources/LobbyButtonFrame.asset";
        if (AssetDatabase.LoadAssetAtPath<Sprite>(framePath) == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/newPossible UI Button-storm Gambit.png");
            var frame = Sprite.Create(source.texture, source.rect, new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(160, 70, 160, 70));
            frame.name = "LobbyButtonFrame";
            AssetDatabase.CreateAsset(frame, framePath);
        }
        const string glassPath = "Assets/Resources/LobbyGlass.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(glassPath) == null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/LobbyGlass.shader");
            AssetDatabase.CreateAsset(new Material(shader) { name = "LobbyGlass" }, glassPath);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Storm Gambit/Integrate Original Lobby Menus")]
    public static void InstallNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before installing lobby navigation.");
        EnsureThemeAssets();
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save LobbyScene before installing navigation.");
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var browser = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LobbyBrowserUI>(true)).Single();
            IntegrateNavigation(scene, browser);
            Validate(browser);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save LobbyScene.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("[Lobby UI] Original landing menu connected to new Host and Join screens.");
    }

    private static void IntegrateNavigation(Scene scene, LobbyBrowserUI browser)
    {
        var navigation = browser.controller.GetComponent<LobbyMenuNavigation>();
        if (navigation == null) navigation = browser.controller.gameObject.AddComponent<LobbyMenuNavigation>();
        if (navigation.landing == null)
        {
            var archive = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Archive/LobbyScene_Legacy.unity");
            try
            {
                // Copy only the original Canvas: never copy its NetworkManager, bootstrap, or controller.
                var source = archive.GetRootGameObjects().Single(root => root.name == "Canvas");
                var landing = UnityEngine.Object.Instantiate(source);
                SceneManager.MoveGameObjectToScene(landing, scene);
                landing.name = "Lobby of the Storm - Original Menu";
                navigation.landing = landing;
            }
            finally { EditorSceneManager.ClosePreviewScene(archive); }
        }
        navigation.browser = browser;
        browser.navigation = navigation;
        var buttons = navigation.landing.GetComponentsInChildren<UIButton>(true);
        navigation.hostButton = buttons.Single(button => button.name == "Host");
        navigation.joinButton = buttons.Single(button => button.name == "JoinButton");
        navigation.mainMenuButton = buttons.Single(button => button.name == "BackButton");
        // Replace the old Relay callbacks with routes; all existing visuals remain untouched.
        foreach (var button in new[] { navigation.hostButton, navigation.joinButton, navigation.mainMenuButton })
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
        // Keep the old objects for reference, but code entry no longer belongs on this landing menu.
        foreach (var child in navigation.landing.GetComponentsInChildren<Transform>(true))
            if (child.name == "JoinCodePanel" || child.name == "JoinCodeInput" || child.name == "StatusText")
                child.gameObject.SetActive(false);

        var frame = Resources.Load<Sprite>("LobbyLongButtonFrame");
        foreach (var button in new[] { browser.emptyHostButton, browser.createButton, browser.passwordJoinButton, browser.backButton })
        {
            var image = button.GetComponent<Image>();
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 3;
            image.color = Color.white;
        }
        foreach (var button in browser.GetComponentsInChildren<UIButton>(true))
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.color = LobbyUIBuilder.MenuTextGold;
        browser.backButton.GetComponentInChildren<TMP_Text>(true).text = "BACK TO LOBBY";
        browser.gameObject.SetActive(false);
        navigation.landing.SetActive(true);
    }

    public static void Validate(LobbyBrowserUI browser)
    {
        foreach (var field in typeof(LobbyBrowserUI).GetFields())
            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) &&
                (UnityEngine.Object)field.GetValue(browser) == null)
                throw new InvalidOperationException("Lobby UI reference missing: " + field.Name);
    }
}
