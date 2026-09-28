using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private LinuxAccessibleControls? accessibleControls;
    private readonly bool hiddenWindow;
    private readonly List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)> accessibleCommands = new();
    private readonly List<string> accessibleDescriptions = new();
    private bool drawingButtonLabel;
    private string AccessibleTextKey => FocusContext + ":" + ActiveTextEntryName;

    private void OpenAccessibleControls() => ShowAccessibleControls(hidden: hiddenWindow);
    private void ShowAccessibleControls(bool hidden)
    {
        if (IsLoading) return;
        if (accessibleControls is null && !LinuxAccessibleControls.TryCreate(out accessibleControls, out string? error))
        { statusMessage = error!; return; }
        if (!controlCenterVisible) OpenControlPage(ControlCenterPage.Overview);
        accessibleControls!.Show(hidden);
        DrawShell(); UpdateAccessibleControls();
    }
    private void AddAccessibleCommand(float x, float y, float width, float height, string label, bool selected, bool enabled, string? focusId)
    {
        if (!controlCenterVisible) return;
        bool navigation = x < 248 && y >= 224;
        string key = navigation ? "navigation:" + y : FocusContext + ":" + (focusId ?? "button") + $":{x}:{y}";
        string name = label == "×" ? "Return to game" : label;
        if (focusId?.StartsWith("text:", StringComparison.Ordinal) == true) name = focusId switch
        { "text:Title" => "Edit cartridge title", "text:Search" => "Search cartridges", "text:Cheat" => "Edit cheat code", _ => "Edit text" };
        bool? option = navigation ? (int)((y - 224) / 50) == (int)controlCenterPage : null;
        if (!navigation && controlCenterPage == ControlCenterPage.Display && y is 230 or 340 or 450) option = selected;
        if (!navigation && controlCenterPage == ControlCenterPage.Audio && y is 224 or 480) option = selected;
        if (!navigation && controlCenterPage == ControlCenterPage.Saves && ((showGallery && y == 260) || (!showGallery && !showBackups && y == 232 && x >= 300 && x <= 756))) option = selected;
        if (controlCenterPage == ControlCenterPage.Input && !showController && y >= 246 && y <= 476 && (x == 424 || x == 754))
        {
            int index = (x == 754 ? 6 : 0) + (int)((y - 246) / 46);
            if (index < BindingActions.Length) name = $"Map {BindingActions[index]}: {label}";
        }
        if (controlCenterPage == ControlCenterPage.Input && showController && y >= 290 && y <= 500 && (x == 400 || x == 790))
        {
            int index = (x == 790 ? 6 : 0) + (int)((y - 290) / 42);
            if (index < BindingActions.Length) name = $"Map controller {BindingActions[index]}: {label}";
        }
        if (focusId?.StartsWith("favorite:") == true) option = selected;
        if (controlCenterPage == ControlCenterPage.Library && x == 570 && y == 254) option = favoritesOnly;
        if (controlCenterPage == ControlCenterPage.System && x == 300 && y == 322) option = usingGameProfile;
        if (controlCenterPage == ControlCenterPage.System && x == 300 && y == 440) option = options.PauseOnFocusLoss;
        if (controlCenterPage == ControlCenterPage.System && x == 560 && y == 562) option = options.UseFirmware;
        if (controlCenterPage == ControlCenterPage.Diagnostics && x == 300 && y == 535) option = options.RecordDiagnostics;
        if (controlCenterPage == ControlCenterPage.Audio && label is "-1%" or "+1%") name = label == "-1%" ? "Decrease volume by 1 percent" : "Increase volume by 1 percent";
        if (focusId?.StartsWith("rename:") == true || focusId?.StartsWith("favorite:") == true)
        {
            var entry = libraryEntries.FirstOrDefault(item => focusId.EndsWith(item.Identity, StringComparison.Ordinal));
            if (entry is not null) name += ": " + entry.Title;
        }
        accessibleCommands.Add((new(key, name, enabled, option, navigation), new() { X = x, Y = y, W = width, H = height }));
    }
    private void DescribeAccessibleText(float x, float y, string text)
    {
        if (controlCenterVisible && !drawingButtonLabel && x >= 278 && y >= 180 && y < 636 && !string.IsNullOrWhiteSpace(text))
            accessibleDescriptions.Add(text);
    }
    private void UpdateAccessibleControls()
    {
        if (accessibleControls is not { IsOpen: true } panel) return;
        if (!controlCenterVisible) { panel.Hide(); return; }
        panel.CaptureKeys = rebindingAction is not null;
        panel.Update("AetherBoy — " + CurrentPageName,
            CurrentPageDescription + "\n" + string.Join("\n", accessibleDescriptions.Distinct()),
            loadError ?? statusMessage,
            accessibleCommands.Select(item => item.Command).ToArray(),
            ActiveTextEntryName is { } name ? new(AccessibleTextKey, name, ActiveTextValue, ActiveTextReadOnly) : null);
        panel.Pump();
        for (int i = 0; i < 16 && panel.TryTakeRequest(out string key, out string? value); i++)
        {
            // Reconcile after every callback, so a queued action from a previous page/ROM cannot target its replacement.
            DrawShell();
            if (key == "close") { CancelActiveText(); panel.Hide(); CloseControlCenter(); break; }
            if (key == "text:" + AccessibleTextKey && !ActiveTextReadOnly) ActiveTextValue = value ?? "";
            else if (key == "commit:" + AccessibleTextKey) CommitActiveText();
            else if (key == "cancel:" + AccessibleTextKey) CancelActiveText();
            else if (key == "key" && rebindingAction is not null)
            {
                string keyName = value switch { "Escape" => "Escape", "BackSpace" => "Backspace", "space" => "Space", _ => value ?? "" };
                SDL.Scancode code = SDL.GetScancodeFromName(keyName);
                if (code != SDL.Scancode.Unknown) HandleKeyboard(new SDL.KeyboardEvent { Scancode = code, Down = true }, true);
                else statusMessage = "That key has no SDL mapping. Try another key or press Escape.";
            }
            else
            {
                var command = accessibleCommands.FirstOrDefault(item => item.Command.Key == key && item.Command.Enabled);
                if (command.Command is not null) HandleMouseClick(command.Bounds.X + command.Bounds.W / 2, command.Bounds.Y + command.Bounds.H / 2);
            }
        }
    }
}
