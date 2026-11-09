using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Installs itself in every scene: Esc quits, Left/Right arrows (or &lt; and &gt;) switch between the scenes in Build Settings.</summary>
public class SceneSwitcher : MonoBehaviour
{
    public static SceneSwitcher Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (Instance != null) { return; }
        var go = new GameObject("Scene Switcher");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SceneSwitcher>();
    }

    /// <summary>Quits the player, or stops Play mode in the Editor.</summary>
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Returns the build index <paramref name="delta"/> places from <paramref name="current"/>, wrapping around <paramref name="count"/> scenes.</summary>
    public static int NextIndex(int current, int delta, int count) => ((current + delta) % count + count) % count;

    /// <summary>Loads the scene <paramref name="delta"/> places after the current one in Build Settings, wrapping around.</summary>
    public static void Step(int delta)
    {
        var count = SceneManager.sceneCountInBuildSettings;
        if (count < 2)
        {
            Debug.LogWarning("Scene Switcher: add both scenes to File > Build Settings to switch between them.");
            return;
        }
        var current = SceneManager.GetActiveScene().buildIndex;
        if (current < 0) { current = 0; }
        SceneManager.LoadScene(NextIndex(current, delta, count));
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { Quit(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.Greater)) { Step(+1); }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.Less)) { Step(-1); }
    }
#else
    private void Awake() { Debug.LogWarning("Scene Switcher needs the legacy Input Manager (Project Settings > Player > Active Input Handling)."); }
#endif

    private void OnGUI()
    {
        GUI.depth = -1000;   // draw on top of the presenters
        var count = SceneManager.sceneCountInBuildSettings;
        var label = $"Esc: quit    Left/Right or < >: switch scene    (scene {SceneManager.GetActiveScene().buildIndex + 1}/{count})";
        GUI.Label(new Rect(20, Screen.height - 30, Screen.width - 40, 24), label);
    }
}
