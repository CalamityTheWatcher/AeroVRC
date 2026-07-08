using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace AeroVRC;

// ============================================================================
//  PHOTOS PAGE  (thumbnail browser: search, hover animation, right-click menu)
// ============================================================================

public partial class MainForm
{
    internal class PhotoItem
    {
        public FileInfo File;
        public string World = "";
        public string WorldId = "";
        public DateTime TimeUtc;
    }
    internal class PhotoTileData
    {
        public string Path;
        public string World;
        public Bitmap Bmp;
        public double Hover;
        public double HoverT;
    }
    internal class LocRange
    {
        public DateTime Start, End;
        public string World = "", WorldId = "";
    }

    internal Panel pgPhotos;
    ComboBox photoWorldCombo;
    TextBox photoSearch;
    FlowLayoutPanel photoGrid;
    Label photoStatus;
    ToolTip photoTip;
    ContextMenuStrip photoMenu;
    internal System.Windows.Forms.Timer photoLoadTimer;
    System.Windows.Forms.Timer photoSearchTimer;
    readonly Queue<PhotoItem> photoQueue = new();
    internal List<PhotoItem> photoAll = new();
    internal readonly List<Panel> photoActive = new();
    bool photoBuilding;
    Panel photoCtxTile;
    readonly Dictionary<string, List<string>> photoPeople = new();   // path -> display names cache
    List<LocRange> vrcxLocRanges = new();                             // UTC visit windows from VRCX

    void BuildPhotosPage()
    {
        pgPhotos = NewPage("Photos");
        var photosTitle = NewPageTitle("Photos");
        pgPhotos.Controls.Add(photosTitle);

        var photosSub = new Label
        {
            Name = "muted",
            Text = "Your VRChat photos. Filter by world, search by name, or right-click a photo for more.",
            Font = Ui.FontMuted, AutoSize = true, Location = new Point(4, 46),
        };
        pgPhotos.Controls.Add(photosSub);

        // Row 1: world filter + action buttons
        var photoBar = new FlowLayoutPanel
        {
            Location = new Point(4, 72), Size = new Size(840, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            WrapContents = false, BackColor = Color.Transparent,
        };
        pgPhotos.Controls.Add(photoBar);

        photoBar.Controls.Add(new Label
        {
            Name = "muted", Text = "World:", Font = Ui.FontBody, AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
        });

        photoWorldCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300, Font = Ui.FontBody,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Margin = new Padding(0, 4, 10, 0),
        };
        photoWorldCombo.SelectedIndexChanged += (s, e) => { if (!loading && !photoBuilding) ApplyPhotoFilter(); };
        photoBar.Controls.Add(photoWorldCombo);

        var photoRefreshBtn = NewToolbarButton("Refresh");
        photoRefreshBtn.Click += (s, e) => ScanPhotos();
        photoBar.Controls.Add(photoRefreshBtn);
        var photoOpenBtn = NewToolbarButton("Open folder");
        photoOpenBtn.Click += (s, e) =>
        {
            if (Directory.Exists(photoDir))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(photoDir) { UseShellExecute = true });
            else
                MessageBox.Show($"No VRChat photos folder found at:\n\n{photoDir}", "Photos", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        photoBar.Controls.Add(photoOpenBtn);
        var photoSortBtn = NewToolbarButton("Sort into world folders");
        photoSortBtn.Click += (s, e) => InvokePhotoSort();
        photoBar.Controls.Add(photoSortBtn);
        var photoDupBtn = NewToolbarButton("Find duplicates");
        photoDupBtn.Click += (s, e) => ShowDuplicatePhotos();
        photoBar.Controls.Add(photoDupBtn);

        photoStatus = new Label
        {
            Name = "muted", Text = "", Font = Ui.FontMuted, AutoSize = true,
            Margin = new Padding(10, 9, 0, 0),
        };
        photoBar.Controls.Add(photoStatus);

        // Row 2: search box (directly below the world dropdown)
        photoSearch = new TextBox
        {
            Location = new Point(4, 116), Width = 300, Font = Ui.FontBody,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
        };
        pgPhotos.Controls.Add(photoSearch);
        pgPhotos.Controls.Add(new Label
        {
            Name = "muted", Text = "Search by file name or world...", Font = Ui.FontMuted,
            AutoSize = true, Location = new Point(312, 119),
        });

        photoGrid = new FlowLayoutPanel
        {
            Location = new Point(4, 150), Size = new Size(840, 464),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AutoScroll = true, BackColor = Color.Transparent,
        };
        pgPhotos.Controls.Add(photoGrid);

        photoTip = new ToolTip();

        // Debounce search typing so we don't refilter on every keystroke.
        photoSearchTimer = new System.Windows.Forms.Timer { Interval = 250 };
        photoSearchTimer.Tick += (s, e) => { photoSearchTimer.Stop(); ApplyPhotoFilter(); };
        photoSearch.TextChanged += (s, e) => { if (!loading) { photoSearchTimer.Stop(); photoSearchTimer.Start(); } };

        // ---- Right-click menu (dark themed) ----
        photoMenu = new ContextMenuStrip { BackColor = Ui.Card, ForeColor = Ui.Text };
        try { photoMenu.Renderer = new ToolStripProfessionalRenderer(new AeroMenuColors()); } catch { }
        ToolStripMenuItem NewPhotoMenuItem(string text, Action onClick)
        {
            var mi = new ToolStripMenuItem(text) { ForeColor = Ui.Text };
            mi.Click += (s, e) => onClick();
            photoMenu.Items.Add(mi);
            return mi;
        }
        photoMenu.Opening += (s, e) => photoCtxTile = photoMenu.SourceControl as Panel;
        PhotoTileData CtxData() => photoCtxTile?.Tag as PhotoTileData;
        NewPhotoMenuItem("Open", () =>
        {
            var d = CtxData();
            if (d != null) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(d.Path) { UseShellExecute = true }); } catch { } }
        });
        NewPhotoMenuItem("Open file location", () =>
        {
            var d = CtxData();
            if (d != null) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{d.Path}\"");
        });
        photoMenu.Items.Add(new ToolStripSeparator());
        NewPhotoMenuItem("Copy image", () =>
        {
            var d = CtxData();
            if (d != null)
            {
                try
                {
                    using var im = Image.FromFile(d.Path);
                    Clipboard.SetImage(im);
                    WriteLog("Photo copied to clipboard.");
                }
                catch { }
            }
        });
        NewPhotoMenuItem("Copy file", () =>
        {
            var d = CtxData();
            if (d != null)
            {
                try
                {
                    var sc = new System.Collections.Specialized.StringCollection { d.Path };
                    Clipboard.SetFileDropList(sc);
                }
                catch { }
            }
        });
        NewPhotoMenuItem("Copy path", () =>
        {
            var d = CtxData();
            if (d != null) Clipboard.SetText(d.Path);
        });
        NewPhotoMenuItem("Set as wallpaper", () =>
        {
            var d = CtxData();
            if (d != null)
            {
                if (Ui.SetWallpaper(d.Path)) WriteLog("Desktop wallpaper set.");
                else WriteLog("Set wallpaper failed.");
            }
        });
        photoMenu.Items.Add(new ToolStripSeparator());
        NewPhotoMenuItem("Properties", () =>
        {
            var d = CtxData();
            if (d != null) ShowPhotoProperties(d.Path);
        });
        NewPhotoMenuItem("Rename...", () => { if (photoCtxTile != null) InvokePhotoRename(photoCtxTile); });
        var miDelete = NewPhotoMenuItem("Delete", () => { if (photoCtxTile != null) InvokePhotoDelete(photoCtxTile); });
        miDelete.ForeColor = Ui.Danger;

        // Thumbnails load a few per tick so the UI never freezes on a big folder.
        photoLoadTimer = new System.Windows.Forms.Timer { Interval = 40 };
        photoLoadTimer.Tick += (s, e) =>
        {
            int loaded = 0;
            while (photoQueue.Count > 0 && loaded < 3)
            {
                var item = photoQueue.Dequeue();
                var tile = NewPhotoTile(item);
                if (tile != null) photoGrid.Controls.Add(tile);
                loaded++;
            }
            if (photoQueue.Count == 0)
            {
                photoLoadTimer.Stop();
                photoStatus.Text = $"{photoGrid.Controls.Count} photo(s).";
            }
            else
            {
                photoStatus.Text = $"{photoGrid.Controls.Count} shown, {photoQueue.Count} loading...";
            }
        };

        RebuildPhotoWorldFilter();
    }

    // ---- World <- photo timestamp mapping ----
    List<LocRange> GetPhotoWorldMap()
    {
        var ranges = new List<LocRange>();
        foreach (var wv in config.WorldHistory)
        {
            if (DateTime.TryParse(wv.Time, out var dt))
                ranges.Add(new LocRange { Start = dt, End = dt.AddSeconds(wv.DurationSec + 180), World = wv.World });
        }
        return ranges;
    }
    static string GetPhotoWorldFor(DateTime ft, List<LocRange> ranges)
    {
        foreach (var r in ranges)
            if (ft >= r.Start && ft <= r.End) return r.World;
        return "";
    }

    // ---- VRCX-enhanced photo tagging ----
    static string FromXmlEntities(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
    }

    class PhotoMeta { public string World = ""; public string WorldId = ""; public string Author = ""; public string CreateDate = ""; }

    // Reads a VRChat screenshot's embedded XMP (world/author/time) by walking only
    // the PNG text chunks - never touches the pixel data, so it stays fast.
    static PhotoMeta GetPhotoMeta(string path)
    {
        if (!path.ToLower().EndsWith(".png")) return null;
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            var sig = br.ReadBytes(8);
            if (sig.Length < 8 || sig[0] != 0x89) return null;
            string xmp = null;
            while (fs.Position < fs.Length - 8)
            {
                var lenB = br.ReadBytes(4);
                if (lenB.Length < 4) break;
                Array.Reverse(lenB);
                uint len = BitConverter.ToUInt32(lenB, 0);
                var type = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (type == "IDAT" || type == "IEND") break;
                if ((type == "iTXt" || type == "tEXt") && len <= 1000000)
                {
                    var s = Encoding.UTF8.GetString(br.ReadBytes((int)len));
                    if (s.Contains("vrc:WorldDisplayName")) { xmp = s; break; }
                }
                else
                {
                    fs.Seek(len, SeekOrigin.Current);   // skip chunk data
                }
                fs.Seek(4, SeekOrigin.Current);          // skip CRC
            }
            if (xmp == null) return null;
            var m = new PhotoMeta();
            var mm = System.Text.RegularExpressions.Regex.Match(xmp, "<vrc:WorldDisplayName>(.*?)</vrc:WorldDisplayName>");
            if (mm.Success) m.World = FromXmlEntities(mm.Groups[1].Value).Trim();
            mm = System.Text.RegularExpressions.Regex.Match(xmp, "<vrc:WorldID>(.*?)</vrc:WorldID>");
            if (mm.Success) m.WorldId = mm.Groups[1].Value.Trim();
            mm = System.Text.RegularExpressions.Regex.Match(xmp, "<xmp:Author>(.*?)</xmp:Author>");
            if (mm.Success) m.Author = FromXmlEntities(mm.Groups[1].Value).Trim();
            mm = System.Text.RegularExpressions.Regex.Match(xmp, "<xmp:CreateDate>(.*?)</xmp:CreateDate>");
            if (mm.Success) m.CreateDate = mm.Groups[1].Value.Trim();
            return m;
        }
        catch { return null; }
    }

    // UTC visit windows from VRCX's gamelog_location, for time-matching photos
    // when a photo has no embedded world. Cheap; rebuilt once per photo scan.
    void UpdateVrcxLocationRanges()
    {
        vrcxLocRanges = new List<LocRange>();
        if (!TestVrcxReady()) return;
        var rows = GetVrcxRows("SELECT created_at, world_name, world_id, time FROM gamelog_location WHERE world_id<>'' ORDER BY created_at");
        foreach (var r in rows)
        {
            if (DateTime.TryParse(r.GetValueOrDefault("created_at"), out var dt))
            {
                var start = dt.ToUniversalTime();
                int.TryParse(r.GetValueOrDefault("time"), out var dur);
                var end = start.AddSeconds(dur / 1000 + 300);
                vrcxLocRanges.Add(new LocRange { Start = start, End = end, World = r.GetValueOrDefault("world_name") ?? "", WorldId = r.GetValueOrDefault("world_id") ?? "" });
            }
        }
    }

    // Resolve a photo -> world/id/time. Priority: (1) VRChat's embedded XMP world,
    // (2) VRCX's visit log matched by time, (3) AeroVRC's own WorldHistory.
    (string World, string WorldId, DateTime TimeUtc) ResolvePhotoWorld(FileInfo file, List<LocRange> whRanges)
    {
        var timeUtc = file.LastWriteTimeUtc;
        var meta = GetPhotoMeta(file.FullName);
        if (meta != null)
        {
            if (meta.CreateDate.Length > 0)
            {
                try { timeUtc = DateTimeOffset.Parse(meta.CreateDate).UtcDateTime; } catch { }
            }
            if (meta.World.Length > 0) return (meta.World, meta.WorldId, timeUtc);
        }
        foreach (var r in vrcxLocRanges)
            if (timeUtc >= r.Start && timeUtc <= r.End) return (r.World, r.WorldId, timeUtc);
        var wn = GetPhotoWorldFor(file.LastWriteTime, whRanges);
        return (wn, "", timeUtc);
    }

    // Who was in the instance when the photo was taken (VRCX join/leave). Bounded
    // to the current visit when known, else the preceding 3 hours; approximate.
    List<string> GetPhotoPeople(DateTime utc)
    {
        if (!TestVrcxReady()) return new List<string>();
        var lo = utc.AddMinutes(-180);
        foreach (var r in vrcxLocRanges)
        {
            if (utc >= r.Start && utc <= r.End) { if (r.Start > lo) lo = r.Start; break; }
        }
        var hiS = utc.ToString("yyyy-MM-ddTHH:mm:ss");
        var loS = lo.ToString("yyyy-MM-ddTHH:mm:ss");
        var rows = GetVrcxRows($@"
SELECT display_name FROM (
  SELECT user_id, display_name, type,
    ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY created_at DESC) rn
  FROM gamelog_join_leave
  WHERE substr(created_at,1,19) <= '{hiS}' AND substr(created_at,1,19) >= '{loS}'
) t WHERE rn=1 AND type='OnPlayerJoined' ORDER BY display_name COLLATE NOCASE");
        return rows.Select(r => r.GetValueOrDefault("display_name") ?? "").Where(n => n.Length > 0).ToList();
    }

    // ---- Custom photo tile: hover zoom + accent border + caption ----
    void DrawPhotoTile(Panel tile, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Ui.OpaqueBack(tile));
        var d = (PhotoTileData)tile.Tag;
        var bmp = d.Bmp;
        if (bmp == null) return;
        double hov = d.Hover;
        const int pad = 4;
        int iw = tile.Width - 2 * pad, ih = tile.Height - 2 * pad;
        using var clipPath = Ui.RoundedPath(pad, pad, iw, ih, 6);
        var saved = g.Save();
        g.SetClip(clipPath);
        double s = 1.0 + 0.10 * hov;
        double dw = iw * s, dh = ih * s;
        g.DrawImage(bmp, (float)(pad - (dw - iw) / 2), (float)(pad - (dh - ih) / 2), (float)dw, (float)dh);
        if (hov > 0.02)
        {
            const int ch = 28;
            using (var ob = new SolidBrush(Color.FromArgb((int)(160 * hov), 0, 0, 0)))
                g.FillRectangle(ob, pad, pad + ih - ch, iw, ch);
            var cap = !string.IsNullOrEmpty(d.World) ? d.World : Path.GetFileName(d.Path);
            using var cb = new SolidBrush(Color.FromArgb((int)(255 * hov), Color.White));
            using var cf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(cap, Ui.FontSmall, cb, new RectangleF(pad + 6, pad + ih - ch, iw - 12, ch), cf);
        }
        g.Restore(saved);
        if (hov > 0.02)
        {
            using var bp = Ui.RoundedPath(pad + 0.5f, pad + 0.5f, iw - 1, ih - 1, 6);
            using var pen = new Pen(Color.FromArgb((int)(230 * hov), Ui.Accent), 2);
            g.DrawPath(pen, bp);
        }
    }

    Panel NewPhotoTile(PhotoItem item)
    {
        var file = item.File;
        var world = item.World;
        const int tw = 176, th = 108;
        Image img;
        try { img = Image.FromFile(file.FullName); }
        catch { return null; }
        // 16-bpp thumbnails: half the memory of 32-bpp, no visible loss at this size.
        var bmp = new Bitmap(tw, th, PixelFormat.Format16bppRgb565);
        using (var gg = Graphics.FromImage(bmp))
        {
            gg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gg.Clear(Ui.LogBg);
            double ar = img.Width / (double)img.Height, tar = tw / (double)th;
            int dw, dh;
            if (ar > tar) { dw = tw; dh = (int)(tw / ar); }
            else { dh = th; dw = (int)(th * ar); }
            gg.DrawImage(img, (tw - dw) / 2, (th - dh) / 2, dw, dh);
        }
        img.Dispose();
        var tile = new Panel
        {
            Size = new Size(tw + 8, th + 8),
            Margin = new Padding(5),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            ContextMenuStrip = photoMenu,
            Tag = new PhotoTileData { Path = file.FullName, World = world, Bmp = bmp, Hover = 0.0, HoverT = 0.0 },
        };
        Ui.SetDoubleBuffered(tile);
        tile.Paint += (s, e) => DrawPhotoTile((Panel)s, e.Graphics);
        tile.MouseEnter += (s, e) =>
        {
            var t = (Panel)s;
            ((PhotoTileData)t.Tag).HoverT = 1.0;
            if (!photoActive.Contains(t)) photoActive.Add(t);
        };
        tile.MouseLeave += (s, e) =>
        {
            var t = (Panel)s;
            ((PhotoTileData)t.Tag).HoverT = 0.0;
            if (!photoActive.Contains(t)) photoActive.Add(t);
        };
        tile.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                var t = (Panel)s;
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(((PhotoTileData)t.Tag).Path) { UseShellExecute = true }); } catch { }
            }
        };
        // Tooltip: world (metadata / VRCX) + who was present (VRCX) + filename.
        var tip = !string.IsNullOrEmpty(world) ? world : "(unknown world)";
        if (TestVrcxReady() && item.TimeUtc != default)
        {
            if (!photoPeople.TryGetValue(file.FullName, out var ppl))
            {
                ppl = GetPhotoPeople(item.TimeUtc);
                photoPeople[file.FullName] = ppl;
            }
            if (ppl.Count > 0)
            {
                var shown = string.Join(", ", ppl.Take(12));
                tip += $"\r\n{ppl.Count} in instance: {shown}";
                if (ppl.Count > 12) tip += $", +{ppl.Count - 12} more";
            }
        }
        tip += $"\r\n{file.Name}";
        photoTip.SetToolTip(tile, tip);
        return tile;
    }

    internal void ClearPhotoGrid()
    {
        photoLoadTimer?.Stop();
        photoActive.Clear();
        foreach (Control c in photoGrid.Controls.Cast<Control>().ToList())
        {
            try
            {
                if (c.Tag is PhotoTileData d && d.Bmp != null) d.Bmp.Dispose();
                c.Dispose();
            }
            catch { }
        }
        photoGrid.Controls.Clear();
    }

    void RebuildPhotoWorldFilter()
    {
        photoBuilding = true;
        var prev = photoWorldCombo.SelectedItem as string;
        photoWorldCombo.Items.Clear();
        photoWorldCombo.Items.Add("All worlds");
        // Build the filter from the worlds actually tagged on photos, falling back
        // to WorldHistory before the first scan runs.
        var src = photoAll.Count > 0
            ? photoAll.Select(p => p.World)
            : config.WorldHistory.Select(w => w.World);
        var worlds = src.Where(w => !string.IsNullOrEmpty(w) && w != "Unknown world").Distinct().OrderBy(w => w).ToList();
        foreach (var w in worlds) photoWorldCombo.Items.Add(w);
        int idx = prev != null ? photoWorldCombo.Items.IndexOf(prev) : 0;
        if (idx < 0) idx = 0;
        photoWorldCombo.SelectedIndex = idx;
        photoBuilding = false;
    }

    // Rescans the folder into the cache, then applies the current filters.
    internal void ScanPhotos()
    {
        ClearPhotoGrid();
        if (!Directory.Exists(photoDir))
        {
            photoStatus.Text = "No VRChat photos folder found.";
            photoAll = new List<PhotoItem>();
            return;
        }
        var ranges = GetPhotoWorldMap();
        UpdateVrcxLocationRanges();
        photoPeople.Clear();
        List<FileInfo> files = new();
        try
        {
            var di = new DirectoryInfo(photoDir);
            files = di.EnumerateFiles("*.png", SearchOption.AllDirectories)
                      .Concat(di.EnumerateFiles("*.jpg", SearchOption.AllDirectories))
                      .OrderByDescending(f => f.LastWriteTime)
                      .ToList();
        }
        catch { }
        photoAll = new List<PhotoItem>();
        foreach (var f in files)
        {
            var (world, worldId, timeUtc) = ResolvePhotoWorld(f, ranges);
            photoAll.Add(new PhotoItem { File = f, World = world, WorldId = worldId, TimeUtc = timeUtc });
        }
        RebuildPhotoWorldFilter();
        ApplyPhotoFilter();
    }

    // Builds the load queue from the cached scan using the world + search filters.
    void ApplyPhotoFilter()
    {
        ClearPhotoGrid();
        var filter = photoWorldCombo.SelectedItem as string;
        var q = photoSearch.Text.Trim().ToLower();
        photoQueue.Clear();
        foreach (var it in photoAll)
        {
            if (!string.IsNullOrEmpty(filter) && filter != "All worlds" && it.World != filter) continue;
            if (q.Length > 0)
            {
                var hay = (it.File.Name + " " + it.World).ToLower();
                if (!hay.Contains(q)) continue;
            }
            photoQueue.Enqueue(it);
            if (photoQueue.Count >= 400) break;
        }
        if (photoQueue.Count == 0) { photoStatus.Text = "No photos match."; return; }
        photoStatus.Text = $"Loading {photoQueue.Count} photo(s)...";
        photoLoadTimer.Start();
    }

    // ---- Right-click actions ----
    void InvokePhotoRename(Panel tile)
    {
        var d = (PhotoTileData)tile.Tag;
        var path = d.Path;
        if (!File.Exists(path)) return;
        var dir = Path.GetDirectoryName(path);
        var ext = Path.GetExtension(path);
        var cur = Path.GetFileNameWithoutExtension(path);
        var newName = Microsoft.VisualBasic.Interaction.InputBox("New name (without extension):", "Rename photo", cur);
        if (string.IsNullOrEmpty(newName)) return;
        newName = System.Text.RegularExpressions.Regex.Replace(newName, "[\\\\/:*?\"<>|]", "_").Trim();
        if (newName.Length == 0 || newName == cur) return;
        var target = Path.Combine(dir, newName + ext);
        if (File.Exists(target))
        {
            MessageBox.Show("A file with that name already exists.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            File.Move(path, target);
            d.Path = target;
            photoTip.SetToolTip(tile, !string.IsNullOrEmpty(d.World) ? $"{d.World}\r\n{newName}{ext}" : $"{newName}{ext}");
            foreach (var it in photoAll)
            {
                if (it.File.FullName == path) { it.File = new FileInfo(target); break; }
            }
            WriteLog($"Renamed photo to {newName}{ext}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rename failed: {ex.Message}", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void InvokePhotoDelete(Panel tile)
    {
        var d = (PhotoTileData)tile.Tag;
        var path = d.Path;
        if (!File.Exists(path)) return;
        var r = MessageBox.Show($"Move this photo to the Recycle Bin?\n\n{Path.GetFileName(path)}", "Delete photo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            photoAll = photoAll.Where(it => it.File.FullName != path).ToList();
            photoActive.Remove(tile);
            photoGrid.Controls.Remove(tile);
            d.Bmp?.Dispose();
            tile.Dispose();
            photoStatus.Text = $"{photoGrid.Controls.Count} photo(s).";
            WriteLog("Photo sent to Recycle Bin.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void InvokePhotoSort()
    {
        if (!Directory.Exists(photoDir))
        {
            MessageBox.Show("No VRChat photos folder found.", "Photos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var r = MessageBox.Show("Move photos into subfolders named after the world they were taken in?\n\nThis moves files on disk. Photos that can't be matched to a world are left where they are.",
            "Sort photos into world folders", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        photoLoadTimer.Stop();
        var ranges = GetPhotoWorldMap();
        UpdateVrcxLocationRanges();
        List<FileInfo> files = new();
        try
        {
            var di = new DirectoryInfo(photoDir);
            files = di.EnumerateFiles("*.png", SearchOption.AllDirectories)
                      .Concat(di.EnumerateFiles("*.jpg", SearchOption.AllDirectories))
                      .ToList();
        }
        catch { }
        int moved = 0, skipped = 0;
        foreach (var f in files)
        {
            var wn = ResolvePhotoWorld(f, ranges).World;
            if (string.IsNullOrEmpty(wn)) { skipped++; continue; }
            var safe = System.Text.RegularExpressions.Regex.Replace(wn, "[\\\\/:*?\"<>|]", "_").Trim();
            if (safe.Length > 60) safe = safe[..60].Trim();
            if (safe.Length == 0) { skipped++; continue; }
            var dest = Path.Combine(photoDir, safe);
            try
            {
                Directory.CreateDirectory(dest);
                var target = Path.Combine(dest, f.Name);
                if (string.Equals(f.FullName, target, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(target)) { skipped++; continue; }
                File.Move(f.FullName, target);
                moved++;
            }
            catch { skipped++; }
        }
        WriteLog($"Sorted {moved} photo(s) into world folders ({skipped} skipped/unmatched).");
        MessageBox.Show($"Moved {moved} photo(s) into world folders.\n{skipped} left in place (no world match or name clash).",
            "Sort complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        ScanPhotos();
    }

    // Properties dialog: file details + overall storage-by-world breakdown.
    void ShowPhotoProperties(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var fi = new FileInfo(path);
        var world = "(unknown)";
        foreach (var it in photoAll)
        {
            if (it.File.FullName == path) { if (!string.IsNullOrEmpty(it.World)) world = it.World; break; }
        }
        var dim = "?";
        try
        {
            using var img = Image.FromFile(path);
            dim = $"{img.Width} x {img.Height}";
        }
        catch { }
        var people = photoPeople.TryGetValue(path, out var ppl) ? string.Join(", ", ppl) : "";
        var sb = new StringBuilder();
        sb.AppendLine($"File:        {fi.Name}");
        sb.AppendLine($"Folder:      {fi.DirectoryName}");
        sb.AppendLine($"Size:        {fi.Length / 1024.0:N0} KB");
        sb.AppendLine($"Dimensions:  {dim}");
        sb.AppendLine($"Taken:       {fi.LastWriteTime}");
        sb.AppendLine($"World:       {world}");
        if (people.Length > 0) sb.AppendLine($"People:      {people}");
        sb.AppendLine("");
        sb.AppendLine("STORAGE BY WORLD (all scanned photos)");
        sb.AppendLine(new string('-', 46));
        var agg = new Dictionary<string, (int Count, long Bytes)>();
        long totBytes = 0;
        int totCount = 0;
        foreach (var it in photoAll)
        {
            var w = !string.IsNullOrEmpty(it.World) ? it.World : "(unknown)";
            agg.TryGetValue(w, out var cur);
            long len = 0;
            try { len = it.File.Length; } catch { }
            agg[w] = (cur.Count + 1, cur.Bytes + len);
            totBytes += len;
            totCount++;
        }
        foreach (var e in agg.OrderByDescending(e => e.Value.Bytes).Take(25))
        {
            var key = e.Key.Length > 32 ? e.Key[..31] + "…" : e.Key;
            sb.AppendLine(string.Format("{0,-32} {1,4} photos   {2,7:N1} MB", key, e.Value.Count, e.Value.Bytes / 1048576.0));
        }
        sb.AppendLine(new string('-', 46));
        sb.AppendLine($"TOTAL: {totCount} photos, {totBytes / 1048576.0:N1} MB");

        using var dlg = new Form
        {
            Text = "Photo properties", Size = new Size(560, 560),
            StartPosition = FormStartPosition.CenterParent, BackColor = Ui.Bg,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
        };
        if (Icon != null) dlg.Icon = Icon;
        try { Ui.SetDarkTitleBar(dlg); } catch { }
        var tb = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Ui.FontMono,
            BackColor = Ui.LogBg, ForeColor = Ui.Text, BorderStyle = BorderStyle.None,
            Location = new Point(14, 14), Size = new Size(516, 452),
            Text = sb.ToString(),
        };
        tb.Select(0, 0);
        dlg.Controls.Add(tb);
        var close = new Button { Text = "Close", Size = new Size(96, 32), Location = new Point(434, 478) };
        Ui.StyleButton(close, "primary");
        close.Click += (s, e) => dlg.Close();
        dlg.Controls.Add(close);
        dlg.ShowDialog(this);
    }

    // Duplicate finder: group photos by size (cheap), hash only same-size
    // candidates, then let the user recycle-bin every extra.
    void ShowDuplicatePhotos()
    {
        if (photoAll.Count == 0) ScanPhotos();
        var files = photoAll.Select(p => p.File).ToList();
        if (files.Count == 0)
        {
            MessageBox.Show("No photos to check.", "Duplicates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Cursor = Cursors.WaitCursor;
        var dupGroups = new List<List<FileInfo>>();
        try
        {
            var bySize = new Dictionary<long, List<FileInfo>>();
            foreach (var f in files)
            {
                long s = 0;
                try { s = f.Length; } catch { }
                if (!bySize.TryGetValue(s, out var list)) { list = new List<FileInfo>(); bySize[s] = list; }
                list.Add(f);
            }
            using var md5 = System.Security.Cryptography.MD5.Create();
            foreach (var grp in bySize.Values)
            {
                if (grp.Count < 2) continue;
                var byHash = new Dictionary<string, List<FileInfo>>();
                foreach (var f in grp)
                {
                    string h = null;
                    try
                    {
                        using var fs = File.OpenRead(f.FullName);
                        h = Convert.ToHexString(md5.ComputeHash(fs));
                    }
                    catch { }
                    if (h != null)
                    {
                        if (!byHash.TryGetValue(h, out var list)) { list = new List<FileInfo>(); byHash[h] = list; }
                        list.Add(f);
                    }
                }
                foreach (var hg in byHash.Values)
                    if (hg.Count >= 2) dupGroups.Add(hg.OrderBy(f => f.LastWriteTime).ToList());
            }
        }
        finally { Cursor = Cursors.Default; }

        if (dupGroups.Count == 0)
        {
            MessageBox.Show("No duplicate photos found.", "Duplicates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        int extra = 0;
        long reclaim = 0;
        foreach (var g in dupGroups)
        {
            extra += g.Count - 1;
            for (int i = 1; i < g.Count; i++) reclaim += g[i].Length;
        }

        var dlg = new Form
        {
            Text = "Duplicate photos", Size = new Size(680, 560),
            StartPosition = FormStartPosition.CenterParent, BackColor = Ui.Bg,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
        };
        if (Icon != null) dlg.Icon = Icon;
        try { Ui.SetDarkTitleBar(dlg); } catch { }
        var lbl = new Label
        {
            Name = "title", Font = Ui.FontBody, AutoSize = true,
            Location = new Point(14, 12),
            ForeColor = Ui.Text, BackColor = Ui.Bg,
            Text = $"{dupGroups.Count} duplicate group(s), {extra} extra file(s), {reclaim / 1048576.0:N1} MB reclaimable. 'KEEP' = oldest copy.",
        };
        dlg.Controls.Add(lbl);
        var list2 = new ListBox
        {
            Name = "logBox", Font = Ui.FontMono, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false,
            Location = new Point(14, 40), Size = new Size(636, 424),
            BackColor = Ui.LogBg, ForeColor = Ui.Text,
        };
        foreach (var g in dupGroups)
        {
            list2.Items.Add($"=== group ({g.Count} copies) ===");
            for (int i = 0; i < g.Count; i++)
            {
                var tag = i == 0 ? "KEEP  " : "DELETE";
                list2.Items.Add($"  [{tag}] {g[i].FullName}");
            }
        }
        dlg.Controls.Add(list2);
        var delBtn = new Button { Text = "Delete all extras (Recycle Bin)", Size = new Size(240, 32), Location = new Point(14, 476) };
        Ui.StyleButton(delBtn, "danger");
        delBtn.Click += (s, e) =>
        {
            if (MessageBox.Show($"Send {extra} duplicate file(s) to the Recycle Bin? The oldest copy in each group is kept.",
                "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int done = 0;
            foreach (var g in dupGroups)
            {
                for (int i = 1; i < g.Count; i++)
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(g[i].FullName,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        done++;
                    }
                    catch { }
                }
            }
            WriteLog($"Duplicate cleanup: sent {done} file(s) to the Recycle Bin.");
            MessageBox.Show($"Removed {done} duplicate(s).", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            dlg.Close();
            ScanPhotos();
        };
        dlg.Controls.Add(delBtn);
        var closeBtn = new Button { Text = "Close", Size = new Size(96, 32), Location = new Point(554, 476) };
        Ui.StyleButton(closeBtn, "secondary");
        closeBtn.Click += (s, e) => dlg.Close();
        dlg.Controls.Add(closeBtn);
        dlg.ShowDialog(this);
        dlg.Dispose();
    }
}
