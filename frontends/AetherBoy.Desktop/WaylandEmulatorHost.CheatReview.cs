using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showCheatReview, confirmCheatValues;
    private int cheatReviewLine;
    private string? cheatReviewKey;
    private CheatInputReview cheatInputReview = new([], [], []);

    private void OpenCheatReview()
    {
        showCheatReview = true; cheatReviewKey = null; statusMessage = "";
        GetCheatReview();
    }

    private CheatInputReview GetCheatReview()
    {
        bool gba = session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true;
        string[] previous = session?.LatestSnapshot.Cheats.Select(c => c.Code).ToArray() ?? [];
        string key = $"{gba}:{(int)cheatFormat}:{cheatCode}\0" + string.Join('\0', previous);
        if (key != cheatReviewKey)
        {
            cheatReviewKey = key; confirmCheatValues = false;
            cheatInputReview = CheatCodeInput.Review(cheatCode, gba, cheatFormat, previous);
            int problem = cheatInputReview.Issues.FirstOrDefault()?.Line ?? 0;
            cheatReviewLine = Math.Max(0, cheatInputReview.Lines.ToList().FindIndex(l => l.Number == problem));
        }
        return cheatInputReview;
    }

    private void DrawCheatReviewPage()
    {
        var review = GetCheatReview();
        bool gba = session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true;
        Ink(300, 198, UiText.Get("Prüfung der eingefügten Codezeilen"), 18, Colors.Cyan, true);
        DrawTextEntry(TextField.Cheat, 300, 236, 810, 42, UiText.Get("ENTER CHEAT CODE"), session is not null && !IsOnlineLink);
        if (gba)
        {
            string[] formats = [UiText.Get("Format automatisch"), "CodeBreaker", "GameShark v1/v2", UiText.Get("GameShark v1/v2 (roh)"), "Action Replay v3", UiText.Get("Action Replay v3 (roh)")];
            ActionButton(300, 288, 250, 36, formats[(int)cheatFormat], () => cheatFormat = (CheatCodeFormat)(((int)cheatFormat + 1) % formats.Length));
            if (review.Candidates.Contains(CheatCodeFormat.GameShark))
                ActionButton(570, 288, 250, 36, "GameShark v1/v2", () => cheatFormat = CheatCodeFormat.GameShark);
            if (review.Candidates.Contains(CheatCodeFormat.ActionReplayV3))
                ActionButton(840, 288, 270, 36, "Action Replay v3", () => cheatFormat = CheatCodeFormat.ActionReplayV3);
        }
        var line = review.Lines.ElementAtOrDefault(cheatReviewLine);
        Ink(300, 341, line is null ? UiText.Get("Hinweise zur Code-Eingabe") : UiText.Format("Zeile {0}: {1}", line.Number, textRenderer.Fit(line.Text, 670, 14)), 14, Colors.Text);
        var issues = review.Issues.Where(i => i.Line == 0 || i.Line == line?.Number).ToArray();
        string message = issues.Length == 0 ? review.RequiresSessionValidation ? review.Summary : UiText.Get("Eingabe geprüft; keine Bestätigung der Spielkompatibilität. Der Code bleibt unverändert.")
            : string.Join(" ", issues.Select(i => i.Message));
        DrawSettingsParagraph(300, 375, message, 810, 14, Colors.Text);
        ActionButton(300, 480, 160, 36, UiText.Get("PREVIOUS"), () => cheatReviewLine--, enabled: cheatReviewLine > 0);
        ActionButton(474, 480, 160, 36, UiText.Get("NEXT"), () => cheatReviewLine++, enabled: cheatReviewLine + 1 < review.Lines.Count);
        ActionButton(650, 480, 230, 36, UiText.Get("Zeile bearbeiten"), () =>
        {
            if (line is null) return;
            BeginTextEditing(TextField.Cheat); textEditor.Select(line.Start, line.Start + line.Length);
        }, enabled: line is not null);
        if (review.NeedsValueConfirmation)
            ActionButton(300, 533, 810, 38, UiText.Get("AAAA ausdrücklich als Hexwert verwenden"),
                () => confirmCheatValues = !confirmCheatValues, confirmCheatValues);
        ActionButton(300, 585, 350, 40, UiText.Get("Code hinzufügen"), AddReviewedCheat,
            enabled: session is not null && !IsOnlineLink && !review.HasErrors && (!review.NeedsValueConfirmation || confirmCheatValues));
        ActionButton(760, 585, 350, 40, UiText.Get("Zurück ohne Hinzufügen"), () =>
        { showCheatReview = false; editingCheat = false; SDL.StopTextInput(window); });
    }

    private void AddReviewedCheat()
    {
        var review = GetCheatReview();
        if (session is null || IsOnlineLink || review.HasErrors || review.NeedsValueConfirmation && !confirmCheatValues) return;
        TryUiAction(() =>
        {
            try
            {
                session.AddCheatAsync("Cheat", session.LatestSnapshot.Rom?.IsGameBoyAdvance == true
                    ? CheatCodeInput.Prepare(cheatCode, cheatFormat) : cheatCode).GetAwaiter().GetResult();
                editingCheat = false; SDL.StopTextInput(window); cheatCode = ""; showCheatReview = false;
            }
            catch (FormatException error)
            {
                cheatInputReview = review with { Issues = [new(0, false, UiText.TechnicalDetails(error.Message))],
                    Candidates = CheatCodeInput.Candidates(error), RequiresSessionValidation = false };
                throw;
            }
        }, UiText.Get("Code hinzugefügt und für diese Spielsitzung aktiviert."));
    }
}
