using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy.Controls;

namespace nanoboy;

public partial class frmCheats
{
    private readonly AetherList reviewList = new() { Name = "cheatInputReview", View = View.Details, MultiSelect = false, ShowItemToolTips = true };
    private readonly AetherTextBox reviewDetails = new() { Name = "cheatReviewDetails", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly AetherCheckBox confirmCheatValues = new() { Name = "confirmCheatValues" };
    private readonly AetherButton chooseShark = new() { Name = "chooseCheatGameShark" };
    private readonly AetherButton chooseReplay = new() { Name = "chooseCheatActionReplay" };
    private CheatInputReview inputReview = new([], [], []);
    private bool updatingReview;

    private void ConfigureInputReview()
    {
        MaximumSize = Size.Empty; MinimumSize = Size.Empty;
        ClientSize = new Size(860, 728); MinimumSize = MaximumSize = Size;
        lstCheats.Height = 182;
        lblName.Top = lblCode.Top = 224;
        txtName.Top = txtCode.Top = 244;
        btnAdd.Top = 244; codeFormat.Top = 294;
        chooseShark.Bounds = new Rectangle(24, 290, 150, 34);
        chooseReplay.Bounds = new Rectangle(184, 290, 150, 34);
        chooseShark.Text = "GameShark v1/v2"; chooseReplay.Text = "Action Replay v3";
        chooseShark.Click += (_, _) => codeFormat.SelectedIndex = (int)CheatCodeFormat.GameShark;
        chooseReplay.Click += (_, _) => codeFormat.SelectedIndex = (int)CheatCodeFormat.ActionReplayV3;
        reviewList.Bounds = new Rectangle(24, 346, 812, 128);
        reviewList.AccessibleName = UiText.Get("Prüfung der eingefügten Codezeilen");
        reviewList.Columns.AddRange([new ColumnHeader { Text = UiText.Get("Zeile"), Width = 60 },
            new ColumnHeader { Text = "Code", Width = 390 }, new ColumnHeader { Text = UiText.Get("Prüfung"), Width = 334 }]);
        reviewDetails.Bounds = new Rectangle(300, 484, 536, 86);
        reviewDetails.AccessibleName = UiText.Get("Hinweise zur Code-Eingabe");
        confirmCheatValues.Bounds = new Rectangle(24, 484, 260, 78);
        confirmCheatValues.Text = UiText.Get("AAAA ausdrücklich als Hexwert verwenden");
        lblExperimentalInfo.Top = 582;
        btnRemove.Top = deviceButton.Top = 672;
        Controls.AddRange([reviewList, reviewDetails, confirmCheatValues, chooseShark, chooseReplay]);
        txtCode.TextChanged += (_, _) => { confirmCheatValues.Checked = false; UpdateInputReview(); };
        codeFormat.SelectedIndexChanged += (_, _) => { confirmCheatValues.Checked = false; UpdateInputReview(); };
        reviewList.SelectedIndexChanged += (_, _) =>
        {
            if (updatingReview || reviewList.SelectedItems.Count == 0) return;
            var line = (CheatInputLine)reviewList.SelectedItems[0].Tag!;
            var issues = inputReview.Issues.Where(i => i.Line == line.Number || i.Line == 0);
            reviewDetails.Text = string.Join(Environment.NewLine, issues.Select(i => i.Message));
            if (reviewDetails.Text.Length == 0) reviewDetails.Text = inputReview.Summary;
        };
        void EditSelectedLine()
        {
            if (reviewList.SelectedItems.Count == 0) return;
            var line = (CheatInputLine)reviewList.SelectedItems[0].Tag!;
            txtCode.Focus(); txtCode.Select(line.Start, line.Length); txtCode.ScrollToCaret();
        }
        reviewList.DoubleClick += (_, _) => EditSelectedLine();
        reviewList.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { EditSelectedLine(); e.Handled = true; } };
        UpdateInputReview();
    }

    private void UpdateInputReview()
    {
        if (updatingReview) return;
        updatingReview = true;
        try
        {
            inputReview = CheatCodeInput.Review(txtCode.Text, IsGba, (CheatCodeFormat)codeFormat.SelectedIndex,
                session.LatestSnapshot.Cheats.Select(c => c.Code));
            reviewList.BeginUpdate(); reviewList.Items.Clear();
            foreach (var line in inputReview.Lines)
            {
                var issues = inputReview.Issues.Where(i => i.Line == line.Number || i.Line == 0).ToArray();
                string status = issues.Length == 0 ? "OK" : string.Join(" ", issues.Select(i => i.Message));
                var row = new AetherListItem([line.Number.ToString(), line.Text, status]) { Tag = line, ToolTipText = status };
                reviewList.Items.Add(row);
            }
            reviewList.EndUpdate();
            reviewDetails.Text = inputReview.Summary;
            confirmCheatValues.Visible = inputReview.NeedsValueConfirmation;
            chooseShark.Visible = inputReview.Candidates.Contains(CheatCodeFormat.GameShark);
            chooseReplay.Visible = inputReview.Candidates.Contains(CheatCodeFormat.ActionReplayV3);
        }
        finally { updatingReview = false; }
    }
}
