namespace AeroVRC;

// ============================================================================
//  BOOKMARKS PAGE  (save world/instance, quick rejoin, paste-a-link launch)
// ============================================================================

public partial class MainForm
{
    internal Panel pgBm;
    FlowLayoutPanel bmList;

    void BuildBookmarksPage()
    {
        pgBm = NewPage("Bookmarks");
        var bmTitle = NewPageTitle("Bookmarks");
        pgBm.Controls.Add(bmTitle);

        var bmSub = new Label
        {
            Name = "muted",
            Text = "Saved worlds/instances. Use the star on the Dashboard to bookmark the world you're in, then rejoin here.",
            Font = Ui.FontMuted, AutoSize = true, Location = new Point(4, 46),
        };
        pgBm.Controls.Add(bmSub);

        // Paste-a-link launch: accepts vrchat.com share links, vrchat:// URLs, or raw ids.
        var bmLinkBox = new TextBox
        {
            Location = new Point(4, 74), Width = 420, Font = Ui.FontBody,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
        };
        pgBm.Controls.Add(bmLinkBox);

        var bmLinkBtn = new Button { Text = "Launch link", Size = new Size(100, 30), Location = new Point(432, 72) };
        Ui.StyleButton(bmLinkBtn, "primary");
        bmLinkBtn.Click += (s, e) =>
        {
            var id = ConvertVrcLink(bmLinkBox.Text);
            if (id == null)
            {
                MessageBox.Show("That doesn't look like a VRChat world/instance link.\n\nAccepted: vrchat.com share links, vrchat:// URLs, or a raw wrld_... id.",
                    "Invalid link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!id.Contains(':')) WriteLog("Link has no instance id - VRChat may open a new/home instance.");
            WriteLog($"Launching pasted link: {id}");
            bmLinkBox.Clear();
            StartVRChat(id);
        };
        pgBm.Controls.Add(bmLinkBtn);

        var bmImportBtn = new Button { Text = "Import", Size = new Size(76, 30), Location = new Point(540, 72) };
        Ui.StyleButton(bmImportBtn, "secondary");
        bmImportBtn.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dlg.FileName));
                int added = 0;
                var items = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? doc.RootElement.EnumerateArray().ToList()
                    : new List<System.Text.Json.JsonElement> { doc.RootElement };
                foreach (var b in items)
                {
                    string S(string name) => b.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() ?? "" : "";
                    var instId = S("InstanceId");
                    if (instId.Length == 0) continue;
                    if (config.Bookmarks.Any(x => x.InstanceId == instId)) continue;
                    config.Bookmarks.Add(new Bookmark
                    {
                        Name = S("Name"), World = S("World"), InstanceId = instId,
                        Added = S("Added"), Note = S("Note"),
                        Pinned = b.TryGetProperty("Pinned", out var pv) && pv.ValueKind == System.Text.Json.JsonValueKind.True,
                    });
                    added++;
                }
                SaveConfig();
                RebuildBookmarks();
                WriteLog($"Imported {added} bookmark(s) from: {dlg.FileName}");
            }
            catch (Exception ex) { WriteLog($"Bookmark import failed: {ex.Message}"); }
        };
        pgBm.Controls.Add(bmImportBtn);

        var bmExportBtn = new Button { Text = "Export", Size = new Size(76, 30), Location = new Point(624, 72) };
        Ui.StyleButton(bmExportBtn, "secondary");
        bmExportBtn.Click += (s, e) =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "AeroVRC-bookmarks.json",
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dlg.FileName, System.Text.Json.JsonSerializer.Serialize(config.Bookmarks,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
                WriteLog($"Bookmarks exported to: {dlg.FileName}");
            }
            catch (Exception ex) { WriteLog($"Bookmark export failed: {ex.Message}"); }
        };
        pgBm.Controls.Add(bmExportBtn);

        var bmSurpriseBtn = new Button { Text = "Surprise me", Size = new Size(108, 30), Location = new Point(708, 72) };
        Ui.StyleButton(bmSurpriseBtn, "secondary");
        bmSurpriseBtn.Click += (s, e) =>
        {
            var bms = config.Bookmarks;
            if (bms.Count == 0)
            {
                MessageBox.Show("No bookmarks yet - save a few worlds first.", "Nothing to pick", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var pick = bms[Random.Shared.Next(bms.Count)];
            WriteLog($"Surprise! Off to: {pick.World}");
            InvokeRejoin(pick.InstanceId);
        };
        pgBm.Controls.Add(bmSurpriseBtn);

        var bmLinkHint = new Label
        {
            Name = "muted",
            Text = "Paste a vrchat.com share link, vrchat:// URL, or wrld_... id - launches in your chosen mode.",
            Font = Ui.FontSmall, AutoSize = true, Location = new Point(6, 102),
        };
        pgBm.Controls.Add(bmLinkHint);

        bmList = new FlowLayoutPanel
        {
            Location = new Point(4, 126), Size = new Size(852, 488),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true,
            BackColor = Color.Transparent,
        };
        pgBm.Controls.Add(bmList);
    }

    // Edit dialog: rename a bookmark and/or attach a note.
    void ShowBookmarkEdit(string instanceId)
    {
        var bm = config.Bookmarks.FirstOrDefault(b => b.InstanceId == instanceId);
        if (bm == null) return;
        using var dlg = new Form
        {
            Text = "Edit bookmark",
            ClientSize = new Size(440, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Ui.Surface, Font = Ui.FontBody,
        };
        if (Icon != null) dlg.Icon = Icon;

        dlg.Controls.Add(new Label { Text = "Name:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(16, 18) });
        var nBox = new TextBox { Location = new Point(80, 15), Width = 340, Text = bm.Name, BackColor = Ui.InputBg, ForeColor = Ui.Text };
        dlg.Controls.Add(nBox);

        dlg.Controls.Add(new Label { Text = "Note:", AutoSize = true, ForeColor = Ui.Text, Location = new Point(16, 56) });
        var tBox = new TextBox { Location = new Point(80, 53), Width = 340, Text = bm.Note, BackColor = Ui.InputBg, ForeColor = Ui.Text };
        dlg.Controls.Add(tBox);

        var ok = new Button { Text = "Save", Size = new Size(88, 30), Location = new Point(240, 100), DialogResult = DialogResult.OK };
        Ui.StyleButton(ok, "primary");
        dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;
        var cancel = new Button { Text = "Cancel", Size = new Size(88, 30), Location = new Point(334, 100), DialogResult = DialogResult.Cancel };
        Ui.StyleButton(cancel, "secondary");
        dlg.Controls.Add(cancel);
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            if (nBox.Text.Trim().Length > 0) bm.Name = nBox.Text.Trim();
            bm.Note = tBox.Text.Trim();
            SaveConfig();
            RebuildBookmarks();
        }
    }

    void SetBookmarkPinned(string instanceId)
    {
        foreach (var b in config.Bookmarks)
        {
            if (b.InstanceId == instanceId) { b.Pinned = !b.Pinned; break; }
        }
        SaveConfig();
        RebuildBookmarks();
    }

    internal void RebuildBookmarks()
    {
        if (bmList == null) return;
        bmList.SuspendLayout();
        bmList.Controls.Clear();
        if (config.Bookmarks.Count == 0)
        {
            bmList.Controls.Add(new Label
            {
                Name = "muted", Text = "No bookmarks yet.", Font = Ui.FontBody, AutoSize = true,
                Margin = new Padding(2, 8, 0, 0),
            });
        }
        else
        {
            // Pinned first, then newest-first within each group.
            var pinned = config.Bookmarks.Where(b => b.Pinned).Reverse();
            var rest = config.Bookmarks.Where(b => !b.Pinned).Reverse();
            foreach (var b in pinned.Concat(rest))
            {
                var card = Ui.NewCard();
                card.Size = new Size(812, 62);
                card.Margin = new Padding(0, 0, 0, 8);

                var disp = !string.IsNullOrEmpty(b.Name) ? b.Name : b.World;
                var nm = new Label
                {
                    Name = "onCard", Font = Ui.FontHeader, AutoSize = true,
                    Text = (b.Pinned ? "⚑ " : "") + disp,
                    Location = new Point(14, 10),
                    MaximumSize = new Size(470, 0),
                };
                card.Controls.Add(nm);

                var inst = b.InstanceId.Split(':').Last();
                // Group instances have huge ids - keep the line clear of the buttons.
                if (inst.Length > 28) inst = inst[..28] + "...";
                var subText = $"Instance {inst}   -   saved {b.Added}";
                if (!string.IsNullOrEmpty(b.Note))
                {
                    var note = b.Note;
                    if (note.Length > 34) note = note[..34] + "...";
                    subText += $"   -   {note}";
                }
                var sub = new Label
                {
                    Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
                    Text = subText,
                    Location = new Point(16, 34),
                    MaximumSize = new Size(470, 0),
                };
                card.Controls.Add(sub);

                var rejoin = new Button
                {
                    Text = "Rejoin", Size = new Size(84, 32), Location = new Point(502, 15),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = b.InstanceId,
                };
                Ui.StyleButton(rejoin, "primary");
                rejoin.Click += (s, e) => InvokeRejoin((string)((Button)s).Tag);
                card.Controls.Add(rejoin);

                var pin = new Button
                {
                    Text = b.Pinned ? "Unpin" : "Pin", Size = new Size(62, 32), Location = new Point(594, 15),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = b.InstanceId,
                };
                Ui.StyleButton(pin, "secondary");
                pin.Click += (s, e) => SetBookmarkPinned((string)((Button)s).Tag);
                card.Controls.Add(pin);

                var edit = new Button
                {
                    Text = "Edit", Size = new Size(58, 32), Location = new Point(662, 15),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = b.InstanceId,
                };
                Ui.StyleButton(edit, "secondary");
                edit.Click += (s, e) => ShowBookmarkEdit((string)((Button)s).Tag);
                card.Controls.Add(edit);

                var rem = new Button
                {
                    Text = "Remove", Size = new Size(76, 32), Location = new Point(726, 15),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = b.InstanceId,
                };
                Ui.StyleButton(rem, "secondary");
                rem.Click += (s, e) => RemoveBookmark((string)((Button)s).Tag);
                card.Controls.Add(rem);

                bmList.Controls.Add(card);
            }
        }
        bmList.ResumeLayout();
        ThemeWalk(bmList);
    }
}
