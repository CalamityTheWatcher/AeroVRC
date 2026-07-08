using System.Runtime.InteropServices;
using System.Text;

namespace AeroVRC;

// ============================================================================
//  NATIVE HELPERS
//  CoreAudio: mute/unmute the default Windows capture device (mic features).
//  winsqlite3: read-only SQLite access for the VRCX integration - the engine
//  ships with Windows 10/11, so nothing extra to install.
// ============================================================================

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr n);
    int UnregisterControlChangeNotify(IntPtr n);
    int GetChannelCount(out uint c);
    int SetMasterVolumeLevel(float l, Guid ctx);
    int SetMasterVolumeLevelScalar(float l, Guid ctx);
    int GetMasterVolumeLevel(out float l);
    int GetMasterVolumeLevelScalar(out float l);
    int SetChannelVolumeLevel(uint n, float l, Guid ctx);
    int SetChannelVolumeLevelScalar(uint n, float l, Guid ctx);
    int GetChannelVolumeLevel(uint n, out float l);
    int GetChannelVolumeLevelScalar(uint n, out float l);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, Guid ctx);
    int GetMute(out bool m);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(int f, int m, out IntPtr d);
    int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice dev);
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorComObject { }

// Returns the process working set to the OS. Windows re-faults pages on demand,
// so this is safe: it sheds the reclaimable slack (JIT scratch, freed GC segments,
// idle UI buffers) that inflates the Task Manager "Memory" number. Best called
// after startup settles and whenever the app goes to the background (minimized).
public static class MemTrim
{
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    public static void Trim()
    {
        // (-1, -1) => trim to the minimum; pages fault back in as they're touched.
        try { SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1)); } catch { }
    }
}

public static class MicCtl
{
    static IAudioEndpointVolume GetVol()
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        en.GetDefaultAudioEndpoint(1, 0, out var dev);   // eCapture, eConsole
        var iid = typeof(IAudioEndpointVolume).GUID;
        dev.Activate(ref iid, 23, IntPtr.Zero, out var o);   // CLSCTX_ALL
        return (IAudioEndpointVolume)o;
    }
    public static void SetMicMute(bool mute) => GetVol().SetMute(mute, Guid.Empty);
    public static bool GetMicMute() { GetVol().GetMute(out var m); return m; }
}

public class AeroSqlResult
{
    public string[] Columns;
    public List<string[]> Rows = new();
}

public static class AeroSqlite
{
    const int SQLITE_OK = 0; const int SQLITE_ROW = 100; const int SQLITE_DONE = 101;
    const int SQLITE_OPEN_READONLY = 0x00000001;
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSql, int nByte, out IntPtr stmt, out IntPtr tail);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_name(IntPtr stmt, int i);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt, int i);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr stmt, int i);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr db);

    static string Utf8(IntPtr p, int len)
    {
        if (p == IntPtr.Zero || len <= 0) return "";
        byte[] b = new byte[len];
        Marshal.Copy(p, b, 0, len);
        return Encoding.UTF8.GetString(b);
    }
    static string Utf8z(IntPtr p)
    {
        if (p == IntPtr.Zero) return "";
        int len = 0;
        while (Marshal.ReadByte(p, len) != 0) len++;
        return Utf8(p, len);
    }
    static byte[] Cstr(string s)
    {
        byte[] u = Encoding.UTF8.GetBytes(s);
        byte[] z = new byte[u.Length + 1];
        Array.Copy(u, z, u.Length);
        return z;
    }

    public static AeroSqlResult Query(string dbPath, string sql)
    {
        int rc = sqlite3_open_v2(Cstr(dbPath), out var db, SQLITE_OPEN_READONLY, IntPtr.Zero);
        if (rc != SQLITE_OK) { string m = Utf8z(sqlite3_errmsg(db)); sqlite3_close(db); throw new Exception("open rc=" + rc + " " + m); }
        try
        {
            rc = sqlite3_prepare_v2(db, Cstr(sql), -1, out var stmt, out _);
            if (rc != SQLITE_OK) throw new Exception("prepare rc=" + rc + " " + Utf8z(sqlite3_errmsg(db)));
            var res = new AeroSqlResult();
            int nc = sqlite3_column_count(stmt);
            res.Columns = new string[nc];
            for (int i = 0; i < nc; i++) res.Columns[i] = Utf8z(sqlite3_column_name(stmt, i));
            try
            {
                while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
                {
                    var row = new string[nc];
                    for (int i = 0; i < nc; i++) row[i] = Utf8(sqlite3_column_text(stmt, i), sqlite3_column_bytes(stmt, i));
                    res.Rows.Add(row);
                }
            }
            finally { sqlite3_finalize(stmt); }
            if (rc != SQLITE_DONE) throw new Exception("step rc=" + rc + " " + Utf8z(sqlite3_errmsg(db)));
            return res;
        }
        finally { sqlite3_close(db); }
    }
}
