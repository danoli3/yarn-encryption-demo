using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace YarnTest.PlayModeTests
{
    public class SceneSwitchTests
    {
        const string Scene1 = "Assets/YarnScene/YarnCodecScene.unity";
        const string Scene2 = "Assets/YarnScene2/YarnCodecScene2.unity";

        [Test]
        public void NextIndex_WrapsBetweenZeroAndOne()
        {
            Assert.AreEqual(1, SceneSwitcher.NextIndex(0, +1, 2));
            Assert.AreEqual(0, SceneSwitcher.NextIndex(1, +1, 2));
            Assert.AreEqual(1, SceneSwitcher.NextIndex(0, -1, 2));
            Assert.AreEqual(0, SceneSwitcher.NextIndex(1, -1, 2));
            Assert.AreEqual(2, SceneSwitcher.NextIndex(0, -1, 3));
        }

        [Test]
        public void BothScenesAreInBuildSettings_AtIndexZeroAndOne()
        {
            // (The test runner may add its own temporary scene, so look indices up by path.)
            var a = SceneUtility.GetBuildIndexByScenePath(Scene1);
            var b = SceneUtility.GetBuildIndexByScenePath(Scene2);
            Assert.GreaterOrEqual(a, 0, "scene 1 must be in Build Settings");
            Assert.GreaterOrEqual(b, 0, "scene 2 must be in Build Settings");
            Assert.AreNotEqual(a, b);
        }

        static IEnumerator WaitForActive(string path)
        {
            var timeout = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().path != path && Time.realtimeSinceStartup < timeout) { yield return null; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Step_MovesToNextAndPreviousScene_AndSwitcherSurvives()
        {
            var a = SceneUtility.GetBuildIndexByScenePath(Scene1);
            var b = SceneUtility.GetBuildIndexByScenePath(Scene2);
            Assert.AreEqual(a + 1, b, "scene 2 should directly follow scene 1");

            yield return SceneManager.LoadSceneAsync(a);
            Assert.AreEqual(Scene1, SceneManager.GetActiveScene().path);
            Assert.IsNotNull(SceneSwitcher.Instance, "the switcher installs itself automatically");

            SceneSwitcher.Step(+1);                       // right arrow / '>'
            yield return WaitForActive(Scene2);
            Assert.AreEqual(Scene2, SceneManager.GetActiveScene().path);

            SceneSwitcher.Step(-1);                       // left arrow / '<'
            yield return WaitForActive(Scene1);
            Assert.AreEqual(Scene1, SceneManager.GetActiveScene().path);

            Assert.IsNotNull(SceneSwitcher.Instance, "the switcher survives scene loads");
        }
    }
}
