namespace AeroVRC;

// ============================================================================
//  PRESETS PAGE  (grouped app sets with auto-launch; reached from Apps)
// ============================================================================

public partial class MainForm
{
    internal Panel pgPresets;
    FlowLayoutPanel presetsList;
    Panel presetsTop;
    Button addPresetBtn;

    void BuildPresetsPage()
    {
        pgPresets = NewPage("Presets");
        var presetsTitle = NewPageTitle("Presets");
        pgPresets.Controls.Add(presetsTitle);

        var presetsSub = new Label
        {
            Name = "muted",
            Text = "Group apps into one-click sets. Tick Auto-launch to open a preset when monitoring starts.",
            Font = Ui.FontMuted, AutoSize = true, Location = new Point(4, 46),
        };
        pgPresets.Controls.Add(presetsSub);

        // Top bar: Back (left) + Add preset (centred).
        presetsTop = new Panel
        {
            Location = new Point(4, 74), Size = new Size(840, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent,
        };
        pgPresets.Controls.Add(presetsTop);

        var presetsBackBtn = new Button
        {
            Text = "← Back to Apps", AutoSize = true,
            MinimumSize = new Size(0, 32),
            Padding = new Padding(12, 4, 12, 4),
            Location = new Point(0, 4),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        Ui.StyleButton(presetsBackBtn, "secondary");
        presetsBackBtn.Click += (s, e) => ShowPage("Apps");
        presetsTop.Controls.Add(presetsBackBtn);

        addPresetBtn = new Button
        {
            Text = "+ Add preset", AutoSize = true,
            MinimumSize = new Size(0, 34),
            Padding = new Padding(16, 5, 16, 5),
            Anchor = AnchorStyles.Top,
            Name = "primaryBtn",
        };
        Ui.StyleButton(addPresetBtn, "primary");
        presetsTop.Controls.Add(addPresetBtn);
        // Keep the Add button horizontally centred as the page resizes.
        presetsTop.Resize += (s, e) => addPresetBtn.Left = (presetsTop.Width - addPresetBtn.Width) / 2;
        addPresetBtn.Left = (presetsTop.Width - addPresetBtn.Width) / 2;

        presetsList = new FlowLayoutPanel
        {
            Location = new Point(4, 124), Size = new Size(852, 554),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true,
            BackColor = Color.Transparent,
        };
        pgPresets.Controls.Add(presetsList);

        addPresetBtn.Click += (s, e) =>
        {
            var res = ShowPresetDialog();
            if (res != null)
            {
                config.Presets.Add(new Preset { Name = res.Name, AutoLaunch = false, Apps = res.Apps });
                SaveConfig();
                WriteLog($"Added preset: {res.Name}");
                RebuildPresetsList();
            }
        };
    }

    // Builds one expandable preset card for Presets[index].
    void AddPresetRow(Preset preset, int index)
    {
        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 12, 16, 12),
            MinimumSize = new Size(788, 0),
        };

        var card = Ui.NewCard();
        card.AutoSize = true;
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        card.Margin = new Padding(0, 0, 0, 14);
        card.Controls.Add(stack);

        // Header row.
        var hdr = new TableLayoutPanel
        {
            ColumnCount = 6, RowCount = 1,
            Size = new Size(752, 36),
            BackColor = Color.Transparent,
        };
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // chevron
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // name
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // auto-launch
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // launch
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // edit
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // delete

        // Body (apps in this preset) - collapsed by default.
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(30, 6, 0, 2),
            Visible = false,
        };
        if (preset.Apps.Count == 0)
        {
            body.Controls.Add(new Label
            {
                Name = "onCardMuted",
                Text = "(no apps yet - click Edit to add some)",
                AutoSize = true, Font = Ui.FontMuted,
                ForeColor = Ui.TextMuted, BackColor = Ui.Card,
            });
        }
        else
        {
            foreach (var a in preset.Apps)
            {
                var desc = a.Type == "steam" ? $"Steam App ID {a.Value}" : a.Value;
                body.Controls.Add(new Label
                {
                    Name = "onCardMuted",
                    Text = $"•  {a.Name}   -   {desc}",
                    AutoSize = true, Font = Ui.FontMuted,
                    ForeColor = Ui.TextMuted, BackColor = Ui.Card,
                    Margin = new Padding(0, 2, 0, 2),
                });
            }
        }

        // Chevron (expand/collapse).
        var chev = new Button
        {
            Text = "▶", Font = new Font("Segoe UI", 9),
            Size = new Size(26, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Ui.Card, ForeColor = Ui.Text,
            Anchor = AnchorStyles.Left,
            Cursor = Cursors.Hand,
            Tag = body,
        };
        chev.FlatAppearance.BorderSize = 0;
        chev.Click += (s, e) =>
        {
            var b = (FlowLayoutPanel)((Button)s).Tag;
            b.Visible = !b.Visible;
            ((Button)s).Text = b.Visible ? "▼" : "▶";
        };
        hdr.Controls.Add(chev, 0, 0);

        var nameLbl = new Label
        {
            Name = "onCard", Text = preset.Name, Font = Ui.FontHeader, AutoSize = true,
            ForeColor = Ui.Text, BackColor = Ui.Card,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(6, 8, 6, 0),
        };
        hdr.Controls.Add(nameLbl, 1, 0);

        var autoCb = new AeroCheckBox
        {
            Name = "onCardMuted", Text = "Auto-launch", AutoSize = true,
            Font = Ui.FontMuted, ForeColor = Ui.TextMuted, BackColor = Ui.Card,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(6, 6, 10, 6),
            Checked = preset.AutoLaunch,
            Tag = index,
        };
        autoCb.CheckedChanged += (s, e) =>
        {
            if (loading) return;
            int i = (int)((AeroCheckBox)s).Tag;
            config.Presets[i].AutoLaunch = ((AeroCheckBox)s).Checked;
            SaveConfig();
        };
        hdr.Controls.Add(autoCb, 2, 0);

        var launchBtn = new Button
        {
            Text = "Launch", AutoSize = true,
            MinimumSize = new Size(78, 30),
            Padding = new Padding(8, 3, 8, 3),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 3, 3, 3),
            Tag = index,
        };
        Ui.StyleButton(launchBtn, "secondary");
        launchBtn.Click += (s, e) => LaunchPreset(config.Presets[(int)((Button)s).Tag]);
        hdr.Controls.Add(launchBtn, 3, 0);

        var editBtn = new Button
        {
            Text = "Edit", AutoSize = true,
            MinimumSize = new Size(64, 30),
            Padding = new Padding(8, 3, 8, 3),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 3, 3, 3),
            Tag = index,
        };
        Ui.StyleButton(editBtn, "secondary");
        editBtn.Click += (s, e) =>
        {
            int i = (int)((Button)s).Tag;
            var res = ShowPresetDialog(config.Presets[i]);
            if (res != null)
            {
                config.Presets[i].Name = res.Name;
                config.Presets[i].Apps = res.Apps;
                SaveConfig();
                RebuildPresetsList();
            }
        };
        hdr.Controls.Add(editBtn, 4, 0);

        var delBtn = new Button
        {
            Text = "Delete", AutoSize = true,
            MinimumSize = new Size(70, 30),
            Padding = new Padding(8, 3, 8, 3),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 3, 3, 3),
            Tag = index,
            Name = "dangerBtn",
        };
        Ui.StyleButton(delBtn, "danger");
        delBtn.Click += (s, e) =>
        {
            int i = (int)((Button)s).Tag;
            var nm = config.Presets[i].Name;
            var r = MessageBox.Show($"Delete the preset “{nm}”?", "Delete preset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            config.Presets.RemoveAt(i);
            SaveConfig();
            WriteLog($"Deleted preset: {nm}");
            RebuildPresetsList();
        };
        hdr.Controls.Add(delBtn, 5, 0);

        stack.Controls.Add(hdr);
        stack.Controls.Add(body);
        presetsList.Controls.Add(card);
    }

    internal void RebuildPresetsList()
    {
        presetsList.SuspendLayout();
        presetsList.Controls.Clear();
        var presets = config.Presets;
        if (presets.Count == 0)
        {
            presetsList.Controls.Add(new Label
            {
                Name = "muted",
                Text = "Uh oh! There's nothing here! Please use the (Add preset) to get started.",
                AutoSize = true, Font = Ui.FontBody, ForeColor = Ui.TextMuted,
                BackColor = Color.Transparent,
                Margin = new Padding(8, 30, 8, 8),
            });
            presetsList.ResumeLayout();
            return;
        }
        for (int i = 0; i < presets.Count; i++) AddPresetRow(presets[i], i);
        presetsList.ResumeLayout();
    }

    // ---- Add / Edit preset dialog ----  returns a Preset (Name + Apps) or null.
    internal Preset ShowPresetDialog(Preset existing = null)
    {
        bool isEdit = existing != null;
        var dlg = new Form
        {
            Text = isEdit ? "Edit preset" : "Add preset",
            ClientSize = new Size(560, 440),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Ui.Surface, Font = Ui.FontBody,
        };
        if (Icon != null) dlg.Icon = Icon;

        var nameLbl = new Label { Text = "Preset name:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(18, 22) };
        dlg.Controls.Add(nameLbl);
        var nameBox = new TextBox { Location = new Point(120, 19), Width = 420, BackColor = Ui.InputBg, ForeColor = Ui.Text };
        dlg.Controls.Add(nameBox);

        var appsLbl = new Label { Text = "Apps in this preset:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(18, 60) };
        dlg.Controls.Add(appsLbl);

        var listBox = new ListBox
        {
            Location = new Point(18, 86), Size = new Size(380, 300),
            BackColor = Ui.InputBg, ForeColor = Ui.Text, BorderStyle = BorderStyle.FixedSingle,
        };
        dlg.Controls.Add(listBox);

        var addBtn = new Button { Text = "Add app", Size = new Size(122, 32), Location = new Point(412, 86) };
        Ui.StyleButton(addBtn, "secondary"); dlg.Controls.Add(addBtn);
        var editAppBtn = new Button { Text = "Edit app", Size = new Size(122, 32), Location = new Point(412, 124) };
        Ui.StyleButton(editAppBtn, "secondary"); dlg.Controls.Add(editAppBtn);
        var rmAppBtn = new Button { Text = "Remove app", Size = new Size(122, 32), Location = new Point(412, 162) };
        Ui.StyleButton(rmAppBtn, "secondary"); dlg.Controls.Add(rmAppBtn);

        // Working copy of the preset's apps.
        var dlgApps = new List<PresetApp>();
        if (isEdit)
        {
            foreach (var a in existing.Apps)
                dlgApps.Add(new PresetApp { Name = a.Name, Type = a.Type, Value = a.Value });
            nameBox.Text = existing.Name;
        }
        void UpdateList()
        {
            listBox.Items.Clear();
            foreach (var a in dlgApps)
            {
                var desc = a.Type == "steam" ? $"Steam App ID {a.Value}" : Path.GetFileName(a.Value);
                listBox.Items.Add($"{a.Name}    -    {desc}");
            }
        }
        UpdateList();

        addBtn.Click += (s, e) =>
        {
            var res = ShowAppDialog();
            if (res != null)
            {
                dlgApps.Add(new PresetApp { Name = res.Name, Type = res.Type, Value = res.Value });
                UpdateList();
            }
        };
        editAppBtn.Click += (s, e) =>
        {
            int sel = listBox.SelectedIndex;
            if (sel < 0) return;
            var cur = dlgApps[sel];
            var res = ShowAppDialog(new CustomApp { Name = cur.Name, Type = cur.Type, Value = cur.Value, Icon = "" });
            if (res != null)
            {
                dlgApps[sel] = new PresetApp { Name = res.Name, Type = res.Type, Value = res.Value };
                UpdateList();
            }
        };
        rmAppBtn.Click += (s, e) =>
        {
            int sel = listBox.SelectedIndex;
            if (sel >= 0) { dlgApps.RemoveAt(sel); UpdateList(); }
        };

        var okBtn = new Button
        {
            Text = isEdit ? "Save" : "Add preset",
            Size = new Size(122, 34), Location = new Point(418, 394),
            Name = "primaryBtn",
        };
        Ui.StyleButton(okBtn, "primary");
        dlg.Controls.Add(okBtn);
        var cancelBtn = new Button
        {
            Text = "Cancel", Size = new Size(96, 34), Location = new Point(18, 394),
            DialogResult = DialogResult.Cancel,
        };
        Ui.StyleButton(cancelBtn, "secondary");
        dlg.Controls.Add(cancelBtn);
        dlg.CancelButton = cancelBtn;

        okBtn.Click += (s, e) =>
        {
            if (nameBox.Text.Trim().Length == 0)
            {
                MessageBox.Show("Please give the preset a name.", "Missing name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        };

        Preset result = null;
        if (dlg.ShowDialog() == DialogResult.OK)
            result = new Preset { Name = nameBox.Text.Trim(), Apps = dlgApps };
        dlg.Dispose();
        return result;
    }
}
