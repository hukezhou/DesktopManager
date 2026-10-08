using System.Runtime.InteropServices;

namespace DesktopManager.Services;

/// <summary>屏幕矩形（物理像素）。</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

/// <summary>显示器信息，供窗口尺寸/位置校验使用。</summary>
public interface IMonitorService
{
    /// <summary>所有活动显示器的工作区（物理像素），第一个通常是主显示器。</summary>
    IReadOnlyList<PixelRect> GetWorkAreas();
}

public sealed class MonitorService : IMonitorService
{
    private const uint MonitorEnumerateActive = 0x00000002;

    public IReadOnlyList<PixelRect> GetWorkAreas()
    {
        var areas = new List<PixelRect>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref RectStruct clip, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var work = info.WorkArea;
                areas.Add(new PixelRect(work.Left, work.Top, work.Right, work.Bottom));
            }

            return true;
        }, IntPtr.Zero);

        return areas;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct Rect;
        public RectStruct WorkArea;
        public uint Flags;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RectStruct clip, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
