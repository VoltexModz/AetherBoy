using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private LinuxAccessibleControls? accessibleControls;
    private readonly bool hiddenWindow;
    private readonly List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)> accessibleCommands = new();
    private readonly List<string> accessibleDescriptions = new();
    private bool drawingButtonLabel;
    private string AccessibleTextKey => ActiveTextField == TextField.SettingsSearch ? "settings-search" : FocusContext + ":" + ActiveTextEntryName;

    private void OpenAccessibleControls() => ShowAccessibleControls(hidden: hiddenWindow);
    private void ShowAccessibleControls(bool hidden)
    {
        if (IsLoading && archiveSelection is null) return;
        if (accessibleControls is null && !LinuxAccessibleControls.TryCreate(out accessibleControls, out string? error))
        { statusMessage = error!; return; }
        if (!controlCenterVisible && archiveSelection is null) OpenControlPage(ControlCenterPage.Overview);
        accessibleControls!.Show(hidden);
        DrawShell(); UpdateAccessibleControls();
    }
    private void AddAccessibleCommand(float x, float y, float width, float height, string label, bool selected, bool enabled, string? focusId)
    {
        if (!controlCenterVisible && archiveSelection is null) return;
        bool navigation = x < 248 && y >= 224;
        string key = navigation ? "navigation:" + y : FocusContext + ":" + (focusId ?? "button") + $":{x}:{y}";
        string name = label == "×" ? global::AetherBoy.Runtime.Localization.UiText.Get("Return to game") : label;
        if (focusId?.StartsWith("text:", StringComparison.Ordinal) == true) name = focusId switch
        { "text:Title" => global::AetherBoy.Runtime.Localization.UiText.Get("Edit cartridge title"), "text:Tags" => global::AetherBoy.Runtime.Localization.UiText.Get("Edit cartridge tags"), "text:Search" => global::AetherBoy.Runtime.Localization.UiText.Get("Search cartridges"), "text:SettingsSearch" => global::AetherBoy.Runtime.Localization.UiText.Get("Search settings"), "text:Cheat" => global::AetherBoy.Runtime.Localization.UiText.Get("Edit cheat code"), _ => global::AetherBoy.Runtime.Localization.UiText.Get("Edit text") };
        bool? option = navigation ? selected : null;
        if (!navigation && controlCenterPage == ControlCenterPage.Audio && y is 224 or 480) option = selected;
        if (!navigation && controlCenterPage == ControlCenterPage.Saves && ((showGallery && y == 260) || (!showGallery && !showBackups && y == 232 && x >= 300 && x <= 756))) option = selected;
        if (controlCenterPage == ControlCenterPage.Input && !showController && !showInputShortcuts && y >= 276 && y <= 496 && (x == 424 || x == 754))
        {
            int index = (x == 754 ? 6 : 0) + (int)((y - 276) / 44);
            if (index < BindingActions.Length) name = global::AetherBoy.Runtime.Localization.UiText.Format("Map {0}: {1}", global::AetherBoy.Runtime.Localization.UiLabels.Input(BindingActions[index].ToString()), label);
        }
        if (controlCenterPage == ControlCenterPage.Input && showController && !showControllerStick && y >= 297 && y <= 512 && (x == 400 || x == 814))
        {
            int index = (x == 814 ? 6 : 0) + (int)((y - 297) / 43);
            if (index < BindingActions.Length) name = global::AetherBoy.Runtime.Localization.UiText.Format("Map controller {0}: {1}", global::AetherBoy.Runtime.Localization.UiLabels.Input(BindingActions[index].ToString()), label);
        }
        if (focusId?.StartsWith("option:") == true) option = selected;
        if (focusId == "setting:auto-pause") { option = options.PauseOnFocusLoss; name = global::AetherBoy.Runtime.Localization.UiText.Get("Pause when unfocused: ") + label; }
        if (focusId == "setting:text-size") name = global::AetherBoy.Runtime.Localization.UiText.Get("Text size: ") + label;
        if (focusId?.StartsWith("result:") == true && Enum.TryParse<LinuxSettingsDestination>(focusId[7..], out var destination))
            name = global::AetherBoy.Runtime.Localization.UiText.Get("Open ") + LinuxSettingsCatalog.Entries.First(entry => entry.Destination == destination).Title;
        if (focusId?.StartsWith("favorite:") == true) option = selected;
        if (controlCenterPage == ControlCenterPage.Library && x == 570 && y == 254) option = favoritesOnly;
        if (controlCenterPage == ControlCenterPage.Diagnostics && x == 300 && y == 535) option = options.RecordDiagnostics;
        if (controlCenterPage == ControlCenterPage.Audio && label is "-1%" or "+1%") name = label == "-1%" ? global::AetherBoy.Runtime.Localization.UiText.Get("Decrease volume by 1 percent") : global::AetherBoy.Runtime.Localization.UiText.Get("Increase volume by 1 percent");
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
        if (!controlCenterVisible && archiveSelection is null) { panel.Hide(); return; }
        panel.CaptureKeys = archiveSelection is null && rebindingAction is not null;
        panel.Update("AetherBoy — " + (archiveSelection is null ? CurrentPageName : global::AetherBoy.Runtime.Localization.UiText.Get("Choose a game from the archive")),
            (archiveSelection is null ? CurrentPageDescription : global::AetherBoy.Runtime.Localization.UiText.Get("Only the selected ROM is imported. Archived saves are not imported.")) + "\n" + string.Join("\n", accessibleDescriptions.Distinct()),
            loadError ?? statusMessage,
            accessibleCommands.Select(item => item.Command).ToArray(),
            archiveSelection is null && ActiveTextEntryName is { } name ? new(AccessibleTextKey, name, ActiveTextValue, ActiveTextReadOnly) : null);
        panel.Pump();
        for (int i = 0; i < 16 && panel.TryTakeRequest(out string key, out string? value); i++)
        {
            // Reconcile after every callback, so a queued action from a previous page/ROM cannot target its replacement.
            DrawShell();
            if (key == "close") { if (archiveSelection is not null) CancelRomLoad(); else { CancelActiveText(); CloseControlCenter(); } panel.Hide(); break; }
            if (archiveSelection is null && key == "text:" + AccessibleTextKey && !ActiveTextReadOnly) ActiveTextValue = value ?? "";
            else if (archiveSelection is null && key == "commit:" + AccessibleTextKey) CommitActiveText();
            else if (archiveSelection is null && key == "cancel:" + AccessibleTextKey) CancelActiveText();
            else if (archiveSelection is null && key == "key" && rebindingAction is not null)
            {
                // SDL key names are API identifiers, not visible labels.
                string keyName = value switch { "Escape" => "Escape", "BackSpace" => "Backspace", "space" => "Space", _ => value ?? "" };
                SDL.Scancode code = SDL.GetScancodeFromName(keyName);
                if (code != SDL.Scancode.Unknown) HandleKeyboard(new SDL.KeyboardEvent { Scancode = code, Down = true }, true);
                else statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("That key has no SDL mapping. Try another key or press Escape.");
            }
            else
            {
                var command = accessibleCommands.FirstOrDefault(item => item.Command.Key == key && item.Command.Enabled);
                if (command.Command is not null) HandleMouseClick(command.Bounds.X + command.Bounds.W / 2, command.Bounds.Y + command.Bounds.H / 2);
            }
        }
    }
}
