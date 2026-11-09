using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

/// <summary>A minimal dialogue presenter that records every line and option it receives and shows them with OnGUI.</summary>
public class LineCapturePresenter : DialoguePresenterBase
{
    [Tooltip("How long each line stays on screen. Use 0 for tests.")]
    public float secondsPerLine = 1.5f;

    [Tooltip("Which option to pick automatically when options are shown.")]
    public int optionToSelect = 0;

    [Tooltip("Black screen with bright green, centered, Matrix-style text.")]
    public bool matrixStyle = false;

    public readonly List<string> Lines = new List<string>();
    public readonly List<string> Options = new List<string>();
    public bool Started;
    public bool Completed;

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        Lines.Add(line.Text.Text);
        var end = Time.realtimeSinceStartup + secondsPerLine;
        while (Time.realtimeSinceStartup < end && !token.NextContentToken.IsCancellationRequested)
        {
            await YarnTask.Yield();
        }
    }

    public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
    {
        foreach (var option in dialogueOptions) { Options.Add(option.Line.Text.Text); }
        await YarnTask.Delay((int)(secondsPerLine * 1000));
        return dialogueOptions[Mathf.Clamp(optionToSelect, 0, dialogueOptions.Length - 1)];
    }

    public override YarnTask OnDialogueStartedAsync() { Started = true; return YarnTask.CompletedTask; }
    public override YarnTask OnDialogueCompleteAsync() { Completed = true; return YarnTask.CompletedTask; }

    // Matrix look: black screen, bright green monospace text, centered; the newest line is brightest.
    static readonly Color Bright = new Color(0.00f, 1.00f, 0.25f, 1f);
    static readonly Color Dim = new Color(0.00f, 0.55f, 0.14f, 1f);
    static readonly Color Glow = new Color(0.00f, 0.35f, 0.08f, 0.9f);
    GUIStyle style;
    Font font;
    int styleHeight;

    private void EnsureStyle()
    {
        if (style != null && styleHeight == Screen.height) { return; }
        // Monospace first; the other names are fallbacks for Japanese and symbols.
        font = font != null ? font : Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Hiragino Sans", "Apple Symbols", "Courier New" }, 32);
        style = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = Mathf.Max(14, Screen.height / 24),
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = false,
        };
        styleHeight = Screen.height;
    }

    private void DrawCentered(Rect area, string text, Color color)
    {
        style.normal.textColor = Glow;
        GUI.Label(new Rect(area.x + 2, area.y + 2, area.width, area.height), text, style);   // soft glow
        style.normal.textColor = color;
        GUI.Label(area, text, style);
    }

    private void DrawMatrix()
    {
        EnsureStyle();
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);

        var width = Screen.width * 0.8f;
        var x = (Screen.width - width) / 2f;
        var lineHeight = style.fontSize * 2.2f;

        // Everything shown so far, then any options, as one block centered on the screen.
        var rows = new List<(string text, Color color)>();
        for (int i = 0; i < Lines.Count; i++) { rows.Add((Lines[i], i == Lines.Count - 1 ? Bright : Dim)); }
        if (Options.Count > 0 && !Completed)
        {
            rows.Add(("", Dim));
            foreach (var option in Options) { rows.Add(("> " + option, Dim)); }
        }
        if (Completed) { rows.Add(("", Dim)); rows.Add(("[ dialogue complete ]", Bright)); }

        var top = (Screen.height - rows.Count * lineHeight) / 2f;
        for (int i = 0; i < rows.Count; i++)
        {
            DrawCentered(new Rect(x, top + i * lineHeight, width, lineHeight), rows[i].text, rows[i].color);
        }
    }

    private void DrawClassic()
    {
        GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
        GUILayout.Label($"Decoded lines ({Lines.Count}):");
        foreach (var l in Lines) { GUILayout.Label(l); }
        if (Options.Count > 0) { GUILayout.Label("Options shown: " + string.Join(" | ", Options)); }
        if (Completed) { GUILayout.Label("-- dialogue complete --"); }
        GUILayout.EndArea();
    }

    private void OnGUI()
    {
        if (matrixStyle) { DrawMatrix(); } else { DrawClassic(); }
    }
}
