using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private void BuildBootIntroPage()
    {
        var page = NewSection("intro", global::AetherBoy.Runtime.Localization.UiText.Get("Startanimation"), global::AetherBoy.Runtime.Localization.UiText.Get("Logo und eigener Klang vor dem Öffnen eines Einzelspiels. Kein Ersatz für Boot-ROM oder BIOS."));
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Zurück zur Bedienung"), 0, 82, 320, () => ShowPage("desktop"));
        var store = frmNano.BootIntroPreferences;
        var selected = store.Load();
        AetherButton enabled = null!, sound = null!;
        var details = CreateSmallLabel("", 0, 425, 780, 76); details.Name = "bootIntroDetails"; page.Controls.Add(details);
        void Refresh()
        {
            enabled.Text = selected.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Vor jedem Spiel: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Vor jedem Spiel: aus"); enabled.Selected = selected.Enabled;
            sound.Text = selected.SoundEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Intro-Ton: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Intro-Ton: aus"); sound.Selected = selected.SoundEnabled;
            details.Text = global::AetherBoy.Runtime.Localization.UiText.Format("Bild: {0}\r\nTon: {1}", (selected.Image is null ? global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy-Logo") : global::AetherBoy.Runtime.Localization.UiText.Get("eigene PNG-Datei")), (selected.Sound is null ? global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy-Klang") : global::AetherBoy.Runtime.Localization.UiText.Get("eigene WAV-Datei")));
        }
        void Save(BootIntroOptions value)
        {
            try { store.Save(value); selected = value; Refresh(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Die Startanimation konnte nicht gespeichert werden. Prüfe den freien Speicher und die Zugriffsrechte."), global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen nicht gespeichert"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        enabled = AddActionButton(page, "", 0, 154, 380, () => Save(selected with { Enabled = !selected.Enabled })); enabled.Name = "bootIntroEnabled";
        sound = AddActionButton(page, "", 400, 154, 380, () => Save(selected with { SoundEnabled = !selected.SoundEnabled })); sound.Name = "bootIntroSound";
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Dauer: 2,4 Sekunden, jederzeit überspringbar. Nicht bei Reset oder Schnellladen. Stummschaltung und Lautstärke gelten auch für das Intro."), 0, 218, 780, 70));
        void Import(bool image)
        {
            using var picker = new AetherFileDialog { Title = image ? global::AetherBoy.Runtime.Localization.UiText.Get("Intro-Bild auswählen") : global::AetherBoy.Runtime.Localization.UiText.Get("Intro-Ton auswählen"), Filter = image ? "PNG-Bild|*.png" : "PCM-WAV|*.wav", CheckFileExists = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string name = store.Import(picker.FileName, image, bytes => { using var decoded = AetherBootIntro.DecodeImage(bytes); });
                Save(image ? selected with { Image = name } : selected with { Sound = name });
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
            { AetherSignal.Show(this, image ? global::AetherBoy.Runtime.Localization.UiText.Get("Das Bild konnte nicht importiert werden. Wähle ein gültiges PNG bis 2048 × 2048 Pixel und 4 MiB.") : global::AetherBoy.Runtime.Localization.UiText.Get("Der Ton konnte nicht importiert werden. Wähle PCM-WAV mit 16 Bit, mono oder stereo, 8–48 kHz und höchstens 2,4 Sekunden."), global::AetherBoy.Runtime.Localization.UiText.Get("Datei nicht übernommen"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Eigenes Bild auswählen"), 0, 303, 380, () => Import(true)).Name = "bootIntroImage";
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Eigenen Ton auswählen"), 400, 303, 380, () => Import(false)).Name = "bootIntroAudio";
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("PNG bis 2048 × 2048 Pixel / 4 MiB. WAV: 16-Bit-PCM, mono oder stereo, 8–48 kHz, maximal 2,4 Sekunden. Eine lokale Kopie bleibt im AetherBoy-Ordner."), 0, 363, 780, 62));
        var preview = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Vorschau abspielen"), 0, 519, 380, () => { }); preview.Name = "bootIntroPreview";
        preview.Click += async (_, _) =>
        {
            preview.Enabled = false;
            try
            {
                if (bridge.PreviewBootIntro is { } play) await play();
                else await AetherBootIntro.PlayAsync(this, store, bridge.Settings.AudioEnable ? bridge.Settings.AudioVolume/100f : 0, CancellationToken.None);
            }
            finally { if (!IsDisposed) preview.Enabled = true; }
        };
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy-Bild und -Ton verwenden"), 400, 519, 380, () => Save(selected with { Image = null, Sound = null }));
        Refresh();
    }
}
