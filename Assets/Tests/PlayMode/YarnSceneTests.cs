using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yarn.Unity;

namespace YarnTest.PlayModeTests
{
    /// <summary>Runs each real scene and checks the player sees decoded text and the stored data matches the project's codec setting.</summary>
    public class YarnSceneTests
    {
        const string Scene1 = "Assets/YarnScene/YarnCodecScene.unity";
        const string Scene2 = "Assets/YarnScene2/YarnCodecScene2.unity";

        static IEnumerator Load(string path)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        }

        // The two scenes use different copies of the presenter script, so go through the shared base class.
        static DialoguePresenterBase FindPresenter() => Object.FindObjectsOfType<DialoguePresenterBase>().FirstOrDefault(p => p.GetType().Name == "LineCapturePresenter");
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name).GetValue(o);

        static bool Contains(byte[] haystack, string needle)
        {
            var n = System.Text.Encoding.UTF8.GetBytes(needle);
            for (int i = 0; i + n.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < n.Length && haystack[i + j] == n[j]) { j++; }
                if (j == n.Length) { return true; }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator Scene_RunsToCompletion_WithDecodedText([ValueSource(nameof(Scenes))] string scene)
        {
            yield return Load(scene);
            var runner = Object.FindAnyObjectByType<DialogueRunner>();
            var presenter = FindPresenter();
            Assert.IsNotNull(runner, $"{scene}: should contain a DialogueRunner");
            Assert.IsNotNull(presenter, $"{scene}: should contain a LineCapturePresenter");
            presenter.GetType().GetField("secondsPerLine").SetValue(presenter, 0f);

            var timeout = Time.realtimeSinceStartup + 20f;
            while (!Field<bool>(presenter, "Completed") && Time.realtimeSinceStartup < timeout) { yield return null; }

            Assert.IsTrue(Field<bool>(presenter, "Completed"), $"{scene}: dialogue should finish");
            var lines = Field<List<string>>(presenter, "Lines");
            Assert.IsNotEmpty(lines, $"{scene}: should have shown lines");
            Assert.That(lines, Has.None.Empty, $"{scene}: every line should have decoded text");
            // Decoded text is human readable, not binary/garbled.
            Assert.IsFalse(string.Join("", lines).Contains("\uFFFD"), $"{scene}: replacement character found"); // ordinal check; NUnit's Contain is culture-sensitive
            Debug.Log($"[scene-test] {scene}: project='{runner.YarnProject.name}' encoded={runner.YarnProject.programCodec != null} salt='{runner.YarnProject.codecSalt}' lines={lines.Count}");
        }

        [UnityTest]
        public IEnumerator Scene_StoredData_MatchesItsCodecSetting([ValueSource(nameof(Scenes))] string scene)
        {
            yield return Load(scene);
            var project = Object.FindAnyObjectByType<DialogueRunner>().YarnProject;
            var loc = project.baseLocalization;
            var firstText = loc.GetLocalizedString(loc.GetLineIDs().First());
            Assert.IsNotNull(firstText);
            var json = JsonUtility.ToJson(loc);
            var titleInProgram = Contains(project.compiledYarnProgram, project.NodeNames.First());

            if (project.programCodec != null)
            {
                Assert.That(json, Does.Not.Contain(firstText), $"{scene}: encoded project must not store line text in plaintext");
                Assert.IsFalse(titleInProgram, $"{scene}: encoded program must not expose node titles");
            }
            else
            {
                Assert.That(json, Does.Contain(firstText), $"{scene}: unencoded project should store plaintext (sanity check)");
                Assert.IsTrue(titleInProgram, $"{scene}: unencoded program should contain the node title (sanity check)");
            }
        }

        [UnityTest]
        public IEnumerator MatrixStyle_IsEnabledInBothScenes([ValueSource(nameof(Scenes))] string scene)
        {
            yield return Load(scene);
            Assert.IsTrue(Field<bool>(FindPresenter(), "matrixStyle"), $"{scene}: should use the green Matrix style");
        }

        static string[] Scenes = { Scene1, Scene2 };
    }
}
