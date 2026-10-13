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
        if (accessibleControls is null && !LinuxAccessibleControls.TryCreate(out accessibleControls, out string? error))
        { statusMessage = error!; return; }
        accessibleControls!.Show(hidden);
        DrawShell(); UpdateAccessibleControls();
    }
    private void AddAccessibleCommand(float x, float y, float width, float height, string label, bool selected, bool enabled, string? focusId)
    {
        bool navigation = focusId?.StartsWith("nav:", StringComparison.Ordinal) == true;
        string key = navigation ? "navigation:" + y : FocusContext + ":" + (focusId ?? "button") + (focusId is null ? $":{x}:{y}" : "");
        string name = label == "×" ? global::AetherBoy.Runtime.Localization.UiText.Get(aetherSelection is not null ? "CANCEL" : "Return to game") : label;
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
        if (focusId?.StartsWith("option:save-slot:", StringComparison.Ordinal) == true && int.TryParse(focusId["option:save-slot:".Length..], out int selectedSlot))
            name = global::AetherBoy.Runtime.Localization.UiText.Format("Save slot {0}", selectedSlot);
        if (focusId?.StartsWith("state:slot:", StringComparison.Ordinal) == true && int.TryParse(focusId["state:slot:".Length..], out int stateSlot))
            name = global::AetherBoy.Runtime.Localization.UiText.Format("Save slot {0}", stateSlot);
        if (focusId == "setting:auto-pause") { option = options.PauseOnFocusLoss; name = global::AetherBoy.Runtime.Localization.UiText.Get("Pause when unfocused: ") + label; }
        if (focusId == "setting:text-size") name = global::AetherBoy.Runtime.Localization.UiText.Get("Text size: ") + label;
        if (focusId?.StartsWith("result:") == true && Enum.TryParse<LinuxSettingsDestination>(focusId[7..], out var destination))
            name = global::AetherBoy.Runtime.Localization.UiText.Get("Open ") + LinuxSettingsCatalog.Entries.First(entry => entry.Destination == destination).Title;
        if (focusId?.StartsWith("favorite:") == true) option = selected;
        if (focusId?.StartsWith("sofa:tab:", StringComparison.Ordinal) == true ||
            focusId?.StartsWith("sofa:favorite:", StringComparison.Ordinal) == true || focusId?.StartsWith("sofa:select:", StringComparison.Ordinal) == true)
            option = selected;
        if (controlCenterPage == ControlCenterPage.Diagnostics && x == 300 && y == 535) option = options.RecordDiagnostics;
        if (controlCenterPage == ControlCenterPage.Audio && label is "-1%" or "+1%") name = label == "-1%" ? global::AetherBoy.Runtime.Localization.UiText.Get("Decrease volume by 1 percent") : global::AetherBoy.Runtime.Localization.UiText.Get("Increase volume by 1 percent");
        if (focusId?.StartsWith("rename:") == true || focusId?.StartsWith("favorite:") == true ||
            focusId?.StartsWith("sofa:play:", StringComparison.Ordinal) == true || focusId?.StartsWith("sofa:favorite:", StringComparison.Ordinal) == true || focusId?.StartsWith("sofa:select:", StringComparison.Ordinal) == true)
        {
            var entry = libraryEntries.FirstOrDefault(item => focusId.EndsWith(item.Identity, StringComparison.Ordinal));
            if (entry is not null) name += ": " + entry.Title;
        }
        if (!controlCenterVisible && archiveSelection is null && !showQuickDeck)
        {
            if (x >= LogicalWidth - 280 && y == 548 && int.TryParse(label, out int slot) && slot is >= 1 and <= 5)
            { name = global::AetherBoy.Runtime.Localization.UiText.Format("Save slot {0}", slot); option = options.SaveSlot == slot; }
            if (label == global::AetherBoy.Runtime.Localization.UiText.Get("Turbo (hold)"))
            { name = global::AetherBoy.Runtime.Localization.UiText.Get("Toggle turbo"); option = mouseTurbo; key = FocusContext + ":turbo"; }
            if (label == global::AetherBoy.Runtime.Localization.UiText.Get("Resume") || label == global::AetherBoy.Runtime.Localization.UiText.Get("Pause")) option = session?.LatestSnapshot.IsPaused == true;
        }
        accessibleCommands.Add((new(key, name, enabled, option, navigation), new() { X = x, Y = y, W = width, H = height }));
    }
    private void DescribeAccessibleText(float x, float y, string text)
    {
        if (!drawingButtonLabel && !string.IsNullOrWhiteSpace(text) &&
            (!controlCenterVisible || x >= 278 && y >= 180 && y < LogicalHeight - 124 || aetherSelection is not null))
            accessibleDescriptions.Add(text);
    }
    private void UpdateAccessibleControls()
    {
        if (accessibleControls is not { IsOpen: true } panel) return;
        panel.CaptureKeys = archiveSelection is null && !showMessageDetails && aetherSelection is null && rebindingAction is not null;
        string title = archiveSelection is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose a game from the archive")
            : showMessageDetails ? global::AetherBoy.Runtime.Localization.UiText.Get("Details")
            : aetherSelection is { } popup ? popup.Title : controlCenterVisible ? CurrentPageName : global::AetherBoy.Runtime.Localization.UiText.Get("Game controls");
        string description = archiveSelection is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Only the selected ROM is imported. Archived saves are not imported.")
            : showMessageDetails ? messageDetailsText : controlCenterVisible ? CurrentPageDescription : CartridgeTitle + " · " + StateLabel;
        panel.Update("AetherBoy — " + title,
            description + "\n" + string.Join("\n", accessibleDescriptions.Distinct()),
            loadError ?? statusMessage,
            accessibleCommands.Select(item => item.Command).ToArray(),
            showMessageDetails ? new("message-details", global::AetherBoy.Runtime.Localization.UiText.Get("Details"), messageDetailsText, true, Math.Max(80, messageDetailsText.Length), Multiline: true)
            : archiveSelection is null && aetherSelection is null && controlCenterVisible && ActiveTextEntryName is { } name ? new(AccessibleTextKey, name, ActiveTextValue, ActiveTextReadOnly) : null);
        panel.Pump();
        for (int i = 0; i < 16 && panel.TryTakeRequest(out string key, out string? value); i++)
        {
            // Reconcile after every callback, so a queued action from a previous page/ROM cannot target its replacement.
            DrawShell();
            if (showMessageDetails && key == "cancel:message-details") { CloseMessageDetails(); break; }
            if (key == "close")
            {
                if (showMessageDetails) { CloseMessageDetails(); break; }
                if (aetherSelection is not null) { CloseAetherSelection(); break; }
                if (archiveSelection is not null) CancelRomLoad();
                else if (controlCenterVisible) { CancelActiveText(); CloseControlCenter(); }
                panel.Hide(); break;
            }
            bool textOffered = archiveSelection is null && !showMessageDetails && aetherSelection is null
                && controlCenterVisible && ActiveTextEntryName is not null && !ActiveTextReadOnly;
            if (textOffered && key == "text:" + AccessibleTextKey) ActiveTextValue = value ?? "";
            else if (textOffered && key == "commit:" + AccessibleTextKey) CommitActiveText();
            else if (textOffered && key == "cancel:" + AccessibleTextKey) CancelActiveText();
            else if (archiveSelection is null && !showMessageDetails && aetherSelection is null && controlCenterVisible
                && key == "key" && rebindingAction is not null)
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
                if (command.Command is not null)
                {
                    if (key == FocusContext + ":turbo") { if (mouseTurbo) ReleaseMouseTurbo(); else HoldMouseTurbo(); }
                    else HandleMouseClick(command.Bounds.X + command.Bounds.W / 2, command.Bounds.Y + command.Bounds.H / 2);
                }
            }
        }
    }
}
