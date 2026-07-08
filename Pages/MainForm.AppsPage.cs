using System.Drawing.Drawing2D;

namespace AeroVRC;

// ============================================================================
//  APPS PAGE  (user-managed app list with icons, auto-launch, edit/remove)
// ============================================================================

public partial class MainForm
{
    internal Panel pgApps;
    internal TableLayoutPanel appsGrid;
    int appsRowCount;
    readonly List<Image> appIconImages = new();   // kept so we can dispose bitmaps on rebuild

    // Generic pill toolbar button (also used by the Logs & Photos pages).
    internal Button NewToolbarButton(string text)
    {
        var b = new Button
        {
            Text = text, AutoSize = true,
            MinimumSize = new Size(0, 32),
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 0),
        };
        Ui.StyleButton(b, "secondary");
        return b;
    }

    void BuildAppsPage()
    {
        pgApps = NewPage("Apps");
        var appsTitle = NewPageTitle("Apps");
        pgApps.Controls.Add(appsTitle);

        var appsSub = new Label
        {
            Name = "muted",
            Text = "Add your companion apps, then tick Auto-launch to open them when monitoring starts.",
            Font = Ui.FontMuted, AutoSize = true, Location = new Point(4, 46),
        };
        pgApps.Controls.Add(appsSub);

        // ---- Toolbar ----
        var appsToolbar = new FlowLayoutPanel
        {
            Location = new Point(4, 74), Size = new Size(840, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            WrapContents = false, BackColor = Color.Transparent,
        };
        pgApps.Controls.Add(appsToolbar);

        var addAppBtn = NewToolbarButton("+ Add App");
        addAppBtn.Name = "primaryBtn"; Ui.StyleButton(addAppBtn, "primary");
        var presetsBtn = NewToolbarButton("Presets");
        var importBtn = NewToolbarButton("Import config");
        var exportBtn = NewToolbarButton("Export config");
        var cacheFolderBtn = NewToolbarButton("Open config folder");
        appsToolbar.Controls.AddRange(new Control[] { addAppBtn, presetsBtn, importBtn, exportBtn, cacheFolderBtn });

        // ---- App list (icon | name | Launch | Auto-launch | Edit | Remove) ----
        appsGrid = new TableLayoutPanel
        {
            Location = new Point(4, 122), Size = new Size(840, 556),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ColumnCount = 6, AutoScroll = true,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = Color.Transparent,
        };
        appsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // icon
        appsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // name (stretches)
        appsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // launch
        appsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // auto-launch
        appsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // edit
        appsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // remove
        pgApps.Controls.Add(appsGrid);

        appsStatus = new Label
        {
            Name = "muted", Text = "Ready.", Dock = DockStyle.Bottom, Height = 22,
            Font = Ui.FontMuted, TextAlign = ContentAlignment.MiddleLeft,
        };
        pgApps.Controls.Add(appsStatus);

        addAppBtn.Click += (s, e) =>
        {
            var app = ShowAppDialog();
            if (app != null)
            {
                config.CustomApps.Add(app);
                SaveConfig();
                WriteLog($"Added app: {app.Name}");
                SetAppsStatus($"Added {app.Name}.");
                RebuildAppsList();
            }
        };
        presetsBtn.Click += (s, e) => ShowPage("Presets");
        exportBtn.Click += (s, e) =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "AeroVRC-config.json",
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, ConfigStore.SerializeForExport(config));
                    WriteLog($"Configuration exported to: {dlg.FileName}");
                    SetAppsStatus("Exported.");
                }
                catch (Exception ex) { WriteLog($"Export failed: {ex.Message}"); SetAppsStatus("Export failed."); }
            }
        };
        importBtn.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (!File.Exists(dlg.FileName)) return;
                config = ConfigStore.Load(dlg.FileName);
                ApplyConfigToUi();
                SaveConfig();
                WriteLog($"Configuration imported from: {dlg.FileName}");
                SetAppsStatus("Imported.");
            }
        };
        cacheFolderBtn.Click += (s, e) =>
        {
            Directory.CreateDirectory(ConfigStore.ConfigDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConfigStore.ConfigDir) { UseShellExecute = true });
        };
    }

    // 32x32 icon cell: the app's chosen image, else a rounded accent chip with its initial.
    Control NewAppIconCell(CustomApp app)
    {
        var img = LoadIconImage(app.Icon);
        if (img != null)
        {
            appIconImages.Add(img);
            return new PictureBox
            {
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = img,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 8, 8),
            };
        }
        var ph = new Panel
        {
            Size = new Size(32, 32),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 8, 8),
            Tag = !string.IsNullOrEmpty(app.Name) ? app.Name[..1].ToUpper() : "?",
        };
        ph.Paint += (s, e) =>
        {
            var p = (Panel)s;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.OpaqueBack(p));
            using var path = Ui.RoundedPath(0.5f, 0.5f, 31, 31, 8);
            var rect = new Rectangle(0, 0, 32, 32);
            using (var b = new LinearGradientBrush(rect, Ui.AccentHover, Ui.Accent2, 90.0f))
                g.FillPath(b, path);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var fb = new SolidBrush(Color.White);
            using var f = new Font("Segoe UI Semibold", 12);
            g.DrawString((string)p.Tag, f, fb, new RectangleF(0, 0, 32, 32), sf);
        };
        return ph;
    }

    // Builds one row for CustomApps[index].
    void AddAppRow(CustomApp app, int index)
    {
        int r = appsRowCount;
        appsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        appsGrid.RowCount = r + 1;

        appsGrid.Controls.Add(NewAppIconCell(app), 0, r);

        var detail = app.Type == "steam" ? $"Steam App ID {app.Value}" : app.Value;
        var lbl = new Label
        {
            Text = $"{app.Name}\n{detail}",
            AutoSize = true, Font = Ui.FontBody, ForeColor = Ui.Text,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 24, 8),
        };
        appsGrid.Controls.Add(lbl, 1, r);

        var btn = new Button
        {
            Text = "Launch", AutoSize = true,
            MinimumSize = new Size(90, 32),
            Padding = new Padding(10, 4, 10, 4),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 3, 6),
            Tag = app,
        };
        Ui.StyleButton(btn, "secondary");
        btn.Click += (s, e) => LaunchAppEntry((CustomApp)((Button)s).Tag);
        appsGrid.Controls.Add(btn, 2, r);

        var cb = new AeroCheckBox
        {
            Name = "muted", Text = "Auto-launch", AutoSize = true,
            Font = Ui.FontMuted, ForeColor = Ui.TextMuted,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(14, 8, 12, 8),
            Tag = app.Name,
        };
        cb.Checked = config.AutoLaunch.TryGetValue(app.Name, out var on) && on;
        cb.CheckedChanged += (s, e) =>
        {
            if (loading) return;
            var k = (string)((AeroCheckBox)s).Tag;
            config.AutoLaunch[k] = ((AeroCheckBox)s).Checked;
            SaveConfig();
            WriteLog(((AeroCheckBox)s).Checked ? $"{k} will auto-launch when monitoring starts." : $"{k} auto-launch disabled.");
        };
        appsGrid.Controls.Add(cb, 3, r);

        var editBtn = new Button
        {
            Text = "Edit", AutoSize = true,
            MinimumSize = new Size(70, 32),
            Padding = new Padding(10, 4, 10, 4),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 3, 6),
            Tag = index,
        };
        Ui.StyleButton(editBtn, "secondary");
        editBtn.Click += (s, e) =>
        {
            int i = (int)((Button)s).Tag;
            var existing = config.CustomApps[i];
            var res = ShowAppDialog(existing);
            if (res != null)
            {
                config.CustomApps[i] = res;
                var oldName = existing.Name; var newName = res.Name;
                if (oldName != newName && config.AutoLaunch.TryGetValue(oldName, out var was))
                {
                    config.AutoLaunch[newName] = was;
                    config.AutoLaunch.Remove(oldName);
                }
                SaveConfig();
                RebuildAppsList();
                SetAppsStatus($"Updated {newName}.");
            }
        };
        appsGrid.Controls.Add(editBtn, 4, r);

        var rmBtn = new Button
        {
            Text = "Remove", AutoSize = true,
            MinimumSize = new Size(84, 32),
            Padding = new Padding(10, 4, 10, 4),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 3, 6),
            Tag = index,
        };
        Ui.StyleButton(rmBtn, "secondary");
        rmBtn.Click += (s, e) =>
        {
            int i = (int)((Button)s).Tag;
            var nm = config.CustomApps[i].Name;
            config.CustomApps.RemoveAt(i);
            config.AutoLaunch.Remove(nm);
            SaveConfig();
            WriteLog($"Removed app: {nm}");
            SetAppsStatus($"Removed {nm}.");
            RebuildAppsList();
        };
        appsGrid.Controls.Add(rmBtn, 5, r);

        appsRowCount++;
    }

    internal void RebuildAppsList()
    {
        appsGrid.SuspendLayout();
        // Detach icon bitmaps from their PictureBoxes and clear the row controls
        // BEFORE disposing the bitmaps. Disposing an image a PictureBox is still
        // showing makes the next repaint throw a GDI+ "Parameter is not valid".
        foreach (Control c in appsGrid.Controls)
            if (c is PictureBox pb) pb.Image = null;
        appsGrid.Controls.Clear();
        foreach (var im in appIconImages) { try { im.Dispose(); } catch { } }
        appIconImages.Clear();
        appsGrid.RowStyles.Clear();
        appsGrid.RowCount = 0;
        appsRowCount = 0;

        var apps = config.CustomApps;
        if (apps.Count == 0)
        {
            appsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appsGrid.RowCount = 1;
            var empty = new Label
            {
                Text = "No apps yet.  Click  “+ Add App”  to add your first companion app.",
                AutoSize = true, Font = Ui.FontBody, ForeColor = Ui.TextMuted,
                BackColor = Color.Transparent,
                Margin = new Padding(6, 24, 6, 6),
            };
            appsGrid.Controls.Add(empty, 0, 0);
            appsGrid.SetColumnSpan(empty, 6);
            appsGrid.ResumeLayout();
            navButtons.ForEach(b => b.Invalidate());
            return;
        }

        for (int i = 0; i < apps.Count; i++) AddAppRow(apps[i], i);
        // trailing spacer soaks up leftover height so the last real row keeps its size
        appsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        appsGrid.RowCount = appsRowCount + 1;
        appsGrid.ResumeLayout();
        navButtons.ForEach(b => b.Invalidate());
    }

    // ---- Add / Edit app dialog ----
    // Returns the app entry or null.
    internal CustomApp ShowAppDialog(CustomApp existing = null)
    {
        bool isEdit = existing != null;
        var dlg = new Form
        {
            Text = isEdit ? "Edit app" : "Add App",
            ClientSize = new Size(474, 316),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Ui.Surface, Font = Ui.FontBody,
        };
        if (Icon != null) dlg.Icon = Icon;

        var nameLbl = new Label { Text = "App Name:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(18, 24) };
        dlg.Controls.Add(nameLbl);
        var nameBox = new TextBox { Location = new Point(150, 21), Width = 306, BackColor = Ui.InputBg, ForeColor = Ui.Text };
        dlg.Controls.Add(nameBox);

        var useSteam = new AeroCheckBox
        {
            Text = "Use Steam", AutoSize = true, ForeColor = Ui.Text, BackColor = Ui.Surface,
            Location = new Point(150, 58),
            Checked = !isEdit || existing.Type != "exe",
        };
        dlg.Controls.Add(useSteam);

        var valLbl = new Label { Text = "Steam App ID:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(18, 100) };
        dlg.Controls.Add(valLbl);
        var valBox = new TextBox { Location = new Point(150, 97), BackColor = Ui.InputBg, ForeColor = Ui.Text };
        dlg.Controls.Add(valBox);
        var browseBtn = new Button
        {
            Font = new Font("Segoe MDL2 Assets", 10),
            Text = "",   // folder glyph
            Size = new Size(34, 26),
        };
        Ui.StyleButton(browseBtn, "secondary");
        dlg.Controls.Add(browseBtn);

        var valHint = new Label { AutoSize = true, Font = Ui.FontMuted, ForeColor = Ui.TextMuted, Location = new Point(150, 124) };
        dlg.Controls.Add(valHint);

        // Icon picker
        var iconLbl = new Label { Text = "Icon:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(18, 166) };
        dlg.Controls.Add(iconLbl);
        var iconBtn = new Button { Text = "Icon", Size = new Size(80, 30), Location = new Point(150, 162) };
        Ui.StyleButton(iconBtn, "secondary");
        dlg.Controls.Add(iconBtn);
        var iconPreview = new PictureBox
        {
            Size = new Size(36, 36), Location = new Point(242, 159),
            SizeMode = PictureBoxSizeMode.Zoom, BackColor = Ui.InputBg,
        };
        dlg.Controls.Add(iconPreview);
        var iconPathLbl = new Label { AutoSize = true, Font = Ui.FontMuted, ForeColor = Ui.TextMuted, Location = new Point(288, 170), Text = "(none)" };
        dlg.Controls.Add(iconPathLbl);
        // The chosen icon path lives in iconPreview.Tag (mutable, visible to handlers).
        iconPreview.Tag = isEdit && !string.IsNullOrEmpty(existing.Icon) ? existing.Icon : "";
        if (((string)iconPreview.Tag).Length > 0)
        {
            iconPathLbl.Text = Path.GetFileName((string)iconPreview.Tag);
            var pi = LoadIconImage((string)iconPreview.Tag);
            if (pi != null) iconPreview.Image = pi;
        }

        void SyncType()
        {
            if (useSteam.Checked)
            {
                valLbl.Text = "Steam App ID:";
                browseBtn.Visible = false;
                valBox.Width = 306;
                valHint.Text = "Numeric Steam App ID (e.g. 438100).";
            }
            else
            {
                valLbl.Text = "File path:";
                browseBtn.Visible = true;
                valBox.Width = 266;
                browseBtn.Location = new Point(422, 96);
                valHint.Text = "Full path to the program’s .exe file.";
            }
        }
        useSteam.CheckedChanged += (s, e) => SyncType();

        browseBtn.Click += (s, e) =>
        {
            using var fd = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*" };
            if (fd.ShowDialog() == DialogResult.OK)
            {
                valBox.Text = fd.FileName;
                if (nameBox.Text.Length == 0) nameBox.Text = Path.GetFileNameWithoutExtension(fd.FileName);
            }
        };
        iconBtn.Click += (s, e) =>
        {
            using var fd = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|All files (*.*)|*.*",
            };
            if (fd.ShowDialog() == DialogResult.OK)
            {
                var img = LoadIconImage(fd.FileName);
                if (img != null)
                {
                    var old = iconPreview.Image;
                    iconPreview.Image = img;
                    old?.Dispose();
                    iconPreview.Tag = fd.FileName;
                    iconPathLbl.Text = Path.GetFileName(fd.FileName);
                }
                else
                {
                    MessageBox.Show("That image could not be loaded.", "Invalid image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        };

        var okBtn = new Button
        {
            Text = isEdit ? "Save" : "Add apps",
            Size = new Size(110, 32), Location = new Point(346, 270),
            Name = "primaryBtn",
        };
        Ui.StyleButton(okBtn, "primary");
        dlg.Controls.Add(okBtn);
        var cancelBtn = new Button
        {
            Text = "Cancel", Size = new Size(96, 32), Location = new Point(18, 270),
            DialogResult = DialogResult.Cancel,
        };
        Ui.StyleButton(cancelBtn, "secondary");
        dlg.Controls.Add(cancelBtn);
        dlg.CancelButton = cancelBtn;

        // Prefill (edit) + initial toggle state.
        if (isEdit) { nameBox.Text = existing.Name; valBox.Text = existing.Value; }
        SyncType();

        okBtn.Click += (s, e) =>
        {
            var n = nameBox.Text.Trim(); var v = valBox.Text.Trim();
            if (n.Length == 0 || v.Length == 0)
            {
                MessageBox.Show("Please fill in both a name and a value.", "Missing information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (useSteam.Checked && !System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+$"))
            {
                MessageBox.Show("A Steam App ID must be numeric (e.g. 438100).", "Invalid App ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        };

        CustomApp result = null;
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            result = new CustomApp
            {
                Name = nameBox.Text.Trim(),
                Type = useSteam.Checked ? "steam" : "exe",
                Value = valBox.Text.Trim(),
                Icon = (string)iconPreview.Tag,
            };
        }
        iconPreview.Image?.Dispose();
        dlg.Dispose();
        return result;
    }
}
