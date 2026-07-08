using System.Text;

namespace AeroVRC;

// ============================================================================
//  LOGS PAGE
// ============================================================================

public partial class MainForm
{
    internal Panel pgLogs;

    void BuildLogsPage()
    {
        pgLogs = NewPage("Logs");
        var logsTitle = NewPageTitle("Logs");
        pgLogs.Controls.Add(logsTitle);

        var logToolbar = new FlowLayoutPanel
        {
            Location = new Point(4, 50), Size = new Size(840, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            WrapContents = false, BackColor = Color.Transparent,
        };
        pgLogs.Controls.Add(logToolbar);

        var logSearch = new TextBox
        {
            Width = 260, Font = Ui.FontBody,
            Margin = new Padding(0, 2, 8, 0),
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
        };
        logToolbar.Controls.Add(logSearch);

        var logClearBtn = NewToolbarButton("Clear");
        var logCopyBtn = NewToolbarButton("Copy");
        var logSaveBtn = NewToolbarButton("Save to file");
        logToolbar.Controls.AddRange(new Control[] { logClearBtn, logCopyBtn, logSaveBtn });

        var logAutoScrollCb = new AeroCheckBox
        {
            Name = "muted", Text = "Auto-scroll", AutoSize = true, Checked = true,
            Font = Ui.FontBody, Margin = new Padding(10, 5, 0, 0),
        };
        logToolbar.Controls.Add(logAutoScrollCb);

        logBox = new TextBox
        {
            Name = "logBox",
            Multiline = true, WordWrap = false, ScrollBars = ScrollBars.Both, ReadOnly = true,
            Font = Ui.FontMono, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Ui.LogBg, ForeColor = Ui.Text,
            Location = new Point(4, 96), Size = new Size(840, 470),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };
        pgLogs.Controls.Add(logBox);

        logSearch.TextChanged += (s, e) => { logFilter = logSearch.Text; RefreshLogView(); };
        logClearBtn.Click += (s, e) => { logLines.Clear(); logBox.Clear(); WriteLog("Log cleared."); };
        logCopyBtn.Click += (s, e) =>
        {
            if (logBox.Text.Length > 0) Clipboard.SetText(logBox.Text);
            WriteLog("Log copied to clipboard.");
        };
        logSaveBtn.Click += (s, e) =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = "AeroVRC-log.txt",
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, string.Join("\r\n", logLines));
                    WriteLog($"Log saved to: {dlg.FileName}");
                }
                catch (Exception ex) { WriteLog($"Log save failed: {ex.Message}"); }
            }
        };
        logAutoScrollCb.CheckedChanged += (s, e) => logAutoScroll = logAutoScrollCb.Checked;
    }

    internal void RefreshLogView()
    {
        if (logBox == null) return;
        logBox.SuspendLayout();
        var sb = new StringBuilder();
        foreach (var l in logLines)
        {
            if (LinePassesFilter(l)) sb.AppendLine(l);
        }
        logBox.Text = sb.ToString();
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
        logBox.ResumeLayout();
    }
}
