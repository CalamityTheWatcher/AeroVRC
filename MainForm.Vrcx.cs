using System.Text.RegularExpressions;

namespace AeroVRC;

// ============================================================================
//  VRCX INTEGRATION  (read-only access to VRCX's SQLite database)
//  Every read runs against a private SNAPSHOT copy of the DB, so VRCX's live
//  database is never opened, locked or written. The snapshot is refreshed at
//  most once per Vrcx.RefreshSec. The per-account table prefix (usr<hex>_...)
//  is discovered from sqlite_master and validated, never hard-coded.
// ============================================================================

public partial class MainForm
{
    internal string vrcxSnap;
    internal DateTime vrcxSnapAt = DateTime.MinValue;
    internal string vrcxPrefix;

    internal string GetVrcxDbPath()
    {
        var p = config.Vrcx.DbPath;
        if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VRCX\VRCX.sqlite3");
    }

    internal bool TestVrcxReady()
    {
        if (!config.Vrcx.Enabled) return false;
        return File.Exists(GetVrcxDbPath());
    }

    // Copy the live DB (+ any -wal/-shm) to a private snapshot under our config dir,
    // throttled so we don't re-copy on every query.
    internal bool UpdateVrcxSnapshot(bool force = false)
    {
        if (!TestVrcxReady()) return false;
        int refresh = config.Vrcx.RefreshSec; if (refresh < 5) refresh = 5;
        bool fresh = vrcxSnap != null && File.Exists(vrcxSnap) && (DateTime.Now - vrcxSnapAt).TotalSeconds < refresh;
        if (!force && fresh) return true;
        var src = GetVrcxDbPath();
        vrcxSnap ??= Path.Combine(ConfigStore.ConfigDir, "vrcx_snapshot.sqlite3");
        try
        {
            Directory.CreateDirectory(ConfigStore.ConfigDir);
            File.Copy(src, vrcxSnap, true);
            foreach (var ext in new[] { "-wal", "-shm" })
            {
                var s = src + ext; var d = vrcxSnap + ext;
                try
                {
                    if (File.Exists(s)) File.Copy(s, d, true);
                    else if (File.Exists(d)) File.Delete(d);
                }
                catch { }
            }
            vrcxSnapAt = DateTime.Now;
            vrcxPrefix = null;   // re-discover against the fresh snapshot
            return true;
        }
        catch (Exception ex)
        {
            WriteLog($"VRCX: snapshot copy failed - {ex.Message}");
            return false;
        }
    }

    // Raw query -> AeroSqlResult (or null on any failure). Pass trusted SQL only.
    internal AeroSqlResult InvokeVrcxQuery(string sql)
    {
        if (!UpdateVrcxSnapshot()) return null;
        try { return AeroSqlite.Query(vrcxSnap, sql); }
        catch (Exception ex) { WriteLog($"VRCX query failed: {ex.Message}"); return null; }
    }

    // Query -> list of column-name -> value rows (empty list on failure).
    internal List<Dictionary<string, string>> GetVrcxRows(string sql)
    {
        var res = InvokeVrcxQuery(sql);
        var outRows = new List<Dictionary<string, string>>();
        if (res == null) return outRows;
        foreach (var row in res.Rows)
        {
            var o = new Dictionary<string, string>();
            for (int i = 0; i < res.Columns.Length; i++) o[res.Columns[i]] = row[i];
            outRows.Add(o);
        }
        return outRows;
    }

    // First column of the first row as a string (or null). Robust for COUNT()-style
    // single-value queries.
    internal string GetVrcxScalar(string sql)
    {
        var res = InvokeVrcxQuery(sql);
        if (res != null && res.Rows.Count > 0 && res.Columns.Length > 0) return res.Rows[0][0];
        return null;
    }

    // The per-account table prefix (e.g. usra350...). Cached per snapshot, validated
    // so it is safe to interpolate into table names.
    internal string GetVrcxPrefix()
    {
        if (vrcxPrefix != null) return vrcxPrefix;
        var res = InvokeVrcxQuery("SELECT name FROM sqlite_master WHERE type='table'");
        if (res == null) return null;
        foreach (var row in res.Rows)
        {
            var m = Regex.Match(row[0], @"^(usr[0-9a-fA-F]+)_feed_online_offline$");
            if (m.Success) { vrcxPrefix = m.Groups[1].Value; return vrcxPrefix; }
        }
        return null;
    }

    internal void RemoveVrcxSnapshot()
    {
        if (vrcxSnap == null) return;
        foreach (var ext in new[] { "", "-wal", "-shm" })
        {
            var f = vrcxSnap + ext;
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }
}
