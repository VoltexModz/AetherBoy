using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime.Localization;

namespace nanoboy.Controls;

// A failed background save must stay visible without opening a nested modal loop.
internal sealed class AetherSaveNotice : AetherSurfacePanel
{
    private readonly Label message;
    private readonly AetherButton retry;
    private readonly AetherButton details;
    private readonly Func<Task> save;
    private readonly Action<Exception> report;
    private Exception? failure;
    internal bool IsSaving { get; private set; }

    internal AetherSaveNotice(Func<Task> save, Action<Exception> report)
    {
        this.save = save;
        this.report = report;
        Name = "settingsSaveNotice";
        Height = 96;
        Padding = new Padding(14, 6, 14, 6);
        BackColor = AetherColors.Surface;
        message = new Label { Dock = DockStyle.Fill, ForeColor = AetherColors.Text,
            Font = new Font("Segoe UI", 9), AutoEllipsis = false };
        retry = new AetherButton { Text = UiText.Get("Erneut speichern"), Dock = DockStyle.Right, Width = 158, Kind = AetherButtonKind.Primary };
        details = new AetherButton { Text = UiText.Get("Details"), Dock = DockStyle.Right, Width = 92, Kind = AetherButtonKind.Secondary };
        var actions = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = AetherColors.Surface };
        actions.Controls.Add(details);
        actions.Controls.Add(retry);
        Controls.Add(message);
        Controls.Add(actions);
        retry.Click += async (_, _) => await RetryAsync();
        details.Click += (_, _) =>
        {
            if (failure is not null) AetherSignal.Show(FindForm(),
                UiText.TechnicalDetails(failure.GetBaseException().Message),
                UiText.Get("Änderung nicht gespeichert"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        Visible = false;
    }

    internal void SetFailure(Exception error)
    {
        failure = error;
        Visible = true;
        if (!IsSaving) message.Text = UiText.Get("Einstellungen noch nicht gespeichert. Deine Änderungen bleiben im Programm erhalten. Prüfe die Details und speichere erneut.");
    }

    internal async Task RetryAsync()
    {
        if (IsSaving || IsDisposed) return;
        IsSaving = true;
        retry.Enabled = details.Enabled = false;
        message.Text = UiText.Get("Einstellungen werden gespeichert …");
        try
        {
            await save();
            if (!IsDisposed) { failure = null; Visible = false; }
        }
        catch (Exception error)
        {
            report(error);
            if (!IsDisposed) { failure = error; Visible = true; }
        }
        finally
        {
            IsSaving = false;
            if (!IsDisposed)
            {
                retry.Enabled = details.Enabled = true;
                if (failure is not null) SetFailure(failure);
            }
        }
    }
}
