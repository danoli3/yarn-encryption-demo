# yarn-encryption-demo

A Unity project that demonstrates **encrypting Yarn Spinner dialogue in built games**, using the string-table codec added in [danoli3/YarnSpinner-Unity](https://github.com/danoli3/YarnSpinner-Unity/tree/feature/string-table-codec) (branch `feature/string-table-codec`).

Without a codec, a Yarn Project stores its line text and compiled program as plain data that asset-extraction tools can read. With a codec, both are stored encoded and decoded one line at a time at runtime.

> **This deters casual extraction, not a determined attacker.** The key material lives in your game's code, and the text must exist in memory while a line is shown, so someone who decompiles the game or hooks the running process can still recover the dialogue.

## Requirements

- Unity **2022.3.62f3** (other 2022.3 versions should work).
- The Yarn Spinner package is pulled from the fork branch above by `Packages/manifest.json`. This is temporary, until the feature is merged upstream.

## What's in here

| Path | What it is |
|---|---|
| `Assets/YarnScene/` | Scene 1 (`YarnCodecScene`), a `SceneCodec` codec asset and the Yarn Project it uses |
| `Assets/YarnScene2/` | Scene 2 (`YarnCodecScene2`) with its own codec asset and a second Yarn Project |
| `Assets/Scripts/SceneSwitcher.cs` | Esc quits; Left/Right arrow or `<` `>` switch between the two scenes |
| `Assets/Tests/` | EditMode import-pipeline tests and PlayMode scene tests |
| `ProjectSettings/Packages/dev.yarnspinner/` | The project-wide encryption default (codec + salt) |

Both scenes run a short dialogue through a small presenter that shows the **decoded** lines in green on a black screen.

## Three ways to turn encryption on

1. **Project-wide default**: *Project Settings → Yarn Spinner → Dialogue encryption*. Tick *Encrypt dialogue by default*, pick a codec asset and a salt. Every Yarn Project set to *Project Default* uses it.
2. **Per project**: select a `.yarnproject`, set *String Encoding* to **Custom**, then choose a codec and a salt.
3. **Off**: set *String Encoding* to **Off** on a project to override the default.

Scene 1's project uses *Project Default*. `Assets/YarnScene2/Dialogue.yarnproject` is set to *Custom*; assign it to scene 2's Dialogue Runner to see per-project encryption (scene 2's runner currently points at the same project as scene 1).

## The codec

`Assets/YarnScene/BundleIdSaltedCodec.cs` is a sample subclass of `AesHmacStringTableCodec` (AES-256-CBC + HMAC-SHA256, with a separate key derived for every line). Its master key is two byte arrays XOR-ed together, mixed with a salt stored in the codec asset.

- **Replace `PartA` / `PartB` with your own random bytes** before using this for anything real. The values here are placeholders.
- *Include Bundle Id* is off by default. The bundle ID is public, so it adds little, and it must be identical at import and at runtime.
- The salt is **not secret**. It separates projects from each other and lets you rotate keys by changing it.
- After changing a codec asset, save it so the Yarn Projects reimport.

## Running the tests

Open *Window → General → Test Runner*.

- **EditMode**: `CodecImportPipelineTests` imports real `.yarn` and `.yarnproject` files with the codec on and off, and checks that nothing readable is stored and that decoding returns the exact text. The package's own tests are included through `testables`.
- **PlayMode**: `YarnSceneTests` runs each scene to completion and checks the decoded text; `SceneSwitchTests` checks the scene switcher.

## Checking a build for plaintext

Make a build, then search its data for a phrase from your dialogue:

```sh
grep -r "swordfish" YourApp.app/Contents/Resources/Data
```

With encryption on there should be no match in the project's own data (a match inside a framework DLL is a coincidence, for example a colour name). Turn encryption off and rebuild to see the control case: the text is there. Line IDs, the salt and the Dialogue Runner's start-node name are visible by design.

## Controls

| Key | Action |
|---|---|
| `Esc` | Quit (stops Play mode in the Editor) |
| `←` / `→` or `<` / `>` | Switch between scene 0 and scene 1 |


## LLM Usage
All code and bugs copyright to me - MIT -
LLM usage for unit tests, git management, documentation 
