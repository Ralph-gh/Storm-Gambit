using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    public static void Validate(LobbyBrowserUI browser)
    {
        foreach (var field in typeof(LobbyBrowserUI).GetFields())
            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) &&
                (UnityEngine.Object)field.GetValue(browser) == null)
                throw new InvalidOperationException("Lobby UI reference missing: " + field.Name);
    }
}
