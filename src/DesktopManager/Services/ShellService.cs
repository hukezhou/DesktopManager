using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DesktopManager.Services;

/// <summary>与 Shell 交互：图标提取、快捷方式解析、打开目标。</summary>
public interface IShellService
{
    /// <summary>提取文件/文件夹/快捷方式的原始图标；失败返回 null。</summary>
    System.Windows.Media.ImageSource? GetIcon(string path);

    /// <summary>解析 .lnk 的真实目标；非快捷方式返回自身。</summary>
    string ResolveTarget(string path);

    /// <summary>用系统默认方式打开文件/文件夹/应用。返回是否成功，失败时给出原因。</summary>
    bool Open(string path, out string? error);
}

/// <summary>基于 shell32 P/Invoke 的实现，不依赖第三方库。</summary>
public sealed class ShellService : IShellService
{
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const int MAX_PATH = 260;

    public System.Windows.Media.ImageSource? GetIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // 目标不存在时（例如失效快捷方式）退化为按扩展名取图标。
        var exists = File.Exists(path) || Directory.Exists(path);
        var info = new SHFILEINFO();
        var size = (uint)Marshal.SizeOf<SHFILEINFO>();
        var flags = SHGFI_ICON | SHGFI_LARGEICON;
        if (!exists)
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
        }

        var result = SHGetFileInfo(path, exists ? 0 : FILE_ATTRIBUTE_NORMAL, ref info, size, flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            // 防御：极少数情况下返回 0 但仍带句柄，不能漏掉 DestroyIcon。
            if (info.hIcon != IntPtr.Zero)
            {
                DestroyIcon(info.hIcon);
            }

            return null;
        }

        try
        {
            var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    public string ResolveTarget(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        object? linkObject = null;
        try
        {
            linkObject = new ShellLink();
            var link = (IShellLinkW)linkObject;
            link.Resolve(IntPtr.Zero, 0x00 | 0x01 | 0x04); // SLGP_FLAGS: no UI, resolve known folders, update
            var buffer = new System.Text.StringBuilder(MAX_PATH);
            link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
            var target = buffer.ToString();
            return string.IsNullOrWhiteSpace(target) ? path : target;
        }
        catch (COMException)
        {
            return path;
        }
        catch (InvalidCastException)
        {
            return path;
        }
        finally
        {
            if (linkObject is not null)
            {
                Marshal.ReleaseComObject(linkObject);
            }
        }
    }

    public bool Open(string path, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                error = "目标不存在：" + path;
                return false;
            }

            var workingDirectory = Path.GetDirectoryName(path) ?? string.Empty;
            var startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory,
            };

            // ShellExecute 复用已有窗口时（典型是打开文件夹由现有资源管理器接管）
            // Process.Start 会返回 null，这并不代表失败，失败只会抛异常。
            // 返回的 Process 持有内核句柄，必须释放，否则每次“打开”都会滞留句柄。
            using var process = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbSizeFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint dwFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
