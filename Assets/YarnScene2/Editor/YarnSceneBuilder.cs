using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Editor;

public static class YarnSceneBuilder
{
    public const string Folder = "Assets/YarnScene";
    public const string ScenePath = Folder + "/YarnCodecScene.unity";
    public const string ProjectPath = Folder + "/Dialogue.yarnproject";
    public const string CodecPath = Folder + "/SceneCodec.asset";

    const string Script =
@"title: Start
---
Alice: Welcome to the encrypted dialogue test. #line:04e5842
Bob: 秘密の合言葉は「剣魚」🐟 — Ünïcödé & ASCII ""quotes"" work. #line:0db4923
-> Ask about the passphrase #line:042f69f
    Alice: The passphrase is swordfish. #line:0985021
-> Say goodbye #line:0c5cf6a
    Alice: Goodbye! #line:0fcad2d
Bob: The end. #line:0e290a4
===
";

    [MenuItem("Tools/Yarn Codec Test/Build Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        // Only create the source files if they're missing, so edits (e.g. added line tags) survive a rebuild.
        if (!File.Exists($"{Folder}/Dialogue.yarn")) { File.WriteAllText($"{Folder}/Dialogue.yarn", Script); }
        AssetDatabase.Refresh();
        if (!File.Exists(ProjectPath)) File.WriteAllText(ProjectPath, "{\"projectFileVersion\": 3, \"sourceFiles\": [\"./Dialogue.yarn\"], \"excludeFiles\": [], \"localisation\": {\"en\": {}}, \"baseLanguage\": \"en\"}");

        if (AssetDatabase.LoadAssetAtPath<BundleIdSaltedCodec>(CodecPath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BundleIdSaltedCodec>(), CodecPath);
        }

        // Give the codec a stored salt (the codec refuses to run without one).
        var codecAsset = AssetDatabase.LoadAssetAtPath<BundleIdSaltedCodec>(CodecPath);
        var codecSo = new SerializedObject(codecAsset);
        if (string.IsNullOrEmpty(codecSo.FindProperty("fixedSalt").stringValue))
        {
            codecSo.FindProperty("fixedSalt").stringValue = "yarn-test-salt-1";
            codecSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(codecAsset);
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.ImportAsset(ProjectPath, ImportAssetOptions.ForceSynchronousImport);

        // Turn on encoding for the project.
        var importer = (YarnProjectImporter)AssetImporter.GetAtPath(ProjectPath);
        importer.stringTableCodecGUID = AssetDatabase.AssetPathToGUID(CodecPath);
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        var project = AssetDatabase.LoadAssetAtPath<YarnProject>(ProjectPath);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("Dialogue Runner");
        var runner = go.AddComponent<DialogueRunner>();
        var presenter = go.AddComponent<LineCapturePresenter>();

        var so = new SerializedObject(runner);
        so.FindProperty("yarnProject").objectReferenceValue = project;
        so.FindProperty("autoStart").boolValue = true;
        so.FindProperty("startNode").stringValue = "Start";
        var presenters = so.FindProperty("dialoguePresenters");
        presenters.arraySize = 1;
        presenters.GetArrayElementAtIndex(0).objectReferenceValue = presenter;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"YarnSceneBuilder: built {ScenePath} (run Tools/Yarn Codec Test/Check Encoding to verify)");
    }

    [MenuItem("Tools/Yarn Codec Test/Check Encoding")]
    public static void CheckEncoding()
    {
        var project = AssetDatabase.LoadAssetAtPath<YarnProject>(ProjectPath);
        var bytes = project.compiledYarnProgram;
        var plainTitle = System.Text.Encoding.UTF8.GetBytes("Start");
        bool titleVisible = System.Linq.Enumerable.Range(0, bytes.Length - plainTitle.Length).Any(i => System.Linq.Enumerable.SequenceEqual(new System.ArraySegment<byte>(bytes, i, plainTitle.Length), plainTitle));
        var json = JsonUtility.ToJson(project.baseLocalization);
        Debug.Log($"CHECK programCodec={(project.programCodec != null)} programBytes={bytes.Length} titleVisibleInBytes={titleVisible} " +
                  $"locJsonHasPlainText={json.Contains("swordfish")} lines={project.baseLocalization.GetLineIDs().Count()} " +
                  $"nodes=[{string.Join(",", project.NodeNames)}] sample=\"{project.baseLocalization.GetLocalizedString(project.baseLocalization.GetLineIDs().First())}\"");
    }

    [MenuItem("Tools/Yarn Codec Test/Build Scene Bundle (for leak check)")]
    public static void BuildSceneBundle()
    {
        var outDir = "BundleCheck";
        if (Directory.Exists(outDir)) { Directory.Delete(outDir, true); }
        Directory.CreateDirectory(outDir);
        var build = new AssetBundleBuild { assetBundleName = "yarnscene", assetNames = new[] { ScenePath } };
        var manifest = BuildPipeline.BuildAssetBundles(outDir, new[] { build }, BuildAssetBundleOptions.UncompressedAssetBundle, EditorUserBuildSettings.activeBuildTarget);
        Debug.Log($"BUNDLE manifest={(manifest != null)} dir={Path.GetFullPath(outDir)} target={EditorUserBuildSettings.activeBuildTarget}");
    }

    public static void SetCodecEnabled(bool enabled)
    {
        var importer = (YarnProjectImporter)AssetImporter.GetAtPath(ProjectPath);
        importer.stringTableCodecGUID = enabled ? AssetDatabase.AssetPathToGUID(CodecPath) : string.Empty;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    // Used by the leak-check script: builds the scene bundle with the codec off (control).
    public static void BuildBundleCodecOff() { SetCodecEnabled(false); BuildSceneBundle(); }
    public static void RestoreCodecOn() { SetCodecEnabled(true); }
}
