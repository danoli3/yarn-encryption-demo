using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Yarn.Unity;
using Yarn.Unity.Editor;

namespace YarnTest.Tests
{
    /// <summary>End-to-end tests: real .yarn + .yarnproject files imported by the real importer, with and without a codec.</summary>
    public class CodecImportPipelineTests
    {
        const string Folder = "Assets/CodecPipelineTestTemp";
        const string Secret1 = "The secret passphrase is swordfish";
        const string Secret2 = "秘密の合言葉は「剣魚」🐟 Ünïcödé";

        PipelineTestCodec codec;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            codec = ScriptableObject.CreateInstance<PipelineTestCodec>();
            AssetDatabase.CreateAsset(codec, $"{Folder}/Codec.asset");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        static bool Contains(byte[] haystack, string needle)
        {
            var n = Encoding.UTF8.GetBytes(needle);
            for (int i = 0; i + n.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < n.Length && haystack[i + j] == n[j]) { j++; }
                if (j == n.Length) { return true; }
            }
            return false;
        }

        // Creates and imports a project. Unless keepProjectDefault is set, the project is switched to
        // "Off" so tests don't depend on the Project Settings encryption default.
        string CreateProject(string name, bool withBaseLocalisation, bool keepProjectDefault = false)
        {
            File.WriteAllText($"{Folder}/{name}.yarn", $"title: Start\n---\nNarrator: {Secret1}\nNarrator: {Secret2}\n===\n");
            AssetDatabase.Refresh();
            var loc = withBaseLocalisation ? "{\"en\": {}}" : "{}";
            File.WriteAllText($"{Folder}/{name}.yarnproject",
                $"{{\"projectFileVersion\": 3, \"sourceFiles\": [\"./{name}.yarn\"], \"excludeFiles\": [], \"localisation\": {loc}, \"baseLanguage\": \"en\"}}");
            var path = $"{Folder}/{name}.yarnproject";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!keepProjectDefault) { SetCodec(path, false); }
            return path;
        }

        void SetCodec(string projectPath, bool enabled, string salt = "")
        {
            var importer = (YarnProjectImporter)AssetImporter.GetAtPath(projectPath);
            importer.codecMode = enabled ? YarnProjectImporter.CodecMode.Custom : YarnProjectImporter.CodecMode.Off;
            importer.stringTableCodecGUID = enabled ? AssetDatabase.AssetPathToGUID($"{Folder}/Codec.asset") : string.Empty;
            importer.codecSalt = salt;
            EditorUtility.SetDirty(importer); // SaveAndReimport only saves dirty importers
            importer.SaveAndReimport();
        }

        static YarnProject Load(string path) => AssetDatabase.LoadAssetAtPath<YarnProject>(path);

        static void AssertDecodedContent(YarnProject project)
        {
            Assert.That(project.NodeNames, Does.Contain("Start"), "program should parse and contain node 'Start'");
            var loc = project.baseLocalization;
            var ids = loc.GetLineIDs().ToList();
            Assert.AreEqual(2, ids.Count);
            var texts = ids.Select(loc.GetLocalizedString).ToList();
            // The string table keeps the speaker prefix as part of the line text.
            CollectionAssert.AreEquivalent(new[] { "Narrator: " + Secret1, "Narrator: " + Secret2 }, texts);
        }

        [TestCase(false, TestName = "DefaultLanguagePath")]
        [TestCase(true, TestName = "ExplicitBaseLocalisationPath")]
        public void Import_WithCodec_EncodesAndDecodes(bool withBaseLocalisation)
        {
            var path = CreateProject("Encoded", withBaseLocalisation);

            // Baseline: no codec -> plaintext everywhere.
            var plain = Load(path);
            Assert.IsNull(plain.programCodec);
            Assert.IsTrue(Contains(plain.compiledYarnProgram, "Start"), "baseline program should contain node title in plaintext");
            Assert.That(JsonUtility.ToJson(plain.baseLocalization), Does.Contain("swordfish"));
            AssertDecodedContent(plain);

            // With codec -> nothing readable in stored data, but decoding still works.
            SetCodec(path, true);
            var encoded = Load(path);
            Assert.AreEqual(codec, encoded.programCodec);
            Assert.IsFalse(Contains(encoded.compiledYarnProgram, "Start"), "encoded program must not expose node title");
            var json = JsonUtility.ToJson(encoded.baseLocalization);
            Assert.That(json, Does.Not.Contain("swordfish"));
            Assert.That(json, Does.Not.Contain("秘密"));
            Assert.That(json, Does.Not.Contain("Ünïcödé"));
            AssertDecodedContent(encoded);

            // Removing the codec returns to plaintext.
            SetCodec(path, false);
            var back = Load(path);
            Assert.IsNull(back.programCodec);
            Assert.That(JsonUtility.ToJson(back.baseLocalization), Does.Contain("swordfish"));
            AssertDecodedContent(back);
        }

        [Test]
        public void EncodedLine_IsBoundToItsLineID()
        {
            var path = CreateProject("Bound", false);
            SetCodec(path, true);
            var loc = Load(path).baseLocalization;
            var ids = loc.GetLineIDs().ToList();

            // Swap two encoded blobs between line IDs: decoding must fail, not return the other line.
            var entries = (System.Collections.Generic.IDictionary<string, Localization.LocalizationTableEntry>)
                typeof(Localization).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(loc);
            var a = entries[ids[0]];
            var b = entries[ids[1]];
            (a.encryptedString, b.encryptedString) = (b.encryptedString, a.encryptedString);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to decode text for"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to decode text for"));
            Assert.IsNull(loc.GetLocalizedString(ids[0]));
            Assert.IsNull(loc.GetLocalizedString(ids[1]));
        }

        [Test]
        public void CustomSalt_IsStoredAndRequiredToDecode()
        {
            var path = CreateProject("Salted", false);
            SetCodec(path, true, "salt-one");
            var project = Load(path);
            Assert.AreEqual("salt-one", project.codecSalt);
            AssertDecodedContent(project);

            // Changing the salt and reimporting re-encodes under the new salt and still decodes.
            SetCodec(path, true, "salt-two");
            project = Load(path);
            Assert.AreEqual("salt-two", project.codecSalt);
            AssertDecodedContent(project);

            // A mismatched salt (simulating stale data) fails with a message that says what to do.
            project.codecSalt = "salt-wrong";
            typeof(YarnProject).GetField("cachedProgram", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(project, null); // Program is cached after the first decode
            var ex = Assert.Throws<System.Security.Cryptography.CryptographicException>(() => { var _ = project.Program; });
            StringAssert.Contains("Reimport", ex.Message);
        }

        [Test]
        public void ProjectDefault_UsesProjectSettings_AndOffOverrides()
        {
            var settingsPath = System.IO.Path.Combine("ProjectSettings", "Packages", "dev.yarnspinner", "YarnSpinnerProjectSettings.json");
            var hadFile = File.Exists(settingsPath);
            var backup = hadFile ? File.ReadAllText(settingsPath) : null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                var guid = AssetDatabase.AssetPathToGUID($"{Folder}/Codec.asset");
                File.WriteAllText(settingsPath, $"{{\"encryptByDefault\": true, \"defaultCodecGuid\": \"{guid}\", \"defaultSalt\": \"proj-salt\"}}");

                var path = CreateProject("Defaulted", false, keepProjectDefault: true);
                // New projects default to "Project Default": the import above already used the settings.
                var project = Load(path);
                Assert.IsNotNull(project.programCodec, "Project Default with encryption enabled should encode");
                Assert.AreEqual("proj-salt", project.codecSalt);
                Assert.That(JsonUtility.ToJson(project.baseLocalization), Does.Not.Contain("swordfish"));
                AssertDecodedContent(project);

                // Off overrides the project default.
                SetCodec(path, false);
                project = Load(path);
                Assert.IsNull(project.programCodec);
                Assert.That(JsonUtility.ToJson(project.baseLocalization), Does.Contain("swordfish"));

                // With the default switched off, Project Default means no encoding.
                File.WriteAllText(settingsPath, "{\"encryptByDefault\": false}");
                var importer = (YarnProjectImporter)AssetImporter.GetAtPath(path);
                importer.codecMode = YarnProjectImporter.CodecMode.ProjectDefault;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Assert.IsNull(Load(path).programCodec);
            }
            finally
            {
                if (hadFile) { File.WriteAllText(settingsPath, backup); } else if (File.Exists(settingsPath)) { File.Delete(settingsPath); }
            }
        }

        [Test]
        public void ChangingProjectSettings_ReimportsExistingProjectsAutomatically()
        {
            var settingsPath = System.IO.Path.Combine("ProjectSettings", "Packages", "dev.yarnspinner", "YarnSpinnerProjectSettings.json");
            var hadFile = File.Exists(settingsPath);
            var backup = hadFile ? File.ReadAllText(settingsPath) : null;
            var registerMethod = typeof(YarnProjectImporter).Assembly.GetType("Yarn.Unity.Editor.StringTableCodecDependency")
                .GetMethod("Register", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, "{\"encryptByDefault\": false}");
                registerMethod.Invoke(null, null);

                var path = CreateProject("Auto", false, keepProjectDefault: true);   // Project Default, encryption off -> plaintext
                Assert.IsNull(Load(path).programCodec);
                Assert.That(JsonUtility.ToJson(Load(path).baseLocalization), Does.Contain("swordfish"));

                // Turn encryption on in the settings; no manual reimport of the Yarn Project.
                var guid = AssetDatabase.AssetPathToGUID($"{Folder}/Codec.asset");
                File.WriteAllText(settingsPath, $"{{\"encryptByDefault\": true, \"defaultCodecGuid\": \"{guid}\", \"defaultSalt\": \"auto-salt\"}}");
                registerMethod.Invoke(null, null);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var project = Load(path);
                Assert.IsNotNull(project.programCodec, "the project should have been reimported with the new default");
                Assert.AreEqual("auto-salt", project.codecSalt);
                Assert.That(JsonUtility.ToJson(project.baseLocalization), Does.Not.Contain("swordfish"));
                AssertDecodedContent(project);
            }
            finally
            {
                if (hadFile) { File.WriteAllText(settingsPath, backup); } else if (File.Exists(settingsPath)) { File.Delete(settingsPath); }
                registerMethod.Invoke(null, null);
            }
        }
    }
}
